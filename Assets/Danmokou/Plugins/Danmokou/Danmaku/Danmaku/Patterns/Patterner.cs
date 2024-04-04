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
using Danmokou.Reflection2;
using Danmokou.Services;
using Danmokou.SM;
using JetBrains.Annotations;
using NUnit;
using UnityEngine;
using GCP = Danmokou.Danmaku.Options.GenCtxProperty;
using static Danmokou.Reflection.Compilers;

namespace Danmokou.Danmaku.Patterns {
public record SyncPattern(SyncPatterner Exec, EnvFrame? EnvFrame = null) : EnvFrameAttacher {
    private SyncPatterner Exec { get; } = Exec;
    public EnvFrame? EnvFrame { get; set; } = EnvFrame;

    public void Run(SyncHandoff sbh) {
        if (EnvFrame == null) {
            Exec(sbh);
            return;
        }
        sbh.ch = sbh.ch.OverrideFrame(EnvFrame);
        Exec(sbh);
        sbh.ch.Dispose();
    }

    public static implicit operator SyncPattern(SyncPatterner sp) => new(sp);

}
public delegate void SyncPatterner(SyncHandoff sbh);

public record AsyncPattern(AsyncPatterner Exec, EnvFrame? EnvFrame = null) : EnvFrameAttacher {
    private AsyncPatterner Exec { get; } = Exec;
    public EnvFrame? EnvFrame { get; set; } = EnvFrame;
    
    public IEnumerator Run(AsyncHandoff abh) {
        return EnvFrame == null ? 
            Exec(abh) : 
            RunWithEf(abh);
    }

    private IEnumerator RunWithEf(AsyncHandoff abh) {
        abh.ch = abh.ch.OverrideFrame(EnvFrame);
        yield return Exec(abh);
        abh.ch.Dispose();
    }

    public static implicit operator AsyncPattern(AsyncPatterner ap) => new(ap);
}
public delegate IEnumerator AsyncPatterner(AsyncHandoff abh);


public struct CommonHandoff : IDisposable {
    public readonly ICancellee cT;
    public DelegatedCreator bc;
    public readonly GenCtx gcx;
    public V2RV2? rv2Override;

    /// <summary>
    /// GCX is NOT automatically copied.
    /// </summary>
    public CommonHandoff(ICancellee cT, DelegatedCreator? bc, GenCtx gcx, V2RV2? rv2Override) {
        this.cT = cT;
        this.bc = bc ?? new DelegatedCreator(gcx.exec, "");
        this.gcx = gcx;
        this.rv2Override = rv2Override;
    }

    /// <summary>
    /// Copies the GCX, possibly deriving a new environment frame if a scope is provided.
    /// </summary>
    public readonly CommonHandoff TryDerive(LexicalScope? scope) {
        var ngcx = gcx.Derive(scope);
        if (scope != null && ngcx.AutoVars is AutoVars.GenCtx && rv2Override is { } overr) {
            ngcx.BaseRV2 = ngcx.RV2 = overr;
            return new(cT, bc, ngcx, null);
        } else
            return new(cT, bc, ngcx, rv2Override);
    }

    /// <summary>
    /// Copies the GCX with a new EnvFrame. The provided envframe is mirrored, and the current GCX's RV2/ST
    ///  are stored as rv2Override.
    /// <br/>(If the envframe is null, then the existing GCX is copied with a cloned envframe.)
    /// </summary>
    public readonly CommonHandoff OverrideFrame(EnvFrame? newFrame) {
        var ngcx = newFrame is null ? gcx.Copy(null) : gcx.Copy(newFrame);
        V2RV2? nrv2Override = null;
        if (gcx.Scope.NearestAutoVars is AutoVars.GenCtx)
            nrv2Override = gcx.RV2;
        return new(cT, bc, ngcx, rv2Override ?? nrv2Override);
    }

    /// <summary>
    /// Copy the contained GCX (ie. copy the GCX, and copy the internal envframe).
    /// </summary>
    public readonly CommonHandoff Copy() {
        return new CommonHandoff(cT, bc, gcx.Copy(null), rv2Override);
    }

