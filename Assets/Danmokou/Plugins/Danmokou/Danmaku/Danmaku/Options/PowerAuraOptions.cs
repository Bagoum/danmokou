using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.Danmaku.Options.PowerAuraOption;

namespace Danmokou.Danmaku.Options {
/// <summary>
/// Properties that modify the behavior of simple bullets.
/// </summary>
[Reflect]
public class PowerAuraOption {
    public static PowerAuraOption Scale(GCXF<float> scale) => new ScaleProp(scale);
    public static PowerAuraOption Color(TP4 color) => new ColorProp(color);
    public static PowerAuraOption SFX(string sfx) => new SFXProp(sfx);
    /// <summary>
    /// Set the time in seconds that the aura effect starts with. Useful if you need the effect to appear immediately
    /// instead of fading inwards.
    /// </summary>
    public static PowerAuraOption InitialTime(GCXF<float> time) => new InitialTimeProp(time);
    /// <summary>
    /// Set the total time for the aura effect.
    /// </summary>
    public static PowerAuraOption Time(GCXF<float> time) => new TimeProp(time);

    public static PowerAuraOption Static() => new StaticFlag();
    /// <summary>
    /// Set positive for inwards power aura, negative for outwards power aura.
    /// </summary>
    public static PowerAuraOption Iterations(GCXF<float> itrs) => new IterationsProp(itrs);
    
    /// <summary>
    /// Renders a power aura on the high effects layer.
    /// </summary>
    public static PowerAuraOption High() => new LayerProp(Layer.HighFX);

    /// <summary>
    /// Run another aura after this one is finished.
    /// </summary>
    public static PowerAuraOption Next(PowerAuraOption[] nextProps) => new NextProp(nextProps);

    /// <summary>
    /// Do 2 iterations inwards with color1 over 1.5 seconds, then 1 iteration outwards with color2 over 0.5 seconds,
    /// using the sound effects "x-powerup-1" and "x-powerdown-1" respectively.
    /// </summary>
    public static PowerAuraOption Boss1(TP4 color1, TP4 color2) => new CompositeProp(
        Color(color1),
        SFX("x-powerup-1"),
        Time(_ => 1.5f),
        Iterations(_ => 2),
        Next(new[] {
            Color(color2),
            SFX("x-powerdown-1"),
            Time(_ => 0.5f),
            Iterations(_ => -1)
        })
    );

    #region impl
    public class ValueProp<T> : PowerAuraOption {
        public readonly T value;
        protected ValueProp(T value) => this.value = value;
    }
    
    public class ScaleProp : ValueProp<GCXF<float>> {
        public ScaleProp(GCXF<float> f) : base(f) { }
    }
    
    public class ColorProp : ValueProp<TP4> {
        public ColorProp(TP4 f) : base(f) { }
    }
    
    public class InitialTimeProp : ValueProp<GCXF<float>> {
        public InitialTimeProp(GCXF<float> f) : base(f) { }
    }
    public class TimeProp : ValueProp<GCXF<float>> {
        public TimeProp(GCXF<float> f) : base(f) { }
    }

    public class IterationsProp : ValueProp<GCXF<float>> {
        public IterationsProp(GCXF<float> f) : base(f) { }
    }
    
    public class SFXProp : ValueProp<string> {
        public SFXProp(string f) : base(f) { }
    }

    public class LayerProp : ValueProp<Layer> {
        public LayerProp(Layer l) : base(l) { }
    }
    public class NextProp : ValueProp<PowerAuraOption[]> {
        public NextProp(PowerAuraOption[] f) : base(f) { }
    }
    
    public class StaticFlag : PowerAuraOption { }
    
    public class CompositeProp : ValueProp<PowerAuraOption[]>, IUnrollable<PowerAuraOption> {
        public IEnumerable<PowerAuraOption> Values => value;
        public CompositeProp(params PowerAuraOption[] props) : base(props) { }
    }
    
    #endregion
}

public readonly struct RealizedPowerAuraOptions {
    public readonly float? scale;
    public readonly BehaviorEntity? parent;
    public readonly Vector2 offset;
    public readonly TP4 color;
    public readonly float initialTime;
    public readonly float totalTime;
    public readonly float iterations;
    public readonly string? sfx;
    public readonly int? layer;

    public readonly ICancellee cT;
    public readonly Action? continuation;

    public RealizedPowerAuraOptions(PowerAuraOptions opts, GenCtx gcx, Vector2 unparentedOffset, ICancellee cT, Func<RealizedPowerAuraOptions, Action> next) {
        scale = opts.scale?.Invoke(gcx);
        color = opts.color;
        initialTime = opts.initialTime?.Invoke(gcx) ?? 0f;
        totalTime = opts.time?.Invoke(gcx) ?? 1f;
        iterations = opts.itrs?.Invoke(gcx) ?? 1f;
        sfx = opts.sfx;
        layer = opts.layer;
        this.cT = cT;
        
        if (opts.static_) {
            parent = null;
            offset = unparentedOffset;
        } else {
            parent = gcx.exec;
            offset = Vector2.zero;
        }

        if (opts.next != null) {
            //Note that you must operate over GCX now, since it may be destroyed after this function is exited.
            continuation = next(new RealizedPowerAuraOptions(opts.next, gcx, unparentedOffset, cT, next));
        } else
            continuation = null;
    }
}

public class PowerAuraOptions {
    public readonly GCXF<float>? scale;
    public readonly TP4 color = _ => Vector4.one;
    public readonly bool static_ = false;
    public readonly GCXF<float>? initialTime;
    public readonly GCXF<float>? time;
    public readonly GCXF<float>? itrs;
    public readonly string? sfx;
    public readonly int? layer = null;
    public readonly PowerAuraOptions? next;

    public PowerAuraOptions(IEnumerable<PowerAuraOption> props) {
        foreach (var prop in props.Unroll()) {
            if (prop is ScaleProp sp) {
                scale = sp.value;
            } else if (prop is StaticFlag) {
                static_ = true;
            } else if (prop is ColorProp cp) {
                color = cp.value;
            } else if (prop is InitialTimeProp itp) {
                initialTime = itp.value;
            }  else if (prop is TimeProp tp) {
                time = tp.value;
            } else if (prop is IterationsProp ip) {
                itrs = ip.value;
            } else if (prop is SFXProp sfxp) {
                sfx = sfxp.value;
            } else if (prop is LayerProp lp) {
                layer = lp.value.Int();
            } else if (prop is NextProp np) {
                next = new PowerAuraOptions(np.value);
            } else throw new Exception($"PowerAura option {prop.GetType()} not handled.");
        }
    }
}
}