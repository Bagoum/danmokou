using DMK.DMath;
using UnityEngine;

namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Colors/MultiPaletteMap")]
public class MultiPaletteMap : ColorMap {
    public Palette red = null!;
    public GradientModifier redMod;
    public Palette green = null!;
    public GradientModifier greenMod;
    public Palette blue = null!;
    public GradientModifier blueMod;
    
    private static readonly Color nc = new Color(0, 0, 0, 0);
    protected override unsafe void Map(Color32* pixels, int len) {
        var gr = red.Gradient.Modify(redMod);
        var gg = green.Gradient.Modify(greenMod);
        var gb = blue.Gradient.Modify(blueMod);
        for (int ii = 0; ii < len; ++ii) {
            Color32 pixel = pixels[ii];
            if (pixel.a > byte.MinValue) {
                float total = pixel.r + pixel.g + pixel.b + 1;
                Color32 newc = 
                    gr.Evaluate(pixel.r / 255f) * ((pixel.r + 1) / total) + 
                    gg.Evaluate(pixel.g / 255f) * (pixel.g / total) + 
                    gb.Evaluate(pixel.b / 255f) * (pixel.b / total);
                newc.a = pixel.a;
                pixels[ii] = newc;
            }
        }
    }
}
}
