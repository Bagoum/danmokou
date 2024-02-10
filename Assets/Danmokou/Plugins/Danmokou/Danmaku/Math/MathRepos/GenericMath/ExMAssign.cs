using System;
using System.Collections.Generic;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExMHelpers;

namespace Danmokou.DMath.Functions {

/// <summary>
/// See <see cref="ExM"/>. This class contains functions related to assignment.
/// It is not reflected as it is only for use with BDSL2, which calls them explicitly.
/// </summary>
[DontReflect]
public static class ExMAssign {
    [Assigns(0)] [BDSL2Operator] [DontReflect]
    public static TEx<T> VariableInitialize<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, y);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> Assign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, y);
    
    //Ex.AddAssign, etc do not work on struct/class fields, so we can't use them directly
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> AddAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Add(x, y));
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> SubAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Subtract(x, y));
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> MulAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Multiply(x, y));
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> DivAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Divide(x, y));
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> ModAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Modulo(x, y));
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> AndAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.And(x, y));
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> OrAssign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, Ex.Or(x, y));

    private static readonly Dictionary<Type, Ex> ones = new() {
        {typeof(int), ExC(1)},
        {typeof(float), ExC(1f)},
        {typeof(double), ExC(1.0)}
    };
    private static Ex GetOne(Type t) {
        if (ones.TryGetValue(t, out var one))
            return one;
        throw new Exception($"Increments and decrements are not supported on the type {t.RName()}.");
    }
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PostIncrement<T>(TEx<T> x) => AddAssign(x, GetOne(typeof(T))).Sub(GetOne(typeof(T)));

    [Assigns(0)]
    [BDSL2Operator]
    public static TEx<T> PreIncrement<T>(TEx<T> x) => AddAssign(x, GetOne(typeof(T)));
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PostDecrement<T>(TEx<T> x) => SubAssign(x, GetOne(typeof(T))).Add(GetOne(typeof(T)));
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PreDecrement<T>(TEx<T> x) => SubAssign(x, GetOne(typeof(T)));
}
}