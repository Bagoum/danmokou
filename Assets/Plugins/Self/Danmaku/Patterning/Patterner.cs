using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DMath;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using static Core.Events;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using GCP = Danmaku.GenCtxProperty;
using ExSBF = System.Func<Danmaku.TExSBC, TEx<int>, TEx<float>>;
using ExSBV2 = System.Func<Danmaku.TExSBC, TEx<int>, TEx<UnityEngine.Vector2>>;
using static Danmaku.Enums;
using static Compilers;
using static DMath.ExMHelpers;
using static DMath.ExM;
using static DMath.ExMRV2;

namespace Danmaku {
public delegate GenCtx SyncPattern(SyncHandoff sbh);

public delegate IEnumerator AsyncPattern(AsyncHandoff abh);

public delegate IEnumerator PAsyncPattern(FixedPatternHandoff fph);


public struct CommonHandoff {
    public CancellationToken cT;
    public DelegatedCreator bc;
    public readonly GenCtx gcx;
    public V2RV2 RV2 {
        get => gcx.RV2;
        set => gcx.RV2 = value;
    }

    public CommonHandoff(CancellationToken cT, DelegatedCreator bc, GenCtx gcx) {
        this.cT = cT;
        this.bc = bc;
        this.gcx = gcx;
    }
    public CommonHandoff(CancellationToken cT, GenCtx gcx) {
        this.cT = cT;
        this.bc = new DelegatedCreator(gcx.exec, "");
        this.gcx = gcx;
    }
    
    public CommonHandoff CopyGCX() => new CommonHandoff(cT, bc, gcx.Copy());
}
public struct SyncHandoff {
    public CommonHandoff ch;
    public DelegatedCreator bc => ch.bc;
    public V2RV2 rv2 => ch.RV2;
    public int index => GCX.index;
    /// <summary>
    /// Starting time of summoned objects (seconds)
    /// </summary>
    public float timeOffset;
    public GenCtx GCX => ch.gcx;

    public SyncHandoff(DelegatedCreator bc, V2RV2 rv2, SMHandoff smh, out GenCtx newGcx) {
        newGcx = smh.GCX.Copy(rv2);
        this.ch = new CommonHandoff(smh.cT, bc, newGcx);
        this.timeOffset = 0f;
    }

    /// <summary>
    /// Derive a SyncHandoff from an AsyncHandoff, where the index is copied.
    /// </summary>
    /// <param name="abh">Original AsyncHandoff</param>
    /// <param name="extraFrames">Number of frames to advance bullet simulation</param>
    public SyncHandoff(ref AsyncHandoff abh, float extraFrames) {
        this.ch = abh.ch;
        this.timeOffset = abh.AdjustedTimeOffset(extraFrames);
    }

    public static implicit operator GenCtx(SyncHandoff sbh) => sbh.GCX;

    public SyncHandoff CopyGCX() {
        var nsh = this;
        nsh.ch = nsh.ch.CopyGCX();
        return nsh;
    }
    
    public void AddTime(float frames) {
        timeOffset += frames * ETime.FRAME_TIME;
    }


}

public struct AsyncHandoff {
    public CommonHandoff ch;
    public bool Cancelled => ch.cT.IsCancellationRequested;
    public Action done;
    private readonly BehaviorEntity exec;
    /// <summary>
    /// Starting time of summoned objects (frames)
    /// </summary>
    private float framesOffset;
    /// <summary>
    /// This constructor automatically copies the GCX.
    /// </summary>
    /// <param name="bc"></param>
    /// <param name="rv2"></param>
    /// <param name="done"></param>
    /// <param name="smh"></param>
    public AsyncHandoff(DelegatedCreator bc, V2RV2 rv2, Action done, SMHandoff smh) {
        this.ch = new CommonHandoff(smh.ch.cT, bc, smh.GCX.Copy(rv2));
        this.done = Functions.Link(ch.gcx.Dispose, done);
        exec = smh.Exec;
        framesOffset = 0f;
    }

    public AsyncHandoff(SyncHandoff sbh) {
        this.ch = new CommonHandoff(sbh.ch.cT, sbh.bc, sbh.GCX.Copy());
        this.done = ch.gcx.Dispose;
        exec = sbh.GCX.exec;
        framesOffset = 0f;
    }

