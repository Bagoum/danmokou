using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Graphics.Backgrounds;
using Danmokou.Player;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.UI;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Danmokou.SM {
/// <summary>
/// `pattern`: Top-level controller for SMs involving danmaku or level control.
/// Does not encompass text control (<see cref="ScriptTSM"/>).
/// </summary>
public class PatternSM : SequentialSM {
    /// <summary>
    /// This is sort of a hack. On `clear phase`, data is not cleared until the end
    /// of the frame to ensure that bullets can cull before data is cleared. Therefore we also wait.
    /// We wait for 2 frames because the data clearing occurs "at the end of the next frame".
    /// We allow this functionality to be disabled during testing because it interferes with timing calculations.
    /// </summary>
    public static bool PHASE_BUFFER = true;
    public PatternProperties Props { get; }
    public PhaseSM[] Phases { get; }

    public PatternSM(List<StateMachine> states, PatternProperties props) : base(states) {
        this.Props = props;
        Phases = states.Select(x => (x as PhaseSM)!).ToArray();
    }

    private int RemainingLives(int fromIndex) {
        int ct = 0;
        for (int ii = fromIndex; ii < Phases.Length; ++ii) {
            if (Phases[ii].props.phaseType?.IsSpell() ?? false) ++ct;
        }
        return ct;
    }

    private static void SetUniqueBossUI(IUIManager? ui, bool first, SMHandoff smh, BossConfig b) {
        if (first) {
            ui?.AddProfile(b.profile);
        } else {
            ui?.SwitchProfile(b.profile);
        }
        ui?.SetBossColor(b.colors.uiColor, b.colors.uiHPColor);
    }

    private static (List<Enemy>, List<BehaviorEntity>) ConfigureAllBosses(IUIManager? ui,
        SMHandoff smh, BossConfig main, BossConfig[]? all) {
        all ??= new[] {main};
        //BossConfig may summon an Enemy or just a BehaviorEntity.
        //Enemies are tracked in the main boss for HP sharing.
        //BehaviorEntities are tracked 
        var subbosses = new List<Enemy>();
        var subsummons = new List<BehaviorEntity>();
        foreach (var (i,b) in all.Enumerate()) {
            var target = smh.Exec;
            if (i > 0) {
                target = Object.Instantiate(b.boss).GetComponent<BehaviorEntity>();
                var mov = new Movement(new Vector2(-50f, 0f), 0f);
                target.Initialize(null, mov, new ParametricInfo(in mov), SMRunner.Null);
                subsummons.Add(target);
            }
            if (target.TryAsEnemy(out var e)) {
                if (i > 0) {
                    e.DisableDistortion(); 
                    //Overlapping distortion effects cause artifacting even with proper sprite ordering since
                    // it destroys continuousness guarantees
                    subbosses.Add(e);
                }
                e.ConfigureBoss(b);
                e.SetVulnerable(Vulnerability.NO_DAMAGE);
            }
            string trackerName = b.BottomTrackerName;
            if (trackerName.Length > 0) ui?.TrackBEH(target, trackerName, smh.cT);
        }
        smh.Exec.Enemy.Subbosses = subbosses;
        return (subbosses, subsummons);
    }

    public override async Task Start(SMHandoff smh) {
        var ctx = new PatternContext(this);
        using var jsmh = smh.CreateJointCancellee(out var cts, ctx);
        var subbosses = new List<Enemy>();
        var subsummons = new List<BehaviorEntity>();
        var ui = ServiceLocator.MaybeFind<IUIManager>();
        if (Props.boss != null) {
            GameManagement.Instance.SetCurrentBoss(Props.boss, jsmh.Exec, jsmh.cT);
            ui?.SetBossHPLoader(jsmh.Exec.Enemy);
            (subbosses, subsummons) = ConfigureAllBosses(ui, jsmh, Props.boss, Props.bosses);
        }
        bool firstBoss = true;
        for (var next = jsmh.Exec.phaseController.GoToNextPhase();
            next > -1 && next < Phases.Length;
            next = jsmh.Exec.phaseController.GoToNextPhase(next + 1)) {
            if (Phases[next].props.skip) 
                continue;
            if (PHASE_BUFFER) 
                await WaitingUtils.WaitForUnchecked(jsmh.Exec, jsmh.cT, ETime.FRAME_TIME * 2f, false);
            jsmh.ThrowIfCancelled();
            ServiceLocator.Find<IAudioTrackService>().InvokeBGM(Props.bgms?.GetBounded(next, null));
            if (Props.boss != null && next >= Props.setUIFrom) {
                SetUniqueBossUI(ui, firstBoss, jsmh, 
                    Props.bosses == null ? Props.boss : 
                        Props.bosses[Props.bossUI?.GetBounded(next, 0) ?? 0]);
                firstBoss = false;
                //don't show lives on setup phase
                if (next > 0) 
                    ui?.ShowBossLives(Phases[next].props.livesOverride ?? RemainingLives(next));
            }
            try {
                var nxtPhaseInd = jsmh.Exec.phaseController.ScanNextPhase(next + 1);
                var nxtPhase = Phases.Try(nxtPhaseInd);
                Action<IBackgroundOrchestrator?>? nextPrePrepare = null;
                if (nxtPhase != null) {
                    nextPrePrepare = bg => nxtPhase.PrePrepareBackgroundGraphics(nxtPhase.MakeContext(ctx), bg);
                }
                await Phases[next].Start(Phases[next].MakeContext(ctx), jsmh, ui, subbosses, nextPrePrepare);
            } catch (OperationCanceledException) {
                //Runs the cleanup code if we were cancelled
                break;
            }
        }
        cts.Cancel();
        if (Props.boss != null && !SceneIntermediary.LOADING) {
            ui?.CloseBoss();
            if (!firstBoss) ui?.CloseProfile();
            foreach (var subsummon in subsummons) {
                subsummon.InvokeCull();
            }
        }
    }
}

/// <summary>
/// `finish`: Child of PhaseSM. When the executing BEH finishes this phase due to timeout (shift-phase) or loss of HP,
/// runs the child SM on a new inode.
/// <br/>Does not run if the executing BEH is destroyed by a cull command, or goes out of range, or the scene is changed.
/// </summary>
public class FinishPSM : StateMachine {

    public FinishPSM(StateMachine state) : base(state) { }

    public void Trigger(BehaviorEntity Exec, GenCtx gcx, ICancellee cT) {
        _ = Exec.GetINode("finish-triggered", null).RunExternalSM(SMRunner.Cull(this.states[0], cT, gcx));
    }

    public override Task Start(SMHandoff smh) {
        throw new NotImplementedException("Do not call Start on FinishSM");
    }
}


/// <summary>
/// `phase`: A high-level SM that controls a "phase", which is a sequence of SMs that may run for variable time
/// under various ending conditions.
/// <br/>Phases are the basic unit for implementing cards or similar concepts.
/// <br/>Phases also generally share some state due to data hoisting, events, etc. If you use the `type` property to declare a card type, or you use the `clear` property, then this state and all bullets will be cleared on phase end.
/// </summary>
public class PhaseSM : SequentialSM {
    //Note that this is only for planned cancellation (eg. phase shift/ synchronization),
    //and is primary used for bosses/multiphase enemies.
    private readonly EndPSM? endPhase = null;
    /// <summary>
    /// This is a callback invoked for planned cancellation as well as the case where
    /// an enemy is killed via normal sources (player bullets, not culling). Use for revenge fire
    /// </summary>
    private readonly FinishPSM? finishPhase = null;
    private readonly float _timeout = 0;
    private float Timeout(BossConfig? boss) => 
        ServiceLocator.MaybeFind<IChallengeManager>()?.BossTimeoutOverride(boss) 
        ?? _timeout;

    public readonly PhaseProperties props;

    public PhaseContext MakeContext(PatternContext parent) =>
        new PhaseContext(parent, parent.SM.Phases.IndexOf(this), props);

    /// <summary>
    /// </summary>
    /// <param name="states">Substates, run sequentially</param>
    /// <param name="timeout">Timeout in seconds before the phase automatically ends. Set to zero for no timeout</param>
    /// <param name="props">Properties describing miscellaneous features of this phase</param>
    public PhaseSM(List<StateMachine> states, [NonExplicitParameter] PhaseProperties props, float timeout) : base(states) {
        this._timeout = timeout;
        this.props = props;
        for (int ii = 0; ii < states.Count; ++ii) {
            if (states[ii] is EndPSM) {
                endPhase = states[ii] as EndPSM;
                states.RemoveAt(ii);
                break;
            }
        }
        for (int ii = 0; ii < states.Count; ++ii) {
            if (states[ii] is FinishPSM) {
                finishPhase = states[ii] as FinishPSM;
                states.RemoveAt(ii);
                break;
            }
        }
    }

    private void _PrepareBackgroundGraphics(PhaseContext ctx, IBackgroundOrchestrator? bgo) {
        if (ctx.Background != null) {
            if (ctx.BgTransitionIn != null) bgo?.QueueTransition(ctx.BgTransitionIn);
            bgo?.ConstructTarget(ctx.Background, true);
        }
    }
    
    /// <summary>
    /// Try to prepare background graphics separately from the standard preparation process.
    /// This function can be called while the previous phase is still executing (eg. during post-phase cull time).
    /// <br/>Note that this can result in the background being set twice (once here and once in PreparePhase),
    ///  but BackgroundOrchestrator will make the second set a noop.
    /// </summary>
    public void PrePrepareBackgroundGraphics(PhaseContext ctx, IBackgroundOrchestrator? bgo) {
        bool requireBossCutin =
            GameManagement.Instance.mode != InstanceMode.BOSS_PRACTICE &&
            !SaveData.Settings.TeleportAtPhaseStart &&
            props.bossCutin && ctx.Boss != null && ctx.Background != null;
        if (!requireBossCutin) {
            _PrepareBackgroundGraphics(ctx, bgo);
        }
    }
    private void PreparePhase(PhaseContext ctx, IUIManager? ui, SMHandoff smh, out Task cutins, 
        IBackgroundOrchestrator? bgo, IAyaPhotoBoard? photoBoard) {
        cutins = Task.CompletedTask;
        ui?.ShowPhaseType(props.phaseType);
        if (props.cardTitle != null || props.phaseType != null) {
            var rate = (ctx.Boss != null) ?
                GameManagement.Instance.LookForSpellHistory(ctx.Boss.key, ctx.Index) :
                null;
            ui?.SetSpellname(props.cardTitle?.ToString(), rate);
        } 
        if (smh.Exec.TriggersUITimeout) 
            ui?.ShowStaticTimeout(props.HideTimeout ? 0 : Timeout(ctx.Boss));
        if (smh.Exec.isEnemy) {
            if (props.photoHP.Try(out var photoHP)) {
                smh.Exec.Enemy.SetPhotoHP(photoHP, photoHP);
            } else if ((props.hp ?? props.phaseType?.DefaultHP()).Try(out var hp)) {
                if (ctx.Boss != null) hp = (int) (hp * GameManagement.Difficulty.bossHPMod);
                smh.Exec.Enemy.SetHP(hp, hp);
            }
            smh.Exec.Enemy.SetHPBar(props.hpbar ?? props.phaseType?.HPBarLength(), props.phaseType);
            //Bosses are by default invulnerable on unmarked phases
            smh.Exec.Enemy.SetVulnerable(props.phaseType?.DefaultVulnerability() ?? 
                                         (ctx.Boss == null ? Vulnerability.VULNERABLE : Vulnerability.NO_DAMAGE));
        }
        if (ctx.BossPhotoHP.Try(out var pins)) {
            photoBoard?.SetupPins(pins);
        }
        bool forcedBG = false;
        if (GameManagement.Instance.mode != InstanceMode.BOSS_PRACTICE && !SaveData.Settings.TeleportAtPhaseStart) {
            if (props.bossCutin && ctx.Boss != null && ctx.Background != null) {
                GameManagement.Instance.AddFaithLenience(ctx.Boss.bossCutinTime);
                ServiceLocator.Find<ISFXService>().RequestSFXEvent(ISFXService.SFXEventType.BossCutin);
                //Service not required since no callback
                ServiceLocator.MaybeFind<IRaiko>()?.Shake(ctx.Boss.bossCutinTime / 2f, null, 1f, smh.cT, null);
                Object.Instantiate(ctx.Boss.bossCutin);
                bgo?.QueueTransition(ctx.Boss.bossCutinTrIn);
                bgo?.ConstructTarget(ctx.Boss.bossCutinBg, true);
                WaitingUtils.WaitFor(smh, ctx.Boss.bossCutinBgTime, false).ContinueWithSync(() => {
                    if (!smh.Cancelled) {
                        bgo?.QueueTransition(ctx.Boss.bossCutinTrOut);
                        bgo?.ConstructTarget(ctx.Background, true);
                    }
                });
                cutins = WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, ctx.Boss.bossCutinTime, false);
                forcedBG = true;
            } else if (ctx.GetSpellCutin(out var sc)) {
                ServiceLocator.Find<ISFXService>().RequestSFXEvent(ISFXService.SFXEventType.BossSpellCutin);
                ServiceLocator.MaybeFind<IRaiko>()?.Shake(1.5f, null, 1f, smh.cT, null);
                Object.Instantiate(sc);
            }
        }
        if (!forcedBG)
            _PrepareBackgroundGraphics(ctx, bgo);
    }

    private void PrepareTimeout(PhaseContext ctx, IUIManager? ui, IReadOnlyList<Enemy> subbosses, 
        SMHandoff smh, Cancellable toCancel) {
        smh.Exec.PhaseShifter = toCancel;
        var timeout = Timeout(ctx.Boss);
        //Note that the <!> HP(hp) sets invulnTime=0.
        if (props.invulnTime != null && props.phaseType != PhaseType.TIMEOUT)
            WaitingUtils.WaitThenCB(smh.Exec, smh.cT, props.invulnTime.Value, false,
                () => smh.Exec.Enemy.SetVulnerable(Vulnerability.VULNERABLE));
        WaitingUtils.WaitThenCancel(smh.Exec, smh.cT, timeout, true, toCancel);
        if (props.phaseType?.IsSpell() ?? false) {
            smh.Exec.Enemy.RequestSpellCircle(timeout, smh.cT);
            foreach (var subboss in subbosses)
                subboss.RequestSpellCircle(timeout, smh.cT);
        }
        //else smh.exec.Enemy.DestroySpellCircle();
        if (!props.HideTimeout && smh.Exec.TriggersUITimeout)
            ui?.ShowTimeout(props.phaseType?.IsCard() ?? false, timeout, smh.cT);
    }

    public override Task Start(SMHandoff smh) => Start(new PhaseContext(null, 0, props), smh, null, new Enemy[0]);
    public async Task Start(PhaseContext ctx, SMHandoff smh, IUIManager? ui, IReadOnlyList<Enemy> subbosses, 
        Action<IBackgroundOrchestrator?>? prePrepareNextPhase=null) {
        foreach (var dispGenerator in props.phaseObjectGenerators)
            ctx.PhaseObjects.Add(dispGenerator());
        var bgo = ServiceLocator.MaybeFind<IBackgroundOrchestrator>();
        var photoBoard = ServiceLocator.MaybeFind<IAyaPhotoBoard>();
        PreparePhase(ctx, ui, smh, out Task cutins, bgo, photoBoard);
        var lenienceToken = props.Lenient ?
            GameManagement.Instance.Lenient.AddConst(true) :
            null;
        if (props.rootMove != null) await props.rootMove.Start(smh);
        await cutins;
        smh.ThrowIfCancelled();
        if (props.phaseType?.IsPattern() == true) ETime.Timer.PhaseTimer.Restart();
        using var joint_smh = smh.CreateJointCancellee(out var pcTS, ctx);
        PrepareTimeout(ctx, ui, subbosses, joint_smh, pcTS);
        //The start snapshot is taken after the root movement,
        // so meter can be used during the 1+2 seconds between cards
        var start_campaign = new CampaignSnapshot(GameManagement.Instance);
        if (props.phaseType != null) ServiceLocator.MaybeFind<IChallengeManager>()?.SetupBossPhase(joint_smh);
        try {
            await base.Start(joint_smh);
            await WaitingUtils.WaitForUnchecked(joint_smh.Exec, joint_smh.cT, 0f,
                true); //Wait for synchronization before returning to parent
            joint_smh.ThrowIfCancelled();
        } catch (OperationCanceledException) {
            ctx.CleanupObjects();
            if (smh.Exec.PhaseShifter == pcTS)
                smh.Exec.PhaseShifter = null;
            //This is critical to avoid boss destruction during the two-frame phase buffer
            if (smh.Exec.isEnemy)
                smh.Exec.Enemy.SetVulnerable(Vulnerability.NO_DAMAGE);
            lenienceToken?.Dispose();
            float finishDelay = 0f;
            var finishTask = Task.CompletedTask;
            if (smh.Exec.AllowFinishCalls) {
                //TODO why does this use parentCT?
                finishPhase?.Trigger(smh.Exec, smh.GCX, smh.parentCT);
                (finishDelay, finishTask) = OnFinish(ctx, smh, pcTS, start_campaign, bgo, photoBoard);
                prePrepareNextPhase?.Invoke(bgo);
            }
            if (props.Cleanup) {
                if (finishDelay > 0) {
                    var acTime = Mathf.Min(EndOfCardAutocullTime, finishDelay);
                    foreach (var player in ServiceLocator.FindAll<PlayerController>())
                        player.MakeInvulnerable((int)(acTime * ETime.ENGINEFPS_F), false);
                    GameManagement.ClearPhaseAutocullOverTime_Initial(
                        props.SoftcullPropsOverTime(smh.Exec, acTime), 
                        props.SoftcullPropsBeh(smh.Exec));
                    await finishTask;
                    GameManagement.ClearPhaseAutocullOverTime_Final();
                } else {
                    GameManagement.ClearPhaseAutocull(
                        props.SoftcullProps(smh.Exec), 
                        props.SoftcullPropsBeh(smh.Exec));
                    await finishTask;
                }
            }
            if (smh.Cancelled) 
                throw;
            if (props.phaseType != null) 
                Logs.Log($"Cleared {props.phaseType.Value} phase: {props.cardTitle?.Value ?? ""}");
            if (endPhase != null)
                await endPhase.Start(smh);
        }
    }

    private const float defaultShakeMag = 0.7f;
    private const float defaultShakeTime = 0.6f;
    private static readonly FXY defaultShakeMult = x => M.Sin(M.PI * (x + 0.4f));

    private (float estDelayTime, Task) OnFinish(PhaseContext ctx, SMHandoff smh, ICancellee prepared, 
        CampaignSnapshot start_campaign, IBackgroundOrchestrator? bgo, IAyaPhotoBoard? photoBoard) {
        if (ctx.BgTransitionOut != null) {
            bgo?.QueueTransition(ctx.BgTransitionOut);
        }
        if (ctx.BossPhotoHP.Try(out _)) {
            photoBoard?.TearDown();
        }
        //The shift-phase token is cancelled by timeout or by HP. 
        var completedBy = prepared.Cancelled ?
            (smh.Exec.isEnemy ?
                (smh.Exec.Enemy.PhotoHP <= 0 && (props.photoHP ?? 0) > 0) ? 
                    PhaseClearMethod.PHOTO :
                    (smh.Exec.Enemy.HP <= 0 && (props.hp ?? 0) > 0) ?
                        PhaseClearMethod.HP :
                        (PhaseClearMethod?) null :
                null) ??
            PhaseClearMethod.TIMEOUT :
            PhaseClearMethod.CANCELLED;
        var pc = new PhaseCompletion(ctx, completedBy, smh.Exec, start_campaign, Timeout(ctx.Boss));
        if (pc.StandardCardFinish) {
            if (ctx.Boss != null) {
                BulletManager.RequestPowerAura("powerup1", 0, 0, new RealizedPowerAuraOptions(
                    new PowerAuraOptions(new[] {
                        PowerAuraOption.Color(_ => ColorHelpers.CV4(ctx.Boss.colors.powerAuraColor)),
                        PowerAuraOption.Time(_ => 1f),
                        PowerAuraOption.Iterations(_ => -1f),
                        PowerAuraOption.Scale(_ => 4.5f),
                        PowerAuraOption.Static(), 
                        PowerAuraOption.High(), 
                    }), GenCtx.Empty, smh.Exec.GlobalPosition(), smh.cT, null!));
            }
            smh.Exec.DropItems(pc.DropItems, 1.4f, 0.6f, 1f, 0.2f, 2f);
            ServiceLocator.MaybeFind<IRaiko>()
                ?.Shake(defaultShakeTime, defaultShakeMult, defaultShakeMag, smh.cT, null);
        }
        GameManagement.Instance.PhaseEnd(pc);
        if (pc.StandardCardFinish && !smh.Cancelled && ctx.Boss != null && pc.CaptureStars.HasValue) {
            Object.Instantiate(GameManagement.References.prefabReferences.phasePerformance)
                .GetComponent<PhasePerformance>().Initialize($"{ctx.Boss.CasualName} / Boss Card", pc);
            return (EndOfCardDelayTime, WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, EndOfCardDelayTime, false));
        }
        return (0, Task.CompletedTask);
    }

    private const float EndOfCardAutocullTime = 0.7f;
    private const float EndOfCardDelayTime = 1.3f;
}

