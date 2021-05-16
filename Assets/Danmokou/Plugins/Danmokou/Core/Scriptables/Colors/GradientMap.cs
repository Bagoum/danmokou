using System;
using Danmokou.Core;
using Danmokou.DMath;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;


namespace Danmokou.Scriptables {
public abstract class ColorMap : ScriptableObject {
    protected virtual void PrepareColors(DRenderMode render) { }

    public Sprite Recolor(Sprite baseSprite, DRenderMode render) {
        PrepareColors(render);
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
    public Gradient gradient = null!;
    [NonSerialized] private IGradient? setGradient;
    
    protected virtual void SetFromPalette(IGradient p, GradientModifier gt, DRenderMode render) =>
        setGradient = p.Modify(gt, render);

    public Sprite Recolor(IGradient p, GradientModifier gt, DRenderMode render, Sprite s) {
        SetFromPalette(p, gt, render);
        return Recolor(s, render);
    }

    protected override unsafe void Map(Color32* pixels, int len) {
        setGradient ??= DGradient.FromUnityGradient(gradient);
        for (int ii = 0; ii < len; ++ii) {
            ref Color32 pixel = ref pixels[ii];
            var a = pixel.a;
            if (a > byte.MinValue) {
                pixel = setGradient.Evaluate32(pixel.r / 255f);
                pixel.a = a;
            }
        }
    }
}
}

