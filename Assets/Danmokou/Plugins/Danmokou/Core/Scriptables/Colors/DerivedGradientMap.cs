using System;
using System.Linq;
using Danmokou.Core;
using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.Scriptables {

[Serializable]
public struct PalettePoint {
    public Palette palette;
    public Palette.Shade shade;
    public float time;
}

[CreateAssetMenu(menuName = "Colors/DerivedGradientMap")]
public class DerivedGradientMap : GradientMap {
    public PalettePoint[] points = null!;
    protected override void PrepareColors(DRenderMode render) {
        gradient = ColorHelpers.FromKeys(points.Select(x => new GradientColorKey(x.palette.GetColor(x.shade), x.time))).ToUnityGradient();
    }
}
}
