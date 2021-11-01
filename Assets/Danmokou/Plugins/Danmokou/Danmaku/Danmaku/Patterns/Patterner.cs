using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.Danmaku.Options;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Pooling;
using Danmokou.Reflection;
using Danmokou.Services;
using Danmokou.SM;
using JetBrains.Annotations;
using UnityEngine;
using GCP = Danmokou.Danmaku.Options.GenCtxProperty;
using static Danmokou.Reflection.Compilers;
using static Danmokou.Expressions.ExMHelpers;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.DMath.Functions.ExMRV2;
using Ex = System.Linq.Expressions.Expression;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using ExBPRV2 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>>;
using static BagoumLib.Tasks.WaitingUtils;

namespace Danmokou.Danmaku.Patterns {
public delegate void SyncPattern(SyncHandoff sbh);

public delegate IEnumerator AsyncPattern(AsyncHandoff abh);


public struct CommonHandoff : IDisposable {
    public readonly ICancellee cT;
    public DelegatedCreator bc;
    public readonly GenCtx gcx;

    /// <summary>
    /// GCX is automatically copied.
    /// </summary>
    public CommonHandoff(ICancellee cT, DelegatedCreator? bc, GenCtx gcx) {
        this.cT = cT;
        this.bc = bc ?? new DelegatedCreator(gcx.exec, "");
        this.gcx = gcx.Copy();
    }

    public readonly CommonHandoff Copy(string? newStyle = null) {
        var nch = new CommonHandoff(cT, bc, gcx);
        if (newStyle != null)
            nch.bc.style = newStyle;
        return nch;
    }

    public readonly void Dispose() {
        gcx.Dispose();
    }
}

/// <summary>
/// A struct containing information for SyncPattern execution.
/// <br/>The caller is responsible for disposing this after it is done.
/// </summary>
public struct SyncHandoff : IDisposable {
    public CommonHandoff ch;
    public DelegatedCreator bc => ch.bc;
    public V2RV2 RV2 => GCX.RV2;
    public int index => GCX.index;
    /// <summary>
    /// Starting time of summoned objects (seconds)
    /// </summary>
    public readonly float timeOffset;
    public GenCtx GCX => ch.gcx;

    /// <summary>
    /// The common handoff is copied from SMH.
    /// </summary>
    public SyncHandoff(DelegatedCreator bc, SMHandoff smh) {
        this.ch = new CommonHandoff(smh.cT, bc, smh.GCX);
        this.timeOffset = 0f;
    }

    /// <summary>
    /// The common handoff is copied.
    /// </summary>
    public SyncHandoff(CommonHandoff ch, float extraTimeSeconds, string? newStyle = null) {
        this.ch = ch.Copy(newStyle);
        this.timeOffset = extraTimeSeconds;
    }

    public static implicit operator GenCtx(SyncHandoff sbh) => sbh.GCX;

    public SyncHandoff Copy(string? newStyle) => new SyncHandoff(ch, timeOffset, newStyle);

    public readonly void Dispose() {
        ch.Dispose();
    }

}

/// <summary>
/// A struct containing information about the execution of an AsyncPattern.
/// <br/>This struct cleans up its own resources when the callee calls its Done() method to mark completion.
/// </summary>
public struct AsyncHandoff {
    public CommonHandoff ch;
    public bool Cancelled => ch.cT.Cancelled;
    private readonly Action? callback;
    private readonly BehaviorEntity exec;

    /// <summary>
    /// The common handoff is copied from SMHandoff.
    /// </summary>
    public AsyncHandoff(DelegatedCreator bc, Action? callback, SMHandoff smh) {
        this.ch = new CommonHandoff(smh.ch.cT, bc, smh.GCX);
        this.callback = callback;
        exec = smh.Exec;
    }

    /// <summary>
    /// The common handoff is copied from SyncHandoff.
    /// </summary>
    public AsyncHandoff(SyncHandoff sbh) {
        this.ch = new CommonHandoff(sbh.ch.cT, sbh.bc, sbh.GCX);
        this.callback = null;
        exec = sbh.GCX.exec;
    }

