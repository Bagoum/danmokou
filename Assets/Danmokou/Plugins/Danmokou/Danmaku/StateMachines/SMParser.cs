using Mizuhashi;
using static Mizuhashi.Combinators;
using System.Collections.Generic;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Functional;
using UnityEngine.Profiling;
using static BagoumLib.Functional.Helpers;
using LPUOrError = BagoumLib.Functional.Either<Danmokou.SM.Parsing.SMParser.LocatedParseUnit, Mizuhashi.ParserError>;

namespace Danmokou.SM.Parsing {
public static class SMParser {
    public const char COMMENT = '#';
    public const string PROP_MARKER = "<!>";
    public const string PROP2_MARKER = "<#>";
    public const string PROP_KW = "!$_PROPERTY_$!";
    public const string PROP2_KW = "!$_PARSER_PROPERTY_$!";
    public const char OPEN_PF = '[';
    public const char CLOSE_PF = ']';
    public const char QUOTE = '`';
    public const char MACRO_INVOKE = '$';
    public const char MACRO_VAR = '%';
    public const string MACRO_OPEN = "!{";
    public const string MACRO_CLOSE = "!}";
    public const string LAMBDA_MACRO_PRM = "!$";
    public const string MACRO_REINVOKE = "$%";
    public const char OPEN_ARG = '(';
    public const char CLOSE_ARG = ')';
    public const char ARG_SEP = ',';
    public const char NEWLINE = '\n';
    
    private static readonly ParserError macroOLOpenErr = new ParserError.Expected("\"!!{ or !!{}\"");
    public static readonly Parser<char, string> MACRO_OL_OPEN = inp => {
        if (inp.Remaining < 3)
            return new(macroOLOpenErr, inp.Index);
        if (inp.CharAt(0) != '!' || inp.CharAt(1) != '!' || inp.CharAt(2) != '{')
            return new(macroOLOpenErr, inp.Index);
        if (inp.Remaining >= 4 && inp.CharAt(3) == '}')
            return new("!!{}", null, inp.Index, inp.Step(4));
        return new("!!{", null, inp.Index, inp.Step(3));
    };

    public static Parser<char, string> Bounded(char c) =>
        Between(c, ManySatisfy(x => x != c));

    public static bool WhiteInline(char c) => c != NEWLINE && char.IsWhiteSpace(c);


    private static Parser<char, char> ArgSep = Char(ARG_SEP);
    private static Parser<char, char> WhitespacedArgSep = inp => {
        var w1 = Whitespace(inp); //cannot fail, so we don't need to check output
        var c = ArgSep(inp);
        if (!c.Result.Valid)
            return new(c.Result, c.Error, w1.Start, c.End);
        var w2 = Whitespace(inp);
        return new(c.Result, c.Error, w1.Start, w2.End);
    };
    /// <summary>
    /// Given an element parser, parse a parentheses sequence of <see cref="ARG_SEP"/>-separated elements.
    /// </summary>
    public static Parser<char, List<T>> Paren<T>(Parser<char, T> p) => 
        Paren1(Whitespace.IgThen(p.SepBy(WhitespacedArgSep)));

    public static Parser<char, T> Paren1<T>(Parser<char, T> p) {
        var p1 = Char(OPEN_ARG);
        var p2 = Char(CLOSE_ARG);
        var err = new ParserError.Failure("Expected parentheses to close here.");
        return inp => {
            var rp1 = p1(inp);
            if (!rp1.Result.Valid)
                return rp1.CastFailure<T>();
            var r = p(inp);
            if (!r.Result.Valid)
                return new(r.Result, r.Error, rp1.Start, r.End);
            var rp2 = p2(inp);
            if (!rp2.Result.Valid)
                return new(Maybe<T>.None, new LocatedParserError(rp2.Start, err), rp1.Start, rp2.End);
            return new(r.Result, r.Error, rp1.Start, rp2.End);
        };
    }

    public static readonly Parser<char, Unit> ILSpaces = SkipManySatisfy(WhiteInline);

    private static bool IsLetter(char c) =>
        c != COMMENT && c != MACRO_INVOKE && c != MACRO_VAR && c != '!'
        && c != OPEN_ARG && c != CLOSE_ARG && c != ARG_SEP
        && c != OPEN_PF && c != CLOSE_PF && c != QUOTE
        && !char.IsWhiteSpace(c);

