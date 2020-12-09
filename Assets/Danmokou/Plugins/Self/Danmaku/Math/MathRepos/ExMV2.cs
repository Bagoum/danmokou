using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using DMK.Expressions;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using static DMK.Expressions.ExUtils;
using static DMK.Expressions.ExMHelpers;
using tfloat = DMK.Expressions.TEx<float>;
using tbool = DMK.Expressions.TEx<bool>;
using tv2 = DMK.Expressions.TEx<UnityEngine.Vector2>;
using tv3 = DMK.Expressions.TEx<UnityEngine.Vector3>;
using trv2 = DMK.Expressions.TEx<DMK.DMath.V2RV2>;
using efloat = DMK.Expressions.EEx<float>;
using ev2 = DMK.Expressions.EEx<UnityEngine.Vector2>;
using ev3 = DMK.Expressions.EEx<UnityEngine.Vector3>;
using erv2 = DMK.Expressions.EEx<DMK.DMath.V2RV2>;
using static DMK.DMath.Functions.ExM;

namespace DMK.DMath.Functions {
/// <summary>
/// Functions that return V2.
/// </summary>
public static partial class ExMV2 {
    
    public static tv2 Circle(tfloat period, tfloat radius, tfloat time) =>
        radius.Mul(CosSin(time.Mul(tau).Div(period)));

    public static tv2 DCircle(tfloat period, tfloat radius, tfloat time) {
        var w = VFloat();
        var t = VFloat();
        return Ex.Block(new[] {w, t},
            w.Is(tau.Div(period)),
            t.Is(w.Mul(time)),
            w.Mul(radius).Mul(V2(EN1.Mul(Sin(t)), Cos(t)))
        );
    }

    public static tv2 MulEntry(ev2 v1, ev2 v2) => EEx.ResolveV2(v1, v2, (a, b) => 
        V2(a.x.Mul(b.x), a.y.Mul(b.y)));
}
}