    /// <summary>
    /// Derive an AsyncHandoff from a parent for localized execution. The common handoff is copied.
    /// </summary>
    public AsyncHandoff(AsyncHandoff parent, CommonHandoff ch, Action? callback) {
        this.ch = ch.Copy();
        this.callback = callback;
        exec = parent.exec;
    }

    //public void RunRIEnumerator(IEnumerator cor) => exec.RunRIEnumerator(cor);
    public void RunPrependRIEnumerator(IEnumerator cor) => exec.RunPrependRIEnumerator(cor);

    /// <summary>
    /// Send completion information via the callback and also dispose the copied handoff.
    /// </summary>
    public readonly void Done() {
        ch.Dispose();
        callback?.Invoke();
    }
}

/// <summary>
/// Functions that describe atomic synchronous actions.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[Reflect]
public static partial class AtomicPatterns {
    /// <summary>
    /// Do nothing.
    /// </summary>
    /// <returns></returns>
    public static SyncPattern Noop() => sbh => {  };
    /// <summary>
    /// Play the audio clip defined by the style.
    /// </summary>
    /// <returns></returns>
    public static SyncPattern SFX() => sbh => sbh.bc.SFX();

    
    /// <summary>
    /// Invoke the provided event with the provided value.
    /// </summary>
    [GAlias(typeof(float), "eventf")]
    public static SyncPattern Event<T>(string evName,  GCXF<T> value) => sbh => 
        Events.ProcRuntimeEvent(evName, value(sbh.GCX));

    /// <summary>
    /// Invoke one of the provided unit events according to the firing index.
    /// </summary>
    public static SyncPattern Event0(string evName) => Event<Unit>(evName, _ => default);

    #region Items

    public static SyncPattern LifeItem() => sbh => 
        ItemPooler.RequestLife(new ItemRequestContext(sbh.bc.ParentOffset, sbh.GCX.RV2.TrueLocation));
    
    public static SyncPattern ValueItem() => sbh => 
        ItemPooler.RequestValue(new ItemRequestContext(sbh.bc.ParentOffset, sbh.GCX.RV2.TrueLocation));
    
    public static SyncPattern SmallValueItem() => sbh => 
        ItemPooler.RequestSmallValue(new ItemRequestContext(sbh.bc.ParentOffset, sbh.GCX.RV2.TrueLocation));
    
    public static SyncPattern PointPPItem() => sbh => 
        ItemPooler.RequestPointPP(new ItemRequestContext(sbh.bc.ParentOffset, sbh.GCX.RV2.TrueLocation));
    
    public static SyncPattern GemItem() => sbh => 
        ItemPooler.RequestGem(new ItemRequestContext(sbh.bc.ParentOffset, sbh.GCX.RV2.TrueLocation));
    
    [Alias("1UpItem")]
    public static SyncPattern OneUpItem() => sbh => 
        ItemPooler.Request1UP(new ItemRequestContext(sbh.bc.ParentOffset, sbh.GCX.RV2.TrueLocation));
    
    public static SyncPattern PowerupShiftItem() => sbh => 
        ItemPooler.RequestPowerupShift(new ItemRequestContext(sbh.bc.ParentOffset, sbh.GCX.RV2.TrueLocation));
    
    #endregion
    
    #region SimpleBullets

    /// <summary>
    /// Fires a simple bullet.
    /// </summary>
    /// <param name="path">Movement descriptor</param>
    /// <returns></returns>
    [Alias("simp")]
    public static SyncPattern S(GCXU<VTP> path) => Simple(path, new SBOptions(new SBOption[0]));

    /// <summary>
    /// Fires a simple bullet. Takes an array of simple bullet options as modifiers.
    /// See BulletManagement/SimpleBulletOptions.cs.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static SyncPattern Simple(GCXU<VTP> path, SBOptions options) => sbh => {
        uint id = sbh.GCX.NextID();
        if (options.player.HasValue) {
            sbh.ch.bc.style = BulletManager.GetOrMakePlayerCopy(sbh.bc.style);
        }
        sbh.bc.Simple(sbh, options, path, id);
    };

    #endregion


