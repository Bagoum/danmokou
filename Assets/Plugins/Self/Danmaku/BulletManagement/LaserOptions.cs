using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DMath;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using static Danmaku.Enums;
using static Danmaku.LaserOption;

namespace Danmaku {

/// <summary>
/// Properties that modify the behavior of lasers.
/// </summary>
public class LaserOption {
    /// <summary>
    /// Set the length, in time, of a laser.
    /// </summary>
    public static LaserOption Length(GCXF<float> len) => new LengthProp(len);
    /// <summary>
    /// Set a laser to repeat.
    /// </summary>
    /// <returns></returns>
    public static LaserOption Repeat() => new RepeatProp(_ => true);
    /// <summary>
    /// Set a laser to repeat iff the function is true.
    /// </summary>
    /// <returns></returns>
    public static LaserOption RepeatF(GCXF<bool> func) => new RepeatProp(func);
    /// <summary>
    /// Draw a straight laser at an angle.
    /// </summary>
    /// <param name="rotOffset">Angle in degrees</param>
    /// <returns></returns>
    public static LaserOption Straight(GCXF<float> rotOffset) => new RotateOffsetProp(rotOffset);
    /// <summary>
    /// Draw a straight laser that rotates.
    /// </summary>
    /// <param name="rotOffset">Initial angle in degrees</param>
    /// <param name="rot">Additional rotation as a function of time in degrees</param>
    /// <returns></returns>
    public static LaserOption Rotate(GCXF<float> rotOffset, GCXU<BPY> rot) => new CompositeProp(new RotateOffsetProp(rotOffset), new RotateProp(rot));
    /// <summary>
    /// Draw a curved laser that does not update.
    /// </summary>
    public static LaserOption Static(GCXU<LVTP> path) => new CurveProp(false, path);
    /// <summary>
    /// Draw a curved laser that does update.
    /// </summary>
    public static LaserOption Dynamic(GCXU<LVTP> path) => new CurveProp(true, path);
    /// <summary>
    /// Draw a laser with an endpoint BEH.
    /// </summary>
    public static LaserOption Endpoint(string behid) => new EndpointProp(behid);
    /// <summary>
    /// Run a StateMachine on a laser.
    /// </summary>
    public static LaserOption SM(StateMachine sm) => new SMProp(sm);
    /// <summary>
    /// Play a sound effect when the laser turns on.
    /// </summary>
    public static LaserOption SFX(string sfx) => new SfxProp(null, sfx);
    /// <summary>
    /// Play a sound effect when the laser is fired and when the laser is turned on.
    /// </summary>
    public static LaserOption SFX2(string onFire, string onOn) => new SfxProp(onFire, onOn);

    /// <summary>
    /// Play the default sound effects (sfx2 x-laser-fire x-laser-on).
    /// </summary>
    /// <returns></returns>
    public static LaserOption dSFX() => SFX2("x-laser-fire", "x-laser-on");
    /// <summary>
    /// Set the width of the laser as a multiplier. The base size is a thickness of 0.2 screen units.
    /// </summary>
    public static LaserOption S(GCXF<float> scale) => new YScaleProp(scale);

    public static LaserOption Low() => new LayerProp(Layer.LowProjectile);
    public static LaserOption High() => new LayerProp(Layer.HighProjectile);

    public static LaserOption Stagger(float mult) => new StaggerProp(mult);
    
    public static LaserOption HueShift(GCXF<float> dps) => new HueShiftProp(dps);
    
    public class CompositeProp : ValueProp<LaserOption[]>, IUnrollable<LaserOption> {
        public IEnumerable<LaserOption> Values => value;
        public CompositeProp(params LaserOption[] props) : base(props) { }
    }
    
    public class ValueProp<T> : LaserOption {
        public readonly T value;
        public ValueProp(T value) => this.value = value;
    }

    public class LayerProp : ValueProp<Layer> {
        public LayerProp(Layer l) : base(l) { }
    }
    public class EndpointProp : ValueProp<string> {
        public EndpointProp(string f) : base(f) { }
    }
    public class SfxProp : LaserOption {
        [CanBeNull] public readonly string onFire;
        [CanBeNull] public readonly string onOn;
        public SfxProp(string onFire, string onOn) {
            this.onFire = onFire;
            this.onOn = onOn;
        }
    }
    public class LengthProp : ValueProp<GCXF<float>> {
        public LengthProp(GCXF<float> f) : base(f) { }
    }
    public class RotateOffsetProp : ValueProp<GCXF<float>> {
        public RotateOffsetProp(GCXF<float> f) : base(f) { }
    }
    public class RotateProp : ValueProp<GCXU<BPY>> {
        public RotateProp(GCXU<BPY> f) : base(f) { }
    }

    public class CurveProp : LaserOption {
        public readonly GCXU<LVTP> curve;
        public readonly bool dynamic;

