using System;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib.Mathematics;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Scriptor;
using Scriptor.Expressions;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;

namespace Danmokou.Expressions {
public static class TExHelpers {
    public static TExPI? MaybeBPI(this TExArgCtx tac) => tac.MaybeGetByExprType<TExPI>(out _);
    public static Ex FCTX(this TExArgCtx tac) => tac.BPI().FiringCtx;
    public static UnaryExpression findex(this TExArgCtx tac) => tac.BPI().findex;
    public static MemberExpression id(this TExArgCtx tac) => tac.BPI().id;
    public static MemberExpression index(this TExArgCtx tac) => tac.BPI().index;
    public static MemberExpression LocV2(this TExArgCtx tac) => tac.BPI().locV2;
    public static MemberExpression LocV3(this TExArgCtx tac) => tac.BPI().locV3;
    public static MemberExpression locx(this TExArgCtx tac) => tac.BPI().locx;
    public static MemberExpression locy(this TExArgCtx tac) => tac.BPI().locy;
    public static MemberExpression locz(this TExArgCtx tac) => tac.BPI().locz;
    public static Ex t(this TExArgCtx tac) => tac.BPI().t;
    public static TExPI BPI(this TExArgCtx tac) => tac.MaybeBPI() ?? throw new CompileException(
        "You are refencing fields on ParametricInfo, but no variable with this type is provided. This is most likely " +
        "because you need to use `Wrap` to make a GCXF<T> instead of a compile-time function call.");
    public static TExSB SB(this TExArgCtx tac) => tac.GetByExprType<TExSB>();
    public static TExSB? MaybeSB(this TExArgCtx tac) => tac.MaybeGetByExprType<TExSB>(out _);
    
    public static TExArgCtx Rehash(this TExArgCtx tac) {
        var bpi = tac.GetByExprType<TExPI>(out var bidx);
        return tac.MakeCopyWith(bidx, TExArgCtx.Arg.FromTEx(tac.Args[bidx].name, new TExPI(bpi.Rehash()), tac.Args[bidx].hasTypePriority));
    }
    public static TExArgCtx CopyWithT(this TExArgCtx tac, Ex newT) {
        var bpi = tac.GetByExprType<TExPI>(out var bidx);
        return tac.MakeCopyWith(bidx, TExArgCtx.Arg.FromTEx(tac.Args[bidx].name, new TExPI(bpi.CopyWithT(newT)), tac.Args[bidx].hasTypePriority));
    }
    
    public static TExArgCtx AppendSB(this TExArgCtx tac, string name, TExSB ex, bool hasPriority=true) {
        var nargs = tac.Args.Append(TExArgCtx.Arg.FromTEx(name, ex, hasPriority));
        if (tac.MaybeGetByExprType<TExPI>(out _) == null) 
            nargs = nargs.Append(TExArgCtx.Arg.FromTEx(name + "_bpi", ex.bpi, true));
        return new TExArgCtx(tac, nargs.ToArray());
    }
    
    /// <summary>
    /// Feed the X,Y components of a V2 into a resolver.
    /// <br/>If the V2 is a `new Vector2` expression,
    ///  then skips the constructor and resolves its arguments directly.
    /// <br/>If singleUse is set to true and the V2 is a `new Vector2` expression,
    ///  then provide the constructor arguments directly to the resolver without copying.
    /// </summary>
    public static Ex ResolveV2AsXY(TEx<Vector2> v2, Func<TEx<float>, TEx<float>, Ex> resolver, bool singleUse = false) =>
        TEx.ResolveFieldsMaybeDeconstructNew(x => resolver(x[0], x[1]), v2, singleUse, "x", "y");
    public static Ex ResolveV2(TEx<Vector2> t1, Func<TExV2, Ex> resolver) =>
        TEx.ResolveCopy(x => resolver(new TExV2(x[0])), t1);
    public static Ex ResolveV3(TEx<Vector3> t1, Func<TExV3, Ex> resolver) =>
        TEx.ResolveCopy(x => resolver(new TExV3(x[0])), t1);
    public static Ex ResolveV2(TEx<Vector2> t1, TEx<Vector2> t2, 
        Func<TExV2, TExV2, Ex> resolver) =>
        TEx.ResolveCopy(x => resolver(new TExV2(x[0]), new TExV2(x[1])), t1, t2);
    /// <inheritdoc cref="TEx.Resolve{T1,T2,T3}"/>
    public static Ex ResolveV3(TEx<Vector3> t1, TEx<Vector3> t2, 
        Func<TExV3, TExV3, Ex> resolver) =>
        TEx.ResolveCopy(x => resolver(new TExV3(x[0]), new TExV3(x[1])), t1, t2);
    public static Ex ResolveV2(TEx<Vector2> t1, TEx<Vector2> t2, TEx<Vector2> t3, 
        Func<TExV2, TExV2, TExV2, Ex> resolver) =>
        TEx.ResolveCopy(x => resolver(new TExV2(x[0]), new TExV2(x[1]), new TExV2(x[2])), t1, t2, t3);
    
    public static TEx? Generate(Type t, ParameterExpression ex) {
        if (t == tv2)
            return new TExV2(ex);
        else if (t == tv3)
            return new TExV3(ex);
        else if (t == typeof(ParametricInfo))
            return new TExPI(ex);
        else if (t == typeof(BulletManager.SimpleBullet))
            return new TExSB(ex);
        else if (t == typeof(V2RV2))
            return new TExRV2(ex);
        else if (t == typeof(BulletManager.SimpleBulletCollection))
            return new TExSBC(ex);
        else if (t == typeof(BulletManager.SimpleBulletCollection.VelocityUpdateState))
            return new TExSBCUpdater(ex);
        else if (t == typeof(Movement))
            return new TExMov(ex);
        else if (t == typeof(LaserMovement))
            return new TExLMov(ex);
        else if (t == typeof(GenCtx))
            return new TExGCX(ex);
        else
            return null;
    }
    
    //Methods for dynamic (dict-based) data lookup
    public static Ex DynamicHas<T>(this TExArgCtx tac, string key) => PIData.ContainsDynamic<T>(tac, key);
    public static Ex DynamicGet<T>(this TExArgCtx tac, string key) => PIData.GetValueDynamic<T>(tac, key);
    public static Ex DynamicSet<T>(this TExArgCtx tac, string key, Ex val) => PIData.SetValueDynamic<T>(tac, key, val);


}
}