    /// <summary>
    /// Fires a complex bullet (ie. controlled by a GameObject).
    /// Do not use for pathers/lasers (use the Pather or Laser functions).
    /// </summary>
    /// <param name="path">Movement descriptor</param>
    /// <param name="options">Bullet constructor options</param>
    /// <returns></returns>
    public static SyncPattern Complex(GCXU<VTP> path, BehOptions options) => sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.Complex(sbh, path, id, options);
    };
    
    /// <summary>
    /// Fires a Pather/Tracker projectile, which "remembers" the points it has gone through and draws a path through them.
    /// </summary>
    /// <param name="maxTime">Maximum remember time. Only enough space to handle this much time will be allocated.</param>
    /// <param name="remember">The current remember time as a function of the current life-time</param>
    /// <param name="path">Movement descriptor</param>
    /// <param name="options">Bullet constructor options</param>
    /// <returns></returns>
    public static SyncPattern Pather(float maxTime, BPY remember, GCXU<VTP> path, BehOptions options) => sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.Pather(sbh, maxTime > 0 ? maxTime : (float?) null, remember, path, id, options);
    };

    /// <summary>
    /// Create a laser.
    /// </summary>
    /// <param name="path">Movement path for the base point of the laser</param>
    /// <param name="cold">Time that the laser is in a non-damaging state</param>
    /// <param name="hot">Time that the laser is in a damaging state</param>
    /// <param name="options">Laser constructor options</param>
    /// <returns></returns>
    public static SyncPattern Laser(GCXU<VTP> path, GCXF<float> cold, GCXF<float> hot, LaserOptions options) => sbh => {
            uint id = sbh.GCX.NextID();
            sbh.bc.Laser(sbh, path, cold(sbh.GCX), hot(sbh.GCX), id, options);
        };

    public static SyncPattern SafeLaser(GCXF<float> cold, LaserOptions options) =>
        Laser("null".Into<GCXU<VTP>>(), cold, _ => 0f, options); 
    
    public static SyncPattern SafeLaserM(GCXU<VTP> path, GCXF<float> cold, LaserOptions options) =>
        Laser(path, cold, _ => 0f, options); 

    public static SyncPattern SummonS(GCXU<VTP> path, StateMachine? sm) =>
        Summon(path, sm, new BehOptions());
    public static SyncPattern SummonSUP(GCXU<VTP> path, StateMachine? sm) =>
        SummonUP(path, sm, new BehOptions());
    public static SyncPattern Inode(GCXU<VTP> path, StateMachine? sm) {
        var f = SummonS(path, sm);
        return sbh => {
            sbh.ch.bc.style = "inode";
            f(sbh);
        };
    }

    private static SyncHandoff _Summon(SyncHandoff sbh, bool pool, GCXU<VTP> path, StateMachine? sm, BehOptions options) {
        uint id = sbh.GCX.NextID();
        sbh.bc.Summon(pool, sbh, options, path, SMRunner.Cull(sm, sbh.ch.cT, sbh.GCX), id);
        return sbh;
    }

    public static SyncPattern Summon(GCXU<VTP> path, StateMachine? sm, BehOptions options) => sbh =>
        _Summon(sbh, true, path, sm, options);
    public static SyncPattern SummonUP(GCXU<VTP> path, StateMachine? sm, BehOptions options) => sbh =>
        _Summon(sbh, false, path, sm, options);

    public static SyncPattern SummonR(RootedVTP path, StateMachine? sm, BehOptions options) => sbh => {
        sbh.ch.bc.Root(path.root(sbh.GCX));
        _Summon(sbh, true, path.path, sm, options);
    };
    public static SyncPattern SummonRUP(RootedVTP path, StateMachine? sm, BehOptions options) => sbh => {
        sbh.ch.bc.Root(path.root(sbh.GCX));
        _Summon(sbh, false, path.path, sm, options);
    };

    public static SyncPattern SummonRZ(StateMachine? sm, BehOptions options) =>
        SummonR(new RootedVTP(0, 0, VTPRepo.Null()), sm, options);

    private static BPRV2 DrawerLoc(SyncHandoff sbh, BPRV2 locScaleAngle, TP? offset = null) {
        var summonLoc = sbh.bc.FacedRV2(sbh.RV2) + ((offset == null) ? sbh.bc.ParentOffset : Vector2.zero);
        return bpi => {
            var offsetLoc = summonLoc + (offset?.Invoke(bpi) ?? Vector2.zero);
            var lsa = locScaleAngle(bpi);
            return new V2RV2(
                (offsetLoc + new Vector3(lsa.nx, lsa.ny, 0)).TrueLocation, //locScaleAng offset is rotational
                lsa.RV,
                lsa.angle + summonLoc.angle
            );
        };
    }

    private static SMRunner WaitForPhase(ICancellee cT) => SMRunner.Cull(Reflector.WaitForPhaseSM, cT);

    public static SyncPattern Circ(TP4 color, BPRV2 locScaleAngle) => sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonCirc(sbh, "_", color, DrawerLoc(sbh, locScaleAngle), WaitForPhase(sbh.ch.cT), id);
    };
    public static SyncPattern gRelCirc(string behId, TP loc, BPRV2 locScaleAngle, TP4 color) => sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonCirc(sbh, behId, color, DrawerLoc(sbh, locScaleAngle, loc), WaitForPhase(sbh.ch.cT), id);
    };

    public static SyncPattern RelCirc(string behId, BEHPointer beh, BPRV2 locScaleAngle, TP4 color) =>
        gRelCirc(behId, _ => beh.Loc, locScaleAngle, color);
    public static SyncPattern Rect(TP4 color, BPRV2 locScaleAngle) => sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonRect(sbh, "_", color, DrawerLoc(sbh, locScaleAngle), WaitForPhase(sbh.ch.cT), id);
    };
    public static SyncPattern gRelRect(string behId, TP loc, BPRV2 locScaleAngle, TP4 color) => sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonRect(sbh, behId, color, DrawerLoc(sbh, locScaleAngle, loc), WaitForPhase(sbh.ch.cT), id);
    };

    public static SyncPattern RelRect(string behId, BEHPointer beh, BPRV2 locScaleAngle, TP4 color) =>
        gRelRect(behId, _ => beh.Loc, locScaleAngle, color);

    
    public static SyncPattern Darkness(TP loc, BPY radius, TP4 color) => sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonDarkness(sbh, "_", loc, radius, color, WaitForPhase(sbh.ch.cT), id);
    };

    public static SyncPattern PowerAura(PowerAuraOptions options) =>
        sbh => sbh.bc.SummonPowerAura(sbh, options, sbh.GCX.NextID());
    
    /// <summary>
    /// 
    /// Create a powerup effect.
    /// These effects are parented directly under the BEH they are attached to. Offsets, etc do not apply. 
    /// </summary>
    /// <param name="sfx">Sound effect to play when the powerup starts</param>
    /// <param name="color">Color multiplier function</param>
    /// <param name="time">Time the powerup exists</param>
    /// <param name="itrs">Number of cycles the powerup goes through</param>
    /// <returns></returns>
    [Obsolete("Use PowerAura instead.")]
    public static SyncPattern Powerup(string sfx, TP4 color, GCXF<float> time, GCXF<float> itrs) {
        var props = new PowerAuraOptions(new[] {
            PowerAuraOption.Color(color),
            PowerAuraOption.Time(time),
            PowerAuraOption.Iterations(itrs),
            PowerAuraOption.SFX(sfx),

        });
        return sbh => sbh.bc.SummonPowerAura(sbh, props, sbh.GCX.NextID());
    }

    /// <summary>
    /// Create a powerup effect, using the V2RV2 offset to position.
    /// </summary>
    /// <param name="sfx">Sound effect to play when the powerup starts</param>
    /// <param name="color">Color multiplier function</param>
    /// <param name="time">Time the powerup exists</param>
    /// <param name="itrs">Number of cycles the powerup goes through</param>
    /// <returns></returns>
    [Obsolete("Use PowerAura instead.")]
    public static SyncPattern PowerupStatic(string sfx, TP4 color, GCXF<float> time, GCXF<float> itrs) {
        var props = new PowerAuraOptions(new[] {
            PowerAuraOption.Color(color),
            PowerAuraOption.Time(time),
            PowerAuraOption.Iterations(itrs),
            PowerAuraOption.SFX(sfx),
            PowerAuraOption.Static(), 

        });
        return sbh => sbh.bc.SummonPowerAura(sbh, props, sbh.GCX.NextID());
    }

    /// <summary>
    /// Create a powerup effect twice.
    /// <br/>The second time, it goes outwards with one iteration.
    /// <br/>This abbreviation is useful for common use cases of powerups.
    /// </summary>
    /// <param name="sfx1">SFX to play when the first powerup starts</param>
    /// <param name="sfx2">SFX to play when the second powerup starts</param>
    /// <param name="color1">Color function for the first powerup</param>
    /// <param name="color2">Color function for the second powerup</param>
    /// <param name="time1">Time the first powerup exists</param>
    /// <param name="itrs1">Number of cycles the first powerup goes through</param>
    /// <param name="delay">Delay after the first powerup dies before spawning the second powerup</param>
    /// <param name="time2">Time the second powerup exists</param>
    /// <returns></returns>
    [Obsolete("Use PowerAura instead.")]
    public static SyncPattern Powerup2(string sfx1, string sfx2, TP4 color1, TP4 color2, GCXF<float> time1, GCXF<float> itrs1, GCXF<float> delay, GCXF<float> time2) {
        var power1 = Powerup(sfx1, color1, time1, itrs1);
        var power2 = Powerup(sfx2, color2, time2, _ => -1);
        return sbh => {
            power1(sbh);
            float t1 = time1(sbh.GCX);
            float wait = t1 + delay(sbh.GCX);
            SyncPatterns.Reexec(AsyncPatterns._AsGCR(
                power2,
                GenCtxProperty.Delay(_ => ETime.ENGINEFPS_F * wait))
            )(sbh);
        };
    }
}
public struct LoopControl<T> {
    public readonly GenCtxProperties<T> props;
    private readonly int parent_index;
    private readonly string parent_style;
    public readonly int times;
    private readonly Parametrization p;
    private CommonHandoff ch;
    public CommonHandoff Handoff => ch;
    public GenCtx GCX => ch.gcx;
    public LoopControl(GenCtxProperties<T> props, CommonHandoff _ch, out bool isClipped) {
        isClipped = false;
        this.props = props;
        p = props.p;
        ch = _ch.Copy();
        parent_index = (props.p_mutater == null) ? ch.gcx.index : (int)props.p_mutater(ch.gcx);
        if (props.resetColor) ch.bc.style = "_";
        if (props.bank != null) {
            var (toZero, banker) = props.bank.Value;
            ch.gcx.RV2 = ch.gcx.RV2.Bank(toZero ? (float?)0f : null) + banker(ch.gcx);
        }
        if (props.forceRoot != null) {
            Vector2 newRoot = props.forceRoot(ch.gcx);
            Vector2 offsetBy = (props.forceRootAdjust) ? 
                ch.bc.ParentOffset - newRoot
                : Vector2.zero;
            ch.gcx.RV2 += offsetBy;
            ch.bc.Root(newRoot);
        } else if (props.laserIndexer != null) {
            var l = (ch.gcx.exec as Laser) ??
                    throw new Exception("Cannot use `onlaser` method on an entity that is not a laser");
            ch.bc.facing = Facing.DEROT;
            var offset = l.Index(props.laserIndexer(ch.gcx));
            if (offset.HasValue) {
                ch.gcx.RV2 += offset.Value;
            } else isClipped = true;
        }
        if (props.target != null) {
            var (method, func) = props.target.Value;
            Vector2 aimAt = func(ch.gcx);
            Vector2 src = props.targetFromSummon ? ch.bc.ToRawPosition(ch.gcx.RV2) : ch.bc.ParentOffset;
            if        (method == RV2ControlMethod.ANG) {
                ch.gcx.RV2 = ch.gcx.RV2.RotateAll(M.AngleFromToDeg(src, aimAt));
            } else if (method == RV2ControlMethod.RANG) {
                ch.gcx.RV2 += M.AngleFromToDeg(src, aimAt);
            } else if (method == RV2ControlMethod.NX) {
                ch.gcx.RV2 += V2RV2.NRot(aimAt.x - src.x, 0f);
            } else if (method == RV2ControlMethod.NY) {
                ch.gcx.RV2 += V2RV2.NRot(0f, aimAt.y - src.y);
            } else if (method == RV2ControlMethod.RX) {
                ch.gcx.RV2 += V2RV2.Rot(aimAt.x - src.x, 0f);
            } else if (method == RV2ControlMethod.RY) {
                ch.gcx.RV2 += V2RV2.Rot(0f, aimAt.y - src.y);
            }
        }
        parent_style = ch.bc.style;
        if (props.facing != null) ch.bc.facing = props.facing.Value;
        ch.gcx.BaseRV2 = ch.gcx.RV2;
        ch.gcx.fs["times"] = times = (int)props.times(ch.gcx);
        ch.gcx.UpdateRules(props.start);
        if (props.centered) ch.gcx.RV2 -= (times - 1f) / 2f * props.PostloopRV2Incr(ch.gcx, times);
        isClipped = isClipped || (props.clipIf?.Invoke(ch.gcx) ?? false);
        elapsed_frames = 0;
        float? af = props.fortime?.Invoke(ch.gcx);
        allowed_frames = af.HasValue ? (int) af : int.MaxValue;
        ch.gcx.i = 0;
        _hasbeencancelled = false;
        if (props.expose != null) ch.gcx.exposed.AddRange(props.expose);
        unmutated_rv2 = ch.gcx.RV2;
    }