    public class Macro {
        public readonly string name;
        public readonly int nprms;
        public readonly Dictionary<string, int> prmIndMap;
        public readonly LocatedParseUnit?[] prmDefaults;
        public readonly LocatedParseUnit unformatted;
        public Macro(string name, int nprms, Dictionary<string, int> prmIndMap, LocatedParseUnit?[] prmDefaults, LocatedParseUnit unformatted) {
            this.name = name;
            this.nprms = nprms;
            this.prmIndMap = prmIndMap;
            this.prmDefaults = prmDefaults;
            this.unformatted = unformatted;
        }

        public static Macro Create(string name, List<MacroArg> prms, LocatedParseUnit unformatted) => new(
            name, prms.Count, prms.Select((x, i) => (x.name, i)).ToDict(), prms.Select(x => x.deflt).ToArray(),
            unformatted.WithUnit(unformatted.unit.CutTrailingNewline()));

        private static LPUOrError ResolveUnit(
            Func<string, LPUOrError> argResolve,
            Func<string, List<LocatedParseUnit>, LPUOrError> macroReinvResolve,
            LocatedParseUnit x) {
            Either<List<LocatedParseUnit>, ParserError> resolveAcc(IEnumerable<LocatedParseUnit> args) =>
                //Reassign locations going down using WithPosition
                args.Select(a => ResolveUnit(argResolve, macroReinvResolve, a.WithPosition(x.position)))
                    .AccFailToR()
                    .FMapR(errs => (ParserError) new ParserError.OneOf(errs));
            LPUOrError reloc(Either<ParseUnit, ParserError> pu) {
                return pu.IsLeft ? x.WithUnit(pu.Left) : pu.Right;
            }
            return x.unit.Match(
                macroVar: argResolve,
                macroReinv: macroReinvResolve,
                paren: ns => reloc(resolveAcc(ns).FMapL(ParseUnit.Paren)),
                words: ns => reloc(resolveAcc(ns).FMapL(ParseUnit.Words)),
                nswords: ns => reloc(resolveAcc(ns).FMapL(ParseUnit.NoSpaceWords)),
                deflt: () => x);
        }

        private LPUOrError RealizeOverUnit(List<LocatedParseUnit> args, LocatedParseUnit unfmtd, PositionRange assignLocation) => Macro.ResolveUnit(
            s => !prmIndMap.TryGetValue(s, out var i) ? 
                new ParserError.Failure($"Macro body has nonexistent variable \"%{s}\"") :
                args[i].WithPosition(assignLocation),
            (s, rargs) => !prmIndMap.TryGetValue(s, out var i) ?
                new ParserError.Failure($"Macro body has nonexistent reinvocation \"$%{s}") :
                args[i].unit.Reduce().Match(
                    partlMacroInv: (m, pargs) => {
                        static bool isLambda(LocatedParseUnit lpu) => lpu.unit.type == ParseUnit.Type.LambdaMacroParam;
                        if (ReplaceEntries(true, pargs, rargs, isLambda) is {IsLeft: true} replaced)
                            return replaced.Left
                                .Select(l => RealizeOverUnit(args, l, assignLocation))
                                .AccFailToR()
                                .FMapR(errs => (ParserError) new ParserError.OneOf(errs))
                                .BindL(lpu => m.Invoke(lpu, assignLocation));
                        else
                            return new ParserError.Failure(
                                $"Macro \"{name}\" provides too many arguments to partial macro " +
                                $"\"{m.name}\". ({rargs.Count} provided, {pargs.Where(isLambda).Count()} required)");
                    },
                    deflt: () => new ParserError.Failure(
                        $"Macro argument \"{name}.%{s}\" (arg #{i+1}) must be a partial macro invocation. " +
                        $"This may occur if you already provided all necessary arguments.")
                    )
            , unfmtd.WithPosition(assignLocation));

        public LPUOrError Realize(List<LocatedParseUnit> args, PositionRange assignLocation) => 
            RealizeOverUnit(args, unformatted, assignLocation);

