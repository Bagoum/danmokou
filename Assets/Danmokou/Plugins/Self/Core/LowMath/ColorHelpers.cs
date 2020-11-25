using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DMath.ColorHelpers;

namespace DMath {
public static class ColorHelpers {

    public static Color WithA(this Color c, float a) {
        c.a = a;
        return c;
    }
    public static Color MulA(this Color c, float a) {
        c.a *= a;
        return c;
    }
    public static Color V4C(Vector4 v4) => new Color(v4.x, v4.y, v4.z, v4.w);
    public static Vector4 CV4(Color c) => new Vector4(c.r, c.g, c.b, c.a);
    
    public static readonly GradientAlphaKey[] fullAlphaKeys = {
        new GradientAlphaKey(1, 0),
        new GradientAlphaKey(1, 1)
    };

    public static DGradient FromKeys(GradientColorKey[] colors, GradientAlphaKey[] alpha) {
        var g = new DGradient();
        g.SetKeys(colors, alpha);
        return g;
    }

    public static DGradient FromKeys(IEnumerable<GradientColorKey> colors, IEnumerable<GradientAlphaKey> alpha) =>
        FromKeys(colors.ToArray(), alpha.ToArray());

    public static DGradient EvenlySpaced(params Color[] colors) {
        float dist = 1f / (colors.Length - 1);
        GradientColorKey[] keys = new GradientColorKey[colors.Length];
        for (int ii = 0; ii < colors.Length; ++ii) {
            keys[ii] = new GradientColorKey(colors[ii], ii * dist);
        }
        return FromKeys(keys, fullAlphaKeys);
    }

    private static GradientAlphaKey FTime(this GradientAlphaKey key, Func<float, float> f) {
        key.time = f(key.time);
        return key;
    }
    public static GradientColorKey FTime(this GradientColorKey key, Func<float, float> f) {
        key.time = f(key.time);
        return key;
    }

    private static IEnumerable<GradientAlphaKey> SelectFTime(IEnumerable<GradientAlphaKey> keys, Func<float, float> f) =>
        keys.Select(x => x.FTime(f));
    private static IEnumerable<GradientColorKey> SelectFTime(IEnumerable<GradientColorKey> keys, Func<float, float> f) =>
        keys.Select(x => x.FTime(f));

    private static DGradient WithTimeModify(this Gradient g, Func<float, float> f) => FromKeys(
        SelectFTime(g.colorKeys, f),
        SelectFTime(g.alphaKeys, f));

    public static DGradient Reverse(this Gradient g) => g.WithTimeModify(t => 1 - t);

    public static DGradient RemapTime(this Gradient g, float start, float end) {
        var scol = g.Evaluate(start);
        var ecol = g.Evaluate(end);
        var sa = new GradientAlphaKey(scol.a, 0f);
        var ea = new GradientAlphaKey(ecol.a, 1f);
        var sc = new GradientColorKey(scol, 0f);
        var ec = new GradientColorKey(ecol, 1f);
        var colors = SelectFTime(g.colorKeys.Where(x => x.time > start && x.time < end), t => (t - start) / (end - start));
        var alphas = SelectFTime(g.alphaKeys.Where(x => x.time > start && x.time < end), t => (t - start) / (end - start));
        return FromKeys(colors.Append(sc).Append(ec), alphas.Append(sa).Append(ea));
    }

    public static IGradient Reverse(this IGradient ig) {
        if (ig is Gradient g) return g.Reverse();
        return new ReverseGradient(ig);
    }

    public static IGradient RemapTime(this IGradient ig, float start, float end) {
        if (ig is Gradient g) return g.RemapTime(start, end);
        return new RemapGradient(ig, start, end);
    }

    public static IGradient Modify(this IGradient ig, GradientModifier gt, RenderMode render) {
        if (render == RenderMode.ADDITIVE) {
            if (gt == GradientModifier.DARKINV) {
                ig = ig.RemapTime(0, 0.9f);
                gt = GradientModifier.DARKINVSOFT;
            }
            else ig = ig.RemapTime(0, 0.9f);
        }
        return ig.Modify(gt);
    }

