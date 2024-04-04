using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
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
    public static readonly Parser<Token, Token> IdentOrType = TokenOfTypes(TokenType.Identifier, TokenType.TypeIdentifier);
    public static readonly Parser<Token, Token?> TypeSuffixStr =
        op("::").IgThen(IdentOrType).OptN();
    public static Either<Type, string> TypeFromString(string content) {
        var tDef = ParseType(content);
        if (tDef.IsRight)
            return tDef.Right;
        var typ = tDef.Left.TryCompile();
        if (typ.IsRight)
            return typ.Right;
        return typ.Left;
    }

    public static Either<(string meth, Type[] args), string> MaybeTypeArgsFromString(string content) {
        var tDef = ParseType(content);
        if (tDef.IsRight)
            return tDef.Right;
        if (tDef.Left is TypeDef.Atom at)
            return (at.Type, Array.Empty<Type>());
        if (tDef.Left is not TypeDef.Generic gen)
            return $"Cannot parse as type-args: {tDef.Left}";
        var args = new Type[gen.Args.Count];
        for (int ii = 0; ii < gen.Args.Count; ++ii) {
            var arg = gen.Args[ii].TryCompile();
            if (arg.IsRight)
                return arg.Right;
            args[ii] = arg.Left;
        }
        return (gen.Type, args);
    }

    public static (string meth, Type[] args) TypeArgsFromString(string content) =>
        MaybeTypeArgsFromString(content).TryL(out var res) ? res : (content, Array.Empty<Type>());

    public static Either<Type?, AST.Failure> TypeFromToken(Token? mtypStr, LexicalScope scope, bool allowVoid = false) {
        if (!mtypStr.Try(out var typStr))
            return null as Type;
        var result = TypeFromString(typStr.Content);
        if (!result.TryL(out var typ))
            return new AST.Failure(new ReflectionException(typStr.Position, result.Right), scope) 
                { IsTypeCompletion = true };
        if (typ == typeof(void) && !allowVoid)
            return new AST.Failure(new(typStr.Position, "Cannot instantiate a value of type void."), scope);
        return typ;
    }

    public static readonly Parser<Token, (Token id, Token? typ)> IdentAndType = 
        Ident.Then(TypeSuffixStr);
    
    private static readonly Parser<Token, Token> openParen = TokenOfType(TokenType.OpenParen);
    private static readonly Parser<Token, Token> closeParen = TokenOfType(TokenType.CloseParen);
    private static readonly Parser<Token, Token> openBrace = TokenOfType(TokenType.OpenBrace);
    private static readonly Parser<Token, Token> closeBrace = TokenOfType(TokenType.CloseBrace);
    private static readonly Parser<Token, Token> comma = TokenOfType(TokenType.Comma);
    private static readonly Parser<Token, Token> semicolon = TokenOfType(TokenType.Semicolon);

    private static Parser<Token, Unit> ImplicitBreak(TokenType breaker, bool allowEoF = false) {
        var err = new ParserError.Expected($"{breaker.ToString().ToLower()} or uniform newline indentation after previous line");
        return input => {
            if (input.Empty) {
                if (allowEoF)
                    return new(new(Unit.Default), null, input.Index, input.Index);
            } else {
                if (input.Next.Type == breaker)
                    return new(new(Unit.Default), null, input.Index, input.Step(1));
                else if ((input.Next.Flags & TokenFlags.ImplicitBreak) > 0)
                    return new(new(Unit.Default), null, input.Index, input.Index);
            }
            return new(err, input.Index);
        };
    }
    public static Parser<Token, Token> Kw(string keyword) => TokenOfTypeValue(TokenType.Keyword, keyword);
    public static Parser<Token, PositionRange> Kwp(string keyword) => 
        TokenOfTypeValue(TokenType.Keyword, keyword).FMap(t => t.Position);

    public static Parser<Token, Token> Flags(TokenFlags flags, string? expected = null) =>
        Satisfy((Token inp) => (inp.Flags & flags) > 0, expected).IsPresent();
    public static Parser<Token, Unit> NotFlags(TokenFlags flags, string? unexpected = null) =>
        Satisfy((Token inp) => (inp.Flags & flags) > 0).IsNotPresent(unexpected);

    public static readonly Parser<Token, Token> Whitespace = Flags(TokenFlags.PrecededByWhitespace, "whitespace");
    public static readonly Parser<Token, Unit> NoWhitespace = NotFlags(TokenFlags.PrecededByWhitespace, "whitespace");


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

    private static Parser<Token, Token> infixOp(string op) =>
        opNoFlag(op, TokenFlags.ImplicitBreak, $"infix operator `{op}`");
    private static Op infix(string op, Associativity assoc, int precedence,
        params MethodSignature[] overloads) => 
        new Op.Infix(infixOp(op), InfixCaller(overloads), assoc, precedence);
    
    private static Parser<Token, Token> prefixOp(string op) =>
        opNoFlag(op, TokenFlags.PostcededByWhitespace, $"prefix operator `{op}`");
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
        
        new Op.Prefix(prefixOp("++"), 
            PrefixCaller(Meth(typeof(ExMAssign), nameof(ExMAssign.PreIncrement))), 19),
        new Op.Prefix(prefixOp("--"), 
            PrefixCaller(Meth(typeof(ExMAssign), nameof(ExMAssign.PreDecrement))), 19),
        
        //NB: It is critical to have the noWhitespace parse for +/- operators, because if we don't,
        // then curried function application of a unary number becomes higher precedence than arithmetic.
        // eg. `x - y` has higher precedence as Curried(x, Negate(y)) than Subtract(x, y).
        //F# handles this quite well by parsing only no-whitespace +/- as unary.
        new Op.Prefix(prefixOp("+"), 
            (t, x) => x with { Position = t.Position.Merge(x.Position) }, 18),
        new Op.Prefix(prefixOp("-"), 
            PrefixCaller(Meth(typeof(ExM), nameof(ExM.Negate))), 18),
        prefix("!", 18, Meth(typeof(ExMPred), nameof(ExMPred.Not))),
    };
    //these operators are lower precedence than curried function application.
    //eg. f x op y = f(x) op y
    public static readonly Op[] looseOperators = {
        infix("^", Associativity.Left, 16, Meth(typeof(ExM), nameof(ExM.Pow))),
        infix("^^", Associativity.Left, 16, Meth(typeof(ExM), nameof(ExM.NPow))),
        infix("^-", Associativity.Left, 16, Meth(typeof(ExM), nameof(ExM.PowSub))),
        infix("%", Associativity.Left, 14, Meth(typeof(ExMMod), nameof(ExMMod.Modulo))),
        //ideally for arithmetic operations we would statically lookup all the defined operators,
        // but that's kind of overkill, so we just include the basics:
        //  float*T, T*float, T/float, T+T, T-T, and the other fixed ones
        new Op.Infix(infixOp("*"), 
            InfixCallerEquivOverloads(
                Meth(typeof(ExM), nameof(ExM.Mul)),
                Meth(typeof(ExM), nameof(ExM.MulRev)),
                Meth(typeof(ExM), nameof(ExM.MulInt))
            ), Associativity.Left, 14),
        infix("/", Associativity.Left, 14, Meth(typeof(ExM), nameof(ExM.Div))),
        //infix("//", Associativity.Left, 14, Lift(typeof(ExM), nameof(ExM.FDiv))),

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
        
        new Op.Infix(TokenOfTypeValue(TokenType.Keyword, "as"), (obj, c, typ) => new ST.TypeAs(obj, c.Position, typ), Associativity.Left, 4),

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
    
    private static Parser<Token, (PositionRange allPosition, List<T> args)> Paren<T>(Parser<Token, T> p, Parser<Token, T>? first = null) {
        var args = p.SepBy(comma, first: first);
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
        //Identifier, optionally with type specification
        IdentAndType.FMap(idtyp => new ST.Ident(idtyp.id, idtyp.typ) as ST),
        //Identifier or type identifier (type identifier as atom is required to support `as` operator)
        IdentOrType.FMap(id => new ST.Ident(id) as ST),
        //num/string/v2rv2
        Num.FMap(t => new ST.Number(t.Position, t.Content) as ST),
        TokenOfType(TokenType.ValueKeyword).FMap(t => t.Content switch {
            "true" => new ST.TypedValue<bool>(t.Position, true) { Kind = SymbolKind.Boolean } as ST,
            "false" => new ST.TypedValue<bool>(t.Position, false) { Kind = SymbolKind.Boolean },
            _ => new ST.Failure(t.Position, $"Unknown value keyword {t.Content}. Please report this.")
        }),
        TokenOfType(TokenType.NullKeyword).Then(TypeSuffixStr).FMap(t => 
            new ST.DefaultValue(t.a.Position, t.b) as ST),
        TokenOfType(TokenType.DefaultKeyword).FMap(t => 
            new ST.DefaultValue(t.Position, null, asFunctionArg: true) as ST),
        TokenOfType(TokenType.String).FMap(t => new ST.TypedValue<string>(t.Position, t.Content) 
            { Kind = SymbolKind.String} as ST),
        TokenOfType(TokenType.Char).FMap(t => t.Content.Length == 1 ?
            new ST.TypedValue<char>(t.Position, t.Content[0]) { Kind = SymbolKind.String} :
            new ST.Failure(t.Position, "A character literal must be exactly one character long.") as ST),
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
        Paren(ValueOrFailure, Value).FMap(vs => vs.args.Count == 1 ? vs.args[0] : new ST.Tuple(vs.allPosition, vs.args)).LabelV("tuple"),
        //Block
        Sequential(blockKW, BracedBlock,
            (o, b) => b with { Position = o.Position.Merge(b.Position) } as ST).LabelV("block"),
        //Array
        Sequential(openBrace, ((Parser<Token,ST>)Value).SepBy(ImplicitBreak(TokenType.Comma)), closeBrace,
            (o, vals, c) => new ST.Array(o.Position.Merge(c.Position), vals.ToArray()) as ST).LabelV("array")
    );

    private static readonly ParserError termMemberFollowErr =
        new ParserError.Expected("member access x.y and/or function application f(x, y)");
    private static readonly ParserError termMemberGenericErr =
        new ParserError.Expected("generic function x.y<T>()");
    private static readonly Parser<Token, Token> period = op(".");
    private static readonly Parser<Token, Token> termMember = inp => {
        var rp = period(inp);
        if (!rp.Result.Try(out var p))
            return rp;
        var rid = IdentOrType(inp);
        //Allow empty identifier here-- it will fail during annotation, but it permits better errors
        if (rid.Status == ResultStatus.ERROR)
            return new(new Token(TokenType.Identifier, p.Position.End.CreateEmptyRange(), ""), rp.MergeErrors(in rid), rp.Start,
                rp.End);
        else
            return rid.WithPreceding(in rp);
    };
    private static readonly Parser<Token, (Maybe<Token>, Maybe<(PositionRange, List<ST>)>)> termMemberFn =
        termMember.Opt().Then(NoWhitespace.IgThen(Paren(ValueOrFailure, Value)).Opt());
    private static readonly Parser<Token, (Maybe<Token>, Maybe<(PositionRange, List<ST>)>)> termMemberFnFollow = inp => {
            var follow = termMemberFn(inp);
            if (follow.Result.Try(out var lr)) {
                if (!lr.Item1.Valid && !lr.Item2.Valid)
                    return follow.AsSameError(termMemberFollowErr);
                if (lr.Item1.Try(out var mem) && mem.Type is TokenType.TypeIdentifier && !lr.Item2.Valid)
                    return follow.AsSameError(termMemberGenericErr, false);
            }
            return follow;
        };
    
    //Term: member access `x.y`, member function `x.f(y)`, indexer `x[y]`,
    // constructor `new X(y)`, C#-style function application `f(x, y)`, partial function call `$(f, x)`,
    //Haskell-style function application `f x y` is handled in term2.
    private static readonly Parser<Token, ST> term =
        Choice(
            Sequential(Kw("new"), IdentOrType, Paren(ValueOrFailure, Value), (kw, typ, args) => 
                    new ST.Constructor(kw.Position.Merge(args.allPosition), kw.Position, typ, args.args.ToArray()) as ST)
                .LabelV("constructor"),
            Sequential(atom, Combinators.Either(
                    termMemberFnFollow,
                    Sequential(TokenOfType(TokenType.OpenBracket), ValueOrFailure, TokenOfType(TokenType.CloseBracket),
                        (open, val, close) => (open, val, close)).LabelV("indexer x[i]") ).Many(),
                (x, seqs) => {
                    for (int ii = 0; ii < seqs.Count; ++ii) {
                        if (seqs[ii].TryR(out var indexer)) {
                            x = new ST.Indexer(x, indexer.open.Position, indexer.val, indexer.close.Position);
                        } else {
                            var (mem, fn) = seqs[ii].Left;
                            if (mem.Try(out var m))
                                if (fn.Try(out var f))
                                    x = new ST.MemberFunction(x.Position.Merge(f.Item1), x, new ST.Ident(m), f.Item2);
                                else
                                    x = new ST.MemberAccess(x, new ST.Ident(m));
                            else if (fn.Try(out var f))
                                x = new ST.FunctionCall(x.Position.Merge(f.Item1), x, f.Item2.ToArray());
                        }
                    }
                    return x;
                }
            ),
            sop("$").IgThen(NoWhitespace).IgThen(
                Paren1(Combinators.SepBy(ValueOrFailure, comma, true, first:Value)).FMap(
                    args => new ST.PartialFunctionCall(
                        PositionRange.Merge(args.Select(a => a.Position)), 
                        args[0], args.Skip(1).ToArray()) as ST
                ))
        );

    //Term + tight operators
    private static readonly Parser<Token, ST> termOps1 = ParseOperators(tightOperators, term);

    private static readonly Parser<Token, Token> curryFnAppSep = NotFlags(TokenFlags.ImplicitBreak, 
        "indent: curried function application across newlines must change the indentation level").IgThen(Whitespace);
    
    //Term + tight operators + curried function application
    private static readonly Parser<Token, ST> term2 = inp => {
        var rf = termOps1(inp);
        //First element must be Ident for curried functions
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

    //Loose operators
    //Note that this would be called an "expression" in most parsers but I won't call it that to avoid ambiguity
    // with Linq.Expression
    private static readonly Parser<Token, ST> term2Ops = ParseOperators(looseOperators, term2);

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
    private static readonly ParserError noValueExpr = new ParserError.Expected("value expression");
    private static ParseResult<ST> ValueOrFailure(InputStream<Token> inp) {
        var rv = value(inp);
        if (rv.Status != ResultStatus.ERROR)
            return rv;
        var pos = inp.Remaining > 0 ? inp.Next.Position : inp.Source[^1].Position;
        return new ParseResult<ST>(Maybe<ST>.Of(new ST.Failure(pos, "No value expression provided")), 
            new LocatedParserError(inp.Index, noValueExpr), inp.Index, inp.Index);
    }

    private static readonly Parser<Token, ST.VarDeclAssign> varInit =
        Sequential(Kw("var").Or(Kw("hvar")), IdentAndType, op("="), value,
            (kw, idTyp, eq, res) => {
                var (id, typ) = idTyp;
                return new ST.VarDeclAssign(kw, id, typ, eq.Position, res);
            });

    private static readonly Parser<Token, ST.FunctionDef> functionDecl =
        Sequential(Kw("function").Or(Kw("hfunction")), Lexer.Ident,
            Paren(IdentAndType.Then(op("=").Then(value).OptN())), 
            TypeSuffixStr, BracedBlock,
            (fn, name, args, type, body) => new ST.FunctionDef(fn, name, args.args, type, body));

    private static readonly Parser<Token, ST> macroDecl =
        Sequential(Kw("macro"), Lexer.Ident, 
            Paren(Ident.Then(op("=").Then(value).OptN())), 
            BracedBlock,
            (fn, name, args, body) => new ST.MacroDef(fn, name, args.args, body) as ST);

    private static readonly Parser<Token, ST> ifElseStatement =
        Sequential(Kw("if"), Paren1(value), BracedBlock, Kw("else")
                .Then(Combinators.FMap<Token, ST.Block, ST>(BracedBlock, b => b).Or(IfElseStatement)).Opt()
            , (kwif, cond, iftrue, iffalse) => {
                var els = iffalse.ValueOrSNull();
                return new ST.IfStatement(kwif.Position, els?.a.Position, cond, iftrue, els?.b) as ST;
            });
    private static ParseResult<ST> IfElseStatement(InputStream<Token> inp) => ifElseStatement(inp);
    
    //Statement: value, variable declaration, special statement, or void-type block (func decl, if/else, for, while)
    private static readonly Parser<Token, ST> statement = ChoiceL("statement",
        value,
        Sequential(Kw("const").OptN(), Combinators.Either(varInit, functionDecl), (cnst, succ) => {
            if (succ.IsLeft) {
                succ.Left.ConstKwPos = cnst?.Position;
                return succ.Left as ST;
            } else {
                succ.Right.ConstKwPos = cnst?.Position;
                return succ.Right;
            }
        }),
        macroDecl,
        Sequential(Kw("return"), value.Opt(), (kw, v) => new ST.Return(kw.Position, v.ValueOrNull()) as ST),
        Kw("continue").FMap(t => new ST.Continue(t.Position) as ST),
        Kw("break").FMap(t => new ST.Break(t.Position) as ST),
        IfElseStatement,
        Sequential(Kw("for"), Paren1(Sequential(
                Combinators.Opt<Token, ST>(Statement),
                TokenOfType(TokenType.Semicolon),
                value.Opt(),
                TokenOfType(TokenType.Semicolon),
                Combinators.Opt<Token, ST>(Statement),
                (initial, _, cond, _, final) => (initial.ValueOrNull(), cond.ValueOrNull(), final.ValueOrNull())
            )), BracedBlock,
            (kw, checks, body) => new ST.Loop(kw.Position, checks.Item1, checks.Item2, checks.Item3, body) as ST
        ),
        Sequential(Kw("while"), Paren1(value), BracedBlock, 
            (kw, check, body) => new ST.Loop(kw.Position, null, check, null, body) as ST)
    );
    private static ParseResult<ST> Statement(InputStream<Token> inp) => statement(inp);
    private static readonly Parser<Token, List<ST>> statements = 
        statement.ThenIg(ImplicitBreak(TokenType.Semicolon, allowEoF: true)).Many()
            .Label("statement block");
    private static readonly Parser<Token, ST.Block> bracedBlock =
        Sequential(openBrace, statements, closeBrace,
            (o, stmts, c) => new ST.Block(o.Position.Merge(c.Position), stmts));
    private static ParseResult<ST.Block> BracedBlock(InputStream<Token> inp) => bracedBlock(inp);
    private static readonly Parser<Token, List<ST>> imports =
        Sequential(
            Kw("import"), 
            Lexer.Ident,
            Kwp("at").Then(TokenOfType(TokenType.String)).Opt(),
            Kwp("as").Then(Lexer.Ident).Opt(),
            (kw, id, loc, alias) => new ST.Import(kw.Position, id, loc.ValueOrSNull(), alias.ValueOrSNull()) as ST).
                ThenIg(ImplicitBreak(TokenType.Semicolon, allowEoF: true)).Many()
                    .Label("imports");

    private static readonly Parser<Token, ST.Block> fullScript = 
        imports.Then(statements).ThenIg(EOF<Token>()).FMap(x => new ST.Block(x.a.Concat(x.b).ToList()));

    public static Either<ST.Block, LocatedParserError> Parse(string source, Token[] tokens, out InputStream<Token> stream) {
        var result = fullScript(stream = new InputStream<Token>(
            tokens, "BDSL2 parser", witness: new TokenWitnessCreator(source)));
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            (result.Error ?? new(0, new ParserError.Failure("Parsing failed, but it's unclear why.")));
    }

    public abstract record TypeDef {
        private static Dictionary<int, Dictionary<string, Type>>? _genericToType;
        public static Dictionary<int, Dictionary<string, Type>> GenericToType => _genericToType ??= LoadTypes();

        private static Dictionary<int, Dictionary<string, Type>> LoadTypes() {
            var d = new Dictionary<int, Dictionary<string, Type>>();
            void AddType(Type typ) {
                var name = typ.Name;
                var generic = 0;
                if (name.Contains('`')) {
                    var parts = name.Split('`', StringSplitOptions.RemoveEmptyEntries);
                    name = parts[0];
                    generic = int.Parse(parts[1]);
                }
                d.Add2(generic, name, typ);
            }
            foreach (var typ in AppDomain.CurrentDomain.GetAssemblies().Where(a => {
                         if (a.IsDynamic) return false;
                         var n = a.GetName().Name;
                         return n == "mscorlib" || n == "System.Private.CoreLib" || n == "System.Core" || 
                                n == "UnityEngine.CoreModule" || n == "System.Linq" ||
                                n.StartsWith("Danmokou") || n == "BagoumLib" || n == "Suzunoya" || n == "Mizuhashi";
                     }).SelectMany(a => a.GetExportedTypes()).Where(t => {
                         if (t.Name.EndsWith("Attribute") || t.Name.Contains("Unsafe")) return false;
                         if (t.Namespace is { } ns) {
                             if (ns.StartsWith("System.Runtime")) return false;
                         }
                         return true;
                     })) {
                AddType(typ);
            }
            AddType(typeof(Unit));
            return d;
        }
        
        public abstract Either<Type, string> TryCompile();

        public record Atom(string Type) : TypeDef {
            private static readonly Dictionary<string, Type> simpleNameTypes =
                CSharpTypePrinter.SimpleTypeNameMap.ToDictionary(kv => kv.Value, kv => kv.Key);

            public override Either<Type, string> TryCompile() {
                if (simpleNameTypes.TryGetValue(Type, out var t))
                    return t;
                if (GenericToType[0].TryGetValue(Type, out t))
                    return t;
                return $"Couldn't recognize type {Type}.";
            }
        }

        public record Generic(string Type, List<TypeDef> Args) : TypeDef {
            public override Either<Type, string> TryCompile() {
                if (GenericToType.TryGet2(Args.Count, Type, out var t)) {
                    return Args
                        .SequenceL(a => a.TryCompile())
                        .FMapL(args => t.MakeGenericType(args.ToArray()));
                }
                var arg = Args.Count == 1 ? "" : "s";
                return $"Couldn't recognize type constructor {Type} with {Args.Count} argument{arg}.";
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
    
    public static Either<TypeDef, string> ParseType(string source) {
        var strm = new InputStream<char>(source, "Type parser");
        var result = typeParser(strm);
        return result.Status == ResultStatus.OK ? 
            result.Result.Value : 
            (result.Error ?? new(0, new ParserError.Failure("This is not a valid type definition."))).Show(strm);
    }
}
}