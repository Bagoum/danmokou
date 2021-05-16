using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.DMath {
public enum GradientModifier {
    //LIGHTERFLAT, // Full range to upmost, with cutoff
    LIGHTFLAT, // Full range to upper, with cutoff
    LIGHT, // /Full range to upper
    LIGHTWIDE,
    COLORFLAT, // Full range to center, with cutoff
    COLOR, // Full range to center
    COLORWIDE,
    //DARK0, // Full range to lower
    DARKINVFLAT, // Full range to lower, reversed, with cutoff
    DARKINV, // Full range to lower, reversed
    DARKINVWIDE,
    DARKINVSOFT, // Full range to lower, reversed, less black than DARKINV
    FULLINV, // Reversed
    FULL, // No change
}

public enum RGBRecolorMode {
    NONE,
    RB,
    RGB
}

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

    public static DGradient FromKeys(IEnumerable<GradientColorKey> colors, IEnumerable<GradientAlphaKey>? alpha = null) => new DGradient(colors, alpha);

    public static DGradient FromKeys(IEnumerable<(float, Color)> colors, IEnumerable<(float, float)>? alpha) =>
        new DGradient(colors, alpha);

    public static DGradient EvenlySpaced(params Color[] colors) {
        float dist = 1f / (colors.Length - 1);
        GradientColorKey[] keys = new GradientColorKey[colors.Length];
        for (int ii = 0; ii < colors.Length; ++ii) {
            keys[ii] = new GradientColorKey(colors[ii], ii * dist);
        }
        return FromKeys(keys, null);
    }

    private static GradientAlphaKey FTime(this GradientAlphaKey key, Func<float, float> f) {
        key.time = f(key.time);
        return key;
    }
    public static GradientColorKey FTime(this GradientColorKey key, Func<float, float> f) {
        key.time = f(key.time);
        return key;
    }
    public static (float t, T c) FTime<T>(this (float t, T c) key, Func<float, float> f) {
        key.t = f(key.t);
        return key;
    }

    private static IEnumerable<GradientAlphaKey> SelectFTime(IEnumerable<GradientAlphaKey> keys, Func<float, float> f) =>
        keys.Select(x => x.FTime(f));
    private static IEnumerable<GradientColorKey> SelectFTime(IEnumerable<GradientColorKey> keys, Func<float, float> f) =>
        keys.Select(x => x.FTime(f));
    private static IEnumerable<(float, T)> SelectFTime<T>(IEnumerable<(float t, T c)> keys, Func<float, float> f) =>
        keys.Select(x => x.FTime(f));

    private static DGradient WithTimeModify(this DGradient g, Func<float, float> f) => FromKeys(
        SelectFTime(g.colors, f),
        g.alphas == null ? null : SelectFTime(g.alphas, f));

    public static DGradient Reverse(this DGradient g) => g.WithTimeModify(t => 1 - t);

    public static DGradient RemapTime(this DGradient g, float start, float end) {
        var scol = g.Evaluate(start);
        var ecol = g.Evaluate(end);
        var colors = SelectFTime(
            g.colors.Where(x => x.t > start && x.t < end), 
            t => (t - start) / (end - start));
        var alphas = g.alphas == null ? null : 
            SelectFTime(
                g.alphas.Where(x => x.t > start && x.t < end), 
                t => (t - start) / (end - start));
        return FromKeys(
            colors.Append((0, scol)).Append((1, ecol)), 
            alphas?.Append((0, scol.a)).Append((1, ecol.a)));
    }

    public static IGradient Reverse(this IGradient ig) {
        if (ig is DGradient g) return g.Reverse();
        return new ReverseGradient(ig);
    }

    public static IGradient RemapTime(this IGradient ig, float start, float end) {
        if (ig is DGradient g) return g.RemapTime(start, end);
        return new RemapGradient(ig, start, end);
    }

    public static IGradient Modify(this IGradient ig, GradientModifier gt, DRenderMode render) {
        if (render == DRenderMode.ADDITIVE) {
            if (gt == GradientModifier.DARKINV) {
                ig = ig.RemapTime(0, 0.9f);
                gt = GradientModifier.DARKINVSOFT;
            }
            else ig = ig.RemapTime(0, 0.9f);
        }
        return ig.Modify(gt);
    }

    public static IGradient Modify(this IGradient ig, GradientModifier gt) =>
        gt switch {
            GradientModifier.LIGHTFLAT => 
                ig.RemapTime(0.25f, 0.93f).RemapTime(0f, 1.4f),
            GradientModifier.LIGHT => 
                ig.RemapTime(0.4f, 0.95f),
            GradientModifier.LIGHTWIDE => 
                ig.RemapTime(0.25f, 0.95f),
            GradientModifier.COLORFLAT => 
                ig.RemapTime(0.2f, 0.65f).RemapTime(-0.1f, 1.3f),
            GradientModifier.COLOR => 
                ig.RemapTime(0.2f, 0.7f),
            GradientModifier.COLORWIDE => 
                ig.RemapTime(0.07f, 0.73f),
            GradientModifier.DARKINVFLAT => 
                ig.RemapTime(0.1f, 0.5f).Reverse().RemapTime(-0.7f, 1.1f),
            GradientModifier.DARKINV => 
                ig.RemapTime(0.1f, 0.5f).Reverse().RemapTime(-0.2f, 1.0f),
            GradientModifier.DARKINVSOFT => 
                ig.RemapTime(0.2f, 0.65f).Reverse().RemapTime(0f, 1.1f),
            GradientModifier.DARKINVWIDE => 
                ig.RemapTime(0f, 0.6f).Reverse(),
            GradientModifier.FULLINV => 
                ig.Reverse(),
            _ => ig
        };
}