    /// <summary>
    /// Mirror the contained GCX (ie. copy the GCX, and mirror the internal envframe).
    /// </summary>
    /// <returns></returns>
    public readonly CommonHandoff Mirror() {
        return new CommonHandoff(cT, bc, gcx.Mirror(), rv2Override);
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
    public V2RV2 RV2 => ch.rv2Override ?? GCX.RV2;
    public int index => GCX.index;
    /// <summary>
    /// Starting time of summoned objects (seconds)
    /// </summary>
    public float timeOffset;
    public GenCtx GCX => ch.gcx;

    /// <summary>
    /// The common handoff is not copied. (Be careful on calling Dispose.)
    /// </summary>
    public SyncHandoff(in DelegatedCreator bc, in SMHandoff smh, V2RV2 rv2Override) {
        this.ch = new CommonHandoff(smh.cT, bc, smh.GCX, rv2Override);
        this.timeOffset = 0f;
    }

    /// <summary>
    /// The common handoff is not copied. (Be careful on calling Dispose.)
    /// </summary>
    public SyncHandoff(in CommonHandoff ch, float extraTimeSeconds) {
        this.ch = ch;
        this.timeOffset = extraTimeSeconds;
    }

    public static implicit operator GenCtx(SyncHandoff sbh) => sbh.GCX;

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
    public Action? callback;
    private readonly BehaviorEntity exec;

    /// <summary>
    /// The common handoff is NOT copied from SMHandoff.
    /// </summary>
    public AsyncHandoff(in DelegatedCreator bc, Action? callback, in SMHandoff smh, V2RV2 rv2Override) {
        this.ch = new CommonHandoff(smh.ch.cT, bc, smh.GCX, rv2Override);
        this.callback = callback;
        exec = smh.Exec;
    }

    /// <summary>
    /// Derive an AsyncHandoff from a parent for localized execution. The common handoff is copied.
    /// </summary>
    public AsyncHandoff(in AsyncHandoff parent, in CommonHandoff ch, Action? callback) {
        this.ch = ch.Copy();
        this.callback = callback;
        exec = parent.exec;
    }

    //public void RunRIEnumerator(IEnumerator cor) => exec.RunRIEnumerator(cor);
    public void RunPrependRIEnumerator(IEnumerator cor) => exec.RunPrependRIEnumerator(cor);

    /// <summary>
    /// Send completion information via the callback.
    /// <br/>This does not clean up resources- <see cref="Cleanup"/> must be called separately.
    /// </summary>
    public readonly void Done() {
        callback?.Invoke();
    }
    
    /// <summary>
    /// Dispose the copied handoff.
    /// </summary>
    public readonly void Cleanup() {
        ch.Dispose();
    }
}

/// <summary>
/// Functions that describe atomic synchronous actions.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[Reflect] [Atomic]
public static partial class AtomicPatterns {
    #region Erasers

    public static SyncPattern Erase<T>(GCXF<T> meth) => new(sbh => meth(sbh.GCX));
    
    #endregion
    
    /// <summary>
    /// Do nothing.
    /// </summary>
    /// <returns></returns>
    public static SyncPattern Noop() => new(sbh => {  });
    /// <summary>
    /// Play the audio clip defined by the style.
    /// </summary>
    /// <returns></returns>
    public static SyncPattern SFX() => new(sbh => sbh.bc.SFX());

    
    /// <summary>
    /// Invoke the provided event with the provided value.
    /// </summary>
    [GAlias("eventf", typeof(float))]
    public static SyncPattern Event<T>(string evName,  GCXF<T> value) => new(sbh => 
        Events.ProcRuntimeEvent(evName, value(sbh.GCX)));

    /// <summary>
    /// Invoke one of the provided unit events according to the firing index.
    /// </summary>
    public static SyncPattern Event0(string evName) => Event<Unit>(evName, _ => default);

    #region Items

    public static SyncPattern LifeItem() => new(sbh =>
        ItemPooler.RequestLife(new ItemRequestContext(sbh.bc.ParentOffset, sbh.RV2.TrueLocation)));
    
    public static SyncPattern ValueItem() =>new(sbh =>
        ItemPooler.RequestValue(new ItemRequestContext(sbh.bc.ParentOffset, sbh.RV2.TrueLocation)));
    
    public static SyncPattern SmallValueItem() => new(sbh => 
        ItemPooler.RequestSmallValue(new ItemRequestContext(sbh.bc.ParentOffset, sbh.RV2.TrueLocation)));
    
    public static SyncPattern PointPPItem() => new(sbh => 
        ItemPooler.RequestPointPP(new ItemRequestContext(sbh.bc.ParentOffset, sbh.RV2.TrueLocation)));
    
    public static SyncPattern GemItem() => new(sbh => 
        ItemPooler.RequestGem(new ItemRequestContext(sbh.bc.ParentOffset, sbh.RV2.TrueLocation)));
    
