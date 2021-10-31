using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Expressions;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;
using static Danmokou.Expressions.ExMHelpers;
using tfloat = Danmokou.Expressions.TEx<float>;
using tbool = Danmokou.Expressions.TEx<bool>;
using tv2 = Danmokou.Expressions.TEx<UnityEngine.Vector2>;
using tv3 = Danmokou.Expressions.TEx<UnityEngine.Vector3>;
using trv2 = Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>;
using efloat = Danmokou.Expressions.EEx<float>;
using ev2 = Danmokou.Expressions.EEx<UnityEngine.Vector2>;
using ev3 = Danmokou.Expressions.EEx<UnityEngine.Vector3>;
using erv2 = Danmokou.Expressions.EEx<Danmokou.DMath.V2RV2>;
using static Danmokou.DMath.Functions.ExM;

namespace Danmokou.DMath.Functions {
/// <summary>
/// Functions that return V2.
/// </summary>
[Reflect]
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