        public CurveProp(bool dynamic, GCXU<LVTP> curve) {
            this.curve = curve;
            this.dynamic = dynamic;
        }
    }
    public class RepeatProp : ValueProp<GCXF<bool>> {
        public RepeatProp(GCXF<bool> f) : base(f) { }
    }
    public class SMProp : ValueProp<StateMachine> {
        public SMProp(StateMachine f) : base(f) { }
    }

    public class YScaleProp : ValueProp<GCXF<float>> {
        public YScaleProp(GCXF<float> f) : base(f) { }
    }

    public class StaggerProp : ValueProp<float> {
        public StaggerProp(float v) : base(v) { }
    }
    public class HueShiftProp : ValueProp<GCXF<float>> {
        public HueShiftProp(GCXF<float> v) : base(v) { }
    }
}

public readonly struct RealizedLaserOptions {
    private const float DEFAULT_LASER_LEN = 15;
    public const float DEFAULT_LASER_WIDTH = 0.5f;
    public readonly float length;
    public readonly bool repeat;
    [CanBeNull] public readonly string endpoint;
    [CanBeNull] public readonly string firesfx;
    [CanBeNull] public readonly string hotsfx;
    public readonly LaserVelocity lpath;
    public readonly bool isStatic;
    public readonly SMRunner smr;
    public readonly float yScale;
    public readonly int? layer;
    public readonly float staggerMultiplier;
    public readonly float hueShift;

    public RealizedBehOptions AsBEH => new RealizedBehOptions(smr, layer);

    public RealizedLaserOptions(LaserOptions opts, GenCtx gcx, uint bpiid, Vector2 parentOffset, V2RV2 localOffset, MovementModifiers modifiers, CancellationToken cT) {
        length = opts.length?.Invoke(gcx) ?? DEFAULT_LASER_LEN;
        repeat = opts.repeat?.Invoke(gcx) ?? false;
        endpoint = opts.endpoint;
        firesfx = opts.firesfx;
        hotsfx = opts.hotsfx;
        layer = opts.layer;
        staggerMultiplier = opts.staggerMultiplier;
        if (opts.curve != null) {
            lpath = new LaserVelocity(opts.curve.Value.Add(gcx, bpiid), parentOffset, localOffset, modifiers);
            isStatic = !opts.dynamic;
        } else {
            lpath = new LaserVelocity(localOffset.angle + (opts.rotateOffset?.Invoke(gcx) ?? 0f), opts.rotate?.Add?.Invoke(gcx, bpiid), modifiers);
            isStatic = true;
        }
        smr = SMRunner.Run(opts.sm, cT);
        yScale = (opts.yScale?.Invoke(gcx) ?? 1f) * DEFAULT_LASER_WIDTH;
        hueShift = opts.hueShift?.Invoke(gcx) ?? 0f;
    }
}

public class LaserOptions {
    [CanBeNull] public readonly GCXF<float> length;
    [CanBeNull] public readonly GCXF<bool> repeat;
    [CanBeNull] public readonly string endpoint;
    [CanBeNull] public readonly string firesfx;
    [CanBeNull] public readonly string hotsfx;
    public readonly bool dynamic;
    public readonly GCXU<LVTP>? curve = null;
    [CanBeNull] public readonly GCXF<float> rotateOffset;
    public readonly GCXU<BPY>? rotate = null;
    [CanBeNull] public readonly StateMachine sm;
    [CanBeNull] public readonly GCXF<float> yScale;
    public readonly int? layer = null;
    public readonly float staggerMultiplier = 1f;
    [CanBeNull] public readonly GCXF<float> hueShift;

    public LaserOptions(params LaserOption[] props) : this(props as IEnumerable<LaserOption>) { }

    public LaserOptions(IEnumerable<LaserOption> props) {
        foreach (var prop in props.Unroll()) {
            if (prop is LengthProp l) length = l.value;
            else if (prop is RepeatProp r) repeat = r.value;
            else if (prop is RotateOffsetProp roff) rotateOffset = roff.value;
            else if (prop is RotateProp rotp) rotate = rotp.value;
            else if (prop is CurveProp cur) {
                dynamic = cur.dynamic;
                curve = cur.curve;
            } else if (prop is EndpointProp ep) endpoint = ep.value;
            else if (prop is SfxProp hsp) {
                firesfx = hsp.onFire;
                hotsfx = hsp.onOn;
            }
            else if (prop is SMProp smp) sm = smp.value;
            else if (prop is YScaleProp yp) yScale = yp.value;
            else if (prop is LayerProp lp) layer = lp.value.Int();
            else if (prop is StaggerProp sp) staggerMultiplier = sp.value;
            else if (prop is HueShiftProp hshp) hueShift = hshp.value;
            else throw new Exception($"Laser property {prop.GetType()} not handled.");
        }
        if (curve != null) {
            if (rotate != null || rotateOffset != null)
                throw new Exception("Lasers cannot have curves and rotation simultaneously.");
        }
    }
}
}