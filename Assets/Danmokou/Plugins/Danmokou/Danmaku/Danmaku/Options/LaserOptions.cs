using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Services;
using JetBrains.Annotations;
using Danmokou.SM;
using UnityEngine;
using static Danmokou.Danmaku.Options.LaserOption;

namespace Danmokou.Danmaku.Options {

/// <summary>
/// Properties that modify the behavior of lasers.
/// </summary>
[Reflect]
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
    /// Every frame, if the condition is true, sets LastActiveTime in private data hoisting to the current laser time (but only once).
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
    
    /// <summary>
    /// Provide a function that indicates how much to shift the color of the summon (in degrees) at any point in time.
    /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
    /// </summary>
    public static LaserOption HueShift(GCXU<BPY> shift) => new HueShiftProp(shift);

    /// <summary>
    /// Manually construct a two-color gradient for the object.
    /// <br/> Note: This will only have effect if you use it with the `recolor` palette.
    /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
    /// </summary>
    public static LaserOption Recolor(GCXU<TP4> black, GCXU<TP4> white) => new RecolorProp(black, white);
    
    /// <summary>
    /// Tint the laser. This is a multiplicative effect on its normal color.
    /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
    /// </summary>
    public static LaserOption Tint(GCXU<TP4> tint) => new TintProp(tint);

    /// <summary>
    /// Player bullets only.
    /// </summary>
    public static LaserOption Nonpiercing() => new NonpiercingFlag();
    
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
        public readonly string? onFire;
        public readonly string? onOn;
        public SfxProp(string? onFire, string? onOn) {
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
    public class HueShiftProp : ValueProp<GCXU<BPY>> {
        public HueShiftProp(GCXU<BPY> v) : base(v) { }
    }
    public class RecolorProp : LaserOption {
        public readonly GCXU<TP4> black;
        public readonly GCXU<TP4> white;

        public RecolorProp(GCXU<TP4> b, GCXU<TP4> w) {
            black = b;
            white = w;
        }
    }
    public class TintProp : ValueProp<GCXU<TP4>> {
        public TintProp(GCXU<TP4> v) : base(v) { }
    }

    public class PlayerBulletProp : ValueProp<PlayerBulletCfg> {
        public PlayerBulletProp(PlayerBulletCfg cfg) : base(cfg) { }
    }
    
    public class NonpiercingFlag : LaserOption { }
    
    #endregion
}

public readonly struct RealizedLaserOptions {
    private const float DEFAULT_LASER_LEN = 15;
    public readonly float maxLength;
    public readonly BPY? varLength;
    public readonly BPY? start;
    public readonly Pred? delete;
    public readonly Pred? deactivate;
    public readonly bool repeat;
    public readonly string? endpoint;
    public readonly string? firesfx;
    public readonly string? hotsfx;
    public readonly LaserMovement lpath;
    public readonly bool isStatic;
    public readonly SMRunner smr;
    public readonly float yScale;
    public readonly int? layer;
    public readonly float staggerMultiplier;
    public readonly BPY? hueShift;
    public readonly (TP4 black, TP4 white)? recolor;
    public readonly TP4? tint;
    public readonly bool nonpiercing;
    public readonly PlayerBullet? playerBullet;

    public RealizedBehOptions AsBEH => new RealizedBehOptions(this);

    public RealizedLaserOptions(LaserOptions opts, GenCtx gcx, FiringCtx fctx, Vector2 parentOffset, V2RV2 localOffset, ICancellee cT) {
        maxLength = opts.length?.max.Invoke(gcx) ?? DEFAULT_LASER_LEN;
        varLength = opts.length?.var?.Invoke(gcx, fctx);
        start = opts.start?.Invoke(gcx, fctx);
        delete = opts.delete?.Invoke(gcx, fctx);
        deactivate = opts.deactivate?.Invoke(gcx, fctx);
        repeat = opts.repeat?.Invoke(gcx) ?? false;
        endpoint = opts.endpoint;
        firesfx = opts.firesfx;
        hotsfx = opts.hotsfx;
        layer = opts.layer;
        staggerMultiplier = opts.staggerMultiplier;
        if (opts.curve != null) {
            lpath = new LaserMovement(opts.curve(gcx, fctx), parentOffset, localOffset);
            isStatic = !opts.dynamic;
        } else {
            lpath = new LaserMovement(localOffset.angle + (opts.rotateOffset?.Invoke(gcx) ?? 0f), opts.rotate?.Invoke(gcx, fctx));
            isStatic = true;
        }
        smr = SMRunner.Run(opts.sm, cT, gcx);
        yScale = opts.yScale?.Invoke(gcx) ?? 1f;
        hueShift = opts.hueShift?.Invoke(gcx, fctx);
        if (opts.recolor.Try(out var rc)) {
            recolor = (rc.black(gcx, fctx), rc.white(gcx, fctx));
        } else recolor = null;
        tint = opts.tint?.Invoke(gcx, fctx);
        nonpiercing = opts.nonpiercing;
        playerBullet = opts.playerBullet?.Realize(fctx.PlayerController);
    }
}

public class LaserOptions {
    public readonly (GCXF<float> max, GCXU<BPY>? var)? length;
    public readonly GCXU<BPY>? start;
    public readonly GCXU<Pred>? delete;
    public readonly GCXU<Pred>? deactivate;
    public readonly GCXF<bool>? repeat;
    public readonly string? endpoint;
    public readonly string? firesfx;
    public readonly string? hotsfx;
    public readonly bool dynamic;
    public readonly GCXU<LVTP>? curve = null;
    public readonly GCXF<float>? rotateOffset;
    public readonly GCXU<BPY>? rotate = null;
    public readonly StateMachine? sm;
    public readonly GCXF<float>? yScale;
    public readonly int? layer = null;
    public readonly float staggerMultiplier = 1f;
    public readonly GCXU<BPY>? hueShift;
    public readonly (GCXU<TP4> black, GCXU<TP4> white)? recolor;
    public readonly GCXU<TP4>? tint;
    public readonly bool nonpiercing;
    public readonly PlayerBulletCfg? playerBullet;

