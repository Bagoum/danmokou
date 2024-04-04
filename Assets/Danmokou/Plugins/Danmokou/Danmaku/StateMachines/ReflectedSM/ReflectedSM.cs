using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
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
using Danmokou.Reflection2;
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
using static Danmokou.Core.RUWaitingUtils;

namespace Danmokou.SM {
/// <summary>
/// All public functions in this repository can be used as LASM state machines.
/// </summary>
[Reflect]
public static class SMReflection {
    private static readonly ReflWrap<Func<float, float, float, ParametricInfo, float>> CrosshairOpacity =
        ReflWrap.FromFunc("SMReflection.CrosshairOpacity", () =>
            Reflection2.Helpers.ParseAndCompileDelegate<Func<float, float, float, ParametricInfo, float>>(@"
t > fadein ?
    (t > homesec ?
        c(einsine((t - homesec) / sticksec)) :
        1) :
    eoutsine(t / fadein)",
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
    public static ReflectableLASM Crosshair(string style, GCXF<Vector2> locator, GCXF<float> homeSec, GCXF<float> stickSec,
        ReflectEx.Hoist<Vector2> locSave, ExBPY indexer) {
        var cindexer = GCXF(indexer);
        var saver = Async("_$CROSSHAIR_INVALID", _ => V2RV2.Zero, AsyncPatterns.GCRepeat2(
            x => 1,
            x => ETime.ENGINEFPS_F * homeSec(x),
            GCXFRepo.RV2Zero,
            new[] { GenCtxProperty.SaveV2((locSave, cindexer, locator)) }, new[] {AtomicPatterns.Noop()}
        ));
        var path = Compilers.VTP(VTPRepo.NROffset(RetrieveHoisted(locSave, indexer)));
        return new(async smh => {
            float homesec = homeSec(smh.GCX);
            float sticksec = stickSec(smh.GCX);
            locSave.Save((int) cindexer(smh.GCX), locator(smh.GCX));
            if (homesec > 0) 
                ISFXService.SFXService.Request("x-crosshair");
            float fadein = Mathf.Max(0.15f, homesec / 5f);
            _ = Sync(style, _ => V2RV2.Zero, SyncPatterns.Loc0(Summon(path,
                new ReflectableLASM(smh2 => {
                    smh2.Exec.DisplayerOrThrow.FadeSpriteOpacity(bpi => CrosshairOpacity.Value(fadein, homesec, sticksec, bpi),
                        homesec + sticksec, smh2.cT, GetAwaiter(out Task t));
                    return t;
                }), new BehOptions()))).Start(smh);
            await saver.Start(smh);
            smh.ThrowIfCancelled();
            ISFXService.SFXService.Request("x-lockon");
            await RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, sticksec, false);
        });
    }

    public static ReflectableLASM dZaWarudo(GCXF<float> time) => ZaWarudo(time, _ => Vector2.zero, null, null, _ => 20);
    public static ReflectableLASM ZaWarudo(GCXF<float> time, GCXF<Vector2> loc, GCXF<float>? t1r, GCXF<float>? t2r, GCXF<float> scale) => new(async smh => {
        float t = time(smh.GCX);
        ISFXService.SFXService.Request("x-zawarudo");
        var anim = Object.Instantiate(ResourceManager.GetSummonable("negative")).GetComponent<ScaleAnimator>();
        anim.transform.position = loc(smh.GCX);
        anim.AssignScales(0, scale(smh.GCX), 0);
        anim.AssignRatios(t1r?.Invoke(smh.GCX), t2r?.Invoke(smh.GCX));
        anim.Initialize(smh.cT, t);
        using var token = PlayerController.AllControlEnabled.AddConst(false);
        foreach (var pc in ServiceLocator.FindAll<PlayerController>()) {
            pc.MakeInvulnerable((int)(t * 120), false);
        };
        await RUWaitingUtils.WaitFor(smh.Exec, smh.cT, t, false);
    });

    #endregion
    
    /// <summary>
    /// Autodeletes enemy bullets. Note that some types of bullets (lasers, pathers, large bullets) may be unaffected.
    /// </summary>
    public static ReflectableLASM ScreenClear() => new(smh => {
        BulletManager.SoftScreenClear();
        return Task.CompletedTask;
    });
    
    public static ReflectableLASM dBossExplode(TP4 powerupColor, TP4 powerdownColor) {
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
        return new(smh => {
            sp.Start(smh);
            return ev(smh);
        });
    }

    /// <summary>
    /// Spawn an effect at the given location.
    /// </summary>
    public static ReflectableLASM Effect(string effect, GCXF<Vector2> loc) {
        var eff = ResourceManager.GetEffect(effect);
        return new(smh => {
            eff.Proc(loc(smh.GCX));
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// (For debugging use) Skip forward in time in the engine. The player will be invulnerable during this time.
    /// </summary>
    public static ReflectableLASM SkipTime(float seconds) => new(smh => {
        foreach (var player in ServiceLocator.FindAll<PlayerController>())
            player.MakeInvulnerable((int)(seconds * 120) + 200, false);
        ETime.SkipTime(seconds);
        return Task.CompletedTask;
    });

    #region ScreenManip
    
    /// <summary>
    /// Create screenshake.
    /// </summary>
    /// <param name="magnitude">Magnitude multiplier. Use 1 for a small but noticeable screenshake</param>
    /// <param name="time">Time of screenshake</param>
    /// <param name="by_time">Magnitude multiplier over time</param>
    [Alias("shake")]
    public static ReflectableLASM Raiko(GCXF<float> magnitude, GCXF<float> time, FXY by_time) => new(smh => {
        ServiceLocator.Find<IRaiko>().Shake(time(smh.GCX), by_time, magnitude(smh.GCX), smh.cT,
            GetAwaiter(out Task t));
        return t;
    });

    public static ReflectableLASM dRaiko(GCXF<float> magnitude, GCXF<float> time) => new(smh => {
        var t = time(smh.GCX);
        ServiceLocator.Find<IRaiko>().Shake(t, null, magnitude(smh.GCX), smh.cT,
            GetAwaiter(out Task tsk));
        return tsk;
    });

    /// <summary>
    /// Rotate the camera around the screen's X-axis.
    /// Note: this returns immediately.
    /// </summary>
    public static ReflectableLASM SeijaX(float degrees, float time) => new(smh => {
        smh.Context.PhaseObjects.Add(ServiceLocator.Find<IShaderCamera>().AddXRotation(degrees, time));
        return Task.CompletedTask;
    });
    
    /// <summary>
    /// Rotate the camera around the screen's Y-axis.
    /// Note: this returns immediately.
    /// </summary>
    public static ReflectableLASM SeijaY(float degrees, float time) {
        return new(smh => {
            smh.Context.PhaseObjects.Add(ServiceLocator.Find<IShaderCamera>().AddYRotation(degrees, time));
            return Task.CompletedTask;
        });
    }

    public static ReflectableLASM StageAnnounce() => new(smh => {
        ServiceLocator.Find<IStageAnnouncer>().AnnounceStage(smh.cT, out float t);
        return RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, t, false);
    });
    
    public static ReflectableLASM StageDeannounce() => new(smh => {
        ServiceLocator.Find<IStageAnnouncer>().DeannounceStage(smh.cT, out float t);
        return RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, t, false);
    });
    
    #endregion

    public static ReflectableLASM NoOp() => new(smh => Task.CompletedTask);

    /// <summary>
    /// Summon a boss, ie. a BEH with its own action handling.
    /// <br/>The boss configuration must be loaded in ResourceManager.
    /// </summary>
    public static ReflectableLASM Boss(string bossKey) {
        var bossCfg = ResourceManager.GetBoss(bossKey);
        return new(smh => {
            var beh = Object.Instantiate(bossCfg.boss).GetComponent<BehaviorEntity>();
            if (smh.Context.LoadCheckpoint is { } ch && ch.StagePhase == (smh.Context as PhaseContext)?.Index && ch.BossCheckpoint is {} bc && bc.boss == bossCfg)
                beh.phaseController.SetGoTo(bc.phase);
            else
                beh.phaseController.SetGoTo(1);
            return beh.RunBehaviorSM(SMRunner.CullRoot(StateMachineManager.FromText(bossCfg.stateMachine)!, smh.cT));
        });
    }
    
    /// <summary>
    /// Run arbitrary code as a StateMachine.
    /// </summary>
    [DontReflect]
    public static ReflectableLASM Exec(ErasedGCXF code) => new(smh => {
        code(smh.GCX);
        return Task.CompletedTask;
    });

    /// <summary>
    /// Run arbitrary code as a StateMachine, SyncPattern, or AsyncPattern.
    /// </summary>
    [BDSL2Only] [RestrictTypes(0, typeof(StateMachine), typeof(SyncPattern), typeof(AsyncPattern))]
    public static T Exec<T>(ErasedGCXF code) where T : class {
        if (typeof(T) == typeof(StateMachine))
            return (Exec(code) as T)!;
        if (typeof(T) == typeof(SyncPattern))
            return (SyncPatterns.Exec(code) as T)!;
        if (typeof(T) == typeof(AsyncPattern))
            return (AsyncPatterns.Exec(code) as T)!;
        throw new Exception($"Incorrect type provided to {nameof(Exec)}: {typeof(T).RName()}");
    }

    /// <summary>
    /// Run some code that returns a StateMachine, and then execute that StateMachine.
    /// </summary>
    [BDSL2Only]
    public static ReflectableLASM Wrap(GCXF<StateMachine> code) => new(async smh => {
        var inner = code(smh.GCX);
        await inner.Start(smh);
        //The created SM has a mirrored envframe on it; since we are no longer using the SM, we should free the EF.
        //If we were to assign or return the SM, then we'd have to keep the EF alive.
        if (inner is EnvFrameAttacher efa) {
            efa.EnvFrame?.Free();
            efa.EnvFrame = null;
        } else if (inner is EFStateMachine efsm) {
            efsm.EnvFrame?.Free();
            efsm.EnvFrame = null;
        }
    });

    /// <summary>
    /// Wait for a synchronization event.
    /// </summary>
    [Alias("wait-for")]
    public static ReflectableLASM Wait(Synchronizer sycnhr) => new(smh => sycnhr(smh));

    /// <summary>
    /// Wait for a synchronization event and then run the child.
    /// </summary>
    [Alias("_")]
    public static ReflectableLASM Delay(Synchronizer synchr, StateMachine state) => new(async smh => {
        await synchr(smh);
        smh.ThrowIfCancelled();
        await state.Start(smh);
    });

    /// <summary>
    /// Run the child and then wait for a synchronization event.
    /// </summary>
    [Alias(">>")]
    public static ReflectableLASM ThenDelay(Synchronizer synchr, StateMachine state) => new(async smh => {
        await state.Start(smh);
        smh.ThrowIfCancelled();
        await synchr(smh);
    });
    
    /// <summary>
    /// Run the child nonblockingly and then wait for a synchronization event.
    /// Same as >> SYNCHR ~ STATE.
    /// </summary>
    [Alias(">>~")]
    public static ReflectableLASM RunDelay(Synchronizer synchr, StateMachine state) => new(smh => {
        _ = state.Start(smh);
        return synchr(smh);
    });
    
    #region Executors

    /// <summary>
    /// Run the provided visual novel script, disabling <see cref="PlayerController"/> input while it is running.
    /// </summary>
    /// <param name="vnTask">Visual novel script function.</param>
    /// <param name="scriptId">Description of the script used when printing debug messages.</param>
    /// <returns></returns>
    public static ReflectableLASM ExecuteVN([LookupMethod] Func<DMKVNState, Task> vnTask, string scriptId) => new(async smh => {
        using var _ = PlayerController.AllControlEnabled.AddConst(false);
        foreach (var pc in ServiceLocator.FindAll<PlayerController>())
            pc.ResetInput();
        var vn = new DMKVNState(smh.cT, scriptId, GameManagement.Instance.VNData);
        var exec = ServiceLocator.Find<IVNWrapper>().TrackVN(vn);
        Logs.Log($"Starting VN script {vn}");
        ServiceLocator.Find<IVNBacklog>().TryRegister(exec);
        try {
            await vnTask(vn);
            vn.UpdateInstanceData();
        } catch (OperationCanceledException e) {
            Logs.LogException(e);
            //Don't throw upwards if the VN was cancelled locally
            if (smh.Cancelled)
                throw;
        } finally {
            Logs.Log(
                $"Completed VN script {vn}. Final completion: {vn.CToken.ToCompletion()}");
            vn.DeleteAll();
        }
    });

    /// <summary>
    /// Asynchronous bullet pattern fire.
    /// </summary>
    public static ReflectableLASM Async(string style, GCXF<V2RV2> rv2, AsyncPattern ap) => new(smh => {
        var abh = new AsyncHandoff(new DelegatedCreator(smh.Exec, 
                BulletManager.StyleSelector.MergeStyles(smh.ch.bc.style, style)), GetAwaiter(out Task t), smh,
                rv2(smh.GCX) + (smh.ch.rv2Override.Try(out var o) ? o : 
                    (smh.GCX.AutoVars is AutoVars.GenCtx ? smh.GCX.RV2 : V2RV2.Zero))
                );
        smh.RunTryPrependRIEnumerator(ap.Run(abh));
        //don't dispose ABH since we didn't copy GCX
        return t;
    });
    
    /// <summary>
    /// Synchronous bullet pattern fire.
    /// </summary>
    public static ReflectableLASM Sync(string style, GCXF<V2RV2> rv2, SyncPattern sp) => new(smh => {
        var sbh = new SyncHandoff(new DelegatedCreator(smh.Exec,
            BulletManager.StyleSelector.MergeStyles(smh.ch.bc.style, style), null), smh,
            rv2(smh.GCX) + (smh.ch.rv2Override.Try(out var o) ? o : 
                (smh.GCX.AutoVars is AutoVars.GenCtx ? smh.GCX.RV2 : V2RV2.Zero))
            );
        sp.Run(sbh);
        //don't dispose SBH since we didn't copy GCX
        return Task.CompletedTask;
    });

    public static ReflectableLASM CreateShot1(V2RV2 rv2, float speed, float angle, string style) =>
        Sync(style, _ => rv2, AtomicPatterns.S(Compilers.VTP(VTPRepo.RVelocity(Parametrics.CR(speed, angle)))));

    public static ReflectableLASM CreateShot2(float x, float y, float speed, float angle, string style) =>
        CreateShot1(new V2RV2(0, 0, x, y, 0), speed, angle, style);

    public static ReflectableLASM Dialogue(string file) {
        StateMachine? sm = null;
        return new(smh => {
            Logs.Log($"Opening dialogue section {file}");
            sm ??= StateMachineManager.LoadDialogue(file);
            return sm.Start(smh);
        });
    }

    /// <summary>
    /// Play a sound.
    /// </summary>
    public static ReflectableLASM SFX(string sfx) => new(smh => {
        ISFXService.SFXService.Request(sfx);
        return Task.CompletedTask;
    });

    /// <summary>
    /// Save some information in public data hoisting.
    /// <br/>Public data hoisting is two-layer: it requires a name and an index.
    /// </summary>
    [GAlias("SaveF", typeof(float))]
    [GAlias("SaveV2", typeof(Vector2))]
    public static ReflectableLASM Save<T>(ReflectEx.Hoist<T> name, GCXF<float> indexer, GCXF<T> valuer) => new(smh => {
        name.Save((int) indexer(smh.GCX), valuer(smh.GCX));
        return Task.CompletedTask;
    });

    /// <summary>
    /// Apply bullet controls to simple bullet pools.
    /// </summary>
    public static ReflectableLASM BulletControl(GCXF<bool> persist, BulletManager.StyleSelector style,
        BulletManager.cBulletControl control) => new(smh => {
        BulletManager.ControlBullets(smh.GCX, persist, style, control, smh.cT.Root);
        return Task.CompletedTask;
    });
    
    /// <summary>
    /// Apply bullet controls to BEH bullet pools.
    /// </summary>
    public static ReflectableLASM BEHControl(GCXF<bool> persist, BulletManager.StyleSelector style,
        BehaviorEntity.cBEHControl control) => new(smh => {
        BehaviorEntity.ControlBullets(smh.GCX, persist, style, control, smh.cT.Root);
        return Task.CompletedTask;
    });
    
    /// <summary>
    /// Apply laser-specific bullet controls to lasers.
    /// </summary>
    public static ReflectableLASM LaserControl(GCXF<bool> persist, BulletManager.StyleSelector style,
        CurvedTileRenderLaser.cLaserControl control) => new(smh => {
        CurvedTileRenderLaser.ControlLasers(smh.GCX, persist, style, control, smh.cT.Root);
        return Task.CompletedTask;
    });

    /// <summary>
    /// Apply a controller function to a pool of entities.
    /// </summary>
    [GAlias("PoolControl", typeof(SPCF), reflectOriginal:false)]
    [GAlias("BEHPoolControl", typeof(BehPF), reflectOriginal:false)]
    [GAlias("LaserPoolControl", typeof(LPCF), reflectOriginal:false)]
    public static ReflectableLASM PoolControl<CF>(BulletManager.StyleSelector style, CF control) => new(smh => {
        if      (control is BehPF bc) 
            smh.Context.PhaseObjects.Add(BehaviorEntity.ControlPool(style, bc, smh.cT.Root));
        else if (control is LPCF lc) 
            smh.Context.PhaseObjects.Add(CurvedTileRenderLaser.ControlPool(style, lc, smh.cT.Root));
        else if (control is SPCF pc) 
            smh.Context.PhaseObjects.Add(BulletManager.ControlPool(style, pc, smh.cT.Root));
        else throw new Exception("Couldn't realize pool-control type");
        return Task.CompletedTask;
    });
    
    #endregion

    #region BEHManip

    /// <summary>
    /// Change the running phase.
    /// </summary>
    public static ReflectableLASM ShiftPhaseTo(int toPhase) => new(smh => {
        if (toPhase != -1)
            smh.Exec.phaseController.LowPriorityGoTo(toPhase);
        smh.Exec.ShiftPhase();
        return Task.CompletedTask;
    });

    /// <summary>
    /// Go to the next phase.
    /// </summary>
    public static ReflectableLASM ShiftPhase() => ShiftPhaseTo(-1);

    /// <summary>
    /// Kill this entity (no death effects).
    /// </summary>
    public static ReflectableLASM Cull() => new(smh => {
        smh.Exec.InvokeCull();
        return Task.CompletedTask;
    });
    
    /// <summary>
    /// Kill this entity (death effects included).
    /// </summary>
    public static ReflectableLASM Poof() => new(smh => {
        smh.Exec.Poof();
        return Task.CompletedTask;
    });

    /// <summary>
    /// Move the executing entity, but cancel movement if the predicate is false.
    /// </summary>
    public static ReflectableLASM MoveWhile(GCXF<float> time, Pred? condition, VTP path) => new(smh => {
        uint randId = RNG.GetUInt();
        var old_override = smh.GCX.idOverride;
        //Note that this use case is limited to when the BEH is provided a temporary new ID (ie. only in MoveWhile).
        //I may deprecate this by having the move function not use a new ID.
        smh.GCX.idOverride = randId;
        var etime = time(smh.GCX);
        smh.GCX.idOverride = old_override;
        var fctx = smh.GCX.DeriveFCTX();
        var cor = smh.Exec.ExecuteVelocity(new LimitedTimeMovement(path, etime,
            FuncExtensions.Then(() => fctx.Dispose(),
                GetAwaiter(out Task t)), smh.cT,
            new ParametricInfo(fctx, Vector2.zero, smh.GCX.index, randId), condition));
        smh.RunTryPrependRIEnumerator(cor);
        return t;
    });

    /// <summary>
    /// Move the executing entity.
    /// </summary>
    public static ReflectableLASM Move(GCXF<float> time, VTP path) => MoveWhile(time, null, path);
    
    /// <summary>
    /// Move the executing entity to a target position over time. This has zero error.
    /// </summary>
    [ExpressionBoundary]
    public static ReflectableLASM MoveTarget(ExBPY time, [LookupMethod] Func<TExArgCtx, TEx<Func<float,float>>> ease, ExTP target) 
        => Move(GCXF(time), Compilers.VTP(
            VTPRepo.NROffset(Parametrics.EaseToTarget(ease, time, target))));

    /// <summary>
    /// Move to a target position, run a state machine, and then move to another target position.
    /// </summary>
    [ExpressionBoundary]
    public static ReflectableLASM MoveWrap(ExBPY t1, ExTP target1, ExBPY t2, ExTP target2, StateMachine wrapped) {
        var w1 = MoveTarget(t1, _ => Expression.Constant((Func<float,float>)Easers.EOutSine), target1);
        var w2 = MoveTarget(t2, _ => Expression.Constant((Func<float,float>)Easers.EInSine), target2);
        return new(async smh => {
            await w1.Start(smh);
            smh.ThrowIfCancelled();
            await wrapped.Start(smh);
            smh.ThrowIfCancelled();
            await w2.Start(smh);
        });
    }

    /// <summary>
    /// Move to a target position, run a state machine nonblockingly, wait for a synchronization event,
    /// and then move to another target position.
    /// </summary>
    [Alias("MoveWrap~")] [Alias("MoveWrap_")]
    [ExpressionBoundary]
    public static ReflectableLASM MoveWrapFixedDelay(Synchronizer s, ExBPY t1, ExTP target1, ExBPY t2, 
        ExTP target2, StateMachine wrapped) 
        => MoveWrap(t1, target1, t2, target2, RunDelay(s, wrapped));
    
    /// <summary>
    /// Run a state machine nonblockingly, move to a target position, wait for a synchronization event,
    /// and then move to another target position.
    /// </summary>
    [Alias("MoveWrap~~")] [Alias("MoveWrap__")]
    [ExpressionBoundary]
    public static ReflectableLASM MoveWrapFixedDelayNB(Synchronizer s, ExBPY t1, ExTP target1, ExBPY t2, 
        ExTP target2, StateMachine wrapped) {
        var mover = MoveWrapFixedDelay(s, t1, target1, t2, target2, noop);
        return new(smh => {
            _ = wrapped.Start(smh);
            return mover.Start(smh);
        });
    }
    
    private static readonly StateMachine noop = NoOp();


    /// <summary>
    /// Move-wrap, but the enemy is set invincible until the wrapped SM starts.
    /// </summary>
    [ExpressionBoundary]
    public static ReflectableLASM IMoveWrap(ExBPY t1, ExTP target1, ExBPY t2, ExTP target2, StateMachine wrapped) {
        var w1 = MoveTarget(t1, _ => Expression.Constant((Func<float,float>)Easers.EOutSine), target1);
        var w2 = MoveTarget(t2, _ => Expression.Constant((Func<float,float>)Easers.EInSine), target2);
        return new(async smh => {
            if (smh.Exec.isEnemy) smh.Exec.Enemy.SetVulnerable(Vulnerability.NO_DAMAGE);
            await w1.Start(smh);
            smh.ThrowIfCancelled();
            if (smh.Exec.isEnemy) smh.Exec.Enemy.SetVulnerable(Vulnerability.VULNERABLE);
            await wrapped.Start(smh);
            smh.ThrowIfCancelled();
            await w2.Start(smh);
        });
    }

    /// <summary>
    /// Set the position of the executing BEH in world coordinates, with X and Y as separate arguments.
    /// </summary>
    public static ReflectableLASM Position(GCXF<float> x, GCXF<float> y) => new(smh => {
        smh.Exec.ExternalSetLocalPosition(new Vector2(x(smh.GCX), y(smh.GCX)));
        return Task.CompletedTask;
    });
    
    /// <summary>
    /// Set the position of the executing BEH in world coordinates using one Vector2.
    /// </summary>
    public static ReflectableLASM Pos(GCXF<Vector2> xy) => new(smh => {
        smh.Exec.ExternalSetLocalPosition(xy(smh.GCX));
        return Task.CompletedTask;
    });

    /// <summary>
    /// Link this entity's HP pool to another enemy. The other enemy will serve as the source and this will simply redirect damage.
    /// </summary>
    public static ReflectableLASM DivertHP(GCXF<BehaviorEntity> target) => new(smh => {
        smh.Exec.Enemy.DivertHP(target(smh.GCX).Enemy);
        return Task.CompletedTask;
    });

    /// <summary>
    /// Set the enemy to be vulnerable if the condition returns true, otherwise set it to be invulnerable.
    /// </summary>
    public static ReflectableLASM Vulnerable(GCXF<bool> isVulnerable) => new(smh => {
        smh.Exec.Enemy.SetVulnerable(isVulnerable(smh.GCX) ? Vulnerability.VULNERABLE : Vulnerability.NO_DAMAGE);
        return Task.CompletedTask;
    });

    /// <summary>
    /// Set the enemy invulnerable, wait for a synchronization event, and then set it vulnerable.
    /// (This SM is blocking.)
    /// </summary>
    public static ReflectableLASM VulnerableAfter(Synchronizer sync) => new(async smh => {
        smh.Exec.Enemy.SetVulnerable(Vulnerability.NO_DAMAGE);
        await sync(smh);
        smh.ThrowIfCancelled();
        smh.Exec.Enemy.SetVulnerable(Vulnerability.VULNERABLE);
    });
    
    public static ReflectableLASM FadeSprite(BPY fader, GCXF<float> time) => new(smh => {
        smh.Exec.DisplayerOrThrow.FadeSpriteOpacity(fader, time(smh.GCX), smh.cT, GetAwaiter(out Task t));
        return t;
    });

    public static ReflectableLASM Scale(BPY scaler, GCXF<float> time) => new(smh => {
        smh.Exec.DisplayerOrThrow.Scale(scaler, time(smh.GCX), smh.cT, GetAwaiter(out Task t));
        return t;
    });
    
    #endregion
    
    #region Slowdown

    /// <summary>
    /// Create a global slowdown effect. Note this will only reset when the nesting context (usually a phase)
    ///  is cancelled.
    /// </summary>
    public static ReflectableLASM Slowdown(GCXF<float> ratio) => new(smh => {
        smh.Context.PhaseObjects.Add(ETime.Slowdown.AddConst(ratio(smh.GCX)));
        return Task.CompletedTask;
    });
    /// <summary>
    /// Create a global slowdown effect for a limited amount of time.
    /// </summary>
    public static ReflectableLASM SlowdownFor(Synchronizer time, GCXF<float> ratio) => new(async smh => {
        using var t = ETime.Slowdown.AddConst(ratio(smh.GCX));
        await time(smh);
    });

    #endregion
    
    #region Shortcuts

    public static ReflectableLASM DangerBot() => Sync("danger", _ => V2RV2.Rot(-1.7f, -6), 
        "gsr2 2 <3.4;:> { root zero } summonsup tpnrot py lerp3 1 1.5 2 2.5 t 2.2 0 -2 stall".Into<SyncPattern>());
    
    public static ReflectableLASM DangerTop() => Sync("danger", _ => V2RV2.Rot(-1.7f, 6), 
        "gsr2 2 <3.4;:> { root zero } summonsup tpnrot py lerp3 1 1.5 2 2.5 t -2.2 0 2 stall".Into<SyncPattern>());

    public static ReflectableLASM DangerLeft() => Async("danger", _ => V2RV2.Rot(-5.5f, -3f),
        "gcr2 24 4 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t 2.2 0 -2 stall".Into<AsyncPattern>());
    
    public static ReflectableLASM DangerRight() => Async("danger", _ => V2RV2.Rot(5.5f, -3f),
        "gcr2 24 4 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t -2.2 0 2 stall".Into<AsyncPattern>());

    public static ReflectableLASM DangerLeft2() => Async("danger", _ => V2RV2.Rot(-5.5f, -1f),
        "gcr2 24 2 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t 2.2 0 -2 stall".Into<AsyncPattern>());
    
    public static ReflectableLASM DangerRight2() => Async("danger", _ => V2RV2.Rot(5.5f, -1f),
        "gcr2 24 2 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t -2.2 0 2 stall".Into<AsyncPattern>());
    
    #endregion
    
    #region PlayerFiring

    public static ReflectableLASM Fire(StateMachine freeFire, StateMachine freeCancel, StateMachine focusFire,
        StateMachine focusCancel) => new(async smh => {
            var o = smh.Exec as FireOption ??
                    throw new Exception("Cannot use fire command on a BehaviorEntity that is not an Option");
            if (!o.Player.IsFiring) await RUWaitingUtils.WaitForUnchecked(o, smh.cT, () => o.Player.IsFiring);
            smh.ThrowIfCancelled();
            var (firer, onCancel, inputReq) = o.Player.IsFocus ?  
                (focusFire, focusCancel, (Func<bool>) (() => o.Player.IsFocus)) :
                (freeFire, freeCancel, (Func<bool>) (() => !o.Player.IsFocus));
            using var joint_smh = smh.CreateJointCancellee(out var fireCTS, null);
            //order is important to ensure cancellation works on the correct frame
            var cancelTask = RUWaitingUtils.WaitForUnchecked(o, smh.cT, () => !o.Player.IsFiring || !inputReq());
            var fireTask = firer.Start(joint_smh);
            await cancelTask;
            fireCTS.Cancel();
            await fireTask; //need to await so we don't dispose joint_smh early
            smh.ThrowIfCancelled();
            if (o.Player.AllowPlayerInput) _ = onCancel.Start(smh);
        });
    /*
    public static ReflectableLASM FireSame(StateMachine fire, StateMachine cancel) => new(async smh => {
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
        });*/
    
    
    public static ReflectableLASM FireSame(StateMachine fire, StateMachine cancel) => new(async smh => {
        var o = smh.Exec as FireOption ??
                throw new Exception("Cannot use fire command on a BehaviorEntity that is not an Option");
        if (!o.Player.IsFiring) await RUWaitingUtils.WaitForUnchecked(o, smh.cT, () => o.Player.IsFiring);
        smh.ThrowIfCancelled();
        using var joint_smh = smh.CreateJointCancellee(out var fireCTS, null);
        //order is important to ensure cancellation works on the correct frame
        var cancelTask = RUWaitingUtils.WaitForUnchecked(o, smh.cT, () => !o.Player.IsFiring);
        var fireTask = fire.Start(joint_smh);
        await cancelTask;
        fireCTS.Cancel();
        await fireTask; //need to await so we don't dispose joint_smh early
        smh.ThrowIfCancelled();
        if (o.Player.AllowPlayerInput) _ = cancel.Start(smh);
    });


    public static ReflectableLASM AssertSimple(string p_pool) => new(smh => {
        BulletManager.GetOrMakePlayerCopy(p_pool);
        return Task.CompletedTask;
    });
    
    public static ReflectableLASM AssertComplex(string p_pool) => new(smh => {
        BulletManager.GetOrMakeComplexPlayerCopy(p_pool);
        return Task.CompletedTask;
    });

    #endregion
    
    #region Utility

    /// <summary>
    /// Print a message to the console.
    /// </summary>
    public static ReflectableLASM Debug(string debug) => new(smh => {
        Logs.Log(debug, false, LogLevel.INFO);
        return Task.CompletedTask;
    });
    
    
    /// <summary>
    /// Print a value to the console.
    /// </summary>
    public static ReflectableLASM Print<T>(GCXF<T> val) => new(smh => {
        Logs.Log(val(smh.GCX)?.ToString() ?? "<null>", false, LogLevel.INFO);
        return Task.CompletedTask;
    });
    
    
    /// <summary>
    /// Select one of several state machines depending on which player is currently in use.
    /// Disambiguates based on the "key" property of the PlayerConfig.
    /// </summary>
    public static ReflectableLASM PlayerVariant((string key, StateMachine exec)[] options) => new(smh => {
        if (GameManagement.Instance.Player == null)
            throw new Exception("Cannot use PlayerVariant state machine when there is no player");
        for (int ii = 0; ii < options.Length; ++ii) {
            if (GameManagement.Instance.Player.key == options[ii].key) {
                return options[ii].exec.Start(smh);
            }
        }
        throw new Exception("Could not find a matching player variant option for player " +
            $"{GameManagement.Instance.Player.key}");
    });

    /// <summary>
    /// Convert excess health to score at the given rate.
    /// This is done with SFX over time and will not return immediately.
    /// </summary>
    public static ReflectableLASM LifeToScore(int value) => new(smh => {
        smh.RunRIEnumerator(_LifeToScore(value, smh.cT, GetAwaiter(out Task t)));
        return t;
    });

    private static IEnumerator _LifeToScore(int value, ICancellee cT, Action done) {
        while (GameManagement.Instance.BasicF.Lives > 1 && !cT.Cancelled) {
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