using System;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using Danmokou.Core;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;

namespace Danmokou.Expressions {

public class EEx {
    protected readonly Ex ex;
    public static bool RequiresCopyOnRepeat(Expression e) => !(
        e.NodeType == ExpressionType.Parameter ||
        e.NodeType == ExpressionType.Constant ||
        e.NodeType == ExpressionType.MemberAccess ||
        (e.NodeType == ExpressionType.Convert && !RequiresCopyOnRepeat((e as UnaryExpression)!.Operand)));
    public EEx(Ex ex) {
        this.ex = ex;
    }
    //Remove this in favor of subtype
    public static implicit operator Ex(EEx ex) => ex.ex;
    public static implicit operator (Ex, bool)(EEx exx) => (exx.ex, RequiresCopyOnRepeat(exx.ex));
    private static Ex ResolveCopy(Func<Ex[], Ex> func, params (Ex, bool)[] requiresCopy) {
        var newvars = ListCache<ParameterExpression>.Get();
        var setters = ListCache<Expression>.Get();
        var usevars = new Expression[requiresCopy.Length];
        for (int ii = 0; ii < requiresCopy.Length; ++ii) {
            var (ex, reqCopy) = requiresCopy[ii];
            if (reqCopy) {
                //Don't name this, as nested EEx should not overlap
                var copy = V(ex.Type);
                usevars[ii] = copy;
                newvars.Add(copy);
                setters.Add(copy.Is(ex));
            } else {
                usevars[ii] = ex;
            }
        }
        setters.Add(func(usevars));
        var block = Ex.Block(newvars, setters);
        ListCache<ParameterExpression>.Consign(newvars);
        ListCache<Expression>.Consign(setters);
        return setters.Count > 1 ? func(usevars) : block;
    }
    public static Ex ResolveF(EEx<float> t1, Func<TEx<float>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0]), t1);
    public static Ex Resolve<T1>(EEx<T1> t1, Func<TEx<T1>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0]), t1);
    public static Ex ResolveV2(EEx<Vector2> t1, Func<TExV2, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0])), t1);
    public static Ex ResolveV3(EEx<Vector3> t1, Func<TExV3, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV3(x[0])), t1);
    public static Ex Resolve<T1,T2>(EEx<T1> t1, EEx<T2> t2, Func<TEx<T1>, TEx<T2>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1]), t1, t2);
    public static Ex ResolveV2(EEx<Vector2> t1, EEx<Vector2> t2, 
        Func<TExV2, TExV2, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0]), new TExV2(x[1])), t1, t2);
    public static Ex ResolveV2(EEx<Vector2> t1, EEx<float> t2, 
        Func<TExV2, TEx<float>, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0]), x[1]), t1, t2);
    public static Ex ResolveV3(EEx<Vector3> t1, EEx<Vector3> t2, 
        Func<TExV3, TExV3, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV3(x[0]), new TExV3(x[1])), t1, t2);
    public static Ex Resolve<T1,T2,T3>(EEx<T1> t1, EEx<T2> t2, EEx<T3> t3, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2]), t1, t2, t3);
    public static Ex ResolveV2(EEx<Vector2> t1, EEx<Vector2> t2, EEx<Vector2> t3, 
        Func<TExV2, TExV2, TExV2, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0]), new TExV2(x[1]), new TExV2(x[2])), t1, t2, t3);
    public static Ex Resolve<T1,T2,T3,T4>(EEx<T1> t1, EEx<T2> t2, EEx<T3> t3, EEx<T4> t4, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3]), t1, t2, t3, t4);
    public static Ex Resolve<T1,T2,T3,T4,T5>(EEx<T1> t1, EEx<T2> t2, EEx<T3> t3, EEx<T4> t4, EEx<T5> t5, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, TEx<T5>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3], x[4]), t1, t2, t3, t4, t5);
}

public class EEx<T> : EEx {
    public EEx(Ex ex) : base(ex) { }

    public static implicit operator EEx<T>(Ex ex) => new EEx<T>(ex);
    public static implicit operator EEx<T>(TEx<T> ex) => new EEx<T>(ex);
    public static implicit operator Ex(EEx<T> ex) => ex.ex;
    public static implicit operator (Ex, bool)(EEx<T> exx) => (exx.ex, RequiresCopyOnRepeat(exx.ex));
    
    public static implicit operator EEx<T>(T obj) => new EEx<T>(Ex.Constant(obj));
}
}