    public static IGradient Modify(this IGradient ig, GradientModifier gt) {
        if (gt == GradientModifier.LIGHTFLAT) {
            return ig.RemapTime(0.25f, 0.93f).RemapTime(0f, 1.4f);
        } else if (gt == GradientModifier.LIGHT) {
            return ig.RemapTime(0.4f, 0.95f);
        } else if (gt == GradientModifier.LIGHTWIDE){
            return ig.RemapTime(0.25f, 0.95f);
        } else if (gt == GradientModifier.COLORFLAT) {
            return ig.RemapTime(0.2f, 0.65f).RemapTime(-0.1f, 1.3f);
        } else if (gt == GradientModifier.COLOR) {
            return ig.RemapTime(0.2f, 0.7f);
        } else if (gt == GradientModifier.COLORWIDE){
            return ig.RemapTime(0.07f, 0.73f);
        }  else if (gt == GradientModifier.DARKINVFLAT) {
            return ig.RemapTime(0.1f, 0.5f).Reverse().RemapTime(-0.7f, 1.1f);
        } else if (gt == GradientModifier.DARKINV) {
            return ig.RemapTime(0.1f, 0.5f).Reverse().RemapTime(-0.2f, 1.0f);
        } else if (gt == GradientModifier.DARKINVSOFT) {
            return ig.RemapTime(0.2f, 0.65f).Reverse().RemapTime(0f, 1.1f);
        } else if (gt == GradientModifier.DARKINVWIDE) {
            return ig.RemapTime(0f, 0.6f).Reverse();
        }  else if (gt == GradientModifier.FULLINV) {
            return ig.Reverse();
        } else return ig;
    }

    public static DGradient Downcast(this Gradient g) => FromKeys(g.colorKeys, g.alphaKeys);
}

public class ReverseGradient : IGradient {
    private readonly IGradient inner;
    public ReverseGradient(IGradient g) => inner = g;
    public Color Evaluate(float time) => inner.Evaluate(1 - time);
}

public class RemapGradient : IGradient {
    private readonly IGradient inner;
    private readonly float start;
    private readonly float eminuss;

    public RemapGradient(IGradient g, float start, float end) {
        inner = g;
        this.start = start;
        eminuss = (end - start);
    }

    public Color Evaluate(float time) => inner.Evaluate(Mathf.Clamp01(start + eminuss * time));
}

public interface IGradient {
    Color Evaluate(float time);
}
public interface INamedGradient {
    IGradient Gradient { get; }
    string Name { get; }
}

public class DGradient : Gradient, IGradient { }

/// <summary>
/// This class is for easy testing of the separate general IGradient modifier pipeline
/// </summary>
public class WrapGradient : IGradient {
    private readonly Gradient g;
    public WrapGradient(Gradient grad) => g = grad;
    public Color Evaluate(float time) => g.Evaluate(time);
}

public class MixedGradient : IGradient {
    private readonly IGradient g1;
    private readonly IGradient g2;
    private readonly float lerpOff;

    public MixedGradient(IGradient g1, IGradient g2, float lerpOffset=0f) {
        this.g1 = g1;
        this.g2 = g2;
        this.lerpOff = lerpOffset;
    }
    public Color Evaluate(float time) => Color.Lerp(g1.Evaluate(time), g2.Evaluate(time), 
        Mathf.Clamp01((time - 0.5f) * (0.5f / (0.5f - lerpOff)) + 0.5f));
}
public class NamedGradient : INamedGradient {
    public IGradient Gradient { get; }
    public string Name { get; }

    public NamedGradient(IGradient g, string n) {
        Gradient = g;
        Name = n;
    }
    public static NamedGradient Mix(INamedGradient ig1, INamedGradient ig2, float lerpOffset=0f) => 
        new NamedGradient(new MixedGradient(ig1.Gradient, ig2.Gradient, lerpOffset), $"{ig1.Name},{ig2.Name}");
}
}