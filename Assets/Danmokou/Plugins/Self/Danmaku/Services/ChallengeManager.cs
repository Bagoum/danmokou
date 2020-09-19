using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Danmaku;
using DMath;
using JetBrains.Annotations;
using SM;
using static Challenge;
using static Danmaku.Enums;

public class ChallengeManager : CoroutineRegularUpdater {
    private static ChallengeManager main;

    private void Awake() {
        main = this;
        CleanupState();
        r = new Restrictions();
    }

    private void OnDestroy() {
        main = null;
        CleanupState();
    }

    private static void CleanupState() {
        Exec = null;
        Completion = null;
        Tracking = null;
        r = new Restrictions();
    }

    public static void ReceivePhaseCompletion(PhaseCompletion pc) {
        if (pc.clear != PhaseClearMethod.CANCELLED && pc.props.phaseType != null && pc.exec == Exec) Completion = pc;
    }

    public static PhaseCompletion? Completion { get; private set; } = null;
    [CanBeNull] public static IChallengeRequest Tracking { get; private set; } = null;

    public static float? BossTimeoutOverride([CanBeNull] BossConfig bc) => 
        (Tracking?.ControlsBoss(bc) == true) ? r.TimeoutOverride : null;

    [CanBeNull] private static BehaviorEntity Exec;


    public class Restrictions {
        public readonly bool HorizAllowed = true;
        public readonly bool VertAllowed = true;

        public readonly bool FocusAllowed = true;
        public readonly bool FocusForced = false;

        public readonly float? TimeoutOverride = null;

        public Restrictions() {}
        public Restrictions(Challenge[] cs) {
            foreach (var c in cs) {
                if (c is NoHorizC) HorizAllowed = false;
                else if (c is NoVertC) VertAllowed = false;
                else if (c is NoFocusC) FocusAllowed = false;
                else if (c is AlwaysFocusC) FocusForced = true;
                else if (c is DestroyTimedC dtc) TimeoutOverride = dtc.time;
            }
        }
    }
    public static Restrictions r { get; private set; } = new Restrictions();
    public static void SetupBEHPhase(SMHandoff smh) {
        if (smh.Exec != Exec || Tracking == null) return;
        var cs = Tracking.Challenges;
        for (int ii = 0; ii < cs.Length; ++ii) cs[ii].SetupPhase(smh);
    }

    public static void LinkBEH(BehaviorEntity exec) {
        if (Tracking == null) throw new Exception("Cannot link BEH when no challenge is tracked");
        Tracking.Start(Exec = exec);
    }
    public static void TrackChallenge(IChallengeRequest cr) {
        Log.Unity($"Tracking challenge {cr.Description}");
        Completion = null;
        Tracking = cr;
        r = new Restrictions(cr.Challenges);
        Exec = null;
        challengePhotos.Clear();
        cr.Initialize();
        main.RunDroppableRIEnumerator(main.TrackChallenges(cr));
    }

    private static void ChallengeFailed(IChallengeRequest cr, TrackingContext ctx) {
        cr.OnFail(ctx);
        CleanupState();
    }

    private static void ChallengeSuccess(IChallengeRequest cr, TrackingContext ctx) {
        cr.OnSuccess(ctx);
        CleanupState();
    }

    //This is not controlled by smh.cT because its scope is the entire segment over which the challenge executes,
    //not just the boss phase. In the case of BPoHC stage events, this scope is the phase cT of the stage section.
    private IEnumerator TrackChallenges(IChallengeRequest cr) {
        while (Exec == null) yield return null;
        var challenges = cr.Challenges;
        var ctx = new TrackingContext(Exec, this);
        
        for (; Completion == null; ctx.t += ETime.FRAME_TIME) {
            for (int ii = 0; ii < challenges.Length; ++ii) {
                if (!challenges[ii].FrameCheck(ctx)) {
                    ChallengeFailed(cr, ctx);
                    yield break;
                }
            }
            yield return null;
        }
        for (int ii = 0; ii < challenges.Length; ++ii) {
            if (!challenges[ii].EndCheck(ctx, Completion.Value)) {
                ChallengeFailed(cr, ctx);
                yield break;
            }
        }
        ChallengeSuccess(cr, ctx);
    }

    public struct TrackingContext {
        public readonly BehaviorEntity exec;
        public readonly ChallengeManager cm;
        public float t;

        public TrackingContext(BehaviorEntity exec, ChallengeManager cm) {
            this.exec = exec;
            this.cm = cm;
            this.t = 0;
        }
    }

    
    private static readonly List<AyaPhoto> challengePhotos = new List<AyaPhoto>();
    public static IEnumerable<AyaPhoto> ChallengePhotos => challengePhotos;

    public static void SubmitPhoto(AyaPhoto p) {
        //There are no restrictions on what type of challenge may receive a photo
        if (Tracking != null) {
            challengePhotos.Add(p);
        }
    }
    
}