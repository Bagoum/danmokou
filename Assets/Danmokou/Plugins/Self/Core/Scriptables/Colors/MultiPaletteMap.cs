using DMath;
using UnityEngine;

namespace Danmaku.Scriptables.Colors {
[CreateAssetMenu(menuName = "Colors/MultiPaletteMap")]
public class MultiPaletteMap : ColorMap {
    public Palette red;
    public GradientModifier redMod;
    public Palette green;
    public GradientModifier greenMod;
    public Palette blue;
    public GradientModifier blueMod;
    
    protected override unsafe void Map(Color32* pixels, int len) {
        var gr = red.Gradient.Modify(redMod);
        var gg = green.Gradient.Modify(greenMod);
        var gb = blue.Gradient.Modify(blueMod);
        for (int ii = 0; ii < len; ++ii) {
            Color32 pixel = pixels[ii];
            if (pixel.a > zero) {
                Color32 newc = gr.Evaluate(pixel.r / 255f) + gg.Evaluate(pixel.g / 255f) + gb.Evaluate(pixel.b / 255f);
                newc.a = pixel.a;
                pixels[ii] = newc;
            }
        }
    }
}
}