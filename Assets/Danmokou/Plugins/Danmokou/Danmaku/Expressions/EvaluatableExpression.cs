using System;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using Danmokou.Core;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;

namespace Danmokou.Expressions {
/*
/// <summary>
/// Base class for <see cref="EEx{T}"/> used for type constraints.
/// </summary>
public class EEx {
    protected readonly Ex ex;
    public EEx(Ex ex) {
        this.ex = ex;
    }
    //Remove this in favor of subtype
    public static implicit operator Ex(EEx ex) => ex.ex;
    public static implicit operator (Ex, bool)(EEx exx) => (exx.ex, RequiresCopyOnRepeat(exx.ex));
}

/// <summary>
/// A typed expression.
/// This typing is syntactic sugar: any expression, regardless of type, can be cast as eg. TEx{float}.
/// <br/>This is used instead of <see cref="TEx{T}"/> when 
/// </summary>
/// <typeparam name="T">Type of expression (eg. float).</typeparam>
public class EEx<T> : EEx {
    public EEx(Ex ex) : base(ex) { }

    public static implicit operator EEx<T>(Ex ex) => new(ex);
    public static implicit operator EEx<T>(TEx<T> ex) => new(ex);
    public static implicit operator Ex(EEx<T> ex) => ex.ex;
    public static implicit operator (Ex, bool)(EEx<T> exx) => (exx.ex, RequiresCopyOnRepeat(exx.ex));
    
    public static implicit operator EEx<T>(T obj) => new(Ex.Constant(obj));
}*/
}