public class DialoguePhaseSM : PhaseSM {
    public DialoguePhaseSM([NonExplicitParameter] PhaseProperties props, string file) : base(new List<StateMachine>() {
        new PhaseSequentialActionSM(new List<StateMachine>() {
            new ReflectableLASM(SMReflection.Dialogue(file)),
            new ReflectableLASM(SMReflection.ShiftPhase())
        }, 0f)
    }, props, 0) {
        
    }
}

public class PhaseJSM : PhaseSM {
    public PhaseJSM(List<StateMachine> states, [NonExplicitParameter] PhaseProperties props, float timeout, int from)
    #if UNITY_EDITOR
        : base(from == 0 ? states : states.Skip(from).Prepend(states[0]).ToList(), props, timeout) {
        if (this.states.Try(from == 0 ? 0 : 1) is PhaseParallelActionSM psm) psm.wait = 0f;
    #else
        : base(states, props, timeout) {
    #endif
    }
}

/// <summary>
/// `paction`: A list of actions that are run in parallel. Place this under a PhaseSM.
/// </summary>
public class PhaseParallelActionSM : ParallelSM {
#if UNITY_EDITOR
    public float wait;
#else
    private readonly float wait;
#endif
    
    public PhaseParallelActionSM(List<StateMachine> states, float wait) : base(states) {
        this.wait = wait;
    }
    