        public LPUOrError Invoke(List<LocatedParseUnit> args, PositionRange assignLocation) {
            if (args.Count != nprms) {
                var defaults = prmDefaults.SoftSkip(args.Count).FilterNone().ToArray();
                if (args.Count + defaults.Length != nprms)
                    return new ParserError.Failure($"Macro \"{name}\" requires {nprms} arguments ({args.Count} provided)");
                else
                    return Realize(args.Concat(defaults).ToList(), assignLocation);
            } else if (args.Any(l => l.unit.type == ParseUnit.Type.LambdaMacroParam)) {
                return new LocatedParseUnit(ParseUnit.PartialMacroInvoke(this, args), 
                    assignLocation);
            } else 
                return Realize(args, assignLocation);
        }

        public static readonly Parser<char, string> Prm = 
            Many1Satisfy(c => char.IsLetterOrDigit(c) || c == '_', "letter/digit/underscore").LabelV("macro parameter name");

    }
    public readonly struct MacroArg {
        public readonly string name;
        public readonly LocatedParseUnit? deflt;
        public MacroArg(string name, LocatedParseUnit? deflt) {
            this.name = name;
            this.deflt = deflt;
        }
    }

    public readonly struct LocatedParseUnit {
        public readonly ParseUnit unit;
        public readonly PositionRange position;
        public Position Start => position.Start;
        public Position End => position.End;
        public LocatedParseUnit(ParseUnit unit, in PositionRange position) {
            this.unit = unit;
            this.position = position;
        }

        public LocatedParseUnit WithUnit(ParseUnit p) => new(p, position);
        public LocatedParseUnit WithPosition(PositionRange p) => new(unit, p);

        public LocatedParseUnit(List<LocatedParseUnit> words) {
            this.unit = ParseUnit.Words(words);
            position = new(
                words.Count > 0 ? words[0].Start : default,
                words.Count > 0 ? words[^1].End : default
            );
        }

        public LocatedParserError Locate(ParserError p) => new LocatedParserError(Start.Index, p);
    }

    private static Parser<char, LocatedParseUnit> Locate(this Parser<char, ParseUnit> p) =>
            p.WrapPosition((x, pos) => new LocatedParseUnit(x, pos));
    
    private static Parser<char, (T val, PositionRange position)> WrapPosition<T>(this Parser<char, T> p) =>
            p.WrapPosition((x, pos) => (x, pos));
        

    private static bool IsNestingType(this ParseUnit.Type t) => 
        t is ParseUnit.Type.Paren or ParseUnit.Type.Words or ParseUnit.Type.NoSpaceWords;
    public class ParseUnit {
        public enum Type {
            Atom,
            Quote,
            MacroVar, 
            LambdaMacroParam,
            PartialMacroInvoke,
            MacroReinvoke,
            Paren,
            Words,
            NoSpaceWords,
            MacroDef,
            Newline,
            End
        }

        public readonly Type type;
        public readonly string sVal;
        public readonly (Macro, List<LocatedParseUnit>) partialMacroInvoke;
        public readonly (string, List<LocatedParseUnit>) macroReinvoke;
        public readonly List<LocatedParseUnit> nestVal;

        private ParseUnit(Type type, string sVal="", (Macro, List<LocatedParseUnit>) partialMacroInvoke=default, (string, List<LocatedParseUnit>) macroReinvoke=default, List<LocatedParseUnit>? nestVal=default) {
            this.type = type;
            this.sVal = sVal;
            this.partialMacroInvoke = partialMacroInvoke;
            this.macroReinvoke = macroReinvoke;
            this.nestVal = nestVal!;
        }

        public static ParseUnit Atom(string x) => new(Type.Atom, x);
        public static ParseUnit Quote(string x) => new(Type.Quote, x);
        public static ParseUnit MacroVar(string x) => new(Type.MacroVar, x);
        public static ParseUnit MacroDef(string x) => new(Type.MacroDef, x);
        public static ParseUnit LambdaMacroParam() => new(Type.LambdaMacroParam);
        public static readonly ParseUnit Newline = new(Type.Newline);
        public static ParseUnit End() => new(Type.End);
        public static ParseUnit PartialMacroInvoke(Macro m, List<LocatedParseUnit> lpus) =>
            new(Type.PartialMacroInvoke, partialMacroInvoke: (m, lpus));
        public static ParseUnit MacroReinvoke(string m, List<LocatedParseUnit> lpus) =>
            new(Type.MacroReinvoke,  macroReinvoke: (m, lpus));
        public static ParseUnit Paren(List<LocatedParseUnit> lpus) => new(Type.Paren, nestVal: lpus);
        public static ParseUnit Words(List<LocatedParseUnit> lpus) => new(Type.Words, nestVal: lpus);
        public static ParseUnit NoSpaceWords(List<LocatedParseUnit> lpus) => new(Type.NoSpaceWords, nestVal: lpus);