    /// <summary>
    /// AsyncHandoff constructor for generic coroutines.
    /// </summary>
    /// <param name="smh">SMHandoff of invoking StateMachine</param>
    /// <param name="rv2"></param>
    /// <param name="t">Task to await on for this object's completion</param>
    public AsyncHandoff(SMHandoff smh, V2RV2 rv2, out Task t) {
        this.ch = new CommonHandoff(smh.ch.cT, new DelegatedCreator(smh.Exec, "_"), smh.GCX.Copy(rv2));
        done = Functions.Link(ch.gcx.Dispose, WaitingUtils.GetAwaiter(out t));
        exec = smh.Exec;
        framesOffset = 0f;
    }

    //public void RunRIEnumerator(IEnumerator cor) => exec.RunRIEnumerator(cor);
    public void RunPrependRIEnumerator(IEnumerator cor) => exec.RunPrependRIEnumerator(cor);

    public void WaitStep() {
        if (--framesOffset < 0) framesOffset = 0;
    }
    public float AdjustedTimeOffset(float extraFrames) => (framesOffset + extraFrames) * ETime.FRAME_TIME;

    public void AddSimulatedTime(float frames) {
        framesOffset += frames;
    }
}

public readonly struct FixedPatternHandoff {
    public readonly CancellationToken cT;
    public readonly Action done;
    public readonly BehaviorEntity exec;

    public FixedPatternHandoff(SMHandoff smh, Action onDone) {
        exec = smh.Exec;
        done = onDone;
        cT = smh.ch.cT;
    }
}

/// <summary>
/// Functions that describe atomic synchronous actions.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static partial class AtomicPatterns {
    /// <summary>
    /// Do nothing.
    /// </summary>
    /// <returns></returns>
    public static SyncPattern Noop() => sbh => sbh;
    /// <summary>
    /// Play the audio clip defined by the style.
    /// </summary>
    /// <returns></returns>
    public static SyncPattern SFX() => sbh => {
        sbh.bc.SFX();
        return sbh;
    };

