using System;
using BagoumLib.Expressions;
using UnityEngine;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Scriptables;
using Ex = System.Linq.Expressions.Expression;
using tfloat = Danmokou.Expressions.TEx<float>;
using tv3 = Danmokou.Expressions.TEx<UnityEngine.Vector3>;
using ev3 = Danmokou.Expressions.EEx<UnityEngine.Vector3>;
using erv2 = Danmokou.Expressions.EEx<Danmokou.DMath.V2RV2>;
using tv4 = Danmokou.Expressions.TEx<UnityEngine.Vector4>;
using static Danmokou.Expressions.ExMHelpers;
using static Danmokou.Expressions.ExUtils;

namespace Danmokou.DMath.Functions {
/// <summary>
/// Functions that return V4.
/// <br/>Note that these are also used for colors, in the format R G B A (0-1 floats).
/// </summary>
[Reflect]
public static partial class ExMV4 {
    public static tv4 Black() => ExC(ColorScheme.GetColor("black"));
    public static tv4 Purple() => ExC(ColorScheme.GetColor("purple"));
    public static tv4 Teal() => ExC(ColorScheme.GetColor("teal"));
    public static tv4 Green() => ExC(ColorScheme.GetColor("green"));
    public static tv4 Orange() => ExC(ColorScheme.GetColor("orange"));
    public static tv4 Yellow() => ExC(ColorScheme.GetColor("yellow"));
    public static tv4 Red() => ExC(ColorScheme.GetColor("red"));
    public static tv4 Pink() => ExC(ColorScheme.GetColor("pink"));
    public static tv4 Blue() => ExC(ColorScheme.GetColor("blue"));

    public static tv4 Palette(string palette, Palette.Shade shade) => 
        ExC(ColorScheme.GetColor(palette, shade));
    
    
    /// <summary>
    /// Derive a color from R, G, B components. Alpha is set to 1.
    /// </summary>
    public static tv4 RGB(tfloat r, tfloat g, tfloat b) => RGBA(r, g, b, E1);
    /// <summary>
    /// Derive a color from R, G, B, A components.
    /// </summary>
    public static tv4 RGBA(tfloat r, tfloat g, tfloat b, tfloat a) => ExUtils.V4(r, g, b, a);

    /// <summary>
    /// Derive a color from a vector3 containing R, G, B components and an separate alpha component.
    /// </summary>
    public static tv4 TP3A(tfloat a, ev3 tp) => EEx.ResolveV3(tp, v3 => ExUtils.V4(v3.x, v3.y, v3.z, a));
    
    /// <summary>
    /// Combine the R,G,B components of a vector4 and a separate alpha component.
    /// </summary>
    public static tv4 WithA(tfloat a, tv4 tp) {
        var v4 = V<Vector4>();
        return Ex.Block(new[] {v4},
            v4.Is(tp),
            v4.Field("w").Is(a),
            v4
        );
    }
    /// <summary>
    /// Multiply a color's alpha component by a float value.
    /// </summary>
    public static tv4 MulA(tfloat a, tv4 tp) {
        var v4 = V<Vector4>();
        return Ex.Block(new[] {v4},
            v4.Is(tp),
            MulAssign(v4.Field("w"), a),
            v4
        );
    }
}
}