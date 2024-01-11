using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Mizuhashi;
using Ex = System.Linq.Expressions.Expression;
using static BagoumLib.Unification.TypeDesignation;

namespace Danmokou.Reflection2 {
public static class Helpers {
    public static (IAST, LexicalScope) CompileToAST(ref string source, params IDelegateArg[] args) {
        var tokens = Lexer.Lex(ref source);
        var parse = Parser.Parse(source, tokens, out var stream);
        if (parse.IsRight) 
            throw new CompileException(stream.ShowAllFailures(parse.Right));
        var gs = LexicalScope.NewTopLevelScope();
        var ast = parse.Left.AnnotateTopLevel(gs, args);
        foreach (var exc in (ast as IAST).FirstPassExceptions)
            throw exc;
        
        return (ast, gs);
    }

    public static IAST Typecheck(this IAST ast, LexicalScope globalScope, out Type type) {
        var typ = IAST.Typecheck(ast, globalScope.Root.Resolver, globalScope);
        if (typ.IsRight)
            throw IAST.EnrichError(typ.Right);
        type = typ.Left;
        foreach (var d in ast.WarnUsage())
            d.Log();
        return ast;
    }
    public static ReadyToCompileExpr<D> CompileToDelegate<D>(string source, params IDelegateArg[] args) where D : Delegate {
        var (ast, gs) = CompileToAST(ref source, args);
        var expr = ast.Typecheck(gs, out _).Realize() as Func<TExArgCtx, TEx>;
        return CompilerHelpers.PrepareDelegate<D>(expr!, args);
    }

    /// <summary>
    /// Make the type TExArgCtx->TEx&lt;T&gt;.
    /// </summary>
    public static Known MakeTExFunc(this TypeDesignation simpleType) =>
        new (typeof(Func<,>),
            new Known(typeof(TExArgCtx)),
            new Known(typeof(TEx<>),
                simpleType
            ));

    public static bool IsTExFunc(this TypeDesignation t) {
        if (t is not Known kt0 || kt0.Typ != typeof(Func<,>) || 
            kt0.Arguments[0] is not Known kt1 || kt1.Typ != typeof(TExArgCtx))
            return false;
        return IsTExType(kt0.Arguments[1]);
    }
    public static TypeDesignation UnwrapTExFunc(this TypeDesignation texFuncType) {
        if (!IsTExFunc(texFuncType))
            throw new Exception($"Not a TExArgCtx->TEx designator: {texFuncType}");
        return texFuncType.Arguments[1].UnwrapTExType();
    }

    public static bool IsTExType(this TypeDesignation t) {
        return t is Known kt1 && kt1.Typ == typeof(TEx<>);
    }

    public static TypeDesignation UnwrapTExType(this TypeDesignation texType) {
        if (!IsTExType(texType))
            throw new Exception($"Not a TEx<T> designator: {texType}");
        return texType.Arguments[0];
    }

    private static readonly Dictionary<Type, (Type, ConstructorInfo)> texTypeCache = new();
    public static (Type type, ConstructorInfo exConstructor) GetTExType(this TypeDesignation simpleType) {
        if (simpleType is not Known kt)
            throw new Exception($"Type not known: {simpleType}");
        if (texTypeCache.TryGetValue(kt.Typ, out var texTyp))
            return texTyp;
        var tt = typeof(TEx<>).MakeGenericType(kt.Typ);
        return texTypeCache[kt.Typ] = (tt, tt.GetConstructor(new[] { typeof(Expression) }));
    }

    public static ConstructorInfo GetTExConstructor(this TypeDesignation texFuncType) =>
        texFuncType.UnwrapTExFunc().GetTExType().exConstructor;

    public static Type AssertKnown(this TypeDesignation t) {
        if (!t.IsResolved)
            throw new Exception($"Type is not resolved: {t}");
        var te = t.Resolve();
        if (te.IsRight)
            throw new Exception(te.ToString()); //todo make this an exception
        return te.Left;
    }

    /// <summary>
    /// Make the type TExArgCtx->TEx&lt;T&gt;.
    /// </summary>
    public static Type MakeTExType(this Type t) => 
        Reflector.Func2Type(typeof(TExArgCtx), typeof(TEx<>).MakeGenericType(t));

    /// <summary>
    /// Retype the provided function as TExArgCtx -> TEx{T}.
    /// </summary>
    public static Func<TExArgCtx, TEx> MakeTypedLambda(this Type t, Func<TExArgCtx, TEx> f) =>
        LambdaTyper.ConvertForType(t, f);

    public class LambdaTyper {
        private static readonly Dictionary<Type, MethodInfo> converters = new();
        private static readonly MethodInfo mi = typeof(LambdaTyper).GetMethod(nameof(Convert))!;

        public static Func<TExArgCtx, TEx<T>> Convert<T>(Func<TExArgCtx, TEx> f) => tac => (Ex)f(tac);
        public static Func<TExArgCtx, TEx> ConvertForType(Type t, Func<TExArgCtx, TEx> f) {
            var conv = converters.TryGetValue(t, out var c) ? c : converters[t] = mi.MakeGenericMethod(t);
            return (conv.Invoke(null, new object[] { f }) as Func<TExArgCtx, TEx>)!;
        }
    }
    
    public class NotWriteableException : Exception {
        public int ArgIndex { get; }

        public NotWriteableException(int argIndex, string err) : base(err) {
            this.ArgIndex = argIndex;
        }

        public NotWriteableException(int argIndex, string err, Exception inner) : base(err) {
            this.ArgIndex = argIndex;
        }
    }

    /// <summary>
    /// Return an exception if the provided expression is not writeable.
    /// <br/>Implementation based on https://source.dot.net/#System.Linq.Expressions/System/Linq/Expressions/Expression.cs,241
    /// </summary>
    public static NotWriteableException? AssertWriteable(int argIndex, object prm) {
        NotWriteableException Err(string err) => new(argIndex, err);
        Expression ex;
        try {
            if (prm is TEx tex)
                ex = tex;
            else
                ex = (Ex)prm;
        } catch (Exception ecx) {
            return new(argIndex, $"Failure: this is not an expression. Please report this.", ecx);
        }
        switch (ex) {
            case IndexExpression iex:
                if (iex.Indexer?.CanWrite is false)
                    return Err("This is an indexing expression, but the indexer is not writeable.");
                return null;
            case MemberExpression mex:
                return mex.Member switch {
                    PropertyInfo p => p.CanWrite ?
                        null :
                        Err($"The property {p.Name} is not writeable."),
                    FieldInfo f => !(f.IsInitOnly || f.IsLiteral) ?
                        null :
                        Err($"The field {f.Name} is not writeable."),
                    { } m => Err($"Member {m.Name} is of an unhandled type {m.GetType().RName()}")
                };
            case ParameterExpression:
                return null;
            default:
                return Err($"This expression has type {ex.GetType().RName()}:{ex.NodeType}, which is not writeable." +
                           " A writeable expression is an array indexer, property, field, or variable.");
            
        }
        
    }
    
}
}