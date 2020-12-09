using System;
using System.Linq;
using DMK.DMath;
using UnityEngine;

namespace DMK.Scriptables {

[Serializable]
public struct PalettePoint {
    public Palette palette;
    public Palette.Shade shade;
    public float time;
}

[CreateAssetMenu(menuName = "Colors/DerivedGradientMap")]
public class DerivedGradientMap : GradientMap {
    public PalettePoint[] points;
    protected override void PrepareColors() {
        gradient = ColorHelpers.FromKeys(points.Select(x => new GradientColorKey(x.palette.GetColor(x.shade), x.time)),
            ColorHelpers.fullAlphaKeys);
    }
}
}
