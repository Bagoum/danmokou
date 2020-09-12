using System;
using System.Linq.Expressions;
using Core;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace DMath {
public static class ExPreAggregators {
    [PAPriority(10)] [PASourceTypes(typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(V2RV2))]
    public static Func<Te, TEx<R>> PA_Mul<Te, R>(Func<Te, TEx<float>> a, Func<Te, TEx<R>> b) where Te : TEx
        => t => ExM.Mul(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<Te, TEx<R>> PA_GT<Te, R>(Func<Te, TEx<float>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => (Ex)ExMPred.Gt(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<Te, TEx<R>> PA_LT<Te, R>(Func<Te, TEx<float>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => (Ex)ExMPred.Lt(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<Te, TEx<R>> PA_GEQ<Te, R>(Func<Te, TEx<float>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => (Ex)ExMPred.Geq(a(t), b(t));
    
    [PAPriority(10)] [PASourceTypes(typeof(bool))]
    public static Func<Te, TEx<R>> PA_LEQ<Te, R>(Func<Te, TEx<float>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => (Ex)ExMPred.Leq(a(t), b(t));
    
    [PAPriority(0)] [PASourceTypes(typeof(bool))]
    public static Func<Te, TEx<R>> PA_EQ<Te, R>(Func<Te, TEx<float>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => (Ex)ExMPred.Eq(a(t), b(t));
    
    [PAPriority(0)] [PASourceTypes(typeof(bool))]
    public static Func<Te, TEx<R>> PA_NEQ<Te, R>(Func<Te, TEx<float>> a, Func<Te, TEx<float>> b) where Te : TEx
        => t => (Ex)ExMPred.Neq(a(t), b(t));
}
}