    public LaserOptions(params LaserOption[] props) : this(props as IEnumerable<LaserOption>) { }

    public LaserOptions(IEnumerable<LaserOption> props) {
        foreach (var p in props.Unroll()) {
            if      (p is LengthProp l) 
                length = l.value;
            else if (p is StartProp stp) 
                start = stp.value;
            else if (p is DeleteProp dp) 
                delete = dp.value;
            else if (p is DeactivateProp dcp) 
                deactivate = dcp.value;
            else if (p is RepeatProp r) 
                repeat = r.value;
            else if (p is RotateOffsetProp roff) 
                rotateOffset = roff.value;
            else if (p is RotateProp rotp) 
                rotate = rotp.value;
            else if (p is CurveProp cur) {
                dynamic = cur.dynamic;
                curve = cur.curve;
            } else if (p is EndpointProp ep) 
                endpoint = ep.value;
            else if (p is SfxProp hsp) {
                firesfx = hsp.onFire;
                hotsfx = hsp.onOn;
            }
            else if (p is SMProp smp) 
                sm = smp.value;
            else if (p is YScaleProp yp) 
                yScale = yp.value;
            else if (p is LayerProp lp) 
                layer = lp.value.Int();
            else if (p is StaggerProp sp) 
                staggerMultiplier = sp.value;
            else if (p is HueShiftProp hshp)
                hueShift = hshp.value;
            else if (p is RecolorProp rcp) 
                recolor = (rcp.black, rcp.white);
            else if (p is TintProp tp) 
                tint = tp.value;
            else if (p is NonpiercingFlag)
                nonpiercing = true;
            else if (p is PlayerBulletProp pbp) 
                playerBullet = pbp.value;
            else throw new Exception($"Laser property {p.GetType()} not handled.");
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