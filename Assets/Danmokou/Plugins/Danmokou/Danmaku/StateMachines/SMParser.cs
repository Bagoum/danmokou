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
        public readonly LPU?[] prmDefaults;
        public readonly LPU unformatted;
        public Macro(string name, int nprms, Dictionary<string, int> prmIndMap, LPU?[] prmDefaults, LPU unformatted) {
            this.name = name;
            this.nprms = nprms;
            this.prmIndMap = prmIndMap;
            this.prmDefaults = prmDefaults;
            this.unformatted = unformatted;
        }

        public static Macro Create(string name, List<MacroArg> prms, LPU unformatted) => new Macro(
            name, prms.Count, prms.Select((x, i) => (x.name, i)).ToDict(), prms.Select(x => x.deflt).ToArray(),
            new LPU(unformatted.unit.CutTrailingNewline(), unformatted.location));

        private static Errorable<LPU> ResolveUnit(
            Func<string, Errorable<LPU>> argResolve,
            Func<string, List<LPU>, Errorable<LPU>> macroReinvResolve,
            LPU x) {
            Errorable<List<LPU>> resolveAcc(IEnumerable<LPU> args) => 
                args.Select(a => ResolveUnit(argResolve, macroReinvResolve, a)).Acc();
            Errorable<LPU> reloc(Errorable<ParseUnit> pu) {
                return pu.Valid ? new LPU(pu.Value, x.location) : Errorable<LPU>.Fail(pu.errors);
            }
            return x.unit.Match(
                macroVar: argResolve,
                macroReinv: macroReinvResolve,
                paren: ns => reloc(resolveAcc(ns).Map(ParseUnit.Paren)),
                words: ns => reloc(resolveAcc(ns).Map(ParseUnit.Words)),
                nswords: ns => reloc(resolveAcc(ns).Map(ParseUnit.NoSpaceWords)),
                postfix: ns => reloc(resolveAcc(ns).Map(ParseUnit.Postfix)),
                deflt: () => Errorable<LPU>.OK(x));
        }

        private Errorable<LPU> RealizeOverUnit(List<LPU> args, LPU unfmtd) => Macro.ResolveUnit(
            s => !prmIndMap.TryGetValue(s, out var i) ? 
                Errorable<LPU>.Fail($"Macro body has nonexistent variable \"%{s}\"") :
                args[i],
            (s, rargs) => !prmIndMap.TryGetValue(s, out var i) ?
                Errorable<LPU>.Fail($"Macro body has nonexistent reinvocation \"$%{s}") :
                args[i].unit.Reduce().Match(
                    partlMacroInv: (m, pargs) => {
                        static bool isLambda(LPU lpu) => lpu.unit.type == ParseUnit.Type.LambdaMacroParam;
                        var replaced = ReplaceEntries(true, pargs, rargs, isLambda);
                        if (replaced.Valid)
                            return replaced.Value.Select(l => RealizeOverUnit(args, l)).Acc().Bind(m.Invoke);
                        else
                            return Errorable<LPU>.Fail(
                                $"Macro \"{name}\" provides too many arguments to partial macro " +
                                $"\"{m.name}\". ({rargs.Count} provided, {pargs.Where(isLambda).Count()} required)");
                    },
                    deflt: () => Errorable<LPU>.Fail(
                        $"Macro argument \"{name}.%{s}\" (arg #{i}) must be a partial macro invocation. " +
                        $"This may occur if you already provided all necessary arguments.")
                    )
                
            
            , unfmtd);

        public Errorable<LPU> Realize(List<LPU> args) =>
            nprms == 0 ? Errorable<LPU>.OK(unformatted) : RealizeOverUnit(args, unformatted);

        public Errorable<LPU> Invoke(List<LPU> args) {
            if (args.Count != nprms) {
                var defaults = prmDefaults.SoftSkip(args.Count).FilterNone().ToArray();
                if (args.Count + defaults.Length != nprms)
                    return Errorable<LPU>.Fail($"Macro \"{name} requires {nprms} arguments ({args.Count} provided)");
                else
                    return Realize(args.Concat(defaults).ToList());
            } else if (args.Any(l => l.unit.type == ParseUnit.Type.LambdaMacroParam)) {
                return new LPU(ParseUnit.PartialMacroInvoke(this, args), args[0].location);
            } else 
                return Realize(args);
        }

        public static readonly Parser<string> Prm = 
            Many1Satisfy(c => char.IsLetterOrDigit(c) || c == '_', "letter/digit/underscore");//.Label("macro parameter name");

    }
    public readonly struct MacroArg {
        public readonly string name;
        public readonly LPU? deflt;
        public MacroArg(string name, LPU? deflt) {
            this.name = name;
            this.deflt = deflt;
        }
    }

    public readonly struct LPU {
        public readonly ParseUnit unit;
        public readonly Position location;
        public LPU(ParseUnit unit, Position location) {
            this.unit = unit;
            this.location = location;
        }
    }

    private static bool IsNestingType(this ParseUnit.Type t) => t == ParseUnit.Type.Paren ||
                                                                t == ParseUnit.Type.Words ||
                                                                t == ParseUnit.Type.NoSpaceWords ||
                                                                t == ParseUnit.Type.Postfix;
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
            Postfix,
            MacroDef,
            Newline,
            End
        }

        public readonly Type type;
        public readonly string sVal;
        public readonly (Macro, List<LPU>) partialMacroInvoke;
        public readonly (string, List<LPU>) macroReinvoke;
        public readonly List<LPU> nestVal;

        private ParseUnit(Type type, string sVal="", (Macro, List<LPU>) partialMacroInvoke=default, (string, List<LPU>) macroReinvoke=default, List<LPU>? nestVal=default) {
            this.type = type;
            this.sVal = sVal;
            this.partialMacroInvoke = partialMacroInvoke;
            this.macroReinvoke = macroReinvoke;
            this.nestVal = nestVal!;
        }

        public static ParseUnit Atom(string x) => new ParseUnit(Type.Atom, x);
        public static ParseUnit Quote(string x) => new ParseUnit(Type.Quote, x);
        public static ParseUnit MacroVar(string x) => new ParseUnit(Type.MacroVar, x);
        public static ParseUnit MacroDef(string x) => new ParseUnit(Type.MacroDef, x);
        public static ParseUnit LambdaMacroParam() => new ParseUnit(Type.LambdaMacroParam);
        public static readonly ParseUnit Newline = new ParseUnit(Type.Newline);
        public static ParseUnit End() => new ParseUnit(Type.End);
        public static ParseUnit PartialMacroInvoke(Macro m, List<LPU> lpus) =>
            new ParseUnit(Type.PartialMacroInvoke, partialMacroInvoke: (m, lpus));
        public static ParseUnit MacroReinvoke(string m, List<LPU> lpus) =>
            new ParseUnit(Type.MacroReinvoke,  macroReinvoke: (m, lpus));
        public static ParseUnit Paren(List<LPU> lpus) => new ParseUnit(Type.Paren, nestVal: lpus);
        public static ParseUnit Words(List<LPU> lpus) => new ParseUnit(Type.Words, nestVal: lpus);
        public static ParseUnit NoSpaceWords(List<LPU> lpus) => new ParseUnit(Type.NoSpaceWords, nestVal: lpus);
        public static ParseUnit Postfix(List<LPU> lpus) => new ParseUnit(Type.Postfix, nestVal: lpus);

        public static ParseUnit Nest(List<LPU> lpus) => lpus.Count == 1 ? lpus[0].unit : Words(lpus);

        public T Match<T>(Func<string, T>? atom = null, Func<string, T>? quote = null, Func<string, T>? macroVar = null,
            Func<string, T>? macroDef = null, Func<T>? lambdaMacroParam = null, Func<T>? newline = null, 
            Func<T>? end = null, Func<Macro, List<LPU>, T>? partlMacroInv = null, Func<string,List<LPU>, T>? macroReinv = null,
            Func<List<LPU>, T>? paren = null, Func<List<LPU>, T>? words = null, Func<List<LPU>, T>? nswords = null, 
            Func<List<LPU>, T>? postfix = null, Func<T>? deflt = null) {
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
            if (type == Type.Postfix && postfix != null) return postfix(nestVal);
            return deflt!();
        }
        public ParseUnit Reduce() {
            ParseUnit reduce(List<LPU> lpus, Func<List<LPU>, ParseUnit> recons) =>
                lpus.Count == 1 ? lpus[0].unit.Reduce() : recons(lpus);
            if (type == Type.Paren) return this;
            if (type == Type.Words) return reduce(nestVal, Words);
            if (type == Type.NoSpaceWords) return reduce(nestVal, NoSpaceWords);
            if (type == Type.Postfix) return reduce(nestVal, Postfix);
            return this;
        }

        public ParseUnit CutTrailingNewline() {
            List<LPU> cut(List<LPU> lpus) {
                var lm1 = lpus.Count - 1;
                return lpus[lm1].unit.type == Type.Newline ? lpus.Take(lm1).ToList() : lpus;
            }
            if (type == Type.Paren) return Paren(cut(nestVal));
            if (type == Type.Words) return Words(cut(nestVal));
            if (type == Type.NoSpaceWords) return NoSpaceWords(cut(nestVal));
            if (type == Type.Postfix) return Postfix(cut(nestVal));
            return this;
            
        }
    }

    public class State {
        public readonly ImmutableDictionary<string, Macro> macros;
        public State(ImmutableDictionary<string, Macro> macros) {
            this.macros = macros;
        }
    }

    private static HashSet<char> notSimpleChars = new HashSet<char>() {
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

    private static LPU CompileMacroVariable(List<string> terms, Position loc) {
        var l = terms
            .Select((x, i) => string.IsNullOrEmpty(x) ?
                null :
                (LPU?) new LPU(i % 2 == 0 ?
                    ParseUnit.Atom(x) :
                    ParseUnit.MacroVar(x), loc))
            .FilterNone()
            .ToList();
        return l.Count == 1 ? l[0] : new LPU(ParseUnit.NoSpaceWords(l), loc);
    }

    private static Parser<T> FailErrorable<T>(Errorable<T> errb) => errb.Valid ?
        PReturn(errb.Value) :
        Fail<T>(string.Join("\n", errb.errors));
    
    private static Parser<LPU> InvokeMacroByName(string name, List<LPU> args) =>
        GetState<State>().SelectMany(state => state.macros.TryGetValue(name, out var m) ?
                FailErrorable(m.Invoke(args)) :
                Fail<LPU>($"No macro exists with name {name}.")
            , (s, lpu) => lpu);

    private static readonly Parser<LPU> CNewln =
        Char(COMMENT).IgThen(SkipManySatisfy(x => x != NEWLINE)).Optional()
            .IgThen(Newline)
            .IgThen(GetPosition)
            .FMap(p => new LPU(ParseUnit.Newline, p));

    private static Parser<List<LPU>> Words(bool allowNewline) {
        //This is required to avoid circular object definitions :(
        Parser<List<LPU>>? lazy = null;
        Parser<List<LPU>> LoadLazy() =>
            (allowNewline ? MainParserNL : MainParser).ThenIg(ILSpaces).Many1();
        return inp => (lazy ??= LoadLazy())(inp);
    }

    private static readonly Parser<List<LPU>> WordsTopLevel = Words(true);
    private static readonly Parser<List<LPU>> WordsInBlock = WordsTopLevel;
    private static readonly Parser<List<LPU>> WordsInline = Words(false);
    
    private static readonly Parser<List<LPU>> ParenArgs = Paren(
        WordsInBlock.Pipe(GetPosition,
            (words, p) => new LPU(ParseUnit.Nest(words), p))
    );

    private static readonly Parser<MacroArg> MacroPrmDecl =
        Macro.Prm.Pipe(
            Whitespace1.IgThen(
                Sequential(
                    WordsInBlock,
                    GetPosition,
                    (dflt, p) => ((List<LPU>, Position)?) (dflt, p))).OptionalOr(null!),
            (key, dp) => dp.HasValue ?
                new MacroArg(key, new LPU(ParseUnit.Nest(dp.Value.Item1), dp.Value.Item2)) :
                new MacroArg(key, null));//.Label("macro parameter");

    private static Parser<LPU> Locate(this Parser<ParseUnit> p) =>
        p.Pipe(GetPosition, (pu, loc) => new LPU(pu, loc));

    private static readonly Parser<LPU> OLMacroParser =
        Sequential(
                String(MACRO_OL_OPEN).IgThen(ILSpaces).IgThen(simpleString1),
                ILSpaces,
                WordsInline,
                CNewln,
                GetPosition,
                (key, _3, content, _4, l) => (key, content, l))
            .SelectMany(kcls =>
                    UpdateState<State>(s => new State(s.macros.SetItem(kcls.key,
                        Macro.Create(kcls.key, new List<MacroArg>(),
                            new LPU(ParseUnit.Words(kcls.content), kcls.l))))),
                (kcls, _) => new LPU(ParseUnit.MacroDef(kcls.key), kcls.l));//.Label("single-line macro parser (!!{)");

    private static readonly Parser<LPU> MacroParser =
        Between(MACRO_OPEN,
            Sequential(
                Whitespace,
                simpleString1,
                Paren(MacroPrmDecl),//.Label("macro parameters"),
                Whitespace.IgThen(WordsTopLevel),
                GetPosition,
                (_1, key, prms, content, loc) => (key, prms, content, loc))
            .SelectMany(kpcls => 
                UpdateState<State>(s => new State(s.macros.SetItem(kpcls.key, 
                    Macro.Create(kpcls.key, kpcls.prms, 
                        new LPU(ParseUnit.Words(kpcls.content), kpcls.loc))))),
                (kpcls, _) => new LPU(ParseUnit.MacroDef(kpcls.key), kpcls.loc)), 
            MACRO_CLOSE
        ).ThenIg(CNewln).Label("macro (bounded with !{ }!)");

    private static Parser<LPU> PropertyParser(string marker, string result) =>
        Sequential(
            String(marker), 
            ILSpaces, 
            WordsInline, 
            GetPosition, 
            (_1, _2, words, p) => new LPU(ParseUnit.Words(
                words.Prepend(new LPU(ParseUnit.Atom(result), p)).ToList()), p));

    private static readonly Parser<LPU> MacroReinvokeParser =
        Sequential(
            String(MACRO_REINVOKE),
            simpleString1,
            ParenArgs,
            GetPosition,
            (_, key, args, p) => new LPU(ParseUnit.MacroReinvoke(key, args), p));

    private static readonly Parser<LPU> MacroInvokeParser =
        Sequential(
            Char(MACRO_INVOKE),
            simpleString1,
            ParenArgs.OptionalOr(new List<LPU>()),
            (_, key, args) => (key, args))
        .Bind(ka => InvokeMacroByName(ka.key, ka.args));


    private static readonly Parser<LPU> MainParser = Choice(
        String("///").IgThen(SkipManySatisfy(_ => true)).FMap(_ => ParseUnit.End()).Locate(), //.Label("end of file"),
        String(LAMBDA_MACRO_PRM).Select(_ => ParseUnit.LambdaMacroParam()).Locate(),
        OLMacroParser,
        MacroParser,
        PropertyParser(PROP_MARKER, PROP_KW),
        PropertyParser(PROP2_MARKER, PROP2_KW),
        // ex. <%A%%B%> = <{A}{B}>. <%A%B> = <{A}B>. 
        Sequential(
            sepByAll2(simpleString0, Between(MACRO_VAR, Macro.Prm)).Attempt(),
            GetPosition,
            CompileMacroVariable),
        //Basic word of nonzero length
        simpleString1.Pipe(GetPosition, (s, l) => new LPU(ParseUnit.Atom(s), l)),
        Sequential(ParenArgs, GetPosition, (lpus, p) => new LPU(ParseUnit.Paren(lpus), p)),
        Between(OPEN_PF, WordsInBlock, CLOSE_PF).FMap(ParseUnit.Postfix).Locate(),
        // %A %B
        Char(MACRO_VAR).IgThen(Macro.Prm.Pipe(GetPosition, (prm, loc) => new LPU(ParseUnit.MacroVar(prm), loc))),
        MacroReinvokeParser,
        MacroInvokeParser,
        Bounded(QUOTE).FMap(ParseUnit.Quote).Locate()
    );
    
    private static readonly Parser<LPU> MainParserNL = CNewln.Or(MainParser);

    private static void PostfixSwap(LPU lpu) {
        if (lpu.unit.type.IsNestingType()) PostfixSwap(lpu.unit.nestVal);
    }
    private static void PostfixSwap(List<LPU> lpus) {
        for (int ii = 0; ii < lpus.Count; ++ii) {
            var me = lpus[ii];
            if (me.unit.type == ParseUnit.Type.Postfix) {
                PostfixSwap(lpus[ii + 1]);
                lpus[ii] = lpus[ii + 1];
                //Since this is destructive, multiple macro use must only perform swapping once
                lpus[++ii] = new LPU(ParseUnit.Words(me.unit.nestVal), me.location);
            }
            PostfixSwap(lpus[ii]);
        }
    }

    private static readonly Dictionary<string, string[]> Replacements = new Dictionary<string, string[]>() {
        {"{{", new[] {"{", "{"}},
        {"}}", new[] {"}", "}"}}
    };

    public abstract class ParsedUnit {
        public class Str : ParsedUnit {
            public readonly string Item;
            public Str(string value) {
                this.Item = value;
            }
        }
        public class Paren : ParsedUnit {
            public readonly (ParsedUnit, Position)[][] Item;
            public Paren((ParsedUnit, Position)[][] value) {
                this.Item = value;
            }
        }
    }

    private static ParsedUnit S(string s) => new ParsedUnit.Str(s);
    private static ParsedUnit P((ParsedUnit, Position)[][] p) => new ParsedUnit.Paren(p);

    private static Errorable<IEnumerable<string>> pFlatten(LPU lpu) {
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
            case ParseUnit.Type.Postfix:
                return ls.Select(pFlatten).Acc().Map(t => t.Join());
            case ParseUnit.Type.NoSpaceWords:
                return ls.Select(pFlatten).Acc().Map(arrs => {
                    var words = new List<string>() {""};
                    foreach (var arr in arrs) {
                        bool first = true;
                        foreach (var x in arr) {
                            if (first) words[words.Count - 1] += x;
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
                    $"The macro \"{m.name}\" was invoked with {args.Count(a => a.unit.type != ParseUnit.Type.LambdaMacroParam)} " +
                    $"arguments ({m.nprms} required)");
            default:
                return Errorable<IEnumerable<string>>.Fail(
                    "Illegal unit in output, please file a bug report");
        }
    }

    private static readonly (ParsedUnit, Position)[] noParsedUnits = { };
    private static Errorable<IEnumerable<(ParsedUnit, Position)>> pFlatten2(LPU lpu) {
        var u = lpu.unit;
        var s = u.sVal;
        var ls = u.nestVal;
        //Since the lambdas reference lpu.location, it's actually highly inefficient to use Match to do this.
        switch (u.type) {
            case ParseUnit.Type.Atom:
                return s.Length > 0 ?
                    Replacements.TryGetValue(s, out var ss) ?
                        Errorable<IEnumerable<(ParsedUnit, Position)>>.OK(ss.Select(x => (S(x), lpu.location))) :
                        new[] {(S(s), lpu.location)} :
                    noParsedUnits;
            case ParseUnit.Type.Quote:
                return new[] {(S(s), lpu.location)};
            case ParseUnit.Type.Paren:
                return ls.Select(pFlatten2).Acc().Map(l => (IEnumerable<(ParsedUnit, Position)>) 
                    new[] {(P(l.Select(xs => xs.ToArray()).ToArray()), lpu.location)});
            case ParseUnit.Type.Words:
                return ls.TakeWhile(l => l.unit.type != ParseUnit.Type.End).Select(pFlatten2).Acc()
                    .Map(t => t.Join());
            case ParseUnit.Type.Postfix:
                return ls.Select(pFlatten2).Acc().Map(t => t.Join());
            case ParseUnit.Type.NoSpaceWords:
                return ls.Select(pFlatten).Acc().Map(t =>
                    (IEnumerable<(ParsedUnit, Position)>) new[] {(S(string.Concat(t.Join())), lpu.location)});
            case ParseUnit.Type.Newline:
                return new[] {(S("\n"), lpu.location)};
            case ParseUnit.Type.MacroDef:
                return noParsedUnits;
            case ParseUnit.Type.MacroVar:
                return Errorable<IEnumerable<(ParsedUnit, Position)>>.Fail($"Found a macro variable \"%{s}\" in the output.");
            case ParseUnit.Type.LambdaMacroParam:
                return Errorable<IEnumerable<(ParsedUnit, Position)>>.Fail(
                    "Found an unbound macro argument (!$) in the output.");
            case ParseUnit.Type.PartialMacroInvoke:
                var (m, args) = u.partialMacroInvoke;
                return Errorable<IEnumerable<(ParsedUnit, Position)>>.Fail(
                    $"The macro \"{m.name}\" was invoked with {args.Count(a => a.unit.type != ParseUnit.Type.LambdaMacroParam)} " +
                    $"arguments ({m.nprms} required)");
            default:
                return Errorable<IEnumerable<(ParsedUnit, Position)>>.Fail(
                    "Illegal unit in output, please file a bug report");
        }
    }


    private static readonly Parser<LPU> FullParser =
        Sequential(
            SetState(new State(ImmutableDictionary<string, Macro>.Empty)),
            WordsTopLevel,
            EOF,
            GetPosition,
            (_, words, __, p) => new LPU(ParseUnit.Words(words), p)); 
        
    /*
    private static Errorable<LPU> _SMParserExec(string s) {
        var result = parse(FullParser, s);
        return result.IsFaulted ? Errorable<LPU>.Fail(result.Reply.Error.ToString()) : result.Reply.Result;
    }*/
    private static Errorable<LPU> _SMParserExec(string s) {
        var result = FullParser(new InputStream("State Machine", s, default!));
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            Errorable<LPU>.Fail(result.Error?.Show(s) ?? "Parsing failed, but it's unclear why.");
    }

    public static Errorable<IEnumerable<string>> SMParserExec(string s) =>
        _SMParserExec(s).Bind(lpu => {
            PostfixSwap(lpu);
            return pFlatten(lpu);
        });

    public static Errorable<string> RemakeSMParserExec(string s) => SMParserExec(s).Map(ss => string.Join(" ", ss));

    public static Errorable<(ParsedUnit, Position)[]> SMParser2Exec(string s) =>
        _SMParserExec(s).Bind(lpu => {
            PostfixSwap(lpu);
            return pFlatten2(lpu).Map(x => x.ToArray());
        });


}
}