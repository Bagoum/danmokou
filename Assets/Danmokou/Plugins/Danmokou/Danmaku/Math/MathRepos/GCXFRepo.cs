﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BagoumLib.Mathematics;
using Danmokou.DMath;
using Danmokou.Expressions;
using Scriptor.Expressions;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.DMath.Functions {
public static class GCXFRepo {

    public static Func<TExArgCtx, TEx> _Fake(Func<TExArgCtx, TEx> target) => args => {
        var fake = new TExPI();
        var inner = target(args.Append("gcx_bpi", args.GetByExprType<TExGCX>().bpi, true));
        if ((Expression) inner is ConstantExpression) return inner;
        
        return Ex.Block(new ParameterExpression[] {fake},
            //This assign is required, else the random id will be recalculated repeatedly!
            Ex.Assign(fake, args.GetByExprType<TExGCX>().bpi),
            inner
        );
    };

    public static readonly GCXF<Vector2> V2Zero = _ => Vector2.zero;
    public static readonly GCXF<V2RV2> RV2Zero = _ => V2RV2.Zero;

    public static readonly GCXF<float> Max = _ => M.IntFloatMax;
}
}