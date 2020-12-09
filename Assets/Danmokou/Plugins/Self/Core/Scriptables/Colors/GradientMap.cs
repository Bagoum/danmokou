using System;
using DMK.Core;
using DMK.DMath;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;


namespace DMK.Scriptables {
public abstract class ColorMap : ScriptableObject {
    protected virtual void PrepareColors() { }
    protected const byte zero = 0; //lol

    public Sprite Recolor(Sprite baseSprite) {
        PrepareColors();
        Texture2D tex = Instantiate(baseSprite.texture);
        NativeArray<Color32> pixels_n = tex.GetRawTextureData<Color32>();
        unsafe {
            Color32* pixels = (Color32*) pixels_n.GetUnsafePtr();
            Map(pixels, pixels_n.Length);
        }
        tex.Apply();
        Vector2 pivot = baseSprite.pivot;
        pivot.x /= baseSprite.rect.width;
        pivot.y /= baseSprite.rect.height;
        var s = Sprite.Create(tex, baseSprite.rect, pivot, baseSprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        return s;
    }

    protected abstract unsafe void Map(Color32* pixels, int len);
}

[CreateAssetMenu(menuName = "Colors/GradientMap")]
public class GradientMap : ColorMap {
    public Gradient gradient;
    [NonSerialized] private IGradient setGradient;

    private void SetFromPalette(IGradient p, GradientModifier gt, DRenderMode render) =>
        setGradient = p.Modify(gt, render);

    public Sprite Recolor(IGradient p, GradientModifier gt, DRenderMode render, Sprite s) {
        SetFromPalette(p, gt, render);
        return Recolor(s);
    }

    protected override unsafe void Map(Color32* pixels, int len) {
        setGradient = setGradient ?? gradient.Downcast();
        for (int ii = 0; ii < len; ++ii) {
            Color32 pixel = pixels[ii];
            if (pixel.a > zero) {
                var nc = setGradient.Evaluate(pixel.r / 255f);
                pixels[ii].r = M.Float01ToByte(nc.r);
                pixels[ii].g = M.Float01ToByte(nc.g);
                pixels[ii].b = M.Float01ToByte(nc.b);
            }
        }
    }
}
}