        public static ParseUnit Nest(List<LocatedParseUnit> lpus) => lpus.Count == 1 ? lpus[0].unit : Words(lpus);

        public T Match<T>(Func<string, T>? atom = null, Func<string, T>? quote = null, Func<string, T>? macroVar = null,
            Func<string, T>? macroDef = null, Func<T>? lambdaMacroParam = null, Func<T>? newline = null, 
            Func<T>? end = null, Func<Macro, List<LocatedParseUnit>, T>? partlMacroInv = null, Func<string,List<LocatedParseUnit>, T>? macroReinv = null,
            Func<List<LocatedParseUnit>, T>? paren = null, Func<List<LocatedParseUnit>, T>? words = null, Func<List<LocatedParseUnit>, T>? nswords = null, 
            Func<T>? deflt = null) {
            if (type == Type.Atom && atom != null) return atom(sVal);
            if (type == Type.Quote && quote != null) return quote(sVal);
            if (type == Type.MacroVar && macroVar != null) return macroVar(sVal);
            if (type == Type.MacroDef && macroDef != null) return macroDef(sVal);
            if (type == Type.LambdaMacroParam && lambdaMacroParam != null) return lambdaMacroParam();
            if (type == Type.Newline && newline != null) return newline();
            if (type == Type.End && end != null) return end();
            if (type == Type.PartialMacroInvoke && partlMacroInv != null) return partlMacroInv(partialMacroInvoke.Item1, partialMacroInvoke.Item2);
            if (type == Type.MacroReinvoke && macroReinv != null) return macroReinv(macroReinvoke.Item1, macroReinvoke.Item2);
            if (type == Type.Paren && paren != null) return paren(nestVal);
            if (type == Type.Words && words != null) return words(nestVal);
            if (type == Type.NoSpaceWords && nswords != null) return nswords(nestVal);
            return deflt!();
        }
        public ParseUnit Reduce() {
            ParseUnit reduce(List<LocatedParseUnit> lpus, Func<List<LocatedParseUnit>, ParseUnit> recons) =>
                lpus.Count == 1 ? lpus[0].unit.Reduce() : recons(lpus);
            if (type == Type.Paren) return this;
            if (type == Type.Words) return reduce(nestVal, Words);
            if (type == Type.NoSpaceWords) return reduce(nestVal, NoSpaceWords);
            return this;
        }

        public ParseUnit CutTrailingNewline() {
            List<LocatedParseUnit> cut(List<LocatedParseUnit> lpus) {
                var lm1 = lpus.Count - 1;
                return lpus[lm1].unit.type == Type.Newline ? lpus.Take(lm1).ToList() : lpus;
            }
            if (type == Type.Paren) return Paren(cut(nestVal));
            if (type == Type.Words) return Words(cut(nestVal));
            if (type == Type.NoSpaceWords) return NoSpaceWords(cut(nestVal));
            return this;
            
        }
    }

    public class State {
        public readonly ImmutableDictionary<string, Macro> macros;
        public State(ImmutableDictionary<string, Macro> macros) {
            this.macros = macros;
        }
    }

    private static HashSet<char> notSimpleChars = new() {
        COMMENT, MACRO_INVOKE, MACRO_VAR, '!',
        OPEN_ARG, CLOSE_ARG, ARG_SEP, OPEN_PF, CLOSE_PF, QUOTE
    };
    private static Parser<char, string> MakeSimpleStringParser(bool atleastOne) {
        var expected = new ParserError.Expected("basic letter");
        return input => {
            var len = 0;
            for (; len < input.Remaining; ++len) {
                var c = input.CharAt(len);
                if (c == COMMENT || c == MACRO_INVOKE || c == MACRO_VAR || c == '!'
                    || c == OPEN_ARG || c == CLOSE_ARG || c == ARG_SEP
                    || c == OPEN_PF || c == CLOSE_PF || c == QUOTE
                    || char.IsWhiteSpace(c))
                    break;
            }
            if (len == 0 && atleastOne)
                return new ParseResult<string>(expected, input.Index);
            return new ParseResult<string>(input.Substring(len), 
                input.MakeError(expected), input.Index, input.Step(len));
        };
    }

