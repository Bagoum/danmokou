using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DMK.DMath;
using DMK.Expressions;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using ExTP = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector3>>;
using ExFXY = System.Func<DMK.Expressions.TEx<float>, DMK.Expressions.TEx<float>>;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExBPRV2 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<DMK.DMath.V2RV2>>;
using ExGCXF = System.Func<DMK.Expressions.TExGCX, DMK.Expressions.TEx>;
using ExPred = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<bool>>;

namespace DMK.DMath.Functions {
public static class GCXFRepo {

    private static ExGCXF _Fake(Func<TExPI, TEx> target) => gcx => {
        var fake = new TExPI();
        return Ex.Block(new ParameterExpression[] {fake}, 
            //This assign is required, else the random id will be recalculated repeatedly!
            Ex.Assign(fake, gcx.bpi),
            target(fake));
    };

    public static ExGCXF GCX_BPRV2(ExBPRV2 target) => _Fake(target);
    public static ExGCXF GCX_TP3(ExTP3 target) => _Fake(target);
    public static ExGCXF GCX_TP4(Func<TExPI, TEx<Vector4>> target) => _Fake(target);
    public static ExGCXF GCX_TP(ExTP target) => _Fake(target);
    public static ExGCXF GCX_BPY(ExBPY target) => _Fake(target);
    public static ExGCXF GCX_Pred(ExPred target) => _Fake(target);
    public static readonly GCXF<Vector2> V2Zero = _ => Vector2.zero;
    public static readonly GCXF<V2RV2> RV2Zero = _ => V2RV2.Zero;

    public static readonly GCXF<float> Max = _ => M.IntFloatMax;
}
}