using System;
using Danmokou.Core;
using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Colors/MultiPaletteMap")]
public class MultiPaletteMap : ColorMap {
    public Palette red = null!;
    public GradientModifier redMod;
    public Palette green = null!;
    public GradientModifier greenMod;
    public Palette blue = null!;
    public GradientModifier blueMod;
    
    private static readonly Color nc = new Color(0, 0, 0, 0);
    
    [NonSerialized] private IGradient? rg;
    [NonSerialized] private IGradient? gg;
    [NonSerialized] private IGradient? bg;

    protected override void PrepareColors(DRenderMode render) {
        rg = red.Gradient.Modify(redMod, render);
        gg = green.Gradient.Modify(greenMod, render);
        bg = blue.Gradient.Modify(blueMod, render);
    }
    
    
    public Sprite Recolor(Palette r, GradientModifier rt, Palette g, GradientModifier gt, 
        Palette b, GradientModifier bt, DRenderMode render, Sprite s) {
        red = r;
        redMod = rt;
        green = g;
        greenMod = gt;
        blue = b;
        blueMod = bt;
        return Recolor(s, render);
    }

    protected override unsafe void Map(Color32* pixels, int len) {
        rg ??= red.Gradient.Modify(redMod);
        gg ??= green.Gradient.Modify(greenMod);
        bg ??= blue.Gradient.Modify(blueMod);
        for (int ii = 0; ii < len; ++ii) {
            ref Color32 pixel = ref pixels[ii];
            if (pixel.a > byte.MinValue) {
                float total = pixel.r + pixel.g + pixel.b + 1;
                //This method assumes that any mixture of channels is a blend operation.
                float evalAt = Math.Max(Math.Max(pixel.r, pixel.g), pixel.b) / 255f;
                var r = rg.Evaluate(evalAt) * ((pixel.r + 1) / total);
                var g = gg.Evaluate(evalAt) * (pixel.g / total);
                var b = bg.Evaluate(evalAt) * (pixel.b / total);
                pixel.r = M.Float01ToByte(r.r + g.r + b.r);
                pixel.g = M.Float01ToByte(r.g + g.g + b.g);
                pixel.b = M.Float01ToByte(r.b + g.b + b.b);
            }
        }
    }
}
}