public class ReverseGradient : IGradient {
    private readonly IGradient inner;
    public ReverseGradient(IGradient g) => inner = g;
    public Color32 Evaluate32(float time) => inner.Evaluate32(1 - time);
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

    public Color32 Evaluate32(float time) => inner.Evaluate32(Mathf.Clamp01(start + eminuss * time));
    public Color Evaluate(float time) => inner.Evaluate(Mathf.Clamp01(start + eminuss * time));
}

public interface IGradient {
    Color32 Evaluate32(float time);
    Color Evaluate(float time);
}
public interface INamedGradient {
    IGradient Gradient { get; }
    string Name { get; }
}

/// <summary>
/// Reimplementation of gradient class using Color32 with inlining efficiency.
/// </summary>
public class DGradient : IGradient {
    public readonly (float t, Color c)[] colors;
    public readonly (float t, float a)[]? alphas;
    public readonly Color32 colorStart;
    public readonly Color32 colorEnd;

    public DGradient(IEnumerable<GradientColorKey> colors, IEnumerable<GradientAlphaKey>? alphas) : this(
        colors.Select(k => (k.time, k.color)),
        alphas?.Select(k => (k.time, k.alpha))) { }

    public DGradient(IEnumerable<(float t , Color c)> colors, IEnumerable<(float t, float a)>? alphas) {
        this.colors = colors.OrderBy(x => x.t).ToArray();
        this.alphas = alphas?.OrderBy(x => x.t).ToArray();
        colorStart = this.colors[0].c;
        colorEnd = this.colors[this.colors.Length - 1].c;
    }

    public Gradient ToUnityGradient() {
        var g = new Gradient();
        g.SetKeys(colors.Select(c => new GradientColorKey(c.c, c.t)).ToArray(), 
            alphas?.Select(a => new GradientAlphaKey(a.a, a.t)).ToArray() ?? ColorHelpers.fullAlphaKeys);
        return g;
    }

    public static DGradient FromUnityGradient(Gradient g) => new DGradient(g.colorKeys, g.alphaKeys);
    
    private static readonly Color32 baseColor = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
    public Color32 Evaluate32(float time) {
        Color32 c;
        if (time <= colors[0].t)
            c = colorStart;
        else if (time >= colors[colors.Length - 1].t)
            c = colorEnd;
        else {
            c = baseColor;
            for (int ic = 1; ic < colors.Length; ++ic) {
                if (time < colors[ic].t) {
                    ref var pc = ref colors[ic - 1];
                    ref var nc = ref colors[ic];
                    float m = (time - pc.t) / (nc.t - pc.t);
                    c.r = M.Float01ToByte(pc.c.r + (nc.c.r - pc.c.r) * m);
                    c.g = M.Float01ToByte(pc.c.g + (nc.c.g - pc.c.g) * m);
                    c.b = M.Float01ToByte(pc.c.b + (nc.c.b - pc.c.b) * m);
                    break;
                }
            }
        }
        if (alphas == null)
            return c;
        if (time <= alphas[0].t)
            c.a = M.Float01ToByte(alphas[0].a);
        else if (time >= alphas[alphas.Length - 1].t)
            c.a = M.Float01ToByte(alphas[alphas.Length - 1].a);
        else {
            for (int ic = 1; ic < alphas.Length; ++ic) {
                if (time < alphas[ic].t) {
                    c.a = M.Float01ToByte(M.Lerp(alphas[ic - 1].t, alphas[ic].t, time, alphas[ic - 1].a, alphas[ic].a));
                    break;
                }
            }
        }
        return c;
    }
    
    
    public Color Evaluate(float time) {
        Color c = Color.white;
        if (time <= colors[0].t)
            c = colors[0].c;
        else if (time >= colors[colors.Length - 1].t)
            c = colors[colors.Length - 1].c;
        else {
            for (int ic = 1; ic < colors.Length; ++ic) {
                if (time < colors[ic].t) {
                    ref var pc = ref colors[ic - 1];
                    ref var nc = ref colors[ic];
                    float m = (time - pc.t) / (nc.t - pc.t);
                    c.r = pc.c.r + (nc.c.r - pc.c.r) * m;
                    c.g = pc.c.g + (nc.c.g - pc.c.g) * m;
                    c.b = pc.c.b + (nc.c.b - pc.c.b) * m;
                    break;
                }
            }
        }
        if (alphas == null)
            return c;
        if (time <= alphas[0].t)
            c.a = alphas[0].a;
        else if (time >= alphas[alphas.Length - 1].t)
            c.a = alphas[alphas.Length - 1].a;
        else {
            for (int ic = 1; ic < alphas.Length; ++ic) {
                if (time < alphas[ic].t) {
                    c.a = M.Lerp(alphas[ic - 1].t, alphas[ic].t, time, alphas[ic - 1].a, alphas[ic].a);
                    break;
                }
            }
        }
        return c;

    }
}

/// <summary>
/// For slow-path testing
/// </summary>
public class WrapGradient : IGradient {
    private readonly DGradient g;
    public WrapGradient(DGradient grad) => g = grad;
    public Color Evaluate(float time) => g.Evaluate(time);
    public Color32 Evaluate32(float time) => g.Evaluate32(time);
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
    public Color32 Evaluate32(float time) => Color.Lerp(g1.Evaluate32(time), g2.Evaluate32(time), 
        Mathf.Clamp01((time - 0.5f) * (0.5f / (0.5f - lerpOff)) + 0.5f));
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