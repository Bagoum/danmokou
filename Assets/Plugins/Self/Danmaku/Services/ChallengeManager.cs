using System;
using System.Collections;
using System.Threading;
using Danmaku;
using DMath;
using JetBrains.Annotations;
using SM;
using static Challenge;

public class ChallengeManager : CoroutineRegularUpdater {
    private static ChallengeManager main;

    private void Awake() {
        main = this;
        r = new Restrictions();
    }

    private void OnDestroy() {
        main = null;
        Completion = null;
        Tracking = null;
        r = new Restrictions();
    }

    public static void ReceivePhaseCompletion(PhaseCompletion pc) {
        if (pc.clear != PhaseClearMethod.CANCELLED && pc.props.phaseType != null && pc.exec == Exec) Completion = pc;
    }

    public static PhaseCompletion? Completion { get; private set; } = null;
    public static ChallengeRequest? Tracking { get; private set; } = null;

    [CanBeNull] private static TP4 _stayInColor = null;
    private static TP4 StayInColor => _stayInColor = _stayInColor ?? "witha lerpt 0 1 0 0.3 green".Into<TP4>();
    [CanBeNull] private static TP4 _stayOutColor = null;
    private static TP4 StayOutColor => _stayOutColor = _stayOutColor ?? "witha lerpt 0 1 0 0.3 red".Into<TP4>();

    public static TaskPattern StayInRange(BehaviorEntity beh, float f) => SMReflection.Sync("_", GCXFRepo.RV2Zero,
        AtomicPatterns.RelCirc("_", new BEHPointer(beh), _ => ExMRV2.RXY(f, f), StayInColor));
    public static TaskPattern StayOutRange(BehaviorEntity beh, float f) => SMReflection.Sync("_", GCXFRepo.RV2Zero,
        AtomicPatterns.RelCirc("_", new BEHPointer(beh), _ => ExMRV2.RXY(f, f), StayOutColor));

    public static float? TimeoutOverride([CanBeNull] BossConfig bc) => 
        (bc == Tracking?.Boss && Tracking?.challenge is DestroyTimedC dt) ? dt.time : (float?)null;

    [CanBeNull] private static BehaviorEntity Exec;

    public static void LinkBEH(BehaviorEntity exec) {
        if (Tracking == null) throw new Exception("Cannot link BEH when no challenge is tracked");
        Exec = exec;
        var cr = Tracking.Value;
        exec.behaviorScript = cr.Boss.stateMachine;
        exec.phaseController.Override(cr.phase.phase.index, () => { });
    }

    public class Restrictions {
        public readonly bool HorizAllowed = true;
        public readonly bool VertAllowed = true;

        public readonly bool FocusAllowed = true;
        public readonly bool FocusForced = false;

        public Restrictions() {}
        public Restrictions(Challenge c) {
            if (c is NoHorizC) HorizAllowed = false;
            else if (c is NoVertC) VertAllowed = false;
            else if (c is NoFocusC) FocusAllowed = false;
            else if (c is AlwaysFocusC) FocusForced = true;
        }
    }
    public static Restrictions r { get; private set; } = new Restrictions();
    public static void SetupBEHPhase(SMHandoff smh) {
        if (smh.Exec != Exec || Tracking == null) return;
        var cr = Tracking.Value;
        if (cr.challenge is WithinC inside) StayInRange(Exec, inside.units)(smh);
        else if (cr.challenge is WithoutC outside) StayOutRange(Exec, outside.units)(smh);
    }

    public static void TrackChallenge(GameRequest gr, ChallengeRequest c) {
        IEnumerator cor;
        if (c.challenge is TrivialConditionC) cor = TrackTrivial(gr, c);
        else if (c.challenge is DestroyTimedC dt) cor = TrackDestroyTimed(gr, c, dt);
        else if (c.challenge is DestroyC d) cor = TrackDestroy(gr, c, d);
        else if (c.challenge is GrazeC g) cor = TrackGraze(gr, c, g);
        else if (c.challenge is DialogueC dg) cor = TrackDialogue(gr, c, dg);
        else if (c.challenge is WithinC within) cor = TrackWithin(gr, c, within);
        else if (c.challenge is WithoutC without) cor = TrackWithout(gr, c, without);
        else throw new Exception($"Couldn't resolve challenge type for {c.Description}");
        Log.Unity($"Tracking challenge {c.Description}");
        //Prevents load lag if this is executed on scene change while camera transition is up
        StateMachineManager.FromText(c.Boss.stateMachine);
        Completion = null;
        Tracking = c;
        r = new Restrictions(c.challenge);
        Exec = null;
        UIManager.RequestChallengeDisplay(c);
        main.RunDroppableRIEnumerator(cor);
    }

