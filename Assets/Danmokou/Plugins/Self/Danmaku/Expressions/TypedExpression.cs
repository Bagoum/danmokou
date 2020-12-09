using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DMK.DMath;
using DMK.Reflection;
using UnityEngine;

namespace DMK.Expressions {

/// <summary>
/// Base class for TEx{T} used for type constraints.
/// </summary>
public class TEx {
    protected readonly Expression ex;
    public readonly Type type;
    protected TEx(Expression ex) {
        this.ex = ex;
        this.type = ex.Type;
    }
    private static readonly IReadOnlyDictionary<Type, Type> TExBoxMap = new Dictionary<Type, Type> {
        { typeof(Vector2), typeof(TExV2) },
        { typeof(Vector3), typeof(TExV3) },
        { typeof(ParametricInfo), typeof(TExPI) },
        { typeof(float), typeof(TEx<float>) },
        { typeof(V2RV2), typeof(TExRV2) },
    };
    private static readonly Type TypeTExT = typeof(TEx<>);
    public static TEx Box(Expression ex) {
        var ext = ex.Type;
        if (!TExBoxMap.TryGetValue(ext, out var tt)) throw new Exception($"Cannot box expression of type {ext}");
        return Activator.CreateInstance(tt, ex) as TEx;
    }

    protected TEx(ExMode mode, Type t) {
        if (mode == ExMode.RefParameter) {
            t = t.MakeByRefType();
        }
        ex = Expression.Parameter(t);
        this.type = ex.Type;
    }
    public static implicit operator TEx(Expression ex) {
        return new TEx(ex);
    }
    public static implicit operator Expression(TEx me) {
        return me.ex;
    }
    public static implicit operator ParameterExpression(TEx me) {
        return (ParameterExpression)me.ex;
    }
}
/// <summary>
/// A typed expression.
/// This typing is syntactic sugar: any expression, regardless of type, can be cast as eg. TEx{float}.
/// However, constructing a parameter expression via TEx{T} will type the expression appropriately.
/// By default, creates a ParameterExpression.
/// </summary>
/// <typeparam name="T">Type of expression.</typeparam>
public class TEx<T> : TEx {

    public TEx() : this(ExMode.Parameter) {}

    public TEx(Expression ex) : base(ex) { }

    public TEx(ExMode m) : base(m, typeof(T)) {}
    
    public static implicit operator TEx<T>(Expression ex) {
        return new TEx<T>(ex);
    }

    public static implicit operator EEx(TEx<T> tex) => tex.ex;

    public static implicit operator TEx<T>(T obj) => Expression.Constant(obj);

    public Expression GetExprDontUseThisGenerally() {
        return ex;
    }
}
public class RTEx<T> : TEx<T> {
    public RTEx() : base(ExMode.RefParameter) { }
}
}