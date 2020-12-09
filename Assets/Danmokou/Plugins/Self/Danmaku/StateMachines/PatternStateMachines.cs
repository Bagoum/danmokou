using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku;
using DMK.DMath;
using DMK.GameInstance;
using DMK.Graphics.Backgrounds;
using DMK.Scenes;
using DMK.Scriptables;
using DMK.Services;
using DMK.UI;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.SM {
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
    private readonly PatternProperties props;

    public PatternSM(List<StateMachine> states, PatternProperties props) : base(states) {
        this.props = props;
        phases = states.Select(x => x as PhaseSM).ToArray();
        phases.ForEachI((i, p) => p.props.LoadDefaults(props, i));
    }

    public readonly PhaseSM[] phases;

    private int RemainingLives(int fromIndex) {
        int ct = 0;
        for (int ii = fromIndex; ii < phases.Length; ++ii) {
            if (phases[ii].props.phaseType?.IsSpell() ?? false) ++ct;
        }
        return ct;
    }

    private static void SetUniqueBossUI([CanBeNull] IUIManager ui, bool first, SMHandoff smh, BossConfig b) {
        if (first) {
            ui?.AddProfile(b.profile);
        } else {
            ui?.SwitchProfile(b.profile);
        }
        ui?.SetBossColor(b.colors.uiColor, b.colors.uiHPColor);
    }

    private static (List<BottomTracker>, List<BehaviorEntity>) ConfigureAllBosses([CanBeNull] IUIManager ui,
        SMHandoff smh, BossConfig main, [CanBeNull] BossConfig[] all) {
        all = all ?? new[] {main};
        var bts = new List<BottomTracker>();
        var subbosses = new List<Enemy>();
        //Anything, enemy or not (eg. Kaguya/Keine in SiMP stage 4)
        var subsummons = new List<BehaviorEntity>();
        foreach (var (i,b) in all.Enumerate()) {
            var target = smh.Exec;
            if (i > 0) {
                target = UnityEngine.Object.Instantiate(b.boss).GetComponent<BehaviorEntity>();
                target.Initialize(null, new Movement(new Vector2(-50f, 0f), 0f), SMRunner.Null);
                subsummons.Add(target);
            }
            if (target.TryAsEnemy(out var e)) {
                if (i > 0) {
                    e.distorter = null; // i dont really like this but it overlaps weirdly
                    subbosses.Add(e);
                }
                e.ConfigureBoss(b);
                e.SetVulnerable(Vulnerability.NO_DAMAGE);
            }
            string trackerName = b.BottomTrackerName;
            if (trackerName.Length > 0) bts.Add(ui?.TrackBEH(target, trackerName, smh.cT));
        }
        smh.Exec.Enemy.Subbosses = subbosses;
        return (bts, subsummons);
    }

    public override async Task Start(SMHandoff smh) {
        var bts = new List<BottomTracker>();
        var subsummons = new List<BehaviorEntity>();
        var ui = DependencyInjection.MaybeFind<IUIManager>();
        if (props.boss != null) {
            GameManagement.instance.OpenBoss(smh.Exec);
            ui?.SetBossHPLoader(smh.Exec.Enemy);
            (bts, subsummons) = ConfigureAllBosses(ui, smh, props.boss, props.bosses);
        }
        var subbosses = subsummons.Where(x => x.TryAsEnemy(out _)).Select(x => x.Enemy).ToArray();
        bool firstBoss = true;
        for (var next = smh.Exec.phaseController.WhatIsNextPhase();
            next > -1 && next < phases.Length;
            next = smh.Exec.phaseController.WhatIsNextPhase(next + 1)) {
            if (phases[next].props.skip) continue;
            if (PHASE_BUFFER) await WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, ETime.FRAME_TIME * 2f, false);
            smh.ThrowIfCancelled();
            if (props.bgms != null) AudioTrackService.InvokeBGM(props.bgms.GetBounded(next, null));
            if (props.boss != null && next >= props.setUIFrom) {
                SetUniqueBossUI(ui, firstBoss, smh, 
                    props.bosses == null ? props.boss : props.bosses[props.bossUI.GetBounded(next, 0)]);
                firstBoss = false;
            }
            //don't show lives on setup phase
            if (next > 0 && props.boss != null) ui?.ShowBossLives(RemainingLives(next));
            try {
                await phases[next].Start(smh, ui, subbosses);
            } catch (OperationCanceledException) {
                //Runs the cleanup code if we were cancelled
                break;
            }
        }
        foreach (var bt in bts) {
            if (bt != null) bt.Finish();
        }
        if (props.boss != null && !SceneIntermediary.LOADING) {
            ui?.ShowBossLives(0);
            GameManagement.instance.CloseBoss();
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
    [CanBeNull] private readonly EndPSM endPhase = null;
    /// <summary>
    /// This is a callback invoked for planned cancellation as well as the case where
    /// an enemy is killed via normal sources (player bullets, not culling). Use for revenge fire
    /// </summary>
    [CanBeNull] private readonly FinishPSM finishPhase = null;
    private readonly float _timeout = 0;
    private float Timeout => 
        DependencyInjection.MaybeFind<IChallengeManager>()?.BossTimeoutOverride(props.Boss) 
        ?? _timeout;
    public readonly PhaseProperties props;

    /// <summary>
    /// </summary>
    /// <param name="states">Substates, run sequentially</param>
    /// <param name="timeout">Timeout in seconds before the phase automatically ends. Set to zero for no timeout</param>
    /// <param name="props">Properties describing miscellaneous features of this phase</param>
    public PhaseSM(List<StateMachine> states, PhaseProperties props, float timeout) : base(states) {
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

    private void PreparePhase([CanBeNull] IUIManager ui, SMHandoff smh, out Task cutins, 
        [CanBeNull] IBackgroundOrchestrator bgo, [CanBeNull] IAyaPhotoBoard photoBoard) {
        cutins = Task.CompletedTask;
        ui?.ShowPhaseType(props.phaseType);
        if (props.cardTitle != null || props.phaseType != null) 
            ui?.SetSpellname(props.cardTitle);
        if (!props.HideTimeout && smh.Exec.TriggersUITimeout) 
            ui?.ShowStaticTimeout(Timeout);
        if (props.livesOverride.HasValue) 
            ui?.ShowBossLives(props.livesOverride.Value);
        if (smh.Exec.isEnemy) {
            if (props.photoHP.Try(out var photoHP)) {
                smh.Exec.Enemy.SetPhotoHP(photoHP, photoHP);
            } else if ((props.hp ?? props.phaseType?.DefaultHP()).Try(out var hp)) {
                if (props.Boss != null) hp = (int) (hp * GameManagement.Difficulty.bossHPMod);
                smh.Exec.Enemy.SetHP(hp, hp);
            }
            if ((props.hpbar ?? props.phaseType?.HPBarLength()).Try(out var hpbar)) {
                smh.Exec.Enemy.SetHPBar(hpbar, props.phaseType ?? PhaseType.NONSPELL);
            }
            if ((props.phaseType?.DefaultVulnerability()).Try(out var v)) 
                smh.Exec.Enemy.SetVulnerable(v);
        }
        if (props.BossPhotoHP.Try(out var pins)) {
            photoBoard?.SetupPins(pins);
        }
        bool forcedBG = false;
        if (GameManagement.instance.mode != InstanceMode.CARD_PRACTICE && !SaveData.Settings.TeleportAtPhaseStart) {
            if (props.bossCutin) {
                GameManagement.instance.ExternalLenience(props.Boss.bossCutinTime);
                SFXService.BossCutin();
                //Service not required since no callback
                DependencyInjection.MaybeFind<IRaiko>()?.Shake(props.Boss.bossCutinTime / 2f, null, 1f, smh.cT, null);
                UnityEngine.Object.Instantiate(props.Boss.bossCutin);
                bgo?.QueueTransition(props.Boss.bossCutinTrIn);
                bgo?.ConstructTarget(props.Boss.bossCutinBg, true);
                WaitingUtils.WaitFor(smh, props.Boss.bossCutinBgTime, false).ContinueWithSync(() => {
                    if (!smh.Cancelled) {
                        bgo?.QueueTransition(props.Boss.bossCutinTrOut);
                        bgo?.ConstructTarget(props.Background, true);
                    }
                });
                cutins = WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, props.Boss.bossCutinTime, false);
                forcedBG = true;
            } else if (props.GetSpellCutin(out var sc)) {
                SFXService.BossSpellCutin();
                DependencyInjection.MaybeFind<IRaiko>()?.Shake(1.5f, null, 1f, smh.cT, null);
                UnityEngine.Object.Instantiate(sc);
            }
        }
        if (!forcedBG && props.Background != null) {
            if (props.BgTransitionIn != null) bgo?.QueueTransition(props.BgTransitionIn);
            bgo?.ConstructTarget(props.Background, true);
        }
    }

    private void PrepareTimeout([CanBeNull] IUIManager ui, Enemy[] subbosses, SMHandoff smh, Cancellable toCancel) {
        smh.Exec.PhaseShifter = toCancel;
        var timeout = Timeout;
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
            ui?.DoTimeout(props.phaseType?.IsCard() ?? false, timeout, smh.cT);
    }

    public override Task Start(SMHandoff smh) => Start(smh, null, new Enemy[0]);
    public async Task Start(SMHandoff smh, [CanBeNull] IUIManager ui, Enemy[] subbosses) {
        var bgo = DependencyInjection.MaybeFind<IBackgroundOrchestrator>();
        var photoBoard = DependencyInjection.MaybeFind<IAyaPhotoBoard>();
        PreparePhase(ui, smh, out Task cutins, bgo, photoBoard);
        if (props.Lenient) GameManagement.instance.Lenience = true;
        if (props.rootMove != null) await props.rootMove.Start(smh);
        await cutins;
        smh.ThrowIfCancelled();
        if (props.phaseType?.IsPattern() == true) ETime.Timer.PhaseTimer.Restart();
        var pcTS = new Cancellable();
        var joint_smh = smh.CreateJointCancellee(pcTS, out var joint);
        PrepareTimeout(ui, subbosses, joint_smh, pcTS);
        var start_frame = ETime.FrameNumber;
        
        var start_campaign = new CampaignSnapshot(GameManagement.instance);
        if (props.phaseType != null) DependencyInjection.MaybeFind<IChallengeManager>()?.SetupBossPhase(joint_smh);
        try {
            await base.Start(joint_smh);
            await WaitingUtils.WaitForUnchecked(smh.Exec, joint, 0f,
                true); //Wait for synchronization before returning to parent
            joint_smh.ThrowIfCancelled();
        } catch (OperationCanceledException) {
            if (smh.Exec.PhaseShifter == pcTS) smh.Exec.PhaseShifter = null;
            if (props.Lenient) GameManagement.instance.Lenience = false;
            if (props.Cleanup) GameManagement.ClearPhaseAutocull(props.autocullTarget, props.autocullDefault);
            if (smh.Exec.AllowFinishCalls) {
                finishPhase?.Trigger(smh.Exec, smh.GCX, smh.parentCT);
                OnFinish(smh, pcTS, start_campaign, start_frame, bgo, photoBoard);
            }
            if (smh.Cancelled) throw;
            if (props.phaseType != null) Log.Unity($"Cleared {props.phaseType.Value} phase: {props.cardTitle ?? ""}");
            if (endPhase != null) await endPhase.Start(smh);
        }
    }

    private const float defaultShakeMag = 0.7f;
    private const float defaultShakeTime = 0.6f;
    private static readonly FXY defaultShakeMult = x => M.Sin(M.PI * (x + 0.4f));

    private void OnFinish(SMHandoff smh, ICancellee prepared, CampaignSnapshot start_campaign, int start_frame,
        [CanBeNull] IBackgroundOrchestrator bgo, [CanBeNull] IAyaPhotoBoard photoBoard) {
        if (props.BgTransitionOut != null) {
            bgo?.QueueTransition(props.BgTransitionOut);
        }
        if (props.BossPhotoHP.Try(out _)) {
            photoBoard?.TearDown();
        }
        float elapsed_ratio = Timeout > 0 ?
            Mathf.Clamp01((ETime.FrameNumber - start_frame) * ETime.FRAME_TIME / Timeout) :
            0;
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
        var pc = new PhaseCompletion(props, completedBy, smh.Exec, start_campaign, elapsed_ratio);
        if (pc.StandardCardFinish) {
            smh.Exec.DropItems(pc.DropItems, 1.4f, 0.6f, 1f, 0.2f, 2f);
            DependencyInjection.MaybeFind<IRaiko>()
                ?.Shake(defaultShakeTime, defaultShakeMult, defaultShakeMag, smh.cT, null);
        }
        GameManagement.instance.PhaseEnd(pc);
    }
}

public class DialoguePhaseSM : PhaseSM {
    public DialoguePhaseSM(PhaseProperties props, string file) : base(new List<StateMachine>() {
        new PhaseSequentialActionSM(new List<StateMachine>() {
            new ReflectableLASM(SMReflection.Dialogue(file)),
            new ReflectableLASM(SMReflection.ShiftPhase())
        }, 0f)
    }, props, 0) {
        
    }
    
}

public class PhaseJSM : PhaseSM {
    public PhaseJSM(List<StateMachine> states, PhaseProperties props, float timeout, int from)
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
    //does not inherit from PASM to avoid being eaten by MetaPASM
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
public static class Synchronization {

    /*
    public static Synchronizer Track(string alias, string keypoint) => smh => {
        if (keypoint == "end") {
            AudioTrackService.OnTrackEnd(alias, WaitingUtils.GetAwaiter(out Task t));
            return t;
        } else {
            AudioTrackService.AddTrackPointDelegate(alias, keypoint, WaitingUtils.GetAwaiter(out Task t));
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