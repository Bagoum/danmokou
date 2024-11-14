using System;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Scriptor;
using Scriptor.Expressions;
using Scriptor.Math;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Expressions {
public static class ExPostAggregators {
    [Operator] [PAPriority(20)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<TExArgCtx, TEx<R>> PA_Add<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<R>> b)
        => t => ExMOperators.Add(a(t), b(t));
    [Operator] [PAPriority(20)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<TExArgCtx, TEx<R>> PA_Sub<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<R>> b)
        => t => ExMOperators.Sub(a(t), b(t));
    
    [Operator] [PAPriority(10)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<TExArgCtx, TEx<R>> PA_Mul<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<float>> b)
        => t => ExMOperators.Mul(b(t), a(t));
    [Operator] [PAPriority(10)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<TExArgCtx, TEx<R>> PA_Div<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<float>> b)
        => t => Ex.Divide(a(t), b(t));
    [Operator] [PAPriority(10)] [PASourceTypes(typeof(float))]
    public static Func<TExArgCtx, TEx<R>> PA_FDiv<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (ExM.FDiv((a(t) as TEx<float>)!, b(t)) as TEx<R>)!;
    [Operator] [PAPriority(0)] [PASourceTypes(typeof(float))]
    public static Func<TExArgCtx, TEx<R>> PA_Pow<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<float>> b)
        => t => (ExMOperators.Pow((a(t) as TEx<float>)!, b(t)) as TEx<R>)!;
    
    [Operator] [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_And<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<bool>> b)
        => t => Ex.AndAlso(a(t), b(t));
    [Operator] [PAPriority(20)] [PASourceTypes(typeof(bool))]
    public static Func<TExArgCtx, TEx<R>> PA_Or<R>(Func<TExArgCtx, TEx<R>> a, Func<TExArgCtx, TEx<bool>> b)
        => t => Ex.OrElse(a(t), b(t));
}
}