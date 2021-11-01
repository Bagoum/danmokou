using System;
using System.Collections;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Behavior.Functions;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Graphics;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.VN;
using JetBrains.Annotations;
using Suzunoya.Data;
using SuzunoyaUnity;
using UnityEngine;
using static Danmokou.DMath.Functions.BPYRepo;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.Reflection.CompilerHelpers;
using static Danmokou.Reflection.Compilers;
using static Danmokou.Danmaku.Patterns.AtomicPatterns;
using Object = UnityEngine.Object;
using tfloat = Danmokou.Expressions.TEx<float>;
using static Danmokou.DMath.Functions.ExMConditionals;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using static BagoumLib.Tasks.WaitingUtils;

namespace Danmokou.SM {
/// <summary>
/// All public functions in this repository can be used as LASM state machines.
/// </summary>
[Reflect]
public static class SMReflection {
    private static readonly ReflWrap<Func<float, float, float, ParametricInfo, float>> CrosshairOpacity =
        ReflWrap.FromFunc("SMReflection.CrosshairOpacity", () =>
            CompileDelegate<Func<float, float, float, ParametricInfo, float>, float>(@"
if (> t &fadein,
    if(> t &homesec,
        c(einsine((t - &homesec) / &sticksec)),
        1),
    eoutsine(t / &fadein))",
                new DelegateArg<float>("fadein"),
                new DelegateArg<float>("homesec"),
                new DelegateArg<float>("sticksec"),
                new DelegateArg<ParametricInfo>("bpi", priority: true)
            ));

    #region Effects
    /// <summary>
    /// `crosshair`: Create a crosshair that follows the target for a limited amount of time
    /// and saves the target's position in public data hoisting.
    /// </summary>
    public static TaskPattern Crosshair(string style, GCXF<Vector2> locator, GCXF<float> homeSec, GCXF<float> stickSec,
        ReflectEx.Hoist<Vector2> locSave, ExBPY indexer) {
        var cindexer = GCXF(indexer);
        var saver = Async("_$CROSSHAIR_INVALID", _ => V2RV2.Zero, AsyncPatterns.GCRepeat2(
            x => 1,
            x => ETime.ENGINEFPS_F * homeSec(x),
            GCXFRepo.RV2Zero,
            new[] { GenCtxProperty.SaveV2((locSave, cindexer, locator)) }, new[] {AtomicPatterns.Noop()}
        ));
        var path = Compilers.GCXU(VTPRepo.NROffset(RetrieveHoisted(locSave, indexer)));
        return async smh => {
            float homesec = homeSec(smh.GCX);
            float sticksec = stickSec(smh.GCX);
            locSave.Save((int) cindexer(smh.GCX), locator(smh.GCX));
            if (homesec > 0) 
                ServiceLocator.SFXService.Request("x-crosshair");
            float fadein = Mathf.Max(0.15f, homesec / 5f);
            _ = Sync(style, _ => V2RV2.Zero, SyncPatterns.Loc0(Summon(path,
                new ReflectableLASM(smh2 => {
                    smh2.Exec.DisplayerOrThrow.FadeSpriteOpacity(bpi => CrosshairOpacity.Value(fadein, homesec, sticksec, bpi),
                        homesec + sticksec, smh2.cT, GetAwaiter(out Task t));
                    return t;
                }), new BehOptions())))(smh);
            await saver(smh);
            smh.ThrowIfCancelled();
            ServiceLocator.SFXService.Request("x-lockon");
            await WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, sticksec, false);
        };
    }

    public static TaskPattern dZaWarudo(GCXF<float> time) => ZaWarudo(time, _ => Vector2.zero, null, null, _ => 20);
    public static TaskPattern ZaWarudo(GCXF<float> time, GCXF<Vector2> loc, GCXF<float>? t1r, GCXF<float>? t2r, GCXF<float> scale) => async smh => {
        float t = time(smh.GCX);
        ServiceLocator.SFXService.Request("x-zawarudo");
        var anim = Object.Instantiate(ResourceManager.GetSummonable("negative")).GetComponent<ScaleAnimator>();
        anim.transform.position = loc(smh.GCX);
        anim.AssignScales(0, scale(smh.GCX), 0);
        anim.AssignRatios(t1r?.Invoke(smh.GCX), t2r?.Invoke(smh.GCX));
        anim.Initialize(smh.cT, t);
        using var token = ServiceLocator.FindAll<PlayerController>()
            .SelectDisposable(p => p.AllControlEnabled.AddConst(false));
        foreach (var player in ServiceLocator.FindAll<PlayerController>())
            player.MakeInvulnerable((int)(t * 120), false);
        await WaitingUtils.WaitFor(smh, t, false);
    };

