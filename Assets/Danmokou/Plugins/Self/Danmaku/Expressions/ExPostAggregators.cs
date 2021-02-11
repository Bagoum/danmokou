using System;
using DMK.Core;
using DMK.DMath;
using DMK.DMath.Functions;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace DMK.Expressions {
public static class ExPostAggregators {
    [PAPriority(20)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<TExArgCtx, TEx<R>> PA_Add<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<R>> b)
        => t => ExM.Add(a(t), b(t));
    [PAPriority(20)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<TExArgCtx, TEx<R>> PA_Sub<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<R>> b)
        => t => ExM.Sub(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<TExArgCtx, TEx<R>> PA_Mul<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<float>> b)
        => t => ExM.Mul(b(t), a(t));
    [PAPriority(10)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<TExArgCtx, TEx<R>> PA_Div<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<float>> b)
        => t => Ex.Divide(a(t), b(t));
    [PAPriority(10)] [PASourceTypes(typeof(float))]
    public static Func<TExArgCtx, TEx<R>> PA_FDiv<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (ExM.FDiv((a(t) as TEx<float>)!, b(t)) as TEx<R>)!;
    [PAPriority(0)] [PASourceTypes(typeof(float))]
    public static Func<TExArgCtx, TEx<R>> PA_Pow<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (ExM.Pow((a(t) as TEx<float>)!, b(t)) as TEx<R>)!;
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_And<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<bool>> b)
        => t => Ex.AndAlso(a(t), b(t));
    [PAPriority(20)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_Or<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<bool>> b)
        => t => Ex.OrElse(a(t), b(t));


    [PAPriority(20)]
    public static BPY PA_Add_noexpr(BPY a, BPY b) => t => a(t) + b(t);
    [PAPriority(20)]
    public static BPY PA_Sub_noexpr(BPY a, BPY b) => t => a(t) - b(t);
    [PAPriority(10)]
    public static BPY PA_Mul_noexpr(BPY a, BPY b) => t => a(t) * b(t);
    [PAPriority(10)]
    public static BPY PA_Div_noexpr(BPY a, BPY b) => t => a(t) / b(t);
}
}