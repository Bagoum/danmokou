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
    public static LaserOption Length(GCXF<float> maxLength) => new LengthProp(maxLength);
    /// <summary>
    /// Set the length, in time, of a laser. The length may be variable but bounded by a maximum.
    /// </summary>
    public static LaserOption VarLength(GCXF<float> maxLength, GCXU<BPY> length) => new LengthProp(maxLength, length);

    /// <summary>
    /// Set the time along the laser length at which the laser starts drawing.
    /// </summary>
    public static LaserOption Start(GCXU<BPY> time) => new StartProp(time);

    /// <summary>
    /// Every frame, the laser will check the condition and destroy itself if it is true.
    /// <br/>Note: This is generally only necessary for player lasers.
    /// <br/>Note: This is the same as BehOption.Delete.
    /// </summary>
    public static LaserOption Delete(GCXU<Pred> cond) => new DeleteProp(cond);

    /// <summary>
    /// Every frame, if the condition is true, sets lastActiveTime in private data hoisting to the current laser time (but only once).
    /// <br/>Note: This is probably unnecessary except for player bullets, which currently have limited support for controls such as updatef. 
    /// </summary>
    public static LaserOption Deactivate(GCXU<Pred> cond) => new DeactivateProp(cond);
    
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

    public static LaserOption Player(int cdFrames, int bossDmg, int stageDmg, string effect) =>
        new PlayerBulletProp(new PlayerBulletCfg(cdFrames, bossDmg, stageDmg, ResourceManager.GetEffect(effect)));
    
    #region impl
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
    public class LengthProp : ValueProp<(GCXF<float>, GCXU<BPY>?)> {
        public LengthProp(GCXF<float> f, GCXU<BPY>? var = null) : base((f, var)) { }
    }

    public class StartProp : ValueProp<GCXU<BPY>> {
        public StartProp(GCXU<BPY> f) : base(f) {}
    }

    public class DeleteProp : ValueProp<GCXU<Pred>> {
        public DeleteProp(GCXU<Pred> f) : base(f) { }
    }
    public class DeactivateProp : ValueProp<GCXU<Pred>> {
        public DeactivateProp(GCXU<Pred> f) : base(f) { }
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

    public class PlayerBulletProp : ValueProp<PlayerBulletCfg> {
        public PlayerBulletProp(PlayerBulletCfg cfg) : base(cfg) { }
    }
    
    #endregion
}

public readonly struct RealizedLaserOptions {
    private const float DEFAULT_LASER_LEN = 15;
    public const float DEFAULT_LASER_WIDTH = 0.5f;
    public readonly float maxLength;
    [CanBeNull] public readonly BPY varLength;
    [CanBeNull] public readonly BPY start;
    [CanBeNull] public readonly Pred delete;
    [CanBeNull] public readonly Pred deactivate;
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
    public readonly PlayerBulletCfg? playerBullet;

    public RealizedBehOptions AsBEH => new RealizedBehOptions(this);

    public RealizedLaserOptions(LaserOptions opts, GenCtx gcx, uint bpiid, Vector2 parentOffset, V2RV2 localOffset, ICancellee cT) {
        maxLength = opts.length?.max.Invoke(gcx) ?? DEFAULT_LASER_LEN;
        varLength = opts.length?.var?.Add(gcx, bpiid);
        start = opts.start?.Add(gcx, bpiid);
        delete = opts.delete?.Add(gcx, bpiid);
        deactivate = opts.deactivate?.Add(gcx, bpiid);
        repeat = opts.repeat?.Invoke(gcx) ?? false;
        endpoint = opts.endpoint;
        firesfx = opts.firesfx;
        hotsfx = opts.hotsfx;
        layer = opts.layer;
        staggerMultiplier = opts.staggerMultiplier;
        if (opts.curve != null) {
            lpath = new LaserVelocity(opts.curve.Value.Add(gcx, bpiid), parentOffset, localOffset);
            isStatic = !opts.dynamic;
        } else {
            lpath = new LaserVelocity(localOffset.angle + (opts.rotateOffset?.Invoke(gcx) ?? 0f), opts.rotate?.Add?.Invoke(gcx, bpiid));
            isStatic = true;
        }
        smr = SMRunner.Run(opts.sm, cT, gcx);
        yScale = (opts.yScale?.Invoke(gcx) ?? 1f) * DEFAULT_LASER_WIDTH;
        hueShift = opts.hueShift?.Invoke(gcx) ?? 0f;
        playerBullet = opts.playerBullet;
    }
}

public class LaserOptions {
    public readonly (GCXF<float> max, GCXU<BPY>? var)? length;
    public readonly GCXU<BPY>? start;
    public readonly GCXU<Pred>? delete;
    public readonly GCXU<Pred>? deactivate;
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
    public readonly PlayerBulletCfg? playerBullet;

    public LaserOptions(params LaserOption[] props) : this(props as IEnumerable<LaserOption>) { }

    public LaserOptions(IEnumerable<LaserOption> props) {
        foreach (var prop in props.Unroll()) {
            if (prop is LengthProp l) length = l.value;
            else if (prop is StartProp stp) start = stp.value;
            else if (prop is DeleteProp dp) delete = dp.value;
            else if (prop is DeactivateProp dcp) deactivate = dcp.value;
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
            else if (prop is PlayerBulletProp pbp) playerBullet = pbp.value;
            else throw new Exception($"Laser property {prop.GetType()} not handled.");
        }
        if (length?.var != null || start != null) {
            if (!dynamic) throw new Exception("Variable length or variable start lasers must be used with DYNAMIC.");
        }
        if (curve != null) {
            if (rotate != null || rotateOffset != null)
                throw new Exception("Lasers cannot have curves and rotation simultaneously.");
        }
    }
}
}