    [Alias("1UpItem")]
    public static SyncPattern OneUpItem() => new(sbh => 
        ItemPooler.Request1UP(new ItemRequestContext(sbh.bc.ParentOffset, sbh.RV2.TrueLocation)));
    
    public static SyncPattern PowerupShiftItem() => new(sbh => 
        ItemPooler.RequestPowerupShift(new ItemRequestContext(sbh.bc.ParentOffset, sbh.RV2.TrueLocation)));
    
    #endregion
    
    #region SimpleBullets

    /// <summary>
    /// Fires a simple bullet.
    /// </summary>
    /// <param name="path">Movement descriptor</param>
    /// <returns></returns>
    [Alias("simp")]
    public static SyncPattern S(VTP path) => Simple(path, new SBOptions(Array.Empty<SBOption>()));

    /// <summary>
    /// Fires a simple bullet. Takes an array of simple bullet options as modifiers.
    /// See <see cref="SBOption"/>
    /// </summary>
    /// <param name="path"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static SyncPattern Simple(VTP path, SBOptions options) => new(sbh => {
        uint id = sbh.GCX.NextID();
        if (options.player.HasValue) {
            sbh.ch.bc.style = BulletManager.GetOrMakePlayerCopy(sbh.bc.style);
        }
        sbh.bc.Simple(sbh, options, path, id);
    });

    #endregion

