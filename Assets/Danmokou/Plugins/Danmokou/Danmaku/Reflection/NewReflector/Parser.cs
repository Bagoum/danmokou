using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.SM;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using UnityEngine;
using static Danmokou.Reflection2.Lexer;
using static Mizuhashi.Combinators;
using Op = Mizuhashi.Operator<Danmokou.Reflection2.Lexer.Token, Danmokou.Reflection2.ST, Danmokou.Reflection2.Lexer.Token>;

namespace Danmokou.Reflection2 {
/// <summary>
/// Parser for BDSL2.
/// </summary>
public static class Parser {
    public static readonly Parser<Token, Token> _Ident = TokenOfType(TokenType.Identifier);
    public static readonly Parser<Token, Maybe<Token>> IdentTypeSuffixStr =
        op("::").IgThen(TokenOfTypes(TokenType.Identifier, TokenType.TypeIdentifier)).Opt();
    public static readonly Parser<Token, (PositionRange, Type)?> IdentTypeSuffix = inp => {
        var rtyp = IdentTypeSuffixStr(inp);
        if (!rtyp.Result.Try(out var mtypstr))
            return rtyp.CastFailure<(PositionRange, Type)?>();
        (PositionRange, Type)? type = null;
        if (mtypstr.Try(out var typstr)) {
            var typeParse = TypeFromToken(typstr);
            if (typeParse.TryR(out var err))
                return new(err, rtyp.Start, rtyp.End);
            type = (typstr.Position, typeParse.Left);
        }
        return new(type, rtyp.Error, rtyp.Start, rtyp.End);
    };
    private static Either<Type, ParserError> TypeFromToken(Token typstr) {
        var tDef = ParseType(typstr.Content);
        if (tDef.IsRight)
            return tDef.Right.Error;
        var typ = tDef.Left.TryCompile();
        if (typ.IsRight)
            return new ParserError.Failure(typ.Right);
        return typ.Left;
    }

    public static readonly Parser<Token, (Token id, (PositionRange, Type)? typ)> IdentAndType = 
        _Ident.Then(IdentTypeSuffix);
    
    private static readonly Parser<Token, Token> openParen = TokenOfType(TokenType.OpenParen);
    private static readonly Parser<Token, Token> closeParen = TokenOfType(TokenType.CloseParen);
    private static readonly Parser<Token, Token> openBrace = TokenOfType(TokenType.OpenBrace);
    private static readonly Parser<Token, Token> closeBrace = TokenOfType(TokenType.CloseBrace);
    private static readonly Parser<Token, Token> comma = TokenOfType(TokenType.Comma);
    private static readonly Parser<Token, Token> semicolon = TokenOfType(TokenType.Semicolon);

    private static Parser<Token, Token> ImplicitBreak(TokenType breaker) {
        var err = new ParserError.Expected($"{breaker.ToString().ToLower()} or uniform newline indentation after previous line");
        return input => {
            if (!input.Empty) {
                if (input.Next.Type == breaker)
                    return new(new(input.Next), null, input.Index, input.Step(1));
                else if ((input.Next.Flags & TokenFlags.ImplicitBreak) > 0)
                    return new(new(input.Next), null, input.Index, input.Index);
            }
            return new(err, input.Index);
        };
    }
    public static Parser<Token, Token> Kw(string keyword) => TokenOfTypeValue(TokenType.Keyword, keyword);

    public static Parser<Token, Token> Flags(TokenFlags flags, string? expected = null) =>
        Satisfy((Token inp) => (inp.Flags & flags) > 0, expected).IsPresent();
    public static Parser<Token, Unit> NotFlags(TokenFlags flags, string? unexpected = null) =>
        Satisfy((Token inp) => (inp.Flags & flags) > 0).IsNotPresent(unexpected);

    public static readonly Parser<Token, Token> Whitespace = Flags(TokenFlags.PrecededByWhitespace, "whitespace");
    public static readonly Parser<Token, Unit> NoWhitespace = NotFlags(TokenFlags.PrecededByWhitespace, "whitespace");
    public static readonly Parser<Token, Unit> NoWhitespaceAfter = 
        NotFlags(TokenFlags.PostcededByWhitespace, "whitespace");
    public static readonly Parser<Token, Token> Newline = Flags(TokenFlags.PrecededByNewline, "newline");


    public static MethodSignature Meth(Type t, string method) =>
        ExFunction.WrapAny(t, method).Mi.Signature();
    private static ST FnIdentFor(Token t, params MethodSignature[] overloads) =>
        new ST.FnIdent(t.Position, overloads.Select(o => o.Call(t.Content)).ToArray());

