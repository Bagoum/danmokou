using System;
using System.Linq.Expressions;
using Core;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace DMath {
public static class ExPostAggregators {
    [PAPriority(20)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<Te, TEx<R>> PA_Add<Te, R>(Func<Te, TEx<R>> a, Func<Te, TEx<R>> b) where Te : TEx
        => t => ExM.Add(a(t), b(t));
    [PAPriority(20)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<Te, TEx<R>> PA_Sub<Te, R>(Func<Te, TEx<R>> a, Func<Te, TEx<R>> b) where Te : TEx
        => t => ExM.Sub(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<Te, TEx<R>> PA_Mul<Te, R>(Func<Te, TEx<R>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => ExM.Mul(b(t), a(t));
    [PAPriority(10)] [PASourceTypes(typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<Te, TEx<R>> PA_Div<Te, R>(Func<Te, TEx<R>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => Ex.Divide(a(t), b(t));
    [PAPriority(10)] [PASourceTypes(typeof(float))]
    public static Func<Te, TEx<R>> PA_FDiv<Te, R>(Func<Te, TEx<R>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => ExM.FDiv(a(t) as TEx<float>, b(t)) as TEx<R>;
    [PAPriority(0)] [PASourceTypes(typeof(float))]
    public static Func<Te, TEx<R>> PA_Pow<Te, R>(Func<Te, TEx<R>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => ExM.Pow(a(t) as TEx<float>, b(t)) as TEx<R>;
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<Te, TEx<R>> PA_And<Te, R>(Func<Te, TEx<R>> a, Func<Te, TEx<bool>> b) where Te : TEx
        => t => Ex.AndAlso(a(t), b(t));
    [PAPriority(20)] [PASourceTypes(typeof(bool))]
    public static Func<Te, TEx<R>> PA_Or<Te, R>(Func<Te, TEx<R>> a, Func<Te, TEx<bool>> b) where Te : TEx
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