    public bool RemainsExceptLast => ch.gcx.i < times - 1;
    public bool Remains => ch.gcx.i < times;

    public bool IsUnpaused => props.runWhile?.Invoke(GCX) ?? true;

    private int elapsed_frames;
    private readonly int allowed_frames;
    public void WaitStep() {
        GCX.SummonTime += ETime.FRAME_TIME;
        ++elapsed_frames;
    }

    private const string ModNumberRequired =
        "Mod number must be provided for Mod parametrization. Use the \"maxtimes\" property and provide the max count as the first number.";
    public static int GetFiringIndex(Parametrization p, int parentIndex, int thisIndex, int? thisRpt) {
        if (p == Parametrization.THIS) return thisIndex;
        if (p == Parametrization.DEFER) return parentIndex;
        if (p == Parametrization.ADDITIVE) return ExM.__Combine(parentIndex, thisIndex);
        
        //Mod handling
        if (p == Parametrization.MOD) {
            if (thisRpt == null) throw new Exception(ModNumberRequired);
            return parentIndex * thisRpt.Value + (thisIndex % thisRpt.Value);
        } if (p == Parametrization.INVMOD) {
            if (thisRpt == null) throw new Exception(ModNumberRequired);
            return parentIndex * thisRpt.Value + (thisRpt.Value - 1 - (thisIndex % thisRpt.Value));
        }
        throw new NotImplementedException($"Firing handling for parametrization {p} given repeater {thisRpt}");
    }

