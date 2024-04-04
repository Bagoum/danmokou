using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Graphics.Backgrounds;
using Danmokou.Player;
using Danmokou.Reflection2;
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
public class PatternSM : SequentialSM, EnvFrameAttacher {
    public PatternProperties Props { get; }
    public PhaseSM[] Phases { get; }
    public EnvFrame? EnvFrame { get; set; }

    public PatternSM(PatternProperties props, [BDSL1ImplicitChildren] StateMachine[] states) : base(states) {
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
                target.Initialize(null, mov, new ParametricInfo(in mov), null);
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
        var ctx = new PatternContext(smh.Context, this);
        using var efsmh = smh.OverrideEnvFrame(EnvFrame);
        using var jsmh = efsmh.CreateJointCancellee(out var cts, ctx);
        var subbosses = new List<Enemy>();
        var subsummons = new List<BehaviorEntity>();
        var ui = ServiceLocator.FindOrNull<IUIManager>();
        if (Props.boss != null) {
            GameManagement.Instance.SetCurrentBoss(Props.boss, jsmh.Exec, jsmh.cT);
            ui?.SetBossHPLoader(jsmh.Exec.Enemy);
            (subbosses, subsummons) = ConfigureAllBosses(ui, jsmh, Props.boss, Props.bosses);
        }
        bool firstBoss = true;
        AudioTrackSet? trackset = null;
        try {
            for (var next = jsmh.Exec.phaseController.GoToNextPhase(); 
                    next > -1 && next < Phases.Length; 
                    next = jsmh.Exec.phaseController.GoToNextPhase(next + 1)) {
                if (Phases[next].props.skip)
                    continue;
                jsmh.ThrowIfCancelled();
                if (Props.bgms is { } bgms) {
                    var nextTracks = bgms.GetAtPriority(next).ValueOrNull();
                    if (trackset is null)
                        nextTracks ??= bgms.GetBounded(next).ValueOrNull();
                    if (nextTracks is {} tracks) {
                        var pi = jsmh.GCX.DeriveFCTX();
                        AudioTrackSet? ntrackset = null;
                        ntrackset = ServiceLocator.Find<IAudioTrackService>().FindTrackset(tracks.Select(x => x.track))
                            ?? ServiceLocator.Find<IAudioTrackService>().AddTrackset(null, pi);
                        if (ntrackset != trackset) {
                            trackset?.FadeOut(null, AudioTrackState.DestroyReady);
                            trackset = ntrackset;
                        }
                        foreach (var (t, vol) in tracks) {
                            trackset.AddTrack(t)?.SetLocalVolume(vol);
                        }
                    }
                }
                if (Props.boss != null && next >= Props.setUIFrom) {
                    SetUniqueBossUI(ui, firstBoss, jsmh,
                        Props.bosses == null ?
                            Props.boss :
                            Props.bosses[Props.bossUI?.GetBounded(next).ValueOrSNull() ?? 0]);
                    firstBoss = false;
                    //don't show lives on setup phase
                    if (next > 0)
                        ui?.ShowBossLives(Phases[next].props.livesOverride ?? RemainingLives(next));
                }
                var nxtPhaseInd = jsmh.Exec.phaseController.ScanNextPhase(next + 1);
                var nxtPhase = Phases.Try(nxtPhaseInd);
                Action<IBackgroundOrchestrator?>? nextPrePrepare = null;
                if (nxtPhase != null) {
                    nextPrePrepare = bg => nxtPhase.PrePrepareBackgroundGraphics(nxtPhase.MakeContext(ctx), bg);
                }
                await Phases[next].Start(Phases[next].MakeContext(ctx), jsmh, ui, subbosses, nextPrePrepare);
            }
        } finally {
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
}


/// <summary>
/// `phase`: A high-level SM that controls a "phase", which is a sequence of SMs that may run for variable time
/// under various ending conditions.
/// <br/>Phases are used to implement 'cards' or 'spells' or 'nonspells' in danmaku games.
/// <br/>Phases also generally share some state due to data hoisting, events, etc. If you use the `type` property to declare a card type, or you use the `clear` property, then this state and all its bullets will be cleared on phase end.
/// </summary>
public class PhaseSM : SequentialSM {
    private readonly EndPSM? endPhase = null;
    private readonly float _timeout = 0;
    private float Timeout(BossConfig? boss) => 
        ServiceLocator.FindOrNull<IChallengeManager>()?.BossTimeoutOverride(boss) 
        ?? _timeout;

    public readonly PhaseProperties props;

    public PhaseContext MakeContext(PatternContext parent) =>
        new(parent, parent.SM.Phases.IndexOf(this), props);

    /// <summary>
    /// </summary>
    /// <param name="states">Substates, run sequentially</param>
    /// <param name="timeout">Timeout in seconds before the phase automatically ends. Set to zero for no timeout</param>
    /// <param name="props">Properties describing miscellaneous features of this phase</param>
    public PhaseSM(float timeout, [NonExplicitParameter] PhaseProperties props, [BDSL1ImplicitChildren] params StateMachine[] states) : base(PullOutEndPSM(states, out var ep)) {
        this._timeout = timeout;
        this.props = props;
        this.endPhase = ep;
    }

    private static StateMachine[] PullOutEndPSM(StateMachine[] states, out EndPSM? ep) {
        for (int ii = 0; ii < states.Length; ++ii) {
            if (states[ii] is EndPSM) {
                ep = states[ii] as EndPSM;
                return states.Where((x, i) => i != ii).ToArray();
            }
        }
        ep = null;
        return states;
    }

    private void _PrepareBackgroundGraphics(PhaseContext ctx, IBackgroundOrchestrator? bgo) {
        if (ctx.Background != null) {
            if (ctx.BgTransitionIn != null) bgo?.QueueTransition(ctx.BgTransitionIn);
            bgo?.ConstructTarget(ctx.Background);
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
    private void PreparePhase(PhaseContext ctx, IUIManager? ui, SMHandoff smh, IReadOnlyList<Enemy> subbosses, 
        out Task cutins, IBackgroundOrchestrator? bgo) {
        cutins = Task.CompletedTask;
        if (GameManagement.Instance.mode == InstanceMode.CAMPAIGN)
            if (ctx.Boss == null) 
                if (props.isCheckpoint)
                    GameManagement.Instance.UpdateStageCheckpoint(ctx.Index, props.phaseType?.IsStageBoss() is true);
                else
                    GameManagement.Instance.UpdateStageNoCheckpoint();
            else if (props.isCheckpoint)
                GameManagement.Instance.UpdateBossCheckpoint(ctx.Boss, ctx.Index); 
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
            smh.SetAllVulnerable(subbosses, props.phaseType?.DefaultVulnerability() ?? 
                                         (ctx.Boss == null ? Vulnerability.VULNERABLE : Vulnerability.NO_DAMAGE));
        }
        bool forcedBG = false;
        if (GameManagement.Instance.mode != InstanceMode.BOSS_PRACTICE && !SaveData.Settings.TeleportAtPhaseStart) {
            if (props.bossCutin && ctx.Boss != null && ctx.Background != null) {
                GameManagement.Instance.AddLenience(ctx.Boss.bossCutinTime);
                ServiceLocator.Find<ISFXService>().RequestSFXEvent(ISFXService.SFXEventType.BossCutin);
                //Service not required since no callback
                ServiceLocator.FindOrNull<IRaiko>()?.Shake(ctx.Boss.bossCutinTime / 2f, null, 1f, smh.cT, null);
                Object.Instantiate(ctx.Boss.bossCutin);
                bgo?.QueueTransition(ctx.Boss.bossCutinTrIn);
                bgo?.ConstructTarget(ctx.Boss.bossCutinBg);
                RUWaitingUtils.WaitThenCB(smh.Exec, smh.cT, ctx.Boss.bossCutinBgTime, false, () => {
                    if (!smh.Cancelled) {
                        bgo?.QueueTransition(ctx.Boss.bossCutinTrOut);
                        bgo?.ConstructTarget(ctx.Background);
                    }
                });
                cutins = RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, ctx.Boss.bossCutinTime, false);
                forcedBG = true;
            } else if (ctx.GetSpellCutin(out var sc)) {
                ServiceLocator.Find<ISFXService>().RequestSFXEvent(ISFXService.SFXEventType.BossSpellCutin);
                ServiceLocator.FindOrNull<IRaiko>()?.Shake(1.5f, null, 1f, smh.cT, null);
                Object.Instantiate(sc);
            }
        }
        if (!forcedBG)
            _PrepareBackgroundGraphics(ctx, bgo);
    }

    private void PrepareTimeout(PhaseContext ctx, IUIManager? ui, IReadOnlyList<Enemy> subbosses, 
        SMHandoff smh, ICancellable toCancel) {
        smh.Exec.PhaseShifter = toCancel;
        var timeout = Timeout(ctx.Boss);
        //Note that the <!> HP(hp) sets invulnTime=0.
        if (props.invulnTime != null && props.phaseType != PhaseType.Timeout)
            RUWaitingUtils.WaitThenCB(smh.Exec, smh.cT, props.invulnTime.Value, false,
                () => smh.SetAllVulnerable(subbosses, Vulnerability.VULNERABLE));
        RUWaitingUtils.WaitThenCancel(smh.Exec, smh.cT, timeout, true, toCancel);
        if (props.phaseType?.IsSpell() is true && ctx.Boss != null) {
            smh.Exec.Enemy.RequestSpellCircle(timeout, smh.cT);
            foreach (var subboss in subbosses)
                subboss.RequestSpellCircle(timeout, smh.cT);
        }
        //else smh.exec.Enemy.DestroySpellCircle();
        if (!props.HideTimeout && smh.Exec.TriggersUITimeout)
            ui?.ShowTimeout(props.phaseType?.IsCard() ?? false, timeout, smh.cT);
    }

    public override Task Start(SMHandoff smh) => Start(new PhaseContext(null, 0, props), smh, null, Array.Empty<Enemy>());
    public async Task Start(PhaseContext ctx, SMHandoff smh, IUIManager? ui, IReadOnlyList<Enemy> subbosses, 
        Action<IBackgroundOrchestrator?>? prePrepareNextPhase=null) {
        foreach (var dispGenerator in props.phaseObjectGenerators)
            ctx.PhaseObjects.Add(dispGenerator());
        var bgo = ServiceLocator.FindOrNull<IBackgroundOrchestrator>();
        PreparePhase(ctx, ui, smh, subbosses, out Task cutins, bgo);
        var photoBoardToken = ctx.BossPhotoHP.Try(out var pins) ?
            ServiceLocator.FindOrNull<IAyaPhotoBoard>()?.SetupPins(pins) :
            null;
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
        if (props.phaseType != null) ServiceLocator.FindOrNull<IChallengeManager>()?.SetupBossPhase(joint_smh);
        try {
            await base.Start(joint_smh);
            await RUWaitingUtils.WaitForUnchecked(joint_smh.Exec, joint_smh.cT, 0f,
                true); //Wait for synchronization before returning to parent
            joint_smh.ThrowIfCancelled();
        } catch (OperationCanceledException) {
            if (smh.Exec.PhaseShifter == pcTS)
                smh.Exec.PhaseShifter = null;
            //This is critical to avoid boss destruction during the two-frame phase buffer
            smh.SetAllVulnerable(subbosses, Vulnerability.NO_DAMAGE);
            float finishDelay = 0f;
            var finishTask = Task.CompletedTask;
            if (smh.Exec.AllowFinishCalls) {
                (finishDelay, finishTask) = OnFinish(ctx, smh, pcTS, start_campaign, bgo);
                prePrepareNextPhase?.Invoke(bgo);
            }
            //Don't run autocull/etc tasks if there's a higher-level cancellation
            if (props.Cleanup && !smh.Cancelled) {
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
            ctx.CleanupObjects();
            photoBoardToken?.Dispose();
            lenienceToken?.Dispose();
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
    private static readonly FXY defaultShakeMult = x => M.Sin(BMath.PI * (x + 0.4f));

    private (float estDelayTime, Task) OnFinish(PhaseContext ctx, SMHandoff smh, ICancellee prepared, 
        CampaignSnapshot start_campaign, IBackgroundOrchestrator? bgo) {
        if (ctx.BgTransitionOut != null) {
            bgo?.QueueTransition(ctx.BgTransitionOut);
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
                BulletManager.RequestPowerAura("powerup1", 0, 0, smh.GCX, new RealizedPowerAuraOptions(
                    new PowerAuraOptions(new[] {
                        PowerAuraOption.Color(_ => ColorHelpers.CV4(ctx.Boss.colors.powerAuraColor)),
                        PowerAuraOption.Time(_ => 1f),
                        PowerAuraOption.Iterations(_ => -1f),
                        PowerAuraOption.Scale(_ => 4.5f),
                        PowerAuraOption.Static(), 
                        PowerAuraOption.High(), 
                    }), smh.GCX, smh.Exec.GlobalPosition(), smh.cT, null!));
            }
            smh.Exec.DropItems(pc.DropItems, 1.4f, 0.6f, 1f, 0.2f, 2f);
            ServiceLocator.FindOrNull<IRaiko>()
                ?.Shake(defaultShakeTime, defaultShakeMult, defaultShakeMag, smh.cT, null);
        }
        GameManagement.Instance.PhaseEnd(pc);
        if (pc.StandardCardFinish && !smh.Cancelled && ctx.Boss != null && pc.CaptureStars.HasValue) {
            Object.Instantiate(GameManagement.References.prefabReferences.phasePerformance)
                .GetComponent<PhasePerformance>().Initialize($"{ctx.Boss.CasualName} / Boss Card", pc);
            return (EndOfCardDelayTime, RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, EndOfCardDelayTime, false));
        }
        return (0, Task.CompletedTask);
    }

    private const float EndOfCardAutocullTime = 0.7f;
    private const float EndOfCardDelayTime = 1.3f;
}

public class DialoguePhaseSM : PhaseSM {
    public DialoguePhaseSM(string file, [NonExplicitParameter] PhaseProperties props) : base(0, props,
        new PhaseSequentialActionSM(0f, 
            SMReflection.Dialogue(file),
            SMReflection.ShiftPhase()
        )
    ) {
    }
}

/// <summary>
/// A PhaseSM that, when run in the editor, skips its first `from` children.
/// </summary>
public class PhaseJSM : PhaseSM {
    public PhaseJSM(float timeout, int from, [NonExplicitParameter] PhaseProperties props, [BDSL1ImplicitChildren] StateMachine[] states)
    #if UNITY_EDITOR
        : base(timeout, props, from == 0 ? states : states.Skip(from).Prepend(states[0]).ToArray()) {
        if (this.states.Try(from == 0 ? 0 : 1) is PhaseParallelActionSM psm) psm.wait = 0f;
        if (this.states.Try(from == 0 ? 0 : 1) is PhaseSequentialActionSM ssm) ssm.wait = 0f;
    #else
        : base(timeout, props, states) {
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
    
    public PhaseParallelActionSM(float wait, [BDSL1ImplicitChildren] StateMachine[] states) : base(states) {
        this.wait = wait;
    }

    private async Task WaitThenStart(SMHandoff smh) {
        await RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, wait, false);
        smh.ThrowIfCancelled();
        await base.Start(smh);
    }

    public override Task Start(SMHandoff smh) => wait > 0 ? WaitThenStart(smh) : base.Start(smh);
}
/// <summary>
/// `saction`: A list of actions that are run in sequence. Place this under <see cref="PhaseSM"/>.
/// </summary>
public class PhaseSequentialActionSM : SequentialSM {
    public float wait;

    /// <summary>
    /// </summary>
    /// <param name="states">Actions to run in parallel</param>
    /// <param name="wait">Artificial delay before this SM starts executing</param>
    public PhaseSequentialActionSM(float wait, [BDSL1ImplicitChildren] params StateMachine[] states) : base(states) {
        this.wait = wait;
    }

    public override async Task Start(SMHandoff smh) {
        await RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, wait, false);
        smh.ThrowIfCancelled();
        await base.Start(smh);
    }
}

/// <summary>
/// `end`: Child of <see cref="PhaseSM"/>. When a phase ends under normal conditions (ie. was not cancelled),
/// run these actions in parallel before moving to the next phase.
/// </summary>
public class EndPSM : ParallelSM {
    //TODO consider making this inherit PhaseParallelActionSM
    public EndPSM([BDSL1ImplicitChildren] StateMachine[] states) : base(states) { }
}

/// <summary>
/// The basic unit of control in SMs. These cannot be used directly and must instead be placed under <see cref="PhaseSequentialActionSM"/> or <see cref="PhaseParallelActionSM"/> or <see cref="GTRepeat"/>.
/// </summary>
public abstract class LineActionSM : StateMachine {
    public LineActionSM(params StateMachine[] states) : base(states) { }
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
    public static Synchronizer Time(GCXF<float> time) => smh => RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, time(smh.GCX), false);

    /// <summary>
    /// Wait for the synchronization event, and then wait some number of seconds.
    /// </summary>
    public static Synchronizer Delay(float time, Synchronizer synchr) => async smh => {
        await synchr(smh);
        smh.ThrowIfCancelled();
        await RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, time, false);
    };
}

}