    private static readonly Parser<char, string> simpleString0 = MakeSimpleStringParser(false);
    private static readonly Parser<char, string> simpleString1 = MakeSimpleStringParser(true);

    private static Parser<char, List<T>> sepByAll2<T>(Parser<char, T> p, Parser<char, T> sep) =>
        //this is a bit faster than calling p.SepByAll(sep, 2), though the error might be less clear
        Sequential(p, sep, p.SepByAll(sep, 1), (a, b, rest) => rest.Prepend(b).Prepend(a).ToList());
    
    private static Parser<char, List<T>> sepByAll1<T>(Parser<char, T> p, Parser<char, T> sep) =>
        //this is a bit faster than calling p.SepByAll(sep, 1), though the error might be less clear
        Sequential(p, sep, p.SepByAll(sep, 0), (a, b, rest) => rest.Prepend(b).Prepend(a).ToList());

    private static ParseUnit CompileMacroVariable(List<(string, PositionRange)> terms) {
        var l = terms
            .Select((x, i) => string.IsNullOrEmpty(x.Item1) ?
                null :
                i % 2 == 0 ? 
                    (LocatedParseUnit?)new LocatedParseUnit(ParseUnit.Atom(x.Item1), x.Item2) :
                    new LocatedParseUnit(ParseUnit.MacroVar(x.Item1), x.Item2))
            .FilterNone()
            .ToList();
        return l.Count == 1 ? l[0].unit : ParseUnit.NoSpaceWords(l);
    }

    
    //For a macro, we don't write the outermost location (in order to return ParseUnit),
    // and we also rewrite all the inner locations to be the same as the outermost location
    // that will be used
    //There is regrettably not much better way to deal with macros since preserving the
    // macro's original locations would really break the parse tree
    private static Parser<char, ParseUnit> InvokeMacroByName(string name, List<LocatedParseUnit> args, PositionRange useLocation) =>
        GetState<char, State>().SelectMany(state => state.macros.TryGetValue(name, out var m) ?
                ReturnOrError<char, LocatedParseUnit>(m.Invoke(args, useLocation)) :
                Fail<char, LocatedParseUnit>($"No macro exists with name {name}.")
            , (s, lpu) => lpu.unit);

    private static readonly Parser<char, LocatedParseUnit> CNewln =
        Char(COMMENT).IgThen(SkipManySatisfy(x => x != NEWLINE)).Optional()
            .IgThen(Newline.WrapPosition())
            .FMap(p => new LocatedParseUnit(ParseUnit.Newline, p.position));

    private static Parser<char, List<LocatedParseUnit>> Words(bool allowNewline) {
        //This is required to avoid circular object definitions :(
        Parser<char, List<LocatedParseUnit>>? lazy = null;
        Parser<char, List<LocatedParseUnit>> LoadLazy() =>
            (allowNewline ? MainParserNL : MainParser).ThenIg(ILSpaces).Many1();
        return inp => (lazy ??= LoadLazy())(inp);
    }

    private static readonly Parser<char, List<LocatedParseUnit>> WordsTopLevel = Words(true);
    private static readonly Parser<char, List<LocatedParseUnit>> WordsInBlock = WordsTopLevel;
    private static readonly Parser<char, List<LocatedParseUnit>> WordsInline = Words(false);

    private static readonly List<LocatedParseUnit> empty = new();
    private static readonly Parser<char, List<LocatedParseUnit>> ParenArgs = 
        //Strictly speaking, paren args must be nonempty, but it's easier to report that in typechecking
        Paren(WordsInBlock.OptionalOr(empty).FMap(ParseUnit.Nest).Locate()).FMap(eles => {
            if (eles.Count == 1 && eles[0].position.Empty && 
                eles[0].unit.type == ParseUnit.Type.Words && eles[0].unit.nestVal.Count == 0)
                return empty;
            return eles;
        });

