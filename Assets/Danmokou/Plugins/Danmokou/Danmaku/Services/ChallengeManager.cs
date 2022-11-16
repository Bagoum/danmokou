using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using Danmokou.SM;
using static Danmokou.GameInstance.Challenge;

namespace Danmokou.Services {
public interface IChallengeManager {
    float? BossTimeoutOverride(BossConfig? bc);
    Task<InstanceRecord> TrackChallenge(IChallengeRequest cr, ICancellee cT);
    void SetupBossPhase(SMHandoff smh);
    void LinkBoss(BehaviorEntity exec, ICancellee cT);
    IEnumerable<AyaPhoto> ChallengePhotos { get; }

    ChallengeManager.Restrictions Restriction { get; }
}

public class ChallengeManager : CoroutineRegularUpdater, IChallengeManager {
    private readonly List<AyaPhoto> challengePhotos = new();
    public IEnumerable<AyaPhoto> ChallengePhotos => challengePhotos;

    private PhaseCompletion? completion = null;
    private IChallengeRequest? tracking = null;
    private BehaviorEntity? Exec { get; set; }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IChallengeManager>(this, new ServiceLocator.ServiceOptions { Unique = true });

        Listen(GameManagement.EvInstance, inst => inst.PhaseCompleted, pc => {
            if (pc.clear != PhaseClearMethod.CANCELLED && pc.phase.PhaseType != null && pc.exec == Exec)
                completion = pc;
        });
        
        Listen(AyaCamera.PhotoTaken, photo => {
            //There are no restrictions on what type of challenge may receive a photo
            if (tracking != null && photo.success) {
                challengePhotos.Add(photo.photo);
            }
        });
    }

    private void OnDestroy() {
        CleanupState();
    }

    private void CleanupState() {
        completion = null;
        Exec = null;
        tracking = null;
        Restriction = new Restrictions();
    }

    public float? BossTimeoutOverride(BossConfig? bc) =>
        (tracking?.ControlsBoss(bc) == true) ? Restriction.TimeoutOverride : null;



    public class Restrictions {
        public static readonly Restrictions Default = new();
        public readonly bool HorizAllowed = true;
        public readonly bool VertAllowed = true;

        public readonly bool FocusAllowed = true;
        public readonly bool FocusForced = false;

        public readonly float? TimeoutOverride = null;

        public Restrictions() { }

        public Restrictions(Challenge[] cs) {
            foreach (var c in cs) {
                if (c is NoHorizC)
                    HorizAllowed = false;
                else if (c is NoVertC)
                    VertAllowed = false;
                else if (c is NoFocusC)
                    FocusAllowed = false;
                else if (c is AlwaysFocusC)
                    FocusForced = true;
                else if (c is DestroyTimedC dtc)
                    TimeoutOverride = dtc.time;
            }
        }
    }

    public Restrictions Restriction { get; private set; } = new();

    public void SetupBossPhase(SMHandoff smh) {
        if (smh.Exec != Exec || tracking == null) return;
        var cs = tracking.Challenges;
        for (int ii = 0; ii < cs.Length; ++ii) cs[ii].SetupPhase(smh);
    }

    public void LinkBoss(BehaviorEntity exec, ICancellee cT) {
        if (tracking == null) throw new Exception("Cannot link BEH when no challenge is tracked");
        Logs.Log($"Linked boss {exec.ID} to challenge {tracking.Description}");
        tracking.Start(Exec = exec, cT);
    }

    public Task<InstanceRecord> TrackChallenge(IChallengeRequest cr, ICancellee cT) =>
        TrackChallenge(cr, cT, new TaskCompletionSource<InstanceRecord>());
    public Task<InstanceRecord> TrackChallenge(IChallengeRequest cr, ICancellee cT, TaskCompletionSource<InstanceRecord> tcs) {
        Logs.Log($"Tracking challenge {cr.Description}");
        CleanupState();
        tracking = cr;
        Restriction = new Restrictions(cr.Challenges);
        challengePhotos.Clear();
        cr.Initialize();
        RunRIEnumerator(TrackChallenges(cr, tcs, cT));
        return tcs.Task;
    }

    private void ChallengeFailed(IChallengeRequest cr, TrackingContext ctx) {
        cr.OnFail(ctx);
        CleanupState();
    }

    private void ChallengeSuccess(IChallengeRequest cr, TrackingContext ctx) {
        if (cr.OnSuccess(ctx)) CleanupState();
    }

    //This is not controlled by smh.cT because its scope is the entire segment over which the challenge executes,
    //not just the boss phase. In the case of BPoHC stage events, this scope is the phase cT of the stage section.
    private IEnumerator TrackChallenges(IChallengeRequest cr, TaskCompletionSource<InstanceRecord> tcs, ICancellee cT) {
        while (Exec == null) {
            if (cT.Cancelled) goto cancelled;
            yield return null;
        }
        var challenges = cr.Challenges;
        var ctx = new TrackingContext(Exec, this, tcs, cT);

        for (; completion == null; ctx.t += ETime.FRAME_TIME) {
            if (cT.Cancelled) goto cancelled;
            for (int ii = 0; ii < challenges.Length; ++ii) {
                if (!challenges[ii].FrameCheck(ctx)) {
                    ChallengeFailed(cr, ctx);
                    yield break;
                }
            }
            yield return null;
        }
        for (int ii = 0; ii < challenges.Length; ++ii) {
            if (cT.Cancelled) goto cancelled;
            if (!challenges[ii].EndCheck(ctx, completion.Value)) {
                ChallengeFailed(cr, ctx);
                yield break;
            }
        }
        ChallengeSuccess(cr, ctx);
        yield break;
        cancelled: ;
        tcs.SetCanceled();
    }

    public struct TrackingContext {
        public readonly BehaviorEntity exec;
        public readonly ChallengeManager cm;
        public readonly TaskCompletionSource<InstanceRecord> tracker;
        public float t;
        public readonly ICancellee cT;

        public TrackingContext(BehaviorEntity exec, ChallengeManager cm, TaskCompletionSource<InstanceRecord> tracker, ICancellee cT) {
            this.exec = exec;
            this.cm = cm;
            this.tracker = tracker;
            this.t = 0;
            this.cT = cT;
        }
    }
}
}