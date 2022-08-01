using Mizuhashi;
using static Mizuhashi.Combinators;
using System.Collections.Generic;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Functional;
using static BagoumLib.Functional.Helpers;

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
    public const string MACRO_OL_OPEN = "!!{";
    public const string MACRO_OPEN = "!{";
    public const string MACRO_CLOSE = "!}";
    public const string LAMBDA_MACRO_PRM = "!$";
    public const string MACRO_REINVOKE = "$%";
    public const char OPEN_ARG = '(';
    public const char CLOSE_ARG = ')';
    public const char ARG_SEP = ',';
    public const char NEWLINE = '\n';

    public static Parser<string> Bounded(char c) =>
        Between(c, ManySatisfy(x => x != c));

    public static bool WhiteInline(char c) => c != NEWLINE && char.IsWhiteSpace(c);


    public static Parser<List<T>> Paren<T>(Parser<T> p) => Paren1(
        Whitespace.IgThen(
            p.SepBy(Whitespace.IgThen(Char(ARG_SEP)).IgThen(Whitespace))
        ));

    public static Parser<T> Paren1<T>(Parser<T> p) => Between(OPEN_ARG, p, CLOSE_ARG);

    public static readonly Parser<Unit> ILSpaces = SkipManySatisfy(WhiteInline);

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

        private static Errorable<LocatedParseUnit> ResolveUnit(
            Func<string, Errorable<LocatedParseUnit>> argResolve,
            Func<string, List<LocatedParseUnit>, Errorable<LocatedParseUnit>> macroReinvResolve,
            LocatedParseUnit x) {
            Errorable<List<LocatedParseUnit>> resolveAcc(IEnumerable<LocatedParseUnit> args) => 
                args.Select(a => ResolveUnit(argResolve, macroReinvResolve, a)).Acc();
            Errorable<LocatedParseUnit> reloc(Errorable<ParseUnit> pu) {
                return pu.Valid ? x.WithUnit(pu.Value) : Errorable<LocatedParseUnit>.Fail(pu.errors);
            }
            return x.unit.Match(
                macroVar: argResolve,
                macroReinv: macroReinvResolve,
                paren: ns => reloc(resolveAcc(ns).Map(ParseUnit.Paren)),
                words: ns => reloc(resolveAcc(ns).Map(ParseUnit.Words)),
                nswords: ns => reloc(resolveAcc(ns).Map(ParseUnit.NoSpaceWords)),
                deflt: () => Errorable<LocatedParseUnit>.OK(x));
        }

        private Errorable<LocatedParseUnit> RealizeOverUnit(List<LocatedParseUnit> args, LocatedParseUnit unfmtd) => Macro.ResolveUnit(
            s => !prmIndMap.TryGetValue(s, out var i) ? 
                Errorable<LocatedParseUnit>.Fail($"Macro body has nonexistent variable \"%{s}\"") :
                args[i],
            (s, rargs) => !prmIndMap.TryGetValue(s, out var i) ?
                Errorable<LocatedParseUnit>.Fail($"Macro body has nonexistent reinvocation \"$%{s}") :
                args[i].unit.Reduce().Match(
                    partlMacroInv: (m, pargs) => {
                        static bool isLambda(LocatedParseUnit lpu) => lpu.unit.type == ParseUnit.Type.LambdaMacroParam;
                        if (ReplaceEntries(true, pargs, rargs, isLambda) is {Valid: true} replaced)
                            return replaced.Value.Select(l => RealizeOverUnit(args, l)).Acc().Bind(m.Invoke);
                        else
                            return Errorable<LocatedParseUnit>.Fail(
                                $"Macro \"{name}\" provides too many arguments to partial macro " +
                                $"\"{m.name}\". ({rargs.Count} provided, {pargs.Where(isLambda).Count()} required)");
                    },
                    deflt: () => Errorable<LocatedParseUnit>.Fail(
                        $"Macro argument \"{name}.%{s}\" (arg #{i}) must be a partial macro invocation. " +
                        $"This may occur if you already provided all necessary arguments.")
                    )
                
            
            , unfmtd);

        public Errorable<LocatedParseUnit> Realize(List<LocatedParseUnit> args) =>
            nprms == 0 ? Errorable<LocatedParseUnit>.OK(unformatted) : RealizeOverUnit(args, unformatted);

        public Errorable<LocatedParseUnit> Invoke(List<LocatedParseUnit> args) {
            if (args.Count != nprms) {
                var defaults = prmDefaults.SoftSkip(args.Count).FilterNone().ToArray();
                if (args.Count + defaults.Length != nprms)
                    return Errorable<LocatedParseUnit>.Fail($"Macro \"{name}\" requires {nprms} arguments ({args.Count} provided)");
                else
                    return Realize(args.Concat(defaults).ToList());
            } else if (args.Any(l => l.unit.type == ParseUnit.Type.LambdaMacroParam)) {
                return new LocatedParseUnit(ParseUnit.PartialMacroInvoke(this, args), 
                    new(args[0].Start, args[^1].End));
            } else 
                return Realize(args);
        }

        public static readonly Parser<string> Prm = 
            Many1Satisfy(c => char.IsLetterOrDigit(c) || c == '_', "letter/digit/underscore");//.Label("macro parameter name");

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

        public LocatedParseUnit(List<LocatedParseUnit> words) {
            this.unit = ParseUnit.Words(words);
            position = new(
                words.Count > 0 ? words[0].Start : default,
                words.Count > 0 ? words[^1].End : default
            );
        }
    }

    private static Parser<LocatedParseUnit> Locate(this Parser<ParseUnit> p) =>
            p.WrapPosition((x, pos) => new LocatedParseUnit(x, pos));
    
    private static Parser<(T val, PositionRange position)> WrapPosition<T>(this Parser<T> p) =>
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
    private static Parser<string> MakeSimpleStringParser(bool atleastOne) {
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

    private static readonly Parser<string> simpleString0 = MakeSimpleStringParser(false);
    private static readonly Parser<string> simpleString1 = MakeSimpleStringParser(true);

    private static Parser<List<T>> sepByAll2<T>(Parser<T> p, Parser<T> sep) =>
        //this is a bit faster than calling p.SepByAll(sep, 2), though the error might be less clear
        Sequential(p, sep, p.SepByAll(sep, 1), (a, b, rest) => rest.Prepend(b).Prepend(a).ToList());
    
    private static Parser<List<T>> sepByAll1<T>(Parser<T> p, Parser<T> sep) =>
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

    private static Parser<T> FailErrorable<T>(Errorable<T> errb) => errb.Valid ?
        PReturn(errb.Value) :
        Fail<T>(string.Join("\n", errb.errors));
    
    //For a macro, we don't write the outermost location (thus we return ParseUnit), but we do write inner locations
    // within the words of this parseunit
    private static Parser<ParseUnit> InvokeMacroByName(string name, List<LocatedParseUnit> args) =>
        GetState<State>().SelectMany(state => state.macros.TryGetValue(name, out var m) ?
                FailErrorable(m.Invoke(args)) :
                Fail<LocatedParseUnit>($"No macro exists with name {name}.")
            , (s, lpu) => lpu.unit);

    private static readonly Parser<LocatedParseUnit> CNewln =
        Char(COMMENT).IgThen(SkipManySatisfy(x => x != NEWLINE)).Optional()
            .IgThen(Newline.WrapPosition())
            .FMap(p => new LocatedParseUnit(ParseUnit.Newline, p.position));

    private static Parser<List<LocatedParseUnit>> Words(bool allowNewline) {
        //This is required to avoid circular object definitions :(
        Parser<List<LocatedParseUnit>>? lazy = null;
        Parser<List<LocatedParseUnit>> LoadLazy() =>
            (allowNewline ? MainParserNL : MainParser).ThenIg(ILSpaces).Many1();
        return inp => (lazy ??= LoadLazy())(inp);
    }

    private static readonly Parser<List<LocatedParseUnit>> WordsTopLevel = Words(true);
    private static readonly Parser<List<LocatedParseUnit>> WordsInBlock = WordsTopLevel;
    private static readonly Parser<List<LocatedParseUnit>> WordsInline = Words(false);
    
    private static readonly Parser<List<LocatedParseUnit>> ParenArgs = 
        Paren(WordsInBlock.FMap(ParseUnit.Nest).Locate());

    private static readonly Parser<MacroArg> MacroPrmDecl =
        Macro.Prm.Pipe(
            Whitespace1.IgThen(
                WrapPosition(WordsInBlock)).OptionalOrNull(),
            (key, dp) => dp.Try(out var d) ?
                new MacroArg(key, new LocatedParseUnit(ParseUnit.Nest(d.val), d.position)) :
                new MacroArg(key, null));//.Label("macro parameter");


    private static readonly Parser<ParseUnit> OLMacroParser =
        Sequential(
                String(MACRO_OL_OPEN).IgThen(ILSpaces).IgThen(simpleString1),
                ILSpaces,
                WordsInline,
                CNewln,
                (key, _3, content, _4) => (key, content))
            .SelectMany(
                kcls =>
                    UpdateState<State>(s => new State(s.macros.SetItem(kcls.key,
                        Macro.Create(kcls.key, new List<MacroArg>(),
                            new LocatedParseUnit(kcls.content))))),
                (kcls, _) => ParseUnit.MacroDef(kcls.key));//.Label("single-line macro parser (!!{)");

    private static readonly Parser<ParseUnit> MacroParser =
        Between(MACRO_OPEN,
            Sequential(
                Whitespace,
                simpleString1,
                Paren(MacroPrmDecl),//.Label("macro parameters"),
                Whitespace.IgThen(WordsTopLevel),
                (_1, key, prms, content) => (key, prms, content))
            .SelectMany(kpcls => 
                UpdateState<State>(s => new State(s.macros.SetItem(kpcls.key, 
                    Macro.Create(kpcls.key, kpcls.prms, 
                        new LocatedParseUnit(kpcls.content))))),
                (kpcls, _) => ParseUnit.MacroDef(kpcls.key)), 
            MACRO_CLOSE
        ).ThenIg(CNewln).Label("macro (bounded with !{ }!)");

    private static Parser<ParseUnit> PropertyParser(string marker, string result) =>
        Sequential(
            String(marker).WrapPosition(), 
            ILSpaces, 
            WordsInline,
            (ps, _2, words) => ParseUnit.Words(words.Prepend(
                new (ParseUnit.Atom(result), ps.position)).ToList()));

    private static readonly Parser<ParseUnit> MacroReinvokeParser =
        Sequential(
            String(MACRO_REINVOKE),
            simpleString1,
            ParenArgs,
            (_, key, args) => ParseUnit.MacroReinvoke(key, args));

    private static readonly Parser<ParseUnit> MacroInvokeParser =
        Sequential(
            Char(MACRO_INVOKE),
            simpleString1,
            ParenArgs.OptionalOr(new List<LocatedParseUnit>()),
            (_, key, args) => (key, args))
        .Bind(ka => InvokeMacroByName(ka.key, ka.args));

//TODO move locate around this
    private static readonly Parser<LocatedParseUnit> MainParser = Choice(
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
    
    private static readonly Parser<LocatedParseUnit> MainParserNL = CNewln.Or(MainParser);
    

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
        public record Paren(ParsedUnit[][] Item, PositionRange Position) : ParsedUnit(Position);
    }

    public static PositionRange ToRange(this ParsedUnit[] pus) {
        if (pus.Length > 0) {
            return new(pus[0].Start, pus[^1].End);
        } else return default;
    }
    private static ParsedUnit S(string s, in LocatedParseUnit lpu) => 
        new ParsedUnit.Str(s, lpu.position);
    private static ParsedUnit P(ParsedUnit[][] p, in LocatedParseUnit lpu) => 
        new ParsedUnit.Paren(p, lpu.position);

    private static Errorable<IEnumerable<string>> pFlatten(LocatedParseUnit lpu) {
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
                    noStrs;
            case ParseUnit.Type.Quote:
                return new[] {s};
            case ParseUnit.Type.Paren:
                return ls.Select(pFlatten).Acc()
                    .Map(x => x.SeparateBy(",").Prepend("(").Append(")"));
            case ParseUnit.Type.Words:
                return ls.TakeWhile(l => l.unit.type != ParseUnit.Type.End)
                    .Select(pFlatten).Acc().Map(t => t.Join());
            case ParseUnit.Type.NoSpaceWords:
                return ls.Select(pFlatten).Acc().Map(arrs => {
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
                return noStrs;
            case ParseUnit.Type.MacroVar:
                return Errorable<IEnumerable<string>>.Fail($"Found a macro variable \"%{s}\" in the output.");
            case ParseUnit.Type.LambdaMacroParam:
                return Errorable<IEnumerable<string>>.Fail(
                    "Found an unbound macro argument (!$) in the output.");
            case ParseUnit.Type.PartialMacroInvoke:
                var (m, args) = u.partialMacroInvoke;
                return Errorable<IEnumerable<string>>.Fail(
                    $"The macro \"{m.name}\" was partially invoked with {args.Count(a => a.unit.type != ParseUnit.Type.LambdaMacroParam)} " +
                    $" realized arguments ({m.nprms} required)");
            default:
                return Errorable<IEnumerable<string>>.Fail(
                    "Illegal unit in output, please file a bug report");
        }
    }

    private static readonly ParsedUnit[] noParsedUnits = { };
    private static Errorable<IEnumerable<ParsedUnit>> pFlatten2(LocatedParseUnit lpu) {
        var u = lpu.unit;
        var s = u.sVal;
        var ls = u.nestVal;
        //Since the lambdas reference lpu.location, it's actually highly inefficient to use Match to do this.
        switch (u.type) {
            case ParseUnit.Type.Atom:
                return s.Length > 0 ?
                    Replacements.TryGetValue(s, out var ss) ?
                        Errorable<IEnumerable<ParsedUnit>>.OK(ss.Select(x => S(x, lpu))) :
                        new[] {S(s, lpu)} :
                    noParsedUnits;
            case ParseUnit.Type.Quote:
                return new[] {S(s, lpu)};
            case ParseUnit.Type.Paren:
                return ls.Select(pFlatten2).Acc().Map(l => (IEnumerable<ParsedUnit>) 
                    new[] {P(l.Select(xs => xs.ToArray()).ToArray(), lpu)});
            case ParseUnit.Type.Words:
                return ls.TakeWhile(l => l.unit.type != ParseUnit.Type.End).Select(pFlatten2).Acc()
                    .Map(t => t.Join());
            case ParseUnit.Type.NoSpaceWords:
                return ls.Select(pFlatten).Acc().Map(t => (IEnumerable<ParsedUnit>) 
                    new[] {S(string.Concat(t.Join()), lpu)});
            case ParseUnit.Type.Newline:
                return new[] {S("\n", lpu)};
            case ParseUnit.Type.MacroDef:
                return noParsedUnits;
            case ParseUnit.Type.MacroVar:
                return Errorable<IEnumerable<ParsedUnit>>.Fail($"Found a macro variable \"%{s}\" in the output.");
            case ParseUnit.Type.LambdaMacroParam:
                return Errorable<IEnumerable<ParsedUnit>>.Fail(
                    "Found an unbound macro argument (!$) in the output.");
            case ParseUnit.Type.PartialMacroInvoke:
                var (m, args) = u.partialMacroInvoke;
                return Errorable<IEnumerable<ParsedUnit>>.Fail(
                    $"The macro \"{m.name}\" was partially invoked with {args.Count(a => a.unit.type != ParseUnit.Type.LambdaMacroParam)} " +
                    $"realized arguments ({m.nprms} required)");
            default:
                return Errorable<IEnumerable<ParsedUnit>>.Fail(
                    "Illegal unit in output, please file a bug report");
        }
    }


    private static readonly Parser<LocatedParseUnit> FullParser =
        SetState(new State(ImmutableDictionary<string, Macro>.Empty))
            .IgThen(WordsTopLevel.FMap(ParseUnit.Words))
            .ThenIg(EOF.Or(p => new ParseResult<Unit>(
                new ParserError.Failure($"The character '{p.Next}' could not be handled."), p.Index, p.Index + 1)))
            .Locate();
        
    /*
    private static Errorable<LPU> _SMParserExec(string s) {
        var result = parse(FullParser, s);
        return result.IsFaulted ? Errorable<LPU>.Fail(result.Reply.Error.ToString()) : result.Reply.Result;
    }*/
    private static Errorable<LocatedParseUnit> _SMParserExec(string s) {
        var result = FullParser(new InputStream("State Machine", s, default!));
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            Errorable<LocatedParseUnit>.Fail(result.Error?.Show(s) ?? "Parsing failed, but it's unclear why.");
    }

    public static Errorable<IEnumerable<string>> SMParserExec(string s) =>
        _SMParserExec(s).Bind(pFlatten);

    public static Errorable<string> RemakeSMParserExec(string s) => SMParserExec(s).Map(ss => string.Join(" ", ss));

    public static Errorable<ParsedUnit[]> SMParser2Exec(string s) =>
        _SMParserExec(s).Bind(lpu => pFlatten2(lpu).Map(x => x.ToArray()));


}
}