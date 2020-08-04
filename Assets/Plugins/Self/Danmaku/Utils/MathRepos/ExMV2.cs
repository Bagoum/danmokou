using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using Danmaku;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using static ExUtils;
using static DMath.ExMHelpers;
using tfloat = TEx<float>;
using tbool = TEx<bool>;
using tv2 = TEx<UnityEngine.Vector2>;
using tv3 = TEx<UnityEngine.Vector3>;
using trv2 = TEx<DMath.V2RV2>;
using efloat = DMath.EEx<float>;
using ev2 = DMath.EEx<UnityEngine.Vector2>;
using ev3 = DMath.EEx<UnityEngine.Vector3>;
using erv2 = DMath.EEx<DMath.V2RV2>;
using static DMath.ExM;

namespace DMath {
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
}
}