    public override async Task Start(SMHandoff smh) {
        await WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, wait, false);
        smh.ThrowIfCancelled();
        await base.Start(smh);
    }
}
/// <summary>
/// `saction`: A list of actions that are run in sequence. Place this under a PhaseSM.
/// <br/>This SM is always blocking.
/// </summary>
public class PhaseSequentialActionSM : SequentialSM {
    private readonly float wait;

    /// <summary>
    /// </summary>
    /// <param name="states">Actions to run in parallel</param>
    /// <param name="wait">Artificial delay before this SM starts executing</param>
    public PhaseSequentialActionSM(List<StateMachine> states, float wait) : base(states) {
        this.wait = wait;
    }

    public override async Task Start(SMHandoff smh) {
        await WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, wait, false);
        smh.ThrowIfCancelled();
        await base.Start(smh);
    }
}

/// <summary>
/// `end`: Child of PhaseSM. When a phase ends under normal conditions (ie. was not cancelled),
/// run these actions in parallel before moving to the next phase.
/// </summary>
public class EndPSM : ParallelSM {
    //TODO consider making this inherit PhaseParallelActionSM
    public EndPSM(List<StateMachine> states) : base(states) { }
}

/// <summary>
/// The basic unit of control in SMs. These cannot be used directly and must instead be placed under `PhaseActionSM` or the like.
/// </summary>
public abstract class LineActionSM : StateMachine {
    public LineActionSM(params StateMachine[] states) : base(new List<StateMachine>(states)) { }
}

/// <summary>
/// Synchronization events for use in SMs.
/// </summary>
[Reflect]
public static class Synchronization {

    /*
    public static Synchronizer Track(string alias, string keypoint) => smh => {
        if (keypoint == "end") {
            AudioTrackService.OnTrackEnd(alias, GetAwaiter(out Task t));
            return t;
        } else {
            AudioTrackService.AddTrackPointDelegate(alias, keypoint, GetAwaiter(out Task t));
            return t;
        }
    };*/
    /// <summary>
    /// Wait for some number of seconds.
    /// </summary>
    [Fallthrough(1)]
    public static Synchronizer Time(GCXF<float> time) => smh => WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, time(smh.GCX), false);

    /// <summary>
    /// Wait for the synchronization event, and then wait some number of seconds.
    /// </summary>
    public static Synchronizer Delay(float time, Synchronizer synchr) => async smh => {
        await synchr(smh);
        smh.ThrowIfCancelled();
        await WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, time, false);
    };
}

}