    /// <summary>
    /// Fires a complex bullet (ie. controlled by a GameObject).
    /// Do not use for pathers/lasers (use the Pather or Laser functions).
    /// </summary>
    /// <param name="path">Movement descriptor</param>
    /// <param name="options">Bullet constructor options</param>
    /// <returns></returns>
    public static SyncPattern Complex(VTP path, BehOptions options) => new(sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.Complex(sbh, path, id, options);
    });

    /// <summary>
    /// Fires a Pather/Tracker projectile, which "remembers" the points it has gone through and draws a path through them.
    /// </summary>
    /// <param name="maxTime">Maximum remember time. Only enough space to handle this much time will be allocated.</param>
    /// <param name="remember">The current remember time as a function of the current life-time</param>
    /// <param name="path">Movement descriptor</param>
    /// <param name="options">Bullet constructor options</param>
    /// <returns></returns>
    public static SyncPattern Pather(float maxTime, BPY remember, VTP path, BehOptions options) => new(sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.Pather(sbh, maxTime > 0 ? maxTime : (float?)null, remember, path, id, options);
    });

    /// <summary>
    /// Create a laser.
    /// </summary>
    /// <param name="path">Movement path for the base point of the laser</param>
    /// <param name="cold">Time that the laser is in a non-damaging state</param>
    /// <param name="hot">Time that the laser is in a damaging state</param>
    /// <param name="options">Laser constructor options</param>
    /// <returns></returns>
    public static SyncPattern Laser(VTP path, GCXF<float> cold, GCXF<float> hot, LaserOptions options) => new(sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.Laser(sbh, path, cold(sbh.GCX), hot(sbh.GCX), id, options);
    });

    public static SyncPattern SafeLaser(GCXF<float> cold, LaserOptions options) =>
        Laser(VTPRepo.NoVTP, cold, _ => 0f, options); 
    
    public static SyncPattern SafeLaserM(VTP path, GCXF<float> cold, LaserOptions options) =>
        Laser(path, cold, _ => 0f, options); 

    public static SyncPattern SummonS(VTP path, StateMachine? sm) =>
        Summon(path, sm, new BehOptions());
    public static SyncPattern SummonSUP(VTP path, StateMachine? sm) =>
        SummonUP(path, sm, new BehOptions());
    public static SyncPattern Inode(VTP path, StateMachine? sm) {
        var f = SummonS(path, sm);
        return new(sbh => {
            sbh.ch.bc.style = "inode";
            f.Run(sbh);
        });
    }

    private static SyncHandoff _Summon(SyncHandoff sbh, bool pool, VTP path, StateMachine? sm, BehOptions options) {
        uint id = sbh.GCX.NextID();
        sbh.bc.Summon(pool, sbh, options, path, SMRunner.Cull(sm, sbh.ch.cT, sbh.GCX), id);
        return sbh;
    }

    public static SyncPattern Summon(VTP path, StateMachine? sm, BehOptions options) => new(sbh =>
        _Summon(sbh, true, path, sm, options));

    public static SyncPattern SummonUP(VTP path, StateMachine? sm, BehOptions options) => new(sbh =>
        _Summon(sbh, false, path, sm, options));

    public static SyncPattern SummonR(RootedVTP path, StateMachine? sm, BehOptions options) => new(sbh => {
        sbh.ch.bc.Root(path.root(sbh.GCX));
        _Summon(sbh, true, path.path, sm, options);
    });

    public static SyncPattern SummonRUP(RootedVTP path, StateMachine? sm, BehOptions options) => new(sbh => {
        sbh.ch.bc.Root(path.root(sbh.GCX));
        _Summon(sbh, false, path.path, sm, options);
    });

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

    private static SMRunner WaitForPhase(ICancellee cT) => SMRunner.Cull(Reflector.WaitForPhaseSM, cT)!.Value;

    public static SyncPattern Circ(TP4 color, BPRV2 locScaleAngle) => new(sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonCirc(sbh, "_", color, DrawerLoc(sbh, locScaleAngle), WaitForPhase(sbh.ch.cT), id);
    });
    public static SyncPattern gRelCirc(string behId, TP loc, BPRV2 locScaleAngle, TP4 color) => new(sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonCirc(sbh, behId, color, DrawerLoc(sbh, locScaleAngle, loc), WaitForPhase(sbh.ch.cT), id);
    });

    public static SyncPattern RelCirc(string behId, Func<TExArgCtx, TEx<BehaviorEntity>> beh, BPRV2 locScaleAngle, TP4 color) =>
        gRelCirc(behId, TP(tac => beh(tac).Field(nameof(BehaviorEntity.Location))), locScaleAngle, color);
    public static SyncPattern Rect(TP4 color, BPRV2 locScaleAngle) => new(sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonRect(sbh, "_", color, DrawerLoc(sbh, locScaleAngle), WaitForPhase(sbh.ch.cT), id);
    });
    public static SyncPattern gRelRect(string behId, TP loc, BPRV2 locScaleAngle, TP4 color) => new(sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonRect(sbh, behId, color, DrawerLoc(sbh, locScaleAngle, loc), WaitForPhase(sbh.ch.cT), id);
    });

    public static SyncPattern RelRect(string behId, Func<TExArgCtx, TEx<BehaviorEntity>> beh, BPRV2 locScaleAngle, TP4 color) =>
        gRelRect(behId, TP(tac => beh(tac).Field(nameof(BehaviorEntity.Location))), locScaleAngle, color);

    
    public static SyncPattern Darkness(TP loc, BPY radius, TP4 color) => new(sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.SummonDarkness(sbh, "_", loc, radius, color, WaitForPhase(sbh.ch.cT), id);
    });

    public static SyncPattern PowerAura(PowerAuraOptions options) =>
        new(sbh => sbh.bc.SummonPowerAura(sbh, options, sbh.GCX.NextID()));
    
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
        return new(sbh => sbh.bc.SummonPowerAura(sbh, props, sbh.GCX.NextID()));
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
        return new(sbh => sbh.bc.SummonPowerAura(sbh, props, sbh.GCX.NextID()));
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
    public LoopControl(GenCtxProperties<T> props, in CommonHandoff baseCh, out bool isClipped) {
        isClipped = false;
        this.props = props;
        p = props.p;
        ch = baseCh.TryDerive(props.Scope);
        parent_index = (props.p_mutater == null) ? ch.gcx.index : (int)props.p_mutater(ch.gcx);
        if (props.resetColor) ch.bc.style = "_";
        if (props.rv2Overrider != null) {
            var orv2 = props.rv2Overrider(ch.gcx);
            if (ch.rv2Override != null || !ch.gcx.HasGCXVars)
                ch.rv2Override = orv2;
            else
                ch.gcx.RV2 = orv2;
        }
        if (props.bank != null) {
            var (toZero, banker) = props.bank.Value;
            ch.gcx.RV2 = ch.gcx.RV2.Bank(toZero ? (float?)0f : null) + banker(ch.gcx);
        }
        if (props.forceRoot != null) {
            Vector2 newRoot = props.forceRoot(ch.gcx);
            if (props.forceRootAdjust)
                ch.gcx.RV2 += ch.bc.ParentOffset - newRoot;
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
        times = (int)props.times(ch.gcx);
        if (ch.gcx.AutoVars is AutoVars.GenCtx) {
            ch.gcx.BaseRV2 = ch.gcx.RV2;
            ch.gcx.EnvFrame.Value<float>(ch.gcx.GCXVars.times) = times;
        }
        ch.gcx.UpdateRules(props.start);
        if (props.centered) ch.gcx.RV2 -= (times - 1f) / 2f * (props.PostloopRV2Incr(ch.gcx, times) ?? V2RV2.Zero);
        isClipped = isClipped || (props.clipIf?.Invoke(ch.gcx) ?? false);
        elapsed_frames = 0;
        float? af = props.fortime?.Invoke(ch.gcx);
        allowed_frames = af.HasValue ? (int) af : int.MaxValue;
        ch.gcx.pi = ch.gcx.i;
        ch.gcx.i = 0;
        _hasbeencancelled = false;
        unmutated_rv2 = ch.gcx.AutoVars is AutoVars.GenCtx ? ch.gcx.RV2 : null;
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
        props.timer?.Invoke(GCX).Restart();
        GCX.index = GetFiringIndex(p, parent_index, GCX.i, props.maxTimes);
        //Automatic bindings
        if (GCX.AutoVars is AutoVars.GenCtx gcxv && !GCX.AutovarsAreInherited) {
            if (gcxv.bindArrow is { } bav) {
                GCX.EnvFrame.Value<float>(bav.axd) = M.HMod(times, GCX.i);
                GCX.EnvFrame.Value<float>(bav.ayd) = M.HNMod(times, GCX.i);
                GCX.EnvFrame.Value<float>(bav.aixd) = M.HMod(times, times - 1 - GCX.i);
                GCX.EnvFrame.Value<float>(bav.aiyd) = M.HNMod(times, times - 1 - GCX.i);
            }
            if (gcxv.bindLR is { } lrv)
                GCX.EnvFrame.Value<float>(lrv.rl) = -1 * (GCX.EnvFrame.Value<float>(lrv.lr) = M.PM1Mod(GCX.i));
            if (gcxv.bindUD is { } udv)
                GCX.EnvFrame.Value<float>(udv.ud) = -1 * (GCX.EnvFrame.Value<float>(udv.ud) = M.PM1Mod(GCX.i));
            if (gcxv.bindAngle is { } av)
                GCX.EnvFrame.Value<float>(av) = GCX.RV2.angle;
            if (gcxv.bindItr is { } biv)
                GCX.EnvFrame.Value<float>(biv) = GCX.i;
        }
        //
        ch.gcx.UpdateRules(props.preloop);
        if (IsCancelled) return false;
        string? ncolor = props.colorFunc?.Invoke(GCX);
        if (props.colors != null) {
            int index = (props.colorsIndexer == null) ? GCX.i : (int) props.colorsIndexer(GCX);
            ncolor ??= props.colors.ModIndex(index);
        }
        if (ncolor != null)
            ch.bc.style = props.colorsReverse ?
                BulletManager.StyleSelector.MergeStyles(ncolor, parent_style) :
                BulletManager.StyleSelector.MergeStyles(parent_style, ncolor);
        if (props.sah != null) {
            GCX.RV2 = GCX.BaseRV2 + V2RV2.Rot(props.sah.Locate(GCX));
            var simp_gcx = GCX.Copy(null);
            simp_gcx.FinishIteration(props.postloop, V2RV2.Zero);
            simp_gcx.UpdateRules(props.preloop);
            GCX.RV2 = props.sah.Angle(GCX, GCX.RV2, GCX.BaseRV2 + V2RV2.Rot(props.sah.Locate(simp_gcx)));
            simp_gcx.Dispose();
        } else if (props.frv2 != null) {
            GCX.RV2 = GCX.BaseRV2 + props.frv2(GCX);
        }
        if (props.saveF != null) {
            for (int ii = 0; ii < props.saveF.Count; ++ii) {
                var (h, i, v) = props.saveF[ii];
                h.Save((int) i(GCX), v(GCX));
            }
        }
        if (props.saveV2 != null) {
            for (int ii = 0; ii < props.saveV2.Count; ++ii) {
                var (h, i, v) = props.saveV2[ii];
                h.Save((int) i(GCX), v(GCX));
            }
        }
        if (props.sfx != null && (props.sfxIf?.Invoke(GCX) ?? true)) {
            int index = (props.sfxIndexer == null) ? GCX.i : (int) props.sfxIndexer(GCX);
            ISFXService.SFXService.Request(props.sfx.ModIndex(index));
        }
        if (unmutated_rv2 != null)
            unmutated_rv2 = GCX.RV2;
        if (props.rv2aMutater != null) {
            GCX.RV2 = GCX.RV2.ForceAngle(props.rv2aMutater(GCX));
        }
        return true;
    }

    private V2RV2? unmutated_rv2;

    public bool PrepareLastIteration() {
        if (GCX.i == times - 1) {
            return PrepareIteration();
        }
        return false;
    }
        
    public void FinishIteration() {
        if (unmutated_rv2 is {} urv2)
            GCX.RV2 = urv2;
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