    //Use a persistent variable so that any further iterations will all fail after cancellation is triggered.
    //This is useful for edge cases as well as the LastIteration handling
    private bool _hasbeencancelled;
    private bool IsCancelled => _hasbeencancelled = _hasbeencancelled 
                                                    || (props.cancelIf?.Invoke(GCX) ?? false) 
                                                    || (elapsed_frames >= allowed_frames);
    public bool PrepareIteration() {
        if (props.resetTime) GCX.SummonTime = 0;
        props.timer?.Restart();
        GCX.index = GetFiringIndex(p, parent_index, GCX.i, props.maxTimes);
        //Automatic bindings
        if (props.bindArrow) {
            ch.gcx.fs["axd"] = M.HMod(times, GCX.i);
            ch.gcx.fs["ayd"] = M.HNMod(times, GCX.i);
            ch.gcx.fs["aixd"] = M.HMod(times, times - 1 - GCX.i);
            ch.gcx.fs["aiyd"] = M.HNMod(times, times - 1 - GCX.i);
        }
        if (props.bindLR) ch.gcx.fs["rl"] = -1 * (ch.gcx.fs["lr"] = M.PM1Mod(GCX.i));
        if (props.bindUD) ch.gcx.fs["du"] = -1 * (ch.gcx.fs["ud"] = M.PM1Mod(GCX.i));
        if (props.bindAngle) ch.gcx.fs["angle"] = GCX.RV2.angle;
        if (props.bindItr != null) ch.gcx.fs[props.bindItr] = GCX.i;
        //
        ch.gcx.UpdateRules(props.preloop);
        if (IsCancelled) return false;
        if (props.colors != null) {
            int index = (props.colorsIndexer == null) ? GCX.i : (int) props.colorsIndexer(GCX);
            ch.bc.style = props.colorsReverse ?
                    BulletManager.StyleSelector.MergeStyles(props.colors.ModIndex(index), parent_style) :
                    BulletManager.StyleSelector.MergeStyles(parent_style, props.colors.ModIndex(index));
        }
        if (props.sah != null) {
            GCX.RV2 = GCX.BaseRV2 + V2RV2.Rot(props.sah.Locate(GCX));
            var simp_gcx = GCX.Copy();
            simp_gcx.FinishIteration(props.postloop, V2RV2.Zero);
            simp_gcx.UpdateRules(props.preloop);
            GCX.RV2 = props.sah.Angle(GCX, GCX.RV2, GCX.BaseRV2 + V2RV2.Rot(props.sah.Locate(simp_gcx)));
            simp_gcx.Dispose();
        } else if (props.frv2 != null) {
            GCX.RV2 = GCX.BaseRV2 + props.frv2(GCX);
        }
        if (props.saveF != null) {
            foreach (var (h, i, v) in props.saveF) {
                h.Save((int) i(GCX), v(GCX));
            }
        }
        if (props.saveV2 != null) {
            foreach (var (h, i, v) in props.saveV2) {
                h.Save((int) i(GCX), v(GCX));
            }
        }
        if (props.sfx != null && (props.sfxIf?.Invoke(GCX) ?? true)) {
            int index = (props.sfxIndexer == null) ? GCX.i : (int) props.sfxIndexer(GCX);
            ServiceLocator.SFXService.Request(props.sfx.ModIndex(index));
        }
        unmutated_rv2 = GCX.RV2;
        if (props.rv2aMutater != null) {
            GCX.RV2 = GCX.RV2.ForceAngle(props.rv2aMutater(GCX));
        }
        return true;
    }

