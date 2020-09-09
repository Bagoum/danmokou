using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Danmaku;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using static DMath.BPYRepo;
using static DMath.ExM;
using static Compilers;
using static Danmaku.AtomicPatterns;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using Object = UnityEngine.Object;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using static DMath.ExMConditionals;
using static DMath.ExMLerps;

namespace SM {
/// <summary>
/// All public functions in this repository can be used as LASM state machines.
/// </summary>
public static class SMReflection {

    [CanBeNull] private static Func<float, float, float, ParametricInfo, float> _crosshairOpacity;
    private static Func<float, float, float, ParametricInfo, float> CrosshairOpacity {
        get {
            if (_crosshairOpacity == null) {
                var fadein = ExUtils.VFloat();
                var homesec = ExUtils.VFloat();
                var sticksec = ExUtils.VFloat();
                var bpi = new TExPI();
                var t = T()(bpi);
                _crosshairOpacity = Expression.Lambda<Func<float, float, float, ParametricInfo, float>>(
                    If(ExMPred.Gt(t, fadein),
                        If(ExMPred.Gt(t, homesec),
                            Complement(Smooth("in-sine", Div(Sub(t, homesec), sticksec))),
                            ExMHelpers.E1
                        ),
                        Smooth("out-sine", Div(t, fadein))
                    ), fadein, homesec, sticksec, bpi).Compile();
            }
            return _crosshairOpacity;
        }
    }

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
            x => ETime.ENGINEFPS * homeSec(x),
            GCXFRepo.RV2Zero,
            new[] { GenCtxProperty.SaveV2((locSave, cindexer, locator)) }, new[] {AtomicPatterns.Noop()}
        ));
        var path = Compilers.GCXU(VTPRepo.NROffset(
            bpi => RetrieveHoisted(locSave, indexer(bpi))
        ));
        return async smh => {
            float homesec = homeSec(smh.GCX);
            float sticksec = stickSec(smh.GCX);
            locSave.Save((int) cindexer(smh.GCX), locator(smh.GCX));
            if (homesec > 0) SFXService.Request("x-crosshair");
            float fadein = Mathf.Max(0.15f, homesec / 5f);
            _ = Sync(style, _ => V2RV2.Zero, SyncPatterns.Loc0(Summon(path,
                new ReflectableLASM(smh2 => {
                    smh2.Exec.FadeSpriteOpacity(bpi => CrosshairOpacity(fadein, homesec, sticksec, bpi),
                        homesec + sticksec, smh2.cT, WaitingUtils.GetAwaiter(out Task t));
                    return t;
                }), new BehOptions())))(smh);
            await saver(smh);
            smh.ThrowIfCancelled();
            SFXService.Request("x-lockon");
            await WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, sticksec, false);
        };
    }

    public static TaskPattern dZaWarudo(GCXF<float> time) => ZaWarudo(time, _ => Vector2.zero, null, null, _ => 20);
    public static TaskPattern ZaWarudo(GCXF<float> time, GCXF<Vector2> loc, [CanBeNull] GCXF<float> t1r, [CanBeNull] GCXF<float> t2r, GCXF<float> scale) => smh => {
        float t = time(smh.GCX);
        SFXService.Request("x-zawarudo");
        var anim = Object.Instantiate(ResourceManager.GetSummonable("negative")).GetComponent<ScaleAnimator>();
        anim.transform.position = loc(smh.GCX);
        anim.AssignScales(0, scale(smh.GCX), 0);
        anim.AssignRatios(t1r?.Invoke(smh.GCX), t2r?.Invoke(smh.GCX));
        anim.Initialize(smh.cT, t);
        ++PlayerInput.SMPlayerControlDisable;
        Events.MakePlayerInvincible.Invoke((int)(t * 120), false);
        return WaitingUtils.WaitFor(smh, t, false).ContinueWithSync(() => {
            --PlayerInput.SMPlayerControlDisable;
        });
    };

    #endregion
    
    public static TaskPattern dBossExplode(TP4 powerupColor, TP4 powerdownColor) {
        var sp = Sync("powerup1", _ => V2RV2.Zero, Powerup2Static("_", "_", powerupColor, powerdownColor, _ => 1.8f, _ => 4f, _ => 0f, _ => 2f));
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
        RaikoCamera.Shake(time(smh.GCX), by_time, magnitude(smh.GCX), smh.cT,
            WaitingUtils.GetAwaiter(out Task t));
        return t;
    };

    public static TaskPattern dRaiko(GCXF<float> magnitude, GCXF<float> time) => smh => {
        var t = time(smh.GCX);
        RaikoCamera.Shake(t, null, magnitude(smh.GCX), smh.cT,
            WaitingUtils.GetAwaiter(out Task tsk));
        return tsk;
    };

    /// <summary>
    /// Flip the screen by rotating the camera around it.
    /// </summary>
    /// <param name="xy">X, Y, -X, or -Y</param>
    /// <param name="easer">Easing function</param>
    /// <param name="time">Time over which rotation occurs</param>
    /// <returns></returns>
    public static TaskPattern Seija(string xy, string easer, float time) {
        char xyc = xy[0];
        bool reverse = false;
        if (xyc == '-') {
            reverse = true;
            xyc = xy[1];
        }
        var method = xyc == 'x' ? SeijaMethod.X : SeijaMethod.Y;
        return smh => {
            if (method == SeijaMethod.X) SeijaCamera.FlipX(easer, time, reverse);
            else if (method == SeijaMethod.Y) SeijaCamera.FlipY(easer, time, reverse);
            return Task.CompletedTask;
        };
    }

    public static TaskPattern StageAnnounce() => smh => {
        UIManager.AnnounceStage(smh.cT, out float t);
        GameManagement.campaign.ExternalLenience(t);
        return WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, t, false);
    };
    public static TaskPattern StageDeannounce() => smh => {
        UIManager.DeannounceStage(smh.cT, out float t);
        GameManagement.campaign.ExternalLenience(t);
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
            return beh.Initialize(SMRunner.Cull(StateMachineManager.FromText(bossCfg.stateMachine), smh.cT));
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
    public static TaskPattern Delay(StateMachine state, Synchronizer synchr) => async smh => {
        await synchr(smh);
        smh.ThrowIfCancelled();
        await state.Start(smh);
    };

    [Alias(">>")]
    public static TaskPattern ThenDelay(StateMachine state, Synchronizer synchr) => async smh => {
        await state.Start(smh);
        smh.ThrowIfCancelled();
        await synchr(smh);
    };
    
    #region Executors

    /// <summary>
    /// Asynchronous bullet pattern fire.
    /// </summary>
    public static TaskPattern Async(string style, GCXF<V2RV2> rv2, AsyncPattern ap) => smh => {
        AsyncHandoff abh = new AsyncHandoff(new DelegatedCreator(smh.Exec, 
                BulletManager.StyleSelector.MergeStyles(smh.ch.bc.style, style)), rv2(smh.GCX) + smh.GCX.RV2, 
            WaitingUtils.GetAwaiter(out Task t), smh);
        smh.RunTryPrependRIEnumerator(ap(abh));
        return t;
    };
    /// <summary>
    /// Synchronous bullet pattern fire.
    /// </summary>
    public static TaskPattern Sync(string style, GCXF<V2RV2> rv2, SyncPattern sp) => smh => {
        sp(new SyncHandoff(new DelegatedCreator(smh.Exec, style, null), rv2(smh.GCX) + smh.GCX.RV2, smh, out var newGcx));
        newGcx.Dispose();
        return Task.CompletedTask;
    };

    public static TaskPattern Dialogue(string file) {
        StateMachine sm = null;
        return async smh => {
            Log.Unity($"Opening dialogue section {file}");
            if (sm == null) sm = StateMachineManager.LoadDialogue(file);
            bool done = false;
            var cts = new Cancellable();
            smh.RunRIEnumerator(WaitingUtils.WaitWhileWithCancellable(() => done, cts,
                () => InputManager.DialogueSkip, smh.cT, () => { }));
            var jsmh = smh.CreateJointCancellee(cts, out _);
            await sm.Start(jsmh).ContinueWithSync(() => {
                done = true;
            });
        };
    }

    /// <summary>
    /// Whenever an event is triggered, run the child.
    /// </summary>
    public static TaskPattern EventListen(StateMachine exec, Events.Event0 ev) => async smh => {
        var dm = ev.Listen(() => exec.Start(smh));
        await WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, 0f, true);
        dm.MarkForDeletion();
        smh.ThrowIfCancelled();
    };

    /// <summary>
    /// Play a sound.
    /// </summary>
    public static TaskPattern SFX(string sfx) => smh => {
        SFXService.Request(sfx);
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
    /// Add a control to bullets that makes them summon an inode to run a state machine when the predicate is satisfied.
    /// DEPRECATED. USE THE NORMAL CONTROL FUNCTIONS WITH THE "SM" CONTROL.
    /// </summary>
    [GAlias(typeof(BulletManager.SimpleBullet), "BulletControlSM")]
    [GAlias(typeof(BehaviorEntity), "BEHControlSM")]
    public static TaskPattern ParticleSMControl<T>(StateMachine sm, Pred persist, BulletManager.StyleSelector style,
        Pred cond) => smh => {
        if (typeof(T) == typeof(BehaviorEntity)) BehaviorEntity.ControlPoolSM(persist, style, sm, smh.cT, cond); 
        else BulletManager.ControlPoolSM(persist, style, sm, smh.cT, cond);
        return Task.CompletedTask;
    };

    public static TaskPattern LaserControlSM(StateMachine sm, Pred persist, BulletManager.StyleSelector style,
        LPred cond) => smh => {
            CurvedTileRenderLaser.ControlPoolSM(persist, style, sm, smh.cT, cond);
            return Task.CompletedTask;
        };
    
    /// <summary>
    /// Apply a controller function to individual entities.
    /// </summary>
    [GAlias(typeof(SBCFc), "BulletControl")]
    [GAlias(typeof(BehCFc), "BEHControl")]
    [GAlias(typeof(LCF), "BulletlControl")]
    public static TaskPattern ParticleControl<CF>(Pred persist, BulletManager.StyleSelector style, CF control) =>
        smh => {
            if (control is BehCFc bc) BehaviorEntity.ControlPool(persist, style, bc, smh.cT);
            else if (control is LCF lc) CurvedTileRenderLaser.ControlPool(persist, style, lc);
            else if (control is SBCFc pc) BulletManager.ControlPool(persist, style, pc, smh.cT);
            else throw new Exception("Couldn't realize bullet-control type");
            return Task.CompletedTask;
        };

    /// <summary>
    /// Apply a controller function to a pool of entities.
    /// </summary>
    [GAlias(typeof(SPCF), "PoolControl")]
    [GAlias(typeof(BehPF), "BEHPoolControl")]
    [GAlias(typeof(LPCF), "PoolLControl")]
    public static TaskPattern PoolControl<CF>(BulletManager.StyleSelector style, CF control) => smh => {
        if (control is BehPF bc) BehaviorEntity.ControlPool(style, bc);
        else if (control is LPCF lc) CurvedTileRenderLaser.ControlPool(style, lc);
        else if (control is SPCF pc) BulletManager.ControlPool(style, pc);
        else throw new Exception("Couldn't realize pool-control type");
        return Task.CompletedTask;
    };
    
    #endregion

    #region BEHManip

    /// <summary>
    /// Change the running phase.
    /// </summary>
    public static TaskPattern ShiftPhaseTo(int toPhase) => smh => {
        if (toPhase != -1) {
            smh.Exec.phaseController.LowPriorityOverride(toPhase);
        }
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
    public static TaskPattern MoveWhile(GCXF<float> time, [CanBeNull] Pred condition, GCXU<VTP> path) => smh => {
        uint randId = RNG.GetUInt();
        var cor = smh.Exec.ExecuteVelocity(new LimitedTimeVelocity(
            path.New(smh.GCX, ref randId), time(smh.GCX), 
                Functions.Link(() => DataHoisting.Destroy(randId),
                WaitingUtils.GetAwaiter(out Task t)), smh.cT, smh.GCX.index, condition), randId);
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
    public static TaskPattern MoveTarget(ExBPY time, string ease, ExTP target) => Move(GCXF(time),
        Compilers.GCXU(VTPRepo.NROffset(Parametrics.EaseToTarget(ease, time, target))));
    
    /// <summary>
    /// Move the executing entity to a target position over time. This has zero error.
    /// </summary>
    public static TaskPattern MoveTarget_noexpr(BPY time, string ease, TP target) => Move(g => time(g.AsBPI),
        NoExprMath_1.GCXU(NoExprMath_1.NROffset(NoExprMath_1.EaseToTarget(ease, time, target))));

    /// <summary>
    /// Move to a target position, run a state machine, and then move to another target position.
    /// </summary>
    public static TaskPattern MoveWrap(StateMachine wrapped, ExBPY t1, ExTP target1, ExBPY t2, ExTP target2) {
        var w1 = MoveTarget(t1, "out-sine", target1);
        var w2 = MoveTarget(t2, "in-sine", target2);
        return async smh => {
            await w1(smh);
            smh.ThrowIfCancelled();
            await wrapped.Start(smh);
            smh.ThrowIfCancelled();
            await w2(smh);
        };
    }
    /// <summary>
    /// Move-wrap, but the enemy is set invincible until the wrapped SM starts.
    /// </summary>
    public static TaskPattern IMoveWrap(StateMachine wrapped, ExBPY t1, ExTP target1, ExBPY t2, ExTP target2) {
        var w1 = MoveTarget(t1, "out-sine", target1);
        var w2 = MoveTarget(t2, "in-sine", target2);
        return async smh => {
            if (smh.Exec.isEnemy) smh.Exec.Enemy.SetDamageable(false);
            await w1(smh);
            smh.ThrowIfCancelled();
            if (smh.Exec.isEnemy) smh.Exec.Enemy.SetDamageable(true);
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
        smh.Exec.Enemy.DivertHP(target.beh.Enemy);
        return Task.CompletedTask;
    };

    public static TaskPattern Vulnerable(GCXF<bool> isVulnerable) => smh => {
        smh.Exec.Enemy.SetDamageable(isVulnerable(smh.GCX));
        return Task.CompletedTask;
    };
    
    #endregion
    
    #region Slowdown

    /// <summary>
    /// Create a global slowdown effect.
    /// </summary>
    public static TaskPattern Slowdown(GCXF<float> ratio) => smh => {
        ETime.SlowdownBy(ratio(smh.GCX));
        return Task.CompletedTask;
    };
    /// <summary>
    /// Create a global slowdown effect for a limited amount of time.
    /// </summary>
    public static TaskPattern SlowdownFor(GCXF<float> time, GCXF<float> ratio) => async smh => {
        ETime.SlowdownBy(ratio(smh.GCX));
        await WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, time(smh.GCX), false);
        ETime.SlowdownReset();
    };

    /// <summary>
    /// Reset the global slowdown to 1.
    /// </summary>
    public static TaskPattern SlowdownReset() => smh => {
        ETime.SlowdownReset();
        return Task.CompletedTask;
    };
    
    #endregion
    
    #region Shortcuts

    public static TaskPattern DangerBot() => Sync("danger", _ => V2RV2.Rot(-3.2f, -6), 
        "gsr2 3 <3.2;:> { root zero } summonsup tpnrot py lerp3 1 1.5 2 2.5 t 2.2 0 -2 wait".Into<SyncPattern>());
    
    public static TaskPattern DangerTop() => Sync("danger", _ => V2RV2.Rot(-3.2f, 6), 
        "gsr2 3 <3.2;:> { root zero } summonsup tpnrot py lerp3 1 1.5 2 2.5 t -2.2 0 2 wait".Into<SyncPattern>());

    public static TaskPattern DangerLeft() => Async("danger", _ => V2RV2.Rot(-7f, -3f),
        "gcr2 24 4 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t 2.2 0 -2 wait".Into<AsyncPattern>());
    
    public static TaskPattern DangerRight() => Async("danger", _ => V2RV2.Rot(7f, -3f),
        "gcr2 24 4 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t -2.2 0 2 wait".Into<AsyncPattern>());

    public static TaskPattern DangerLeft2() => Async("danger", _ => V2RV2.Rot(-7f, -1f),
        "gcr2 24 2 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t 2.2 0 -2 wait".Into<AsyncPattern>());
    
    public static TaskPattern DangerRight2() => Async("danger", _ => V2RV2.Rot(7f, -1f),
        "gcr2 24 2 <;2:> { root zero } summonsup tpnrot px lerp3 1 1.5 2 2.5 t -2.2 0 2 wait".Into<AsyncPattern>());
    
    #endregion
    
    #region PlayerFiring

    public static TaskPattern Fire(StateMachine freeFire, StateMachine freeCancel, StateMachine focusFire,
        StateMachine focusCancel) =>
        async smh => {
            var o = smh.Exec as FireOption ??
                    throw new Exception("Cannot use fire command on a BehaviorEntity that is not an Option");
            if (!PlayerInput.FiringAndAllowed) await WaitingUtils.WaitForUnchecked(o, smh.cT, () => PlayerInput.FiringAndAllowed);
            smh.ThrowIfCancelled();
            var (firer, onCancel, inputReq) = PlayerInput.IsFocus ?  
                (focusFire, focusCancel, (Func<bool>) (() => PlayerInput.IsFocus)) :
                (freeFire, freeCancel, (Func<bool>) (() => !PlayerInput.IsFocus));
            var fireCTS = new Cancellable();
            var joint_smh = smh.CreateJointCancellee(fireCTS, out _);
            //order is important to ensure cancellation works on the correct frame
            var waiter = WaitingUtils.WaitForUnchecked(o, smh.cT, () => !PlayerInput.FiringAndAllowed || !inputReq());
            _ = firer.Start(joint_smh);
            await waiter;
            fireCTS.Cancel();
            smh.ThrowIfCancelled();
            if (PlayerInput.AllowPlayerInput) _ = onCancel.Start(smh);
        };
    public static TaskPattern FireSame(StateMachine fire, StateMachine cancel) =>
        async smh => {
            var o = smh.Exec as FireOption ??
                    throw new Exception("Cannot use fire command on a BehaviorEntity that is not an Option");
            if (!PlayerInput.FiringAndAllowed) await WaitingUtils.WaitForUnchecked(o, smh.cT, () => PlayerInput.FiringAndAllowed);
            smh.ThrowIfCancelled();
            var fireCTS = new Cancellable();
            var joint_smh = smh.CreateJointCancellee(fireCTS, out _);
            //order is important to ensure cancellation works on the correct frame
            var waiter = WaitingUtils.WaitForUnchecked(o, smh.cT, () => !PlayerInput.FiringAndAllowed);
            _ = fire.Start(joint_smh);
            await waiter;
            fireCTS.Cancel();
            smh.ThrowIfCancelled();
            if (PlayerInput.AllowPlayerInput) _ = cancel.Start(smh);
        };

    public static TaskPattern FireContinued(StateMachine fireFree, StateMachine fireFocus) => async smh => {
        var o = smh.Exec as FireOption ??
                throw new Exception("Cannot use fire command on a BehaviorEntity that is not an Option");
        if (!PlayerInput.FiringAndAllowed) await WaitingUtils.WaitForUnchecked(o, smh.cT, () => PlayerInput.FiringAndAllowed);
        smh.ThrowIfCancelled();
        var fireCTS = new Cancellable();
        var joint_smh = smh.CreateJointCancellee(fireCTS, out _);
        joint_smh.Exec = o.freeFirer;
        //order is important to ensure cancellation works on the correct frame
        var waiter = WaitingUtils.WaitForUnchecked(o, smh.cT, () => !PlayerInput.FiringAndAllowed);
        _ = fireFree.Start(joint_smh);
        joint_smh.Exec = o.focusFirer;
        _ = fireFocus.Start(joint_smh);
        await waiter;
        fireCTS.Cancel();
        smh.ThrowIfCancelled();
    };

    #endregion
}

}