    /// <summary>
    /// Invoke one of the provided events according to the firing index.
    /// </summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public static SyncPattern Event(Maybe<Event0>[] events) => sbh => {
        events[sbh.index % events.Length].ValueOrNull?.InvokeIfNotRefractory();
        return sbh;
    };

    /// <summary>
    /// Save a float value to external data hoisting.
    /// </summary>
    /// <param name="name">Name of the hoisted variable</param>
    /// <param name="indexer">Indexing method</param>
    /// <param name="valuer">Value</param>
    /// <returns></returns>
    public static SyncPattern SaveF(ReflectEx.Hoist<float> name, GCXF<float> indexer, GCXF<float> valuer) => sbh => {
        name.Save((int) indexer(sbh.GCX), valuer(sbh.GCX));
        return sbh.GCX;
    };

    /// <summary>
    /// Save a vector2 value to external data hoisting.
    /// </summary>
    /// <param name="name">Name of the hoisted variable</param>
    /// <param name="indexer">Indexing method</param>
    /// <param name="valuer">Value</param>
    /// <returns></returns>
    public static SyncPattern SaveV2(ReflectEx.Hoist<Vector2> name, GCXF<float> indexer, GCXF<Vector2> valuer) => sbh => {
        name.Save((int) indexer(sbh.GCX), valuer(sbh.GCX));
        return sbh.GCX;
    };
    
    #region Items

    public static SyncPattern LifeItem() => sbh => {
        ItemPooler.RequestLife(sbh.bc.ToRawPosition(sbh.GCX.RV2));
        return sbh;
    };
    public static SyncPattern ValueItem() => sbh => {
        ItemPooler.RequestValue(sbh.bc.ToRawPosition(sbh.GCX.RV2));
        return sbh;
    };
    public static SyncPattern PointPPItem() => sbh => {
        ItemPooler.RequestPointPP(sbh.bc.ToRawPosition(sbh.GCX.RV2));
        return sbh;
    };
    #endregion
    
    #region SimpleBullets

    /// <summary>
    /// Fires a simple bullet.
    /// </summary>
    /// <param name="path">Movement descriptor</param>
    /// <returns></returns>
    public static SyncPattern S(GCXU<VTP> path) {
        return sbh => {
            uint id = sbh.GCX.NextID();
            sbh.bc.Simple(sbh, null, null, path.New(sbh.GCX, ref id), id);
            return sbh;
        };
    }

    /// <summary>
    /// Fires a simple bullet with custom direction.
    /// <br/>Note: Direction must be provided as a cos,sin pair. Use CosSinDeg or CosSin.
    /// </summary>
    /// <param name="dir">Direction function (cos,sin pair)</param>
    /// <param name="path">Movement descriptor</param>
    /// <returns></returns>
    public static SyncPattern SD(GCXU<TP> dir, GCXU<VTP> path) {
        return sbh => {
            uint id = sbh.GCX.NextID();
            sbh.bc.Simple(sbh, null, dir.New(sbh.GCX, ref id), path.Add(sbh.GCX, id), id);
            return sbh;
        };
    }

    /// <summary>
    /// Fires a simple bullet with custom direction.
    /// </summary>
    /// <param name="dir">Direction (degrees)</param>
    /// <param name="path">Movement descriptor</param>
    /// <returns></returns>
    public static SyncPattern SDD(ExBPY dir, GCXU<VTP> path) => SD(GCXU(x => CosSinDeg(dir(x))), path);

    /// <summary>
    /// Fires a scalable simple bullet.
    /// </summary>
    /// <param name="scale">Scaling function</param>
    /// <param name="path">Movement descriptor</param>
    /// <returns></returns>
    public static SyncPattern SS(BPY scale, GCXU<VTP> path) => sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.Simple(sbh, scale, null, path.New(sbh.GCX, ref id), id);
        return sbh;
    };
    /// <summary>
    /// Fires a scalable simple bullet with custom diction.
    /// </summary>
    public static SyncPattern SSD(BPY scale, GCXU<TP> dir, GCXU<VTP> path) => sbh => {
        uint id = sbh.GCX.NextID();
        sbh.bc.Simple(sbh, scale, dir.New(sbh.GCX, ref id), path.Add(sbh.GCX, id), id);
        return sbh;
    };

    /// <summary>
    /// Fires a scalable simple bullet with custom diction.
    /// </summary>
    public static SyncPattern SSDD(BPY scale, ExBPY dir, GCXU<VTP> path) =>
        SSD(scale, GCXU(x => CosSinDeg(dir(x))), path);

    /// <summary>
    /// Fires a player bullet with a damage value and an on-hit effect.
    /// </summary>
    public static SyncPattern PS(int bossDmg, int stageDmg, string effectStrategy, VTP path) {
        var effect = ResourceManager.GetEffect(effectStrategy);
        return sbh => {
            sbh.ch.bc.style = BulletManager.GetOrMakePlayerCopy(sbh.bc.style);
            uint id = sbh.GCX.NextID();
            sbh.bc.Simple(sbh, null, null, path, id);
            PlayerFireDataHoisting.Record(id, bossDmg, stageDmg, effect);
            return sbh;
        };
    }
    /// <summary>
    /// Fires a player bullet with a damage value, a scaling function, and an on-hit effect.
    /// </summary>
    public static SyncPattern PSS(int bossDmg, int stageDmg, string effectStrategy, BPY scaler, VTP path) {
        var effect = ResourceManager.GetEffect(effectStrategy);
        return sbh => {
            sbh.ch.bc.style = BulletManager.GetOrMakePlayerCopy(sbh.bc.style);
            uint id = sbh.GCX.NextID();
            sbh.bc.Simple(sbh, scaler, null, path, id);
            PlayerFireDataHoisting.Record(id, bossDmg, stageDmg, effect);
            return sbh;
        };
    }
    /// <summary>
    /// Fires a player bullet with a damage value, a direction function, and an on-hit effect.
    /// </summary>
    public static SyncPattern PSSDD(int bossDmg, int stageDmg, string effectStrategy, BPY scaler, ExBPY dir, VTP path) {
        var effect = ResourceManager.GetEffect(effectStrategy);
        var dir2 = TP(x => CosSinDeg(dir(x)));
        return sbh => {
            sbh.ch.bc.style = BulletManager.GetOrMakePlayerCopy(sbh.bc.style);
            uint id = sbh.GCX.NextID();
            sbh.bc.Simple(sbh, scaler, dir2, path, id);
            PlayerFireDataHoisting.Record(id, bossDmg, stageDmg, effect);
            return sbh;
        };
    }

    #endregion

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
        sbh.bc.Pather(sbh, maxTime > 0 ? maxTime : (float?) null, remember, path.New(sbh.GCX, ref id), id, options);
        return sbh;
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
            sbh.bc.Laser(sbh, path.New(sbh.GCX, ref id), cold(sbh.GCX), hot(sbh.GCX), id, options);
            return sbh;
        };

    public static SyncPattern SafeLaser(GCXF<float> cold, LaserOption[] options) =>
        Laser("null".Into<GCXU<VTP>>(), cold, _ => 0f, new LaserOptions(options.Prepend(
            LaserOption.S(_ => 1/RealizedLaserOptions.DEFAULT_LASER_WIDTH)))); 
    
    public static SyncPattern SafeLaserM(GCXU<VTP> path, GCXF<float> cold, LaserOption[] options) =>
        Laser(path, cold, _ => 0f, new LaserOptions(options.Prepend(
            LaserOption.S(_ => 1/RealizedLaserOptions.DEFAULT_LASER_WIDTH)))); 

    public static SyncPattern SummonS(GCXU<VTP> path, [CanBeNull] StateMachine sm) =>
        Summon(path, sm, new BehOptions());
    public static SyncPattern SummonSUP(GCXU<VTP> path, [CanBeNull] StateMachine sm) =>
        SummonUP(path, sm, new BehOptions());
    public static SyncPattern Inode(GCXU<VTP> path, [CanBeNull] StateMachine sm) {
        var f = SummonS(path, sm);
        return sbh => {
            sbh.ch.bc.style = "inode";
            f(sbh);
            return sbh;
        };
    }

    private static SyncHandoff _Summon(SyncHandoff sbh, bool pool, GCXU<VTP> path, [CanBeNull] StateMachine sm, BehOptions options) {
        uint id = sbh.GCX.NextID();
        sbh.bc.Summon(pool, sbh, options, path.New(sbh.GCX, ref id), SMRunner.Cull(sm, sbh.ch.cT, sbh.GCX), id);
        return sbh;
    }

    public static SyncPattern Summon(GCXU<VTP> path, [CanBeNull] StateMachine sm, BehOptions options) => sbh =>
        _Summon(sbh, true, path, sm, options);
    public static SyncPattern SummonUP(GCXU<VTP> path, [CanBeNull] StateMachine sm, BehOptions options) => sbh =>
        _Summon(sbh, false, path, sm, options);

    public static SyncPattern SummonR(RootedVTP path, [CanBeNull] StateMachine sm, BehOptions options) => sbh => {
        sbh.ch.bc.Root(path.root(sbh.GCX));
        return _Summon(sbh, true, path.path, sm, options);
    };
    public static SyncPattern SummonRUP(RootedVTP path, [CanBeNull] StateMachine sm, BehOptions options) => sbh => {
        sbh.ch.bc.Root(path.root(sbh.GCX));
        return _Summon(sbh, false, path.path, sm, options);
    };

    public static SyncPattern SummonRZ([CanBeNull] StateMachine sm, BehOptions options) =>
        SummonR(new RootedVTP(0, 0, VTPRepo.Null()), sm, options);

    public static SyncPattern Rect(TP4 color, ExBPRV2 locScaleAngle) => sbh => {
        uint id = sbh.GCX.NextID();
        var summonLoc = sbh.bc.FacedRV2(sbh.rv2) + sbh.bc.ParentOffset;
        var locator = BPRV2(bpi => EEx.Resolve<V2RV2>(locScaleAngle(bpi), _lcs => {
            var lcs = new TExRV2(_lcs);
            return V2V2F(
                RV2ToXY(AddRVA(summonLoc, ExUtils.V3(lcs.nx, lcs.ny, E0))), //locScaleAng offset is rotational
                ExUtils.V2(lcs.rx, lcs.ry),
                Add<float>(lcs.angle, summonLoc.angle)
            );
        }));
        sbh.bc.SummonRect(sbh, "_", color, locator, SMRunner.Cull(Reflector.WaitForPhaseSM, sbh.ch.cT), id);
        return sbh;
    };
    public static SyncPattern gRelRect(string behId, ExTP loc, ExBPRV2 locScaleAngle, TP4 color) => sbh => {
        uint id = sbh.GCX.NextID();
        var summonLoc = sbh.bc.FacedRV2(sbh.rv2);
        var locator = BPRV2(bpi => EEx.Resolve<V2RV2>(locScaleAngle(bpi), _lcs => {
            var lcs = new TExRV2(_lcs);
            return V2V2F(
                RV2ToXY(AddRVA(AddNV(summonLoc, loc(bpi)), ExUtils.V3(lcs.nx, lcs.ny, E0))), //locScaleAng offset is rotational
                ExUtils.V2(lcs.rx, lcs.ry),
                Add<float>(lcs.angle, summonLoc.angle)
            );
        }));
        sbh.bc.SummonRect(sbh, behId, color, locator, SMRunner.Cull(Reflector.WaitForPhaseSM, sbh.ch.cT), id);
        return sbh;
    };

    public static SyncPattern RelRect(string behId, BEHPointer beh, ExBPRV2 locScaleAngle, TP4 color) =>
        gRelRect(behId, _ => LBEH(beh), locScaleAngle, color);

    /// <summary>
    /// Create a powerup effect.
    /// These effects are parented directly under the BEH they are attached to. Offsets, etc do not apply. 
    /// </summary>
    /// <param name="sfx">Sound effect to play when the powerup starts</param>
    /// <param name="color">Color multiplier function</param>
    /// <param name="time">Time the powerup exists</param>
    /// <param name="itrs">Number of cycles the powerup goes through</param>
    /// <returns></returns>
    public static SyncPattern Powerup(string sfx, TP4 color, GCXF<float> time, GCXF<float> itrs) => sbh => {
        uint id = sbh.GCX.NextID();
        SFXService.Request(sfx);
        sbh.bc.SummonPowerup(sbh, color, time(sbh.GCX), itrs(sbh.GCX), id);
        return sbh;
    };
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
    public static SyncPattern Powerup2(string sfx1, string sfx2, TP4 color1, TP4 color2, GCXF<float> time1, GCXF<float> itrs1, GCXF<float> delay, GCXF<float> time2) => sbh => {
        uint id = sbh.GCX.NextID();
        float t1 = time1(sbh.GCX);
        SFXService.Request(sfx1);
        sbh.bc.SummonPowerup(sbh, color1, t1, itrs1(sbh.GCX), id);
        float wait = t1 + delay(sbh.GCX);
        SyncPatterns.Reexec(AsyncPatterns._AsGCR(
                Powerup(sfx2, color2, time2, _ => -1),
                GenCtxProperty.Delay(_ => ETime.ENGINEFPS * wait))
            )(sbh);
        return sbh;
    };

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
        ch = _ch;
        parent_index = (props.p_mutater == null) ? ch.gcx.index : (int)props.p_mutater(ch.gcx);
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
            if (method == RV2ControlMethod.ANG) {
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
        ch.gcx.UpdateRules(props.start);
        times = (int)props.times(ch.gcx);
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
        "Mod number must be provided for Mod parametrization. Use the \"tm\" tag and provide the max count as the first number.";
    public static int GetFiringIndex(Parametrization p, int parentIndex, int thisIndex, int? thisRpt) {
        if (p == Parametrization.THIS) return thisIndex;
        if (p == Parametrization.DEFER) return parentIndex;
        if (p == Parametrization.ADDITIVE) return ExM.__Combine(parentIndex, thisIndex);
        
        //Mod handling
        if (p == Parametrization.MOD) {
            if (thisRpt == null) throw new Exception(ModNumberRequired);
            return parentIndex * thisRpt.Value + thisIndex;
        } if (p == Parametrization.INVMOD) {
            if (thisRpt == null) throw new Exception(ModNumberRequired);
            return parentIndex * thisRpt.Value + (thisRpt.Value - 1 - thisIndex);
        }
        throw new NotImplementedException($"Firing handling for parametrization {p}");
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
            SFXService.Request(props.sfx.ModIndex(index));
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
        
    public void FinishIteration(List<GenCtx> childGen) {
        GCX.RV2 = unmutated_rv2;
        //TODO reconciliation
        for (int ii = 0; ii < childGen.Count; ++ii) childGen[ii].Dispose();
        childGen.Clear();
        GCX.FinishIteration(props.postloop, props.PostloopRV2Incr(GCX, times));
    }

    public GenCtx IAmDone() {
        GCX.UpdateRules(props.end);
        return GCX;
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

/// <summary>
/// Enum describing the direction in which a bullet is fired from a parent.
/// </summary>
public enum Facing {
    /// <summary>
    /// Starts from beh.original_angle. This is zero except for summons,
    /// for which it is set to the V2RV2 angle of the summon.
    /// </summary>
    ORIGINAL,
    /// <summary>
    /// Starts from 0.
    /// </summary>
    DEROT, 
    /// <summary>
    /// Starts from the velocity direction of the BEH.
    /// </summary>
    VELOCITY, 
    /// <summary>
    /// Starts from original_angle + the velocity direction.
    /// </summary>
    ROTVELOCITY
}
public class SAOffsetHandler : SummonAlongHandler {
    private readonly GCXF<Vector2> nextLocation;
    public SAOffsetHandler(SAAngle sah, GCXF<float> offsetAngle, GCXF<Vector2> nextLocation): base(sah, offsetAngle) {
        this.nextLocation = nextLocation;
    }

    public override Vector2 Locate(GenCtx gcx) => nextLocation(gcx);
}

}