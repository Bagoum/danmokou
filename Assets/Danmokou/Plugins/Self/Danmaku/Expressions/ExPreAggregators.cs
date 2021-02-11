using System;
using DMK.Core;
using DMK.DMath;
using DMK.DMath.Functions;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace DMK.Expressions {
public static class ExPreAggregators {
    [PAPriority(10)] [PASourceTypes(typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<TExArgCtx, TEx<R>> PA_Mul<R>(Func<TExArgCtx, TEx<float>> a, Func<TExArgCtx, TEx<R>> b)
        => t => ExM.Mul(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_GT<R>(Func<TExArgCtx, TEx<float>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (Ex)ExMPred.Gt(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_LT<R>(Func<TExArgCtx, TEx<float>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (Ex)ExMPred.Lt(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_GEQ<R>(Func<TExArgCtx, TEx<float>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (Ex)ExMPred.Geq(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_LEQ<R>(Func<TExArgCtx, TEx<float>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (Ex)ExMPred.Leq(a(t), b(t));
    
    [PAPriority(0)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_EQ<R>(Func<TExArgCtx, TEx<float>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (Ex)ExMPred.Eq(a(t), b(t));
    
    [PAPriority(0)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_NEQ<R>(Func<TExArgCtx, TEx<float>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (Ex)ExMPred.Neq(a(t), b(t));
}
}