    private static readonly Parser<char, MacroArg> MacroPrmDecl =
        Macro.Prm.Pipe(
            Whitespace1.IgThen(
                WrapPosition(WordsInBlock)).OptionalOrNull(),
            (key, dp) => dp.Try(out var d) ?
                new MacroArg(key, new LocatedParseUnit(ParseUnit.Nest(d.val), d.position)) :
                new MacroArg(key, null));//.Label("macro parameter");


    private static readonly Parser<char, ParseUnit> OLMacroParser =
        Sequential(
                MACRO_OL_OPEN.IgThen(ILSpaces).IgThen(simpleString1),
                ILSpaces,
                WordsInline,
                CNewln,
                (key, _3, content, _4) => (key, content))
            .SelectMany(
                kcls =>
                    UpdateState<char, State>(s => new State(s.macros.SetItem(kcls.key,
                        Macro.Create(kcls.key, new List<MacroArg>(),
                            new LocatedParseUnit(kcls.content))))),
                (kcls, _) => ParseUnit.MacroDef(kcls.key));//.Label("single-line macro parser (!!{)");

    private static readonly Parser<char, ParseUnit> MacroParser =
        Between(MACRO_OPEN,
            Sequential(
                Whitespace,
                simpleString1,
                Paren(MacroPrmDecl),//.Label("macro parameters"),
                Whitespace.IgThen(WordsTopLevel),
                (_1, key, prms, content) => (key, prms, content))
            .SelectMany(kpcls => 
                UpdateState<char, State>(s => new State(s.macros.SetItem(kpcls.key, 
                    Macro.Create(kpcls.key, kpcls.prms, 
                        new LocatedParseUnit(kpcls.content))))),
                (kpcls, _) => ParseUnit.MacroDef(kpcls.key)), 
            MACRO_CLOSE
        ).ThenIg(CNewln).LabelV("macro (bounded with !{ }!)");

    private static Parser<char, ParseUnit> PropertyParser(string marker, string result) =>
        Sequential(
            String(marker).WrapPosition(), 
            ILSpaces, 
            WordsInline,
            (ps, _2, words) => ParseUnit.Words(words.Prepend(
                new (ParseUnit.Atom(result), ps.position)).ToList()));

    private static readonly Parser<char, ParseUnit> MacroReinvokeParser =
        Sequential(
            String(MACRO_REINVOKE),
            simpleString1,
            ParenArgs,
            (_, key, args) => ParseUnit.MacroReinvoke(key, args));

    private static readonly Parser<char, ParseUnit> MacroInvokeParser =
        Sequential(
            Char(MACRO_INVOKE),
            simpleString1,
            ParenArgs.OptionalOr(new List<LocatedParseUnit>()),
            (_, key, args) => (key, args))
        .WrapPosition()
        .Bind(ka => InvokeMacroByName(ka.val.key, ka.val.args, ka.position));

    private static readonly Parser<char, LocatedParseUnit> MainParser = Choice(
        String("///").IgThen(SkipManySatisfy(_ => true)).FMap(_ => ParseUnit.End()), //.Label("end of file"),
        String(LAMBDA_MACRO_PRM).Select(_ => ParseUnit.LambdaMacroParam()),
        OLMacroParser,
        MacroParser,
        PropertyParser(PROP_MARKER, PROP_KW),
        PropertyParser(PROP2_MARKER, PROP2_KW),
        // ex. <%A%%B%> = <{A}{B}>. <%A%B> = <{A}B>. 
        sepByAll2(simpleString0.WrapPosition(), 
            Between(MACRO_VAR, Macro.Prm).WrapPosition()).Attempt().FMap(CompileMacroVariable),
        //Basic word of nonzero length
        simpleString1.FMap(ParseUnit.Atom),
        ParenArgs.FMap(ParseUnit.Paren),
        //Postfix must be followed by simple string, paren, or macro
        Sequential(
            Between(OPEN_PF, WordsInBlock, CLOSE_PF),
            ILSpaces,
            Choice(
                simpleString1.FMap(ParseUnit.Atom),
                ParenArgs.FMap(ParseUnit.Paren),
                MacroInvokeParser
            ).Locate(),
            (pf, _, word) => ParseUnit.Words(pf.Prepend(word).ToList())),
        // %A %B
        Char(MACRO_VAR).IgThen(Macro.Prm.FMap(ParseUnit.MacroVar)),
        MacroReinvokeParser,
        MacroInvokeParser,
        Bounded(QUOTE).FMap(ParseUnit.Quote)
    ).Locate();
    