    private V2RV2 unmutated_rv2;

    public bool PrepareLastIteration() {
        if (GCX.i == times - 1) {
            return PrepareIteration();
        }
        return false;
    }
        
    public void FinishIteration() {
        GCX.RV2 = unmutated_rv2;
        GCX.FinishIteration(props.postloop, props.PostloopRV2Incr(GCX, times));
    }

    public void IAmDone(bool normalEnd) {
        if (normalEnd) {
            GCX.UpdateRules(props.end);
        }
        ch.Dispose();
    }
}

public abstract class SummonAlongHandler {

    private readonly SAAngle angleHandle;
    private readonly GCXF<float> offsetAngle;

    protected SummonAlongHandler(SAAngle sah, GCXF<float> offsetAngle) {
        this.offsetAngle = offsetAngle;
        angleHandle = sah;
    }

    public abstract Vector2 Locate(GenCtx gcx);
    public V2RV2 Angle(GenCtx gcx, V2RV2 result, V2RV2 nextResult) {
        if (angleHandle == SAAngle.ORIGINAL_BANK) return result.Bank() + offsetAngle(gcx);
        if (angleHandle == SAAngle.REL_ORIGIN_BANK) {
            var trueLoc = result.Bank();
            return result.Bank(M.Atan2D(trueLoc.ny, trueLoc.nx) + offsetAngle(gcx));
        }
        if (angleHandle == SAAngle.TANGENT_BANK) {
            var trueLoc = result.Bank();
            var nextTrueLoc = nextResult.Bank();
            return result.Bank(M.Atan2D(nextTrueLoc.ny - trueLoc.ny, nextTrueLoc.nx - trueLoc.nx) + offsetAngle(gcx));
        }
        return result + offsetAngle(gcx);
    }
}
public class SAOffsetHandler : SummonAlongHandler {
    private readonly GCXF<Vector2> nextLocation;
    public SAOffsetHandler(SAAngle sah, GCXF<float> offsetAngle, GCXF<Vector2> nextLocation): base(sah, offsetAngle) {
        this.nextLocation = nextLocation;
    }

    public override Vector2 Locate(GenCtx gcx) => nextLocation(gcx);
}

}