    private static void ChallengeFailed(GameRequest gr, ChallengeRequest cr) {
        Log.Unity($"FAILED challenge {cr.Description}");
        UIManager.MessageChallengeEnd(false, out float t);
        if (Exec != null) Exec.ShiftPhase();
        WaitingUtils.WaitThenCB(main, CancellationToken.None, t, false, () => {
            Core.Events.TryHitPlayer.Invoke((999, true));
        });
    }

    private static void ChallengeSuccess(GameRequest gr, ChallengeRequest cr) {
        Log.Unity($"PASSED challenge {cr.Description}");
        if (!Replayer.IsReplaying) {
            Log.Unity("Committing challenge to save data");
            SaveData.r.CompleteChallenge(gr, cr);
            SaveData.SaveRecord();
        }
        var next = cr.NextChallenge;
        if (next != null && Exec != null) {
            var e = Exec;
            Replayer.Cancel(); //can't replay both scenes together
            Log.Unity($"Autoproceeding to next challenge: {next.Value.Description}");
            GameRequest.LastGame = new GameRequest(gr.cb, gr.difficulty, challenge: next.Value, shot: gr.shot);
            TrackChallenge(gr, next.Value);
            LinkBEH(e);
            e.RunAttachedSM();
        } else {
            UIManager.MessageChallengeEnd(true, out float t);
            WaitingUtils.WaitThenCB(main, CancellationToken.None, t, false, gr.vFinishAndPostReplay);
        }
    }

    private static void ChallengeSuccessIf(GameRequest gr, ChallengeRequest cr, bool cond) {
        if (cond) ChallengeSuccess(gr, cr);
        else ChallengeFailed(gr, cr);
    }

    private static IEnumerator TrackDialogue(GameRequest gr, ChallengeRequest cr, DialogueC c) => TrackTrivial(gr, cr);
    private static IEnumerator TrackTrivial(GameRequest gr, ChallengeRequest cr) {
        while (Completion == null) yield return null;
        ChallengeSuccess(gr, cr);
    }
    private static IEnumerator TrackDestroy(GameRequest gr, ChallengeRequest cr, DestroyC c) {
        while (Completion == null) yield return null;
        ChallengeSuccessIf(gr, cr, Completion.Value.Cleared == true);
    }

    /// <summary>
    /// TimeoutOverride handles timeout shenanigans
    /// </summary>
    private static IEnumerator TrackDestroyTimed(GameRequest gr, ChallengeRequest cr, DestroyTimedC c) => TrackDestroy(gr, cr, c);
    private static IEnumerator TrackGraze(GameRequest gr, ChallengeRequest cr, GrazeC c) {
        while (Completion == null) yield return null;
        ChallengeSuccessIf(gr, cr, GameManagement.campaign.Graze >= c.graze);
    }

    private static IEnumerator TrackWithin(GameRequest gr, ChallengeRequest cr, WithinC c) {
        while (Exec == null) yield return null;
        for (float t = 0; Completion == null; t += ETime.FRAME_TIME) {
            if (t > c.yield && (Exec.rBPI.loc - main.player.location).magnitude > c.units) {
                ChallengeFailed(gr, cr);
                yield break;
            }
            yield return null;
        }
        ChallengeSuccess(gr, cr);
    }
    private static IEnumerator TrackWithout(GameRequest gr, ChallengeRequest cr, WithoutC c) {
        while (Exec == null) yield return null;
        for (float t = 0; Completion == null; t += ETime.FRAME_TIME) {
            if (t > c.yield && (Exec.rBPI.loc - main.player.location).magnitude < c.units) {
                ChallengeFailed(gr, cr);
                yield break;
            }
            yield return null;
        }
        ChallengeSuccess(gr, cr);
    }

    public SOCircle player;
}