    private static readonly Parser<char, LocatedParseUnit> MainParserNL = CNewln.Or(MainParser);
    

    private static readonly Dictionary<string, string[]> Replacements = new() {
        {"{{", new[] {"{", "{"}},
        {"}}", new[] {"}", "}"}}
    };

    /// <summary>
    /// Output of parser, simplified into strings and parenthesized expressions.
    /// </summary>
    public abstract record ParsedUnit(PositionRange Position) {
        public Position Start => Position.Start;
        public Position End => Position.End;

        public record Str(string Item, PositionRange Position) : ParsedUnit(Position);
        public record Paren((ParsedUnit[] units, PositionRange position)[] Items, PositionRange Position) :
            ParsedUnit(Position);
        
    }

    public static PositionRange ToRange(this ParsedUnit[] pus) {
        if (pus.Length > 0) {
            return new(pus[0].Start, pus[^1].End);
        } else return default;
    }
    private static ParsedUnit S(string s, in LocatedParseUnit lpu) => 
        new ParsedUnit.Str(s, lpu.position);
    private static ParsedUnit P((ParsedUnit[], PositionRange)[] parts, in PositionRange pos) => 
        new ParsedUnit.Paren(parts, pos);

    private static Either<IEnumerable<string>, List<LocatedParserError>> pFlatten(LocatedParseUnit lpu) {
        var u = lpu.unit;
        var s = u.sVal;
        var ls = u.nestVal;
        //Since the lambdas reference lpu.location, it's actually highly inefficient to use Match to do this.
        switch (u.type) {
            case ParseUnit.Type.Atom:
                return s.Length > 0 ?
                    Replacements.TryGetValue(s, out var ss) ?
                        ss :
                        new[] {s} :
                    Array.Empty<string>();
            case ParseUnit.Type.Quote:
                return new[] {s};
            case ParseUnit.Type.Paren:
                return ls.Select(pFlatten).AccFailToR()
                    .FMapL(x => x.SeparateBy(",").Prepend("(").Append(")"));
            case ParseUnit.Type.Words:
                return ls.TakeWhile(l => l.unit.type != ParseUnit.Type.End)
                    .Select(pFlatten).AccFailToR()
                    .FMapL(t => t.Join());
            case ParseUnit.Type.NoSpaceWords:
                return ls.Select(pFlatten).AccFailToR()
                    .FMapL(arrs => {
                        var words = new List<string>() {""};
                        foreach (var arr in arrs) {
                            bool first = true;
                            foreach (var x in arr) {
                                if (first) words[^1] += x;
                                else words.Add(x);
                                first = false;
                            }
                        }
                        return (IEnumerable<string>) words;
                    });
            case ParseUnit.Type.Newline:
                return new[] {"\n"};
            case ParseUnit.Type.MacroDef:
                return Array.Empty<string>();
            case ParseUnit.Type.MacroVar:
                return new List<LocatedParserError>{lpu.Locate(new ParserError.Failure
                        ($"Found a macro variable \"%{s}\" in the output."))};
            case ParseUnit.Type.LambdaMacroParam:
                return new List<LocatedParserError>{lpu.Locate(new ParserError.Failure
                    ("Found an unbound macro argument (!$) in the output."))};
            case ParseUnit.Type.PartialMacroInvoke:
                var (m, args) = u.partialMacroInvoke;
                return new List<LocatedParserError>{lpu.Locate(new ParserError.Failure
                    ($"The macro \"{m.name}\" was partially invoked with {args.Count(a => a.unit.type != ParseUnit.Type.LambdaMacroParam)} " +
                    $" realized arguments ({m.nprms} required)"))};
            default:
                return new List<LocatedParserError>{lpu.Locate(new ParserError.Failure(
                    "Illegal unit in output, please file a bug report"))};
        }
    }

