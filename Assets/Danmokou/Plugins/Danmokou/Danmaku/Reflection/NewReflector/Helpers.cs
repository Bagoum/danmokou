using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Reflection;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Ex = System.Linq.Expressions.Expression;
using static BagoumLib.Reflection.TypeDesignation;

namespace Danmokou.Reflection2 {
public static class Helpers {

    /// <summary>
    /// Make the type TExArgCtx->TEx&lt;T&gt;.
    /// </summary>
    public static TypeDesignation MakeTExFunc(this TypeDesignation simpleType) =>
        new Known(typeof(Func<,>),
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
    
}
}