    private static Func<ST, Token, ST, ST> InfixCaller(params MethodSignature[] overloads) =>
        (a, t, b) => new ST.FunctionCall(a.Position.Merge(b.Position), FnIdentFor(t, overloads), a, b);
    private static Func<ST, Token, ST, ST> InfixCallerEquivOverloads(params MethodSignature[] overloads) =>
        (a, t, b) => new ST.FunctionCall(a.Position.Merge(b.Position), FnIdentFor(t, overloads), a, b) { OverloadsInterchangeable = true };
    private static Func<Token, ST, ST> PrefixCaller(params MethodSignature[] overloads) =>
        (t, x) => new ST.FunctionCall(t.Position.Merge(x.Position), FnIdentFor(t, overloads), x);
    private static Func<ST, Token, ST> PostfixCaller(params MethodSignature[] overloads) =>
        (x, t) => new ST.FunctionCall(x.Position.Merge(t.Position), FnIdentFor(t, overloads), x);
    
    private static Parser<Token, Token> op(string op) => TokenOfTypeValue(TokenType.Operator, op);
    private static Parser<Token, Token> sop(string sop) => TokenOfTypeValue(TokenType.SpecialOperator, sop);
    private static Parser<Token, Token> opNoFlag(string op, TokenFlags f, string desc) => 
        TokenOfTypeValueNotFlag(TokenType.Operator, op, f, desc);
    private static Op infix(string op, Associativity assoc, int precedence,
        params MethodSignature[] overloads) => 
        new Op.Infix(TokenOfTypeValue(TokenType.Operator, op), InfixCaller(overloads), assoc, precedence);
    private static Op prefix(string op, int precedence,
        params MethodSignature[] overloads) => 
        new Op.Prefix(TokenOfTypeValue(TokenType.Operator, op), PrefixCaller(overloads), precedence);

    private static Op assigner(string op, string method) =>
        infix(op, Associativity.Right, 2, Meth(typeof(ExMAssign), method));
    

    //these operators are higher precedence than curried function application.
    // eg. f x op y = f(x op y)
    public static readonly Op[] tightOperators = {
        new Op.Postfix(opNoFlag("++", TokenFlags.PrecededByWhitespace, "postfix operator `++`"), 
            PostfixCaller(Meth(typeof(ExMAssign), nameof(ExMAssign.PostIncrement))), 20),
        new Op.Postfix(opNoFlag("--", TokenFlags.PrecededByWhitespace, "postfix operator `--`"), 
            PostfixCaller(Meth(typeof(ExMAssign), nameof(ExMAssign.PostDecrement))), 20),
        
        new Op.Prefix(opNoFlag("++", TokenFlags.PostcededByWhitespace, "prefix operator `++`"), 
            PrefixCaller(Meth(typeof(ExMAssign), nameof(ExMAssign.PreIncrement))), 18),
        new Op.Prefix(opNoFlag("--", TokenFlags.PostcededByWhitespace, "prefix operator `--`"), 
            PrefixCaller(Meth(typeof(ExMAssign), nameof(ExMAssign.PreDecrement))), 18),
        
        //NB: It is critical to have the noWhitespace parse for +/- operators, because if we don't,
        // then curried function application of a unary number becomes higher precedence than arithmetic.
        // eg. `x - y` has higher precedence as Curried(x, Negate(y)) than Subtract(x, y).
        //F# handles this quite well by parsing only no-whitespace +/- as unary.
        new Op.Prefix(opNoFlag("+", TokenFlags.PostcededByWhitespace, "prefix operator `+`"), 
            (t, x) => x with { Position = t.Position.Merge(x.Position) }, 18),
        new Op.Prefix(opNoFlag("-", TokenFlags.PostcededByWhitespace, "prefix operator `-`"), 
            PrefixCaller(Meth(typeof(ExM), nameof(ExM.Negate))), 18),
        prefix("!", 18, Meth(typeof(ExMPred), nameof(ExMPred.Not))),
    };
    //these operators are lower precedence than curried function application.
    //eg. f x op y = f(x) op y
    public static readonly Op[] looseOperators = {
        infix("%", Associativity.Left, 14, Meth(typeof(ExMMod), nameof(ExMMod.Modulo))),
        //ideally for arithmetic operations we would statically lookup all the defined operators,
        // but that's kind of overkill, so we just include the basics:
        //  float*T, T*float, T/float, T+T, T-T, and the other fixed ones
        //todo: what about ints? are we actually supporting them?
        new Op.Infix(TokenOfTypeValue(TokenType.Operator, "*"), 
            InfixCallerEquivOverloads(
                Meth(typeof(ExM), nameof(ExM.Mul)),
                Meth(typeof(ExM), nameof(ExM.MulRev))
            ), Associativity.Left, 14),
        infix("/", Associativity.Left, 14, Meth(typeof(ExM), nameof(ExM.Div))),
        //infix("//", Associativity.Left, 14, Lift(typeof(ExM), nameof(ExM.FDiv))),
        infix("^", Associativity.Left, 14, Meth(typeof(ExM), nameof(ExM.Pow))),

        infix("+", Associativity.Left, 12, Meth(typeof(ExM), nameof(ExM.Add))),
        infix("-", Associativity.Left, 12, Meth(typeof(ExM), nameof(ExM.Sub))),

        infix("<", Associativity.Left, 10, Meth(typeof(ExMPred), nameof(ExMPred.Lt))),
        infix(">", Associativity.Left, 10, Meth(typeof(ExMPred), nameof(ExMPred.Gt))),
        infix("<=", Associativity.Left, 10, Meth(typeof(ExMPred), nameof(ExMPred.Leq))),
        infix(">=", Associativity.Left, 10, Meth(typeof(ExMPred), nameof(ExMPred.Geq))),

        infix("==", Associativity.Left, 8, Meth(typeof(ExMPred), nameof(ExMPred.Eq))),
        infix("!=", Associativity.Left, 8, Meth(typeof(ExMPred), nameof(ExMPred.Neq))),
        //& is defined to be the same as &&, not bitwise
        infix("&&", Associativity.Left, 6, Meth(typeof(ExMPred), nameof(ExMPred.And))),
        infix("||", Associativity.Left, 6, Meth(typeof(ExMPred), nameof(ExMPred.Or))),
        infix("&", Associativity.Left, 6, Meth(typeof(ExMPred), nameof(ExMPred.And))),
        infix("|", Associativity.Left, 6, Meth(typeof(ExMPred), nameof(ExMPred.Or))),

        assigner("=", nameof(ExMAssign.Assign)),
        assigner("+=", nameof(ExMAssign.AddAssign)),
        assigner("-=", nameof(ExMAssign.SubAssign)),
        assigner("*=", nameof(ExMAssign.MulAssign)),
        assigner("/=", nameof(ExMAssign.DivAssign)),
        assigner("%=", nameof(ExMAssign.ModAssign)),
        assigner("&=", nameof(ExMAssign.AndAssign)),
        assigner("|=", nameof(ExMAssign.OrAssign))
    };

    private static Parser<Token, T> Paren1<T>(Parser<Token, T> p) {
        var err = new ParserError.Failure(
            "Expected parentheses with only a single object. The parentheses should close here.");
        return inp => {
            var ropen = openParen(inp);
            if (!ropen.Result.Valid)
                return ropen.CastFailure<T>();
            var rval = p(inp);
            if (!rval.Result.Valid)
                return new(rval.Result, rval.Error, ropen.Start, rval.End);
            var rclose = closeParen(inp);
            if (!rclose.Result.Valid)
                return new(Maybe<T>.None, new LocatedParserError(rclose.Start, err), ropen.Start, rclose.End);
            return new(rval.Result, rval.Error, ropen.Start, rclose.End);
        };
    }
    
    private static Parser<Token, (PositionRange allPosition, List<T> args)> Paren<T>(Parser<Token, T> p, bool atleastOne = false) {
        var args = p.SepBy(comma, atleastOne);
        return inp => {
            var ropen = openParen(inp);
            if (!ropen.Result.Valid)
                return ropen.CastFailure<(PositionRange, List<T>)>();
            var rval = args(inp);
            if (!rval.Result.Valid)
                return new(Maybe<(PositionRange, List<T>)>.None, rval.Error, ropen.Start, rval.End);
            var rclose = closeParen(inp);
            if (!rclose.Result.Valid)
                return new(Maybe<(PositionRange, List<T>)>.None, rclose.Error, ropen.Start, rclose.End);
            return new((ropen.Result.Value.Position.Merge(rclose.Result.Value.Position), rval.Result.Value), 
                rval.Error, ropen.Start, rclose.End);
        };
    }

    private static readonly MethodSignature constFloat =
        MethodSignature.Get(ExFunction.WrapAny(typeof(AtomicBPYRepo), "Const").Mi);

    private static readonly ParserError blockKWErr = new ParserError.Expected("`block` or `b{` keyword");
    private static readonly Parser<Token, Token> blockKW = input => {
        if (input.Empty || input.Next.Type != TokenType.Keyword || (input.Next.Content != "block" && input.Next.Content != "b"))
            return new(blockKWErr, input.Index);
        else
            return new(new(input.Next), null, input.Index, input.Step(1));
    };
    
    //Atom: identifier; number/string/etc; parenthesized value; block 
    private static readonly Parser<Token, ST> atom = ChoiceL(
        "atom (identifier, number, array, block, or parenthesized expression)",
        //Identifier
        IdentAndType.FMap(idtyp => new ST.Ident(idtyp.id, idtyp.typ) as ST),
        //num/string/v2rv2
        Num.FMap(t => new ST.Number(t.Position, t.Content) as ST),
        TokenOfType(TokenType.ValueKeyword).FMap(t => t.Content switch {
            "true" => new ST.TypedValue<bool>(t.Position, true) { Kind = SymbolKind.Boolean } as ST,
            "false" => new ST.TypedValue<bool>(t.Position, false) { Kind = SymbolKind.Boolean },
            _ => new ST.DefaultValue(t.Position)
        }),
        TokenOfType(TokenType.String).FMap(t => new ST.TypedValue<string>(t.Position, t.Content) 
            { Kind = SymbolKind.String} as ST),
        TokenOfType(TokenType.LString).Bind(t => {
            LString v;
            var diagnostics = Array.Empty<ReflectDiagnostic>();
            if (LocalizedStrings.IsLocalizedStringReference(t.Content))
                if (LocalizedStrings.TryFindReference(t.Content) is { } ls)
                    v = ls;
                else if (Reflector.SOFT_FAIL_ON_UNMATCHED_LSTRING) {
                    v = $"Unresolved LocalizedString {t.Content}";
                    diagnostics = new ReflectDiagnostic[] {
                        new ReflectDiagnostic.Warning(t.Position,
                            $"Couldn't resolve LocalizedString {t.Content}. It may work properly in-game.")
                    };
                } else
                    return new ParseResult<ST>(
                        new ParserError.Failure($"Couldn't resolve LocalizedString {t.Position}"),
                        t.Position.Start.Index, t.Position.End.Index);
            else
                v = t.Content;
            return new ParseResult<ST>(new ST.TypedValue<LString>(t.Position, v) {
                Kind = SymbolKind.String,
                Diagnostics = diagnostics
            }, null, t.Position.Start.Index, t.Position.End.Index);
        }),
        TokenOfType(TokenType.V2RV2).FMap(t => new ST.TypedValue<V2RV2>(t.Position, DMath.Parser.ParseV2RV2(t.Content)) 
                { Kind = SymbolKind.Number } as ST),
        //Parenthesized value/tuple
        Paren(Value).FMap(vs => vs.args.Count == 1 ? vs.args[0] : new ST.Tuple(vs.allPosition, vs.args)).LabelV("tuple"),
        //Block
        Sequential(blockKW, BracedBlock,
            (o, b) => b with { Position = o.Position.Merge(b.Position) } as ST).LabelV("block"),
        //Array
        Sequential(openBrace, ((Parser<Token,ST>)Value).SepBy(ImplicitBreak(TokenType.Comma)), closeBrace,
            (o, vals, c) => new ST.Array(o.Position.Merge(c.Position), vals.ToArray()) as ST).LabelV("array")
    );

    private static readonly ParserError termMemberFollowErr =
        new ParserError.Expected("member access x.y and/or function application f(x, y)");
    private static readonly Parser<Token, (Maybe<Token>, Maybe<(PositionRange, List<ST>)>)> termMemberFn =
        op(".").IgThen(Ident).Opt().Then(NoWhitespace.IgThen(Paren(Value)).Opt());
    private static readonly Parser<Token, (Maybe<Token>, Maybe<(PositionRange, List<ST>)>)> termMemberFnFollow = inp => {
            var follow = termMemberFn(inp);
            if (follow.Result.Try(out var lr)) {
                if (!lr.Item1.Valid && !lr.Item2.Valid)
                    return follow.AsSameError(termMemberFollowErr);
            }
            return follow;
        };
    
    //Term: member access `x.y`, member function `x.f(y)`, 
    // C#-style function application `f(x, y)`, partial function call `$(f, x)`,
    // type specifier 
    //Haskell-style function application `f x y` is handled in term2.
    private static readonly Parser<Token, ST> term =
        Choice(
            Sequential(atom, termMemberFnFollow.Many(),
                (x, seqs) => {
                    for (int ii = 0; ii < seqs.Count; ++ii) {
                        var (mem, fn) = seqs[ii];
                        if (mem.Try(out var m))
                            if (fn.Try(out var f))
                                x = new ST.MemberFunction(x.Position.Merge(f.Item1), x, new ST.Ident(m), f.Item2);
                            else
                                x = new ST.MemberAccess(x, new ST.Ident(m));
                        else if (fn.Try(out var f))
                            x = new ST.FunctionCall(x.Position.Merge(f.Item1), x, f.Item2.ToArray());
                    }
                    return x;
                }
            ),
            sop("$").IgThen(NoWhitespace).IgThen(
                Paren1(Combinators.SepBy(Value, comma, true, Ident.FMap(id => new ST.Ident(id) as ST)))).FMap(
                    args => new ST.PartialFunctionCall(
                        PositionRange.Merge(args.Select(a => a.Position)), 
                        (ST.Ident)args[0], args.Skip(1).ToArray()) as ST
                )
        );

    //Term + tight operators
    private static readonly Parser<Token, ST> termOps1 = ParseOperators(tightOperators, term);

    private static readonly Parser<Token, Token> curryFnAppSep = NotFlags(TokenFlags.ImplicitBreak, 
        "indent: curried function application across newlines must change the indentation level").IgThen(Whitespace);
    
    //Term + tight operators + curried function application
    private static readonly Parser<Token, ST> term2 = inp => {
        var rf = termOps1(inp);
        //First element must be Ident
        if (!rf.Result.Try(out var f) || f is not ST.Ident)
            return rf;
        var start = rf.Start;
        while (true) {
            var rsep = curryFnAppSep(inp);
            //this separator cannot fatal
            if (rsep.Status != ResultStatus.OK)
                return new(f, rf.MergeErrors(rsep), start, rf.End);
            rf = termOps1(inp);
            if (rf.Status == ResultStatus.FATAL)
                return rf;
            if (rf.Status == ResultStatus.ERROR && !rsep.Consumed)
                return new(f, rsep.MergeErrors(rf), start, rf.End);
            f = new ST.CurriedFunctionCall(f, rf.Result.Value);
        }
    };

    //Loose operators or lambda
    //Note that this would be called an "expression" in most parsers but I won't call it that to avoid ambiguity
    // with Linq.Expression
    private static readonly Parser<Token, ST> term2Ops = ChoiceL("value expression",
        ParseOperators(looseOperators, term2)
        //todo lambda
    );

    //Conditional expression
    private static readonly Parser<Token, ST> value = Sequential(
        term2Ops,
        op("?").IgThen(term2Ops).ThenIg(op(":")).Then(term2Ops).Opt(),
        (a, b) => {
            if (!b.Try(out var rest))
                return a;
            return new ST.IfExpression(a, rest.a, rest.b);
        }
    );
    
    private static ParseResult<ST> Value(InputStream<Token> inp) => value(inp);

    private static readonly Parser<Token, ST> varInit =
        Sequential(Kw("var").Or(Kw("hvar")), IdentAndType, op("="), value,
            (kw, idTyp, eq, res) => {
                var (id, typ) = idTyp;
                return new ST.VarDeclAssign(kw.Position,
                        new VarDecl(id.Position, kw.Content == "hvar", typ?.Item2, id.Content), eq.Position, res)
                    { TypeKwPos = typ?.Item1 } as ST;
            });

    private static readonly Parser<Token, ST> functionDecl =
        Sequential(Kw("function"), Ident, Paren(IdentAndType), IdentTypeSuffix, BracedBlock,
            (fn, name, args, type, body) => new ST.FunctionDef(fn.Position, name, args.args, type, body) as ST);
            
    
    //Statement: value, variable declaration, or void-type block (if/else, for, while)
    private static readonly Parser<Token, ST> statement = ChoiceL("statement",
        value,
        varInit,
        functionDecl,
        Sequential(Kw("return"), value, (kw, v) => new ST.Return(kw.Position, v) as ST),
        Kw("continue").FMap(t => new ST.Continue(t.Position) as ST),
        Kw("break").FMap(t => new ST.Break(t.Position) as ST),
        Sequential(Kw("if"), Paren1(value), BracedBlock, Kw("else").Then(BracedBlock).Opt()
            , (kwif, cond, iftrue, iffalse) => {
                var els = iffalse.ValueOrSNull();
                return new ST.IfStatement(kwif.Position, els?.a.Position, cond, iftrue, els?.b) as ST;
            })
        //todo: if/else, for, while
    );
    private static readonly Parser<Token, List<ST>> statements = 
        statement.ThenIg(ImplicitBreak(TokenType.Semicolon)).Many()
            .Label("statement block");
    private static readonly Parser<Token, ST.Block> bracedBlock =
        Sequential(openBrace, statements, closeBrace,
            (o, stmts, c) => new ST.Block(o.Position.Merge(c.Position), stmts));
    private static ParseResult<ST.Block> BracedBlock(InputStream<Token> inp) => bracedBlock(inp);


    private static readonly Parser<Token, ST.Block> fullScript = 
        statements.ThenIg(EOF<Token>()).FMap(x => new ST.Block(x));

    public static Either<ST.Block, LocatedParserError> Parse(string source, Token[] tokens, out InputStream<Token> stream) {
        var result = fullScript(stream = new InputStream<Token>(
            tokens, "BDSL2 parser", witness: new TokenWitnessCreator(source)));
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            (result.Error ?? new(0, new ParserError.Failure("Parsing failed, but it's unclear why.")));
    }

    public abstract record TypeDef {
        public abstract Either<Type, string> TryCompile();

        public record Atom(string Type) : TypeDef {
            private static readonly Dictionary<string, Type> atomicTypes = new() {
                { "int", typeof(int) },
                { "float", typeof(float) },
                { "f", typeof(float) },
                { "string", typeof(string) },
                { "Vector2", typeof(Vector2) },
                { "v2", typeof(Vector2) },
                { "Vector3", typeof(Vector3) },
                { "v3", typeof(Vector3) },
                { "Vector4", typeof(Vector4) },
                { "v4", typeof(Vector4) },
                { "V2RV2", typeof(V2RV2) },
                { "rv2", typeof(V2RV2) },
                { "SM", typeof(StateMachine) }
            };

            public override Either<Type, string> TryCompile() {
                if (atomicTypes.TryGetValue(Type, out var t))
                    return t;
                return $"Couldn't recognize type {Type}";
            }
        }

        public record Generic(string Type, List<TypeDef> Args) : TypeDef {
            private static readonly Dictionary<string, Type> genericTypes = new() {
                { "List", typeof(List<>) },
                //todo: fn type
            };

            public override Either<Type, string> TryCompile() {
                if (genericTypes.TryGetValue(Type, out var t)) {
                    if (t.GetGenericArguments().Length != Args.Count)
                        return $"Incorrect number of type parameters for {Type}: " +
                               $"{Args.Count} required, {t.GetGenericArguments().Length} provided";
                    return Args
                        .Select(a => a.TryCompile())
                        .SequenceL()
                        .FMapL(args => t.MakeGenericType(args.ToArray()));
                }
                return $"Couldn't recognize type {Type}";
            }
        }

        public record Array(TypeDef Arg) : TypeDef {
            public override Either<Type, string> TryCompile() => Arg.TryCompile().FMapL(a => a.MakeArrayType());
        }
    }


    private static readonly Parser<char, TypeDef> typeParser =
        Sequential(
            Sequential(Satisfy(char.IsLetter), ManySatisfy(c => char.IsLetterOrDigit(c) || c == '_'), (a, b) => $"{a}{b}")
                .Label("simple type name"),
            Combinators.Between('<', ((Parser<char, TypeDef>)TypeParser).SepBy1(Char(',').IgThen(WhitespaceIL)), '>').Opt(),
            Combinators.Between('[', WhitespaceIL, ']').Many(),
            (x, gen, arr) => {
                var td = gen.Try(out var g) ? new TypeDef.Generic(x, g) : new TypeDef.Atom(x) as TypeDef;
                for (int ii = 0; ii < arr.Count; ++ii)
                    td = new TypeDef.Array(td);
                return td;
            }
        ).Label("type");
    
    private static ParseResult<TypeDef> TypeParser(InputStream<char> inp) => typeParser(inp);
    
    public static Either<TypeDef, LocatedParserError> ParseType(string source) {
        var result = typeParser(new InputStream<char>(source, "Type parser"));
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            (result.Error ?? new(0, new ParserError.Failure("This is not a valid type definition.")));
    }
}
}