    private static readonly Either<IEnumerable<ParsedUnit>, List<LocatedParserError>> 
        noParsedUnits = Array.Empty<ParsedUnit>();
    private static Either<IEnumerable<ParsedUnit>, List<LocatedParserError>> pFlatten2(LocatedParseUnit lpu) {
        Either<IEnumerable<ParsedUnit>, List<LocatedParserError>> Fail(string err) =>
            new List<LocatedParserError> { lpu.Locate(new ParserError.Failure(err)) };
        var u = lpu.unit;
        var s = u.sVal;
        var ls = u.nestVal;
        //Since the lambdas reference lpu.location, it's actually highly inefficient to use Match to do this.
        switch (u.type) {
            case ParseUnit.Type.Atom:
                return s.Length > 0 ?
                    Replacements.TryGetValue(s, out var ss) ?
                        new Either<IEnumerable<ParsedUnit>, List<LocatedParserError>>(
                            ss.Select(x => S(x, lpu))) :
                        (new[] {S(s, lpu)}) :
                    noParsedUnits;
            case ParseUnit.Type.Quote:
                return new[] {S(s, lpu)};
            case ParseUnit.Type.Paren:
                var results = new (ParsedUnit[], PositionRange)[ls.Count];
                List<LocatedParserError>? errs = null;
                for (int ii = 0; ii < ls.Count; ++ii) {
                    var l = ls[ii];
                    var fl = pFlatten2(l);
                    if (fl.IsRight) {
                        errs ??= new();
                        errs.AddRange(fl.Right);
                    } else if (errs is null) {
                        results[ii] = (fl.Left.ToArray(), l.position);
                    }
                }
                return errs is null ?
                    new[] { P(results, in lpu.position) } :
                    errs;
            case ParseUnit.Type.Words:
                return ls
                    .TakeWhile(l => l.unit.type != ParseUnit.Type.End)
                    .Select(pFlatten2)
                    .AccFailToR()
                    .FMapL(t => t.Join());
            case ParseUnit.Type.NoSpaceWords:
                return ls
                    .Select(pFlatten)
                    .AccFailToR()
                    .FMapL(t => (IEnumerable<ParsedUnit>) 
                        new[] {S(string.Concat(t.Join()), lpu)});
            case ParseUnit.Type.Newline:
                return new[] {S("\n", lpu)};
            case ParseUnit.Type.MacroDef:
                return noParsedUnits;
            case ParseUnit.Type.MacroVar:
                return Fail($"Found a macro variable \"%{s}\" in the output.");
            case ParseUnit.Type.LambdaMacroParam:
                return Fail(
                    "Found an unbound macro argument (!$) in the output.");
            case ParseUnit.Type.PartialMacroInvoke:
                var (m, args) = u.partialMacroInvoke;
                return Fail(
                    $"The macro \"{m.name}\" was partially invoked with {args.Count(a => a.unit.type != ParseUnit.Type.LambdaMacroParam)} " +
                    $"realized arguments ({m.nprms} required)");
            default:
                return Fail("Illegal unit in output, please file a bug report");
        }
    }

    private static readonly Parser<char, LocatedParseUnit> FullParser =
        SetState<char, State>(new State(ImmutableDictionary<string, Macro>.Empty))
            .IgThen(WordsTopLevel.FMap(ParseUnit.Words))
            .ThenIg(EOF<char>().Or(p => new ParseResult<Unit>(
                new ParserError.Failure($"The character '{p.Next}' could not be handled."), p.Index, p.Index + 1)))
            .Locate();
        
    private static Either<LocatedParseUnit, LocatedParserError> RunSMParser(string s, out InputStream<char> stream) {
        var result = FullParser(stream = new InputStream<char>(s, "State Machine"));
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            (result.Error ?? new(0, new ParserError.Failure("Parsing failed, but it's unclear why.")));
    }

    public static Either<string, List<LocatedParserError>> RunSMParserAndRemakeAsString(string s, out InputStream<char> stream) =>
        RunSMParser(s, out stream)
            //We only get at most one error from the base combinatorial parsing
            .FMapR(err => new List<LocatedParserError>(){err})
            .BindL(pFlatten)
            .FMapL(ss => string.Join(" ", ss));

    public static Either<ParsedUnit[], List<LocatedParserError>> ExportSMParserToParsedUnits(string s, out InputStream<char> stream) =>
        RunSMParser(s, out stream)
            .FMapR(err => new List<LocatedParserError>(){err})
            .BindL(lpu => pFlatten2(lpu)
            .FMapL(x => x.ToArray()));


}
}