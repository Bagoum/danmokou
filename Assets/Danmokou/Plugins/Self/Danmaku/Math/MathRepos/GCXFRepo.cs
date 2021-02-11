using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DMK.DMath;
using DMK.Expressions;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace DMK.DMath.Functions {
public static class GCXFRepo {

    public static Func<TExArgCtx, TEx<T>> _Fake<T>(Func<TExArgCtx, TEx<T>> target) => gcx => {
        var fake = new TExPI();
        var inner = target(gcx.Append("gcx_bpi", fake, true));
        if ((Expression) inner is ConstantExpression) return inner;
        
        return Ex.Block(new ParameterExpression[] {fake},
            //This assign is required, else the random id will be recalculated repeatedly!
            Ex.Assign(fake, gcx.GetByExprType<TExGCX>().bpi),
            inner
        );
    };

    public static readonly GCXF<Vector2> V2Zero = _ => Vector2.zero;
    public static readonly GCXF<V2RV2> RV2Zero = _ => V2RV2.Zero;

    public static readonly GCXF<float> Max = _ => M.IntFloatMax;
}
}