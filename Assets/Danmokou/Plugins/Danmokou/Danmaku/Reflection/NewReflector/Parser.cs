using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.SM;
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
    public static readonly Parser<Token, Token> IdentOrType = TokenOfTypes(TokenType.Identifier, TokenType.TypeIdentifier);
    private static readonly Parser<Token, Token> openParen = TokenOfType(TokenType.OpenParen);
    private static readonly Parser<Token, Token> closeParen = TokenOfType(TokenType.CloseParen);
    private static readonly Parser<Token, Token> openBrace = TokenOfType(TokenType.OpenBrace);
    private static readonly Parser<Token, Token> closeBrace = TokenOfType(TokenType.CloseBrace);
    private static readonly Parser<Token, Token> comma = TokenOfType(TokenType.Comma);
    private static readonly Parser<Token, Token> semicolon = TokenOfType(TokenType.Semicolon);

    private static Parser<Token, Token> ImplicitBreak(TokenType breaker) {
        //var terr = new ParserError.Expected($"{breaker}");
        var err = new ParserError.Expected($"{breaker}, or uniform newline indentation");
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


    public static Reflector.LiftedMethodSignature<TExArgCtx> Lift(Type t, string method) =>
        ExFunction.WrapAny(t, method).Mi.Signature().Lift<TExArgCtx>();
    private static ST FnIdentFor(Token t, params Reflector.MethodSignature[] overloads) =>
        new ST.FnIdent(t.Position, overloads.Select(o => o.Call(t.Content)).ToArray());

    private static Func<ST, Token, ST, ST> InfixCaller(params Reflector.MethodSignature[] overloads) =>
        (a, t, b) => new ST.FunctionCall(a.Position.Merge(b.Position), FnIdentFor(t, overloads), a, b);
    private static Func<Token, ST, ST> PrefixCaller(params Reflector.MethodSignature[] overloads) =>
        (t, x) => new ST.FunctionCall(t.Position.Merge(x.Position), FnIdentFor(t, overloads), x);
    private static Func<ST, Token, ST> PostfixCaller(params Reflector.MethodSignature[] overloads) =>
        (x, t) => new ST.FunctionCall(x.Position.Merge(t.Position), FnIdentFor(t, overloads), x);
    
    private static Parser<Token, Token> op(string op) => TokenOfTypeValue(TokenType.Operator, op);
    private static Parser<Token, Token> opNoFlag(string op, TokenFlags f, string desc) => 
        TokenOfTypeValueNotFlag(TokenType.Operator, op, f, desc);
    private static Op infix(string op, Associativity assoc, int precedence,
        params Reflector.MethodSignature[] overloads) => 
        new Op.Infix(TokenOfTypeValue(TokenType.Operator, op), InfixCaller(overloads), assoc, precedence);
    private static Op prefix(string op, int precedence,
        params Reflector.MethodSignature[] overloads) => 
        new Op.Prefix(TokenOfTypeValue(TokenType.Operator, op), PrefixCaller(overloads), precedence);

    private static Op assigner(string op, string method) =>
        infix(op, Associativity.Right, 2, Lift(typeof(ExMAssign), method));
    

    //these operators are higher precedence than partial function application.
    // eg. f x op y = f(x op y)
    public static readonly Op[] tightOperators = {
        new Op.Postfix(opNoFlag("++", TokenFlags.PrecededByWhitespace, "postfix operator `++`"), 
            PostfixCaller(Lift(typeof(ExMAssign), nameof(ExMAssign.PostIncr))), 20),
        new Op.Postfix(opNoFlag("--", TokenFlags.PrecededByWhitespace, "postfix operator `--`"), 
            PostfixCaller(Lift(typeof(ExMAssign), nameof(ExMAssign.PostDecr))), 20),
        
        new Op.Prefix(opNoFlag("++", TokenFlags.PostcededByWhitespace, "prefix operator `++`"), 
            PrefixCaller(Lift(typeof(ExMAssign), nameof(ExMAssign.PreIncr))), 18),
        new Op.Prefix(opNoFlag("--", TokenFlags.PostcededByWhitespace, "prefix operator `--`"), 
            PrefixCaller(Lift(typeof(ExMAssign), nameof(ExMAssign.PreDecr))), 18),
        
        //NB: It is critical to have the noWhitespace parse for +/- operators, because if we don't,
        // then partial function application of a unary number becomes higher precedence than arithmetic.
        // eg. `x - y` has higher precedence as Partial(x, Negate(y)) than Subtract(x, y).
        //F# handles this quite well by parsing only no-whitespace +/- as unary.
        new Op.Prefix(opNoFlag("+", TokenFlags.PostcededByWhitespace, "prefix operator `+`"), 
            (t, x) => x with { Position = t.Position.Merge(x.Position) }, 18),
        new Op.Prefix(opNoFlag("-", TokenFlags.PostcededByWhitespace, "prefix operator `-`"), 
            PrefixCaller(Lift(typeof(ExM), nameof(ExM.Negate))), 18),
        prefix("!", 18, Lift(typeof(ExMPred), nameof(ExMPred.Not))),
    };
    //these operators are lower precedence than partial function application.
    //eg. f x op y = f(x) op y
    public static readonly Op[] looseOperators = {
        infix("%", Associativity.Left, 14, Lift(typeof(ExMMod), nameof(ExMMod.Modulo))),
        //ideally for arithmetic operations we would statically lookup all the defined operators,
        // but that's kind of overkill, so we just include the basics:
        //  float*T, T*float, T/float, T+T, T-T, and the other fixed ones
        //todo: what about ints? are we actually supporting them?
        infix("*", Associativity.Left, 14, Lift(typeof(ExM), nameof(ExM.Mul)),
            Lift(typeof(ExM), nameof(ExM.MulRev))),
        infix("/", Associativity.Left, 14, Lift(typeof(ExM), nameof(ExM.Div))),
        //infix("//", Associativity.Left, 14, Lift(typeof(ExM), nameof(ExM.FDiv))),
        infix("^", Associativity.Left, 14, Lift(typeof(ExM), nameof(ExM.Pow))),

        infix("+", Associativity.Left, 12, Lift(typeof(ExM), nameof(ExM.Add))),
        infix("-", Associativity.Left, 12, Lift(typeof(ExM), nameof(ExM.Sub))),

        infix("<", Associativity.Left, 10, Lift(typeof(ExMPred), nameof(ExMPred.Lt))),
        infix(">", Associativity.Left, 10, Lift(typeof(ExMPred), nameof(ExMPred.Gt))),
        infix("<=", Associativity.Left, 10, Lift(typeof(ExMPred), nameof(ExMPred.Leq))),
        infix(">=", Associativity.Left, 10, Lift(typeof(ExMPred), nameof(ExMPred.Geq))),

        infix("==", Associativity.Left, 8, Lift(typeof(ExMPred), nameof(ExMPred.Eq))),
        infix("!=", Associativity.Left, 8, Lift(typeof(ExMPred), nameof(ExMPred.Neq))),
        //& is defined to be the same as &&, not bitwise
        infix("&&", Associativity.Left, 6, Lift(typeof(ExMPred), nameof(ExMPred.And))),
        infix("||", Associativity.Left, 6, Lift(typeof(ExMPred), nameof(ExMPred.Or))),
        infix("&", Associativity.Left, 6, Lift(typeof(ExMPred), nameof(ExMPred.And))),
        infix("|", Associativity.Left, 6, Lift(typeof(ExMPred), nameof(ExMPred.Or))),

        assigner("=", nameof(ExMAssign.Is)),
        assigner("+=", nameof(ExMAssign.IsAdd)),
        assigner("-=", nameof(ExMAssign.IsSub)),
        assigner("*=", nameof(ExMAssign.IsMul)),
        assigner("/=", nameof(ExMAssign.IsDiv)),
        assigner("%=", nameof(ExMAssign.IsMod)),
        assigner("&=", nameof(ExMAssign.IsAnd)),
        assigner("|=", nameof(ExMAssign.IsOr))
    };

    private static Parser<Token, ST> Paren1(Parser<Token, ST> p) {
        var err = new ParserError.Failure(
            "Expected parentheses with only a single object. The parentheses should close here.");
        return inp => {
            var ropen = openParen(inp);
            if (!ropen.Result.Valid)
                return ropen.CastFailure<ST>();
            var rval = p(inp);
            if (!rval.Result.Valid)
                return new(rval.Result, rval.Error, ropen.Start, rval.End);
            var rclose = closeParen(inp);
            if (!rclose.Result.Valid)
                return new(Maybe<ST>.None, new LocatedParserError(rclose.Start, err), ropen.Start, rclose.End);
            return new(rval.Result, rval.Error, ropen.Start, rclose.End);
        };
    }
    
    private static Parser<Token, (PositionRange allPosition, List<ST> args)> Paren(Parser<Token, ST> p) {
        //empty parens allowed
        var args = p.SepBy(comma);
        return inp => {
            var ropen = openParen(inp);
            if (!ropen.Result.Valid)
                return ropen.CastFailure<(PositionRange, List<ST>)>();
            var rval = args(inp);
            if (!rval.Result.Valid)
                return new(Maybe<(PositionRange, List<ST>)>.None, rval.Error, ropen.Start, rval.End);
            var rclose = closeParen(inp);
            if (!rclose.Result.Valid)
                return new(Maybe<(PositionRange, List<ST>)>.None, rclose.Error, ropen.Start, rclose.End);
            return new((ropen.Result.Value.Position.Merge(rclose.Result.Value.Position), rval.Result.Value), 
                rval.Error, ropen.Start, rclose.End);
        };
    }

    private static readonly Reflector.MethodSignature constFloat =
        Reflector.MethodSignature.Get(ExFunction.WrapAny(typeof(AtomicBPYRepo), "Const").Mi);

    private static readonly ParserError blockKWErr = new ParserError.Expected("`block` or `b{` keyword");
    private static readonly Parser<Token, Token> blockKW = input => {
        if (input.Empty || input.Next.Type != TokenType.Keyword || (input.Next.Content != "block" && input.Next.Content != "b"))
            return new(blockKWErr, input.Index);
        else
            return new(new(input.Next), null, input.Index, input.Step(1));
    };
    
    //Atom: identifier; number/string/etc; parenthesized value; block 
    private static readonly Parser<Token, ST> atom = ChoiceL("atom (identifier, number, array, block, or parenthesized expression)",
        //Identifier
        IdentOrType.FMap(id => new ST.Ident(id) as ST),
        //num/string/v2rv2
        Num.FMap(t => new ST.Number(t.Position, DMath.Parser.Float(t.Content)) as ST),
        TokenOfType(TokenType.String).FMap(t => new ST.TypedValue<string>(t.Position, t.Content) as ST),
        TokenOfType(TokenType.V2RV2).FMap(t => new ST.TypedValue<V2RV2>(t.Position, DMath.Parser.ParseV2RV2(t.Content)) as ST),
        //Parenthesized value
        Paren1(Value),
        //Block
        Sequential(blockKW, openBrace, Statements, closeBrace,
            (o, _, stmts, c) => new ST.Block(o.Position.Merge(c.Position), stmts.ToArray()) as ST).LabelV("block"),
        //Array
        Sequential(openBrace, ((Parser<Token,ST>)Value).SepBy(ImplicitBreak(TokenType.Comma)), closeBrace,
            (o, vals, c) => new ST.Array(o.Position.Merge(c.Position), vals.ToArray()) as ST).LabelV("array")
    );

    //Term: member access `x.y`; C#-style function application `f(x, y)`.
    //Haskell-style function application `f x y` is handled in term2.
    private static readonly Parser<Token, ST> term =
        Sequential(atom,
            Either(op(".").IgThen(Ident), NoWhitespace.IgThen(Paren(Value))).Many(),
            (x, seqs) => {
                for (int ii = 0; ii < seqs.Count; ++ii) {
                    var s = seqs[ii];
                    if (s.IsLeft)
                        x = new ST.MemberAccess(x, new ST.Ident(s.Left));
                    else
                        x = new ST.FunctionCall(s.Right.Item1, x, s.Right.Item2.ToArray());
                }
                return x;
            });

    //Term + tight operators
    private static readonly Parser<Token, ST> termOps1 = ParseOperators(tightOperators, term);

    //Term + tight operators + partial function application
    private static readonly Parser<Token, ST> term2 =
        termOps1.SepBy1(NotFlags(TokenFlags.ImplicitBreak, "indent: partial function application across newlines must change the indentation level").IgThen(Whitespace)).FMap(ops => {
            for (int ii = 1; ii < ops.Count; ++ii)
                ops[0] = new ST.PartialFunctionCall(ops[0], ops[ii]);
            return ops[0];
        });

    //Value: Operator over term/partial fn app, or lambda
    //Note that this would be called an "expression" in most parsers but I won't call it that to avoid ambiguity
    // with Linq.Expression
    private static readonly Parser<Token, ST> value = ChoiceL("value expression",
        ParseOperators(looseOperators, term2)
        //todo lambda
    );
    private static ParseResult<ST> Value(InputStream<Token> inp) => value(inp);
    
    //Statement: value, variable declaration, or void-type block (if/else, for, while)
    private static readonly Parser<Token, ST> statement = ChoiceL("statement",
        value,
        //var is required even with type specified to avoid parsing `type name` as a partial function application
        Kw("var").IgThen(IdentOrType).Then(Ident.Opt()).ThenIg(op("=")).Then(value).Bind(decl => {
            if (decl.a.b.Try(out var id)) {
                //type + id specification
                var tDef = ParseType(decl.a.a.Content);
                if (tDef.IsRight)
                    return new ParseResult<ST>(
                        new LocatedParserError(decl.a.a.Index + tDef.Right.Index, tDef.Right.Error), decl.a.a.Index,
                        decl.a.a.Position.End.Index);
                var typ = tDef.Left.TryCompile();
                if (typ.IsRight)
                    return new ParseResult<ST>(
                        new LocatedParserError(decl.a.a.Index, new ParserError.Failure(typ.Right)), decl.a.a.Index,
                        decl.a.a.Position.End.Index);
                return new ParseResult<ST>(new ST.VarDeclAssign(
                        new VarDecl(decl.a.a.Position.Merge(id.Position), typ.Left, id.Content), decl.b) as ST,
                    null, decl.a.a.Index, decl.b.Position.End.Index);
            } else {
                if (decl.a.a.Type == TokenType.TypeIdentifier)
                    return new ParseResult<ST>(
                        new LocatedParserError(decl.a.a.Index, new ParserError.Expected(
                            "`var VARIABLE` or `var TYPE VARIABLE`")), decl.a.a.Index, decl.a.a.Position.End.Index);
                return new ParseResult<ST>(new ST.VarDeclAssign(
                        new VarDecl(decl.a.a.Position, null, decl.a.a.Content), decl.b) as ST,
                    null, decl.a.a.Index, decl.b.Position.End.Index);
            }
        })
        //todo
    );
    private static readonly Parser<Token, List<ST>> statements = 
        statement.SepBy(ImplicitBreak(TokenType.Semicolon)).Label("statement block");
    private static ParseResult<List<ST>> Statements(InputStream<Token> inp) => statements(inp);

    private static readonly Parser<Token, ST.Block> fullScript = 
        statements.ThenIg(EOF<Token>()).FMap(x => new ST.Block(x));

    public static Either<ST.Block, LocatedParserError> Parse(string source, Token[] tokens, out InputStream<Token> stream) {
        var result = fullScript(stream = new InputStream<Token>(
            "BDSL2 parser", tokens, null!, new TokenWitnessCreator(source)));
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
        var result = typeParser(new InputStream<char>("Type parser", source.ToCharArray(), null!));
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            (result.Error ?? new(0, new ParserError.Failure("This is not a valid type definition.")));
    }
}
}