    #endregion
    
    /// <summary>
    /// Autodeletes enemy bullets. Note that some types of bullets (lasers, pathers, large bullets) may be unaffected.
    /// </summary>
    public static TaskPattern ScreenClear() => smh => {
        BulletManager.SoftScreenClear();
        return Task.CompletedTask;
    };
    
    public static TaskPattern dBossExplode(TP4 powerupColor, TP4 powerdownColor) {
        var paOpts = new PowerAuraOptions(new[] {
            PowerAuraOption.Color(powerupColor),
            PowerAuraOption.Time(_ => EventLASM.BossExplodeWait),
            PowerAuraOption.Iterations(_ => 4f),
            PowerAuraOption.Static(), 
            PowerAuraOption.Scale(_ => 2f), 
            PowerAuraOption.Next(new[] {
                PowerAuraOption.Color(powerdownColor),
                PowerAuraOption.Time(_ => 2f),
                PowerAuraOption.Iterations(_ => -1f),
                PowerAuraOption.Static(), 
                PowerAuraOption.Scale(_ => 2f), 
            }),
        });
        var sp = Sync("powerup1", _ => V2RV2.Zero, PowerAura(paOpts));
        var ev = EventLASM.BossExplode();
        return smh => {
            sp(smh);
            return ev(smh);
        };
    }

    #region ScreenManip
    
    /// <summary>
    /// Create screenshake.
    /// </summary>
    /// <param name="magnitude">Magnitude multiplier. Use 1 for a small but noticeable screenshake</param>
    /// <param name="time">Time of screenshake</param>
    /// <param name="by_time">Magnitude multiplier over time</param>
    [Alias("shake")]
    public static TaskPattern Raiko(GCXF<float> magnitude, GCXF<float> time, FXY by_time) => smh => {
        ServiceLocator.Find<IRaiko>().Shake(time(smh.GCX), by_time, magnitude(smh.GCX), smh.cT,
            GetAwaiter(out Task t));
        return t;
    };

    public static TaskPattern dRaiko(GCXF<float> magnitude, GCXF<float> time) => smh => {
        var t = time(smh.GCX);
        ServiceLocator.Find<IRaiko>().Shake(t, null, magnitude(smh.GCX), smh.cT,
            GetAwaiter(out Task tsk));
        return tsk;
    };

    /// <summary>
    /// Rotate the camera around the screen's X-axis.
    /// Note: this returns immediately.
    /// </summary>
    public static TaskPattern SeijaX(float degrees, float time) {
        return smh => {
            smh.Context.PhaseObjects.Add(ServiceLocator.Find<IShaderCamera>().AddXRotation(degrees, time));
            return Task.CompletedTask;
        };
    }
    /// <summary>
    /// Rotate the camera around the screen's Y-axis.
    /// Note: this returns immediately.
    /// </summary>
    public static TaskPattern SeijaY(float degrees, float time) {
        return smh => {
            smh.Context.PhaseObjects.Add(ServiceLocator.Find<IShaderCamera>().AddYRotation(degrees, time));
            return Task.CompletedTask;
        };
    }

    public static TaskPattern StageAnnounce() => smh => {
        ServiceLocator.Find<IStageAnnouncer>().AnnounceStage(smh.cT, out float t);
        return WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, t, false);
    };
    public static TaskPattern StageDeannounce() => smh => {
        ServiceLocator.Find<IStageAnnouncer>().DeannounceStage(smh.cT, out float t);
        return WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, t, false);
    };
    
    #endregion

    public static TaskPattern NoOp() => smh => Task.CompletedTask;

    /// <summary>
    /// Summon a boss, ie. a BEH with its own action handling.
    /// <br/>The boss configuration must be loaded in ResourceManager.
    /// </summary>
    public static TaskPattern Boss(string bossKey) {
        var bossCfg = ResourceManager.GetBoss(bossKey);
        return smh => {
            var beh = Object.Instantiate(bossCfg.boss).GetComponent<BehaviorEntity>();
            beh.phaseController.SetGoTo(1, null);
            return beh.Initialize(SMRunner.CullRoot(StateMachineManager.FromText(bossCfg.stateMachine), smh.cT));
        };
    }

    /// <summary>
    /// Wait for a synchronization event.
    /// </summary>
    [Alias("wait-for")]
    public static TaskPattern Wait(Synchronizer sycnhr) => smh => sycnhr(smh);

    /// <summary>
    /// Wait for a synchronization event and then run the child.
    /// </summary>
    [Alias("_")]
    public static TaskPattern Delay(Synchronizer synchr, StateMachine state) => async smh => {
        await synchr(smh);
        smh.ThrowIfCancelled();
        await state.Start(smh);
    };

    /// <summary>
    /// Run the child and then wait for a synchronization event.
    /// </summary>
    [Alias(">>")]
    public static TaskPattern ThenDelay(Synchronizer synchr, StateMachine state) => async smh => {
        await state.Start(smh);
        smh.ThrowIfCancelled();
        await synchr(smh);
    };
    
    /// <summary>
    /// Run the child nonblockingly and then wait for a synchronization event.
    /// Same as >> SYNCHR ~ STATE.
    /// </summary>
    [Alias(">>~")]
    public static TaskPattern RunDelay(Synchronizer synchr, StateMachine state) => smh => {
        _ = state.Start(smh);
        return synchr(smh);
    };
    
    #region Executors

    /// <summary>
    /// Run the visual novel scene attached to the executing BEH.
    /// TODO: improve save-related handling here.
    /// </summary>
    public static TaskPattern ExecuteVN([LookupMethod] Func<DMKVNState, Task> vnTask, string scriptId) => async smh => {
        // ReSharper disable once UnusedVariable
        var save = await ((DMKVNWrapper) ServiceLocator.Find<IVNWrapper>())
            .ExecuteVN((data, cT) => new DMKVNState(cT, scriptId, data), vnTask, new InstanceData(new GlobalData()), smh.cT);
    };

    /// <summary>
    /// Asynchronous bullet pattern fire.
    /// </summary>
    public static TaskPattern Async(string style, GCXF<V2RV2> rv2, AsyncPattern ap) => smh => {
        var abh = new AsyncHandoff(new DelegatedCreator(smh.Exec, 
                BulletManager.StyleSelector.MergeStyles(smh.ch.bc.style, style)), GetAwaiter(out Task t), smh);
        abh.ch.gcx.OverrideRV2(rv2(smh.GCX) + smh.GCX.RV2);
        smh.RunTryPrependRIEnumerator(ap(abh));
        return t;
    };
    /// <summary>
    /// Synchronous bullet pattern fire.
    /// </summary>
    public static TaskPattern Sync(string style, GCXF<V2RV2> rv2, SyncPattern sp) => smh => {
        using var sbh = new SyncHandoff(new DelegatedCreator(smh.Exec,
            BulletManager.StyleSelector.MergeStyles(smh.ch.bc.style, style), null), smh);
        sbh.ch.gcx.OverrideRV2(rv2(smh.GCX) + smh.GCX.RV2);
        sp(sbh);
        return Task.CompletedTask;
    };

    /// <summary>
    /// = async(_, RV2, AP)
    /// </summary>
    [Alias("sts")]
    public static TaskPattern AsyncStageFire(GCXF<V2RV2> rv2, AsyncPattern ap) => Async("_", rv2, ap);
    
    /// <summary>
    /// = async(_, RV2, isetp(P, AP))
    /// </summary>
    [Alias("stsp")]
    public static TaskPattern AsyncStageFireP(GCXF<float> p, GCXF<V2RV2> rv2, AsyncPattern ap) => 
        AsyncStageFire(rv2, AsyncPatterns.ISetP(p, ap));

    public static TaskPattern CreateShot1(V2RV2 rv2, float speed, float angle, string style) =>
        Sync(style, _ => rv2, AtomicPatterns.S(Compilers.GCXU(VTPRepo.RVelocity(Parametrics.CR(speed, angle)))));

    public static TaskPattern CreateShot2(float x, float y, float speed, float angle, string style) =>
        CreateShot1(new V2RV2(0, 0, x, y, 0), speed, angle, style);
    

    public static TaskPattern Dialogue(string file) {
        StateMachine? sm = null;
        return smh => {
            Logs.Log($"Opening dialogue section {file}");
            sm ??= StateMachineManager.LoadDialogue(file);
            return sm.Start(smh);
        };
    }

    /// <summary>
    /// Play a sound.
    /// </summary>
    public static TaskPattern SFX(string sfx) => smh => {
        ServiceLocator.SFXService.Request(sfx);
        return Task.CompletedTask;
    };

    /// <summary>
    /// Save some information in public data hoisting.
    /// <br/>Public data hoisting is two-layer: it requires a name and an index.
    /// </summary>
    [GAlias(typeof(float), "SaveF")]
    [GAlias(typeof(Vector2), "SaveV2")]
    public static TaskPattern Save<T>(ReflectEx.Hoist<T> name, GCXF<float> indexer, GCXF<T> valuer) => smh => {
        name.Save((int) indexer(smh.GCX), valuer(smh.GCX));
        return Task.CompletedTask;
    };

    
    /// <summary>
    /// Apply a controller function to individual entities.
    /// </summary>
    [GAlias(typeof(BulletManager.cBulletControl), "BulletControl")]
    [GAlias(typeof(BehaviorEntity.cBEHControl), "BEHControl")]
    [GAlias(typeof(CurvedTileRenderLaser.cLaserControl), "LaserControl")]
    public static TaskPattern ParticleControl<CF>(Pred persist, BulletManager.StyleSelector style, CF control) {
        return smh => {
            if (control is BehaviorEntity.cBEHControl bc)
                //Use .Root because stage-enemy-generated controls should be bounded by the stage cT
                BehaviorEntity.ControlPool(persist, style, bc, smh.cT.Root);
            else if (control is CurvedTileRenderLaser.cLaserControl lc)
                CurvedTileRenderLaser.ControlPool(persist, style, lc, smh.cT.Root);
            else if (control is BulletManager.cBulletControl pc)
                BulletManager.ControlPool(persist, style, pc, smh.cT.Root);
            else throw new Exception("Couldn't realize bullet-control type");
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Apply a controller function to a pool of entities.
    /// </summary>
    [GAlias(typeof(SPCF), "PoolControl")]
    [GAlias(typeof(BehPF), "BEHPoolControl")]
    [GAlias(typeof(LPCF), "LaserPoolControl")]
    public static TaskPattern PoolControl<CF>(BulletManager.StyleSelector style, CF control) => smh => {
        if      (control is BehPF bc) 
            smh.Context.PhaseObjects.Add(BehaviorEntity.ControlPool(style, bc, smh.cT.Root));
        else if (control is LPCF lc) 
            smh.Context.PhaseObjects.Add(CurvedTileRenderLaser.ControlPool(style, lc, smh.cT.Root));
        else if (control is SPCF pc) 
            smh.Context.PhaseObjects.Add(BulletManager.ControlPool(style, pc, smh.cT.Root));
        else throw new Exception("Couldn't realize pool-control type");
        return Task.CompletedTask;
    };
    
    #endregion

    #region BEHManip

    /// <summary>
    /// Change the running phase.
    /// </summary>
    public static TaskPattern ShiftPhaseTo(int toPhase) => smh => {
        if (toPhase != -1)
            smh.Exec.phaseController.LowPriorityGoTo(toPhase);
        smh.Exec.ShiftPhase();
        return Task.CompletedTask;
    };

    /// <summary>
    /// Go to the next phase.
    /// </summary>
    public static TaskPattern ShiftPhase() => ShiftPhaseTo(-1);

    /// <summary>
    /// Kill this entity (no death effects).
    /// </summary>
    public static TaskPattern Cull() => smh => {
        smh.Exec.InvokeCull();
        return Task.CompletedTask;
    };
    /// <summary>
    /// Kill this entity (death effects included).
    /// </summary>
    public static TaskPattern Poof() => smh => {
        smh.Exec.Poof();
        return Task.CompletedTask;
    };

    /// <summary>
    /// Move the executing entity, but cancel movement if the predicate is false.
    /// </summary>
    public static TaskPattern MoveWhile(GCXF<float> time, Pred? condition, GCXU<VTP> path) => smh => {
        uint randId = RNG.GetUInt();
        var fctx = FiringCtx.New();
        var epath = path(smh.GCX, fctx);
        var old_override = smh.GCX.idOverride;
        //Note that this use case is limited to when the BEH is provided a temporary new ID (ie. only in MoveWhile).
        //I may deprecate this by having the move function not use a new ID.
        smh.GCX.idOverride = randId;
        var etime = time(smh.GCX);
        smh.GCX.idOverride = old_override;
        var cor = smh.Exec.ExecuteVelocity(new LimitedTimeMovement(epath, etime, 
                FuncExtensions.Then(() => fctx.Dispose(),
                GetAwaiter(out Task t)), smh.cT, 
                new ParametricInfo(Vector2.zero, smh.GCX.index, randId, ctx: fctx), condition));
        smh.RunTryPrependRIEnumerator(cor);
        return t;
    };

    /// <summary>
    /// Move the executing entity.
    /// </summary>
    public static TaskPattern Move(GCXF<float> time, GCXU<VTP> path) => MoveWhile(time, null, path);
    
    /// <summary>
    /// Move the executing entity to a target position over time. This has zero error.
    /// </summary>
    public static TaskPattern MoveTarget(ExBPY time, [LookupMethod] Func<tfloat, tfloat> ease, ExTP target) 
        => Move(GCXF(time), Compilers.GCXU(
            VTPRepo.NROffset(Parametrics.EaseToTarget(ease, time, target))));

    /// <summary>
    /// Move to a target position, run a state machine, and then move to another target position.
    /// </summary>
    public static TaskPattern MoveWrap(ExBPY t1, ExTP target1, ExBPY t2, ExTP target2, StateMachine wrapped) {
        var w1 = MoveTarget(t1, ExMEasers.EOutSine, target1);
        var w2 = MoveTarget(t2, ExMEasers.EInSine, target2);
        return async smh => {
            await w1(smh);
            smh.ThrowIfCancelled();
            await wrapped.Start(smh);
            smh.ThrowIfCancelled();
            await w2(smh);
        };
    }

    /// <summary>
    /// Move to a target position, run a state machine nonblockingly, wait for a synchronization event,
    /// and then move to another target position.
    /// </summary>
    [Alias("MoveWrap~")]
    public static TaskPattern MoveWrapFixedDelay(Synchronizer s, ExBPY t1, ExTP target1, ExBPY t2, 
        ExTP target2, StateMachine wrapped) 
        => MoveWrap(t1, target1, t2, target2, new ReflectableLASM(RunDelay(s, wrapped)));
    
    /// <summary>
    /// Run a state machine nonblockingly, move to a target position, wait for a synchronization event,
    /// and then move to another target position.
    /// </summary>
    [Alias("MoveWrap~~")]
    public static TaskPattern MoveWrapFixedDelayNB(Synchronizer s, ExBPY t1, ExTP target1, ExBPY t2, 
        ExTP target2, StateMachine wrapped) {
        var mover = MoveWrapFixedDelay(s, t1, target1, t2, target2, noop);
        return smh => {
            _ = wrapped.Start(smh);
            return mover(smh);
        };
    }
    
    private static readonly StateMachine noop = new ReflectableLASM(NoOp());


    /// <summary>
    /// Move-wrap, but the enemy is set invincible until the wrapped SM starts.
    /// </summary>
    public static TaskPattern IMoveWrap(ExBPY t1, ExTP target1, ExBPY t2, ExTP target2, StateMachine wrapped) {
        var w1 = MoveTarget(t1, ExMEasers.EOutSine, target1);
        var w2 = MoveTarget(t2, ExMEasers.EInSine, target2);
        return async smh => {
            if (smh.Exec.isEnemy) smh.Exec.Enemy.SetVulnerable(Vulnerability.NO_DAMAGE);
            await w1(smh);
            smh.ThrowIfCancelled();
            if (smh.Exec.isEnemy) smh.Exec.Enemy.SetVulnerable(Vulnerability.VULNERABLE);
            await wrapped.Start(smh);
            smh.ThrowIfCancelled();
            await w2(smh);
        };
    }

    /// <summary>
    /// Set the position of the executing BEH in world coordinates, with X and Y as separate arguments.
    /// </summary>
    public static TaskPattern Position(GCXF<float> x, GCXF<float> y) => smh => {
        smh.Exec.ExternalSetLocalPosition(new Vector2(x(smh.GCX), y(smh.GCX)));
        return Task.CompletedTask;
    };
    /// <summary>
    /// Set the position of the executing BEH in world coordinates using one Vector2.
    /// </summary>
    public static TaskPattern Pos(GCXF<Vector2> xy) => smh => {
        smh.Exec.ExternalSetLocalPosition(xy(smh.GCX));
        return Task.CompletedTask;
    };

    /// <summary>
    /// Link this entity's HP pool to another enemy. The other enemy will serve as the source and this will simply redirect damage.
    /// </summary>
    public static TaskPattern DivertHP(BEHPointer target) => smh => {
        smh.Exec.Enemy.DivertHP(target.Beh.Enemy);
        return Task.CompletedTask;
    };

    public static TaskPattern Vulnerable(GCXF<bool> isVulnerable) => smh => {
        smh.Exec.Enemy.SetVulnerable(isVulnerable(smh.GCX) ? Vulnerability.VULNERABLE : Vulnerability.NO_DAMAGE);
        return Task.CompletedTask;
    };
    
    public static TaskPattern FadeSprite(BPY fader, GCXF<float> time) => smh => {
        smh.Exec.DisplayerOrThrow.FadeSpriteOpacity(fader, time(smh.GCX), smh.cT, GetAwaiter(out Task t));
        return t;
    };

    public static TaskPattern Scale(BPY scaler, GCXF<float> time) => smh => {
        smh.Exec.DisplayerOrThrow.Scale(scaler, time(smh.GCX), smh.cT, GetAwaiter(out Task t));
        return t;
    };
    
    #endregion
    
    #region Slowdown

    /// <summary>
    /// Create a global slowdown effect. Note this will only reset when the nesting context (usually a phase)
    ///  is cancelled.
    /// </summary>
    public static TaskPattern Slowdown(GCXF<float> ratio) => smh => {
        smh.Context.PhaseObjects.Add(ETime.Slowdown.AddConst(ratio(smh.GCX)));
        return Task.CompletedTask;
    };
    /// <summary>
    /// Create a global slowdown effect for a limited amount of time.
    /// </summary>
    public static TaskPattern SlowdownFor(Synchronizer time, GCXF<float> ratio) => async smh => {
        using var t = ETime.Slowdown.AddConst(ratio(smh.GCX));
        await time(smh);
    };

    #endregion
    
    #region Shortcuts

    public static TaskPattern DangerBot() => Sync("danger", _ => V2RV2.Rot(-1.7f, -6), 
        "gsr2 2 <3.4;:> { root zero } summonsup tpnrot py lerp3 1 1.5 2 2.5 t 2.2 0 -2 stall".Into<SyncPattern>());
    
    public static TaskPattern DangerTop() => Sync("danger", _ => V2RV2.Rot(-1.7f, 6), 
        "gsr2 2 <3.4;:> { root zero } summonsup tpnrot py lerp3 1 1.5 2 2.5 t -2.2 0 2 stall".Into<SyncPattern>());

    public static TaskPattern DangerLeft() => Async("danger", _ => V2RV2.Rot(-5.5f, -3f),
        "gcr2 24 4 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t 2.2 0 -2 stall".Into<AsyncPattern>());
    
    public static TaskPattern DangerRight() => Async("danger", _ => V2RV2.Rot(5.5f, -3f),
        "gcr2 24 4 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t -2.2 0 2 stall".Into<AsyncPattern>());

    public static TaskPattern DangerLeft2() => Async("danger", _ => V2RV2.Rot(-5.5f, -1f),
        "gcr2 24 2 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t 2.2 0 -2 stall".Into<AsyncPattern>());
    
    public static TaskPattern DangerRight2() => Async("danger", _ => V2RV2.Rot(5.5f, -1f),
        "gcr2 24 2 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t -2.2 0 2 stall".Into<AsyncPattern>());
    
    #endregion
    
    #region PlayerFiring

    public static TaskPattern Fire(StateMachine freeFire, StateMachine freeCancel, StateMachine focusFire,
        StateMachine focusCancel) =>
        async smh => {
            var o = smh.Exec as FireOption ??
                    throw new Exception("Cannot use fire command on a BehaviorEntity that is not an Option");
            if (!o.Player.IsFiring) await WaitingUtils.WaitForUnchecked(o, smh.cT, () => o.Player.IsFiring);
            smh.ThrowIfCancelled();
            var (firer, onCancel, inputReq) = o.Player.IsFocus ?  
                (focusFire, focusCancel, (Func<bool>) (() => o.Player.IsFocus)) :
                (freeFire, freeCancel, (Func<bool>) (() => !o.Player.IsFocus));
            var joint_smh = smh.CreateJointCancellee(out var fireCTS, null);
            //order is important to ensure cancellation works on the correct frame
            var waiter = WaitingUtils.WaitForUnchecked(o, smh.cT, () => !o.Player.IsFiring || !inputReq());
            _ = firer.Start(joint_smh).ContinueWithSync(joint_smh.Dispose);
            await waiter;
            fireCTS.Cancel();
            smh.ThrowIfCancelled();
            if (o.Player.AllowPlayerInput) _ = onCancel.Start(smh);
        };
    public static TaskPattern FireSame(StateMachine fire, StateMachine cancel) =>
        async smh => {
            var o = smh.Exec as FireOption ??
                    throw new Exception("Cannot use fire command on a BehaviorEntity that is not an Option");
            if (!o.Player.IsFiring) await WaitingUtils.WaitForUnchecked(o, smh.cT, () => o.Player.IsFiring);
            smh.ThrowIfCancelled();
            var joint_smh = smh.CreateJointCancellee(out var fireCTS, null);
            //order is important to ensure cancellation works on the correct frame
            var waiter = WaitingUtils.WaitForUnchecked(o, smh.cT, () => !o.Player.IsFiring);
            _ = fire.Start(joint_smh).ContinueWithSync(joint_smh.Dispose);
            await waiter;
            fireCTS.Cancel();
            smh.ThrowIfCancelled();
            if (o.Player.AllowPlayerInput) _ = cancel.Start(smh);
        };


    public static TaskPattern AssertSimple(string p_pool) => smh => {
        BulletManager.GetOrMakePlayerCopy(p_pool);
        return Task.CompletedTask;
    };
    public static TaskPattern AssertComplex(string p_pool) => smh => {
        BulletManager.GetOrMakeComplexPlayerCopy(p_pool);
        return Task.CompletedTask;
    };

    #endregion
    
    #region Utility

    /// <summary>
    /// Print a message to the console.
    /// </summary>
    public static TaskPattern Debug(string debug) => smh => {
        Logs.Log(debug, false, LogLevel.INFO);
        return Task.CompletedTask;
    };
    
    
    /// <summary>
    /// Select one of several state machines depending on which player is currently in use.
    /// Disambiguates based on the "key" property of the PlayerConfig.
    /// </summary>
    public static TaskPattern PlayerVariant((string key, StateMachine exec)[] options) => smh => {
        if (GameManagement.Instance.Player == null)
            throw new Exception("Cannot use PlayerVariant state machine when there is no player");
        for (int ii = 0; ii < options.Length; ++ii) {
            if (GameManagement.Instance.Player.key == options[ii].key) {
                return options[ii].exec.Start(smh);
            }
        }
        throw new Exception("Could not find a matching player variant option for player " +
            $"{GameManagement.Instance.Player.key}");
    };

    /// <summary>
    /// Convert excess health to score at the given rate.
    /// This is done with SFX over time and will not return immediately.
    /// </summary>
    public static TaskPattern LifeToScore(int value) => smh => {
        smh.RunRIEnumerator(_LifeToScore(value, smh.cT, GetAwaiter(out Task t)));
        return t;
    };

    private static IEnumerator _LifeToScore(int value, ICancellee cT, Action done) {
        while (GameManagement.Instance.Lives > 1 && !cT.Cancelled) {
            GameManagement.Instance.SwapLifeScore(value, true);
            for (int ii = 0; ii < 60; ++ii) {
                yield return null;
                if (cT.Cancelled) break;
            }
        }
        done();
    }


    #endregion
}

}