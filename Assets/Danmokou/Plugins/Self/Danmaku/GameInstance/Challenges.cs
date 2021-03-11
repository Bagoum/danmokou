using System;
using System.Collections.Generic;
using System.Linq;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku;
using DMK.Danmaku.Patterns;
using DMK.DMath;
using DMK.DMath.Functions;
using DMK.GameInstance;
using DMK.Reflection;
using DMK.Scriptables;
using DMK.Services;
using JetBrains.Annotations;
using DMK.SM;
using DMK.UI;
using InstanceLowRequest = DMK.Core.DU<DMK.GameInstance.CampaignRequest, DMK.GameInstance.BossPracticeRequest, 
    DMK.GameInstance.PhaseChallengeRequest, DMK.GameInstance.StagePracticeRequest>;


namespace DMK.GameInstance {
public interface IChallengeRequest {

    InstanceRequest Requester { get; }
    LocalizedString Description { get; }
    Challenge[] Challenges { get; }

    bool ControlsBoss(BossConfig? boss);

    /// <summary>
    /// Called when the challenge request is constructed immediately after the scene loads
    /// (when the loading screen is still fully up). Use to set UI elements, etc.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Called when the boss has awoken and is ready to receive commands
    /// (when the scene is fully visible).
    /// </summary>
    void Start(BehaviorEntity exec);

    /// <summary>
    /// Returns true if the challenge operations are finished and cleanup may begin.
    /// Sequential challenges will return false.
    /// </summary>
    bool OnSuccess(ChallengeManager.TrackingContext ctx);

    void OnFail(ChallengeManager.TrackingContext ctx);
}

public readonly struct SceneChallengeReqest : IChallengeRequest {
    public readonly PhaseChallengeRequest cr;
    public InstanceRequest Requester { get; }
    public readonly FixedDifficulty difficulty;

    public SceneChallengeReqest(InstanceRequest gr, PhaseChallengeRequest cr) {
        this.Requester = gr;
        this.cr = cr;
        difficulty = Requester.metadata.difficulty.standard ??
                     throw new Exception("Scene challenges must used fixed difficulties");
    }

    public void Initialize() {
        //Prevents load lag if this is executed on scene change while camera transition is up
        StateMachineManager.FromText(cr.Boss.stateMachine);
        UIManager.RequestChallengeDisplay(cr, Requester.metadata);
    }

    public void Start(BehaviorEntity exec) {
        exec.phaseController.Override(cr.phase.phase.index, () => { });
        exec.RunSMFromScript(cr.Boss.stateMachine);
    }

    public bool OnSuccess(ChallengeManager.TrackingContext ctx) {
        Log.Unity($"PASSED challenge {cr.Description}");
        //This saves completion. Needs to be done locally in case of continuations.
        //The callback will handle replaying.
        var record = Requester.MakeGameRecord(ctx.cm.ChallengePhotos.ToArray());
        Requester.TrySave(record);

        if (Requester.replay == null && cr.NextChallenge(Requester.metadata).Try(out var nextC)) {
            Log.Unity($"Autoproceeding to next challenge: {nextC.Description}");
            var nextGr = new InstanceRequest(Requester.cb, Requester.metadata, new InstanceLowRequest(nextC), null);
            nextGr.SetupInstance();
            Replayer.Cancel(); //can't replay both scenes together,
            //or even just the second scene due to time-dependency of world objects such as shots
            ctx.cm.TrackChallenge(new SceneChallengeReqest(nextGr, nextC), ctx.onSuccess);
            ctx.cm.LinkBoss(ctx.exec);
            return false;
        } else {
            UIManager.MessageChallengeEnd(true, out _);
            //The callback should have a wait procedure in it
            ctx.onSuccess(record);
            //WaitingUtils.WaitThenCB(ctx.cm, Cancellable.Null, t, false, () => ctx.onSuccess(record));
            return true;
        }
    }

    public void OnFail(ChallengeManager.TrackingContext ctx) {
        Log.Unity($"FAILED challenge {cr.Description}");
        UIManager.MessageChallengeEnd(false, out float t);
        if (ctx.exec != null) ctx.exec.ShiftPhase();
        WaitingUtils.WaitThenCB(ctx.cm, Cancellable.Null, t, false,
            () => { BulletManager.PlayerTarget.Player.Hit(999, true); });
    }

    public LocalizedString Description => cr.Description;

    public Challenge[] Challenges => new[] {cr.challenge};

    public bool ControlsBoss(BossConfig? boss) => cr.Boss == boss;

}

public readonly struct PhaseChallengeRequest {
    public DayCampaignConfig Campaign => phase.boss.day.campaign.campaign;
    public DayConfig Day => phase.boss.day.day;
    public BossConfig Boss => phase.boss.boss;
    public readonly SMAnalysis.DayPhase phase;
    public readonly Challenge challenge;
    public int ChallengeIdx => phase.challenges.IndexOf(challenge);
    public LocalizedString Description => challenge.Description(Boss);

    public PhaseChallengeRequest? NextChallenge(SharedInstanceMetadata d) {
        if (challenge is Challenge.DialogueC dc && dc.point == Challenge.DialogueC.DialoguePoint.INTRO) {
            if (phase.Next?.CompletedOne(d) == false) return new PhaseChallengeRequest(phase.Next);
        } else if (phase.Next?.challenges.Try(0) is Challenge.DialogueC dce &&
                   dce.point == Challenge.DialogueC.DialoguePoint.CONCLUSION && phase.Next?.Enabled(d) == true
                   && phase.Next?.CompletedOne(d) == false) {
            return new PhaseChallengeRequest(phase.Next);
        }
        return null;
    }

    public PhaseChallengeRequest(SMAnalysis.DayPhase p, int index = 0) {
        phase = p;
        challenge = p.challenges[index];
    }

    public PhaseChallengeRequest(SMAnalysis.DayPhase p, Challenge c) {
        phase = p;
        challenge = c;
    }

    public ((((string, int), string), int), int) Key => (phase.Key, ChallengeIdx);

    public static PhaseChallengeRequest Reconstruct(((((string, int), string), int), int) key) {
        var phase = SMAnalysis.DayPhase.Reconstruct(key.Item1);
        return new PhaseChallengeRequest(phase, phase.challenges[key.Item2]);
    }
}

[Reflect]
public abstract class Challenge {
    public abstract LocalizedString Description(BossConfig boss);
    public virtual void SetupPhase(SMHandoff smh) { }
    public virtual bool FrameCheck(ChallengeManager.TrackingContext ctx) => true;
    public virtual bool EndCheck(ChallengeManager.TrackingContext ctx, PhaseCompletion pc) => true;

    public static Challenge Survive() => new SurviveC();
    public static Challenge Destroy() => new DestroyC();
    public static Challenge Graze(int graze) => new GrazeC(graze);
    public static Challenge DestroyTimed(float seconds) => new DestroyTimedC(seconds);
    public static Challenge IntroDialogue() => new DialogueC(DialogueC.DialoguePoint.INTRO);
    public static Challenge EndDialogue() => new DialogueC(DialogueC.DialoguePoint.CONCLUSION);
    public static Challenge Within(float units) => new WithinC(units);
    public static Challenge Without(float units) => new WithoutC(units);
    public static Challenge NoHorizontal() => new NoHorizC();
    public static Challenge NoVertical() => new NoVertC();
    public static Challenge NoFocus() => new NoFocusC();
    public static Challenge AlwaysFocus() => new AlwaysFocusC();

    public class SurviveC : Challenge {
        public override LocalizedString Description(BossConfig boss) => "Don't die".Locale("死なないで");
    }

    public class NoHorizC : Challenge {
        public override LocalizedString Description(BossConfig boss) => "You cannot move left/right".Locale("左右の動きは出来ない");
    }

    public class NoVertC : Challenge {
        public override LocalizedString Description(BossConfig boss) => "You cannot move up/down".Locale("上下の動きは出来ない");
    }

    public class NoFocusC : Challenge {
        public override LocalizedString Description(BossConfig boss) => "You cannot use slow movement".Locale("低速移動は出来ない");
    }

    public class AlwaysFocusC : Challenge {
        public override LocalizedString Description(BossConfig boss) => "You cannot use fast movement".Locale("高速移動は出来ない");
    }

    public class DialogueC : Challenge {
        public enum DialoguePoint {
            INTRO,
            CONCLUSION
        }

        public override LocalizedString Description(BossConfig boss) =>
            $"Have a chat with {boss.CasualName}".Locale($"{boss.CasualName}との会話");

        public readonly DialoguePoint point;

        public DialogueC(DialoguePoint point) {
            this.point = point;
        }
    }

    public class WithinC : Challenge {
        public readonly float units;
        public readonly float yield = 4;

        public override LocalizedString Description(BossConfig boss) =>
            $"Stay close to {boss.CasualName}".Locale($"{boss.CasualName}から離れないで");

        public WithinC(float units) {
            this.units = units;
        }

        private static readonly ReflWrap<TP4> StayInColor = new ReflWrap<TP4>("witha lerpt 0 1 0 0.3 green");

        private static ReflWrap<TaskPattern> StayInRange(BehaviorEntity beh, float f) =>
            ReflWrap.FromFunc($"Challenge.StayInRange.{f}", () => SMReflection.Sync("_", GCXFRepo.RV2Zero,
                AtomicPatterns.RelCirc("_", new BEHPointer("_", beh), _ => V2RV2.Rot(f, f), StayInColor)));
        
        

        public override void SetupPhase(SMHandoff smh) {
            StayInRange(smh.Exec, units).Value(smh);
        }

        public override bool FrameCheck(ChallengeManager.TrackingContext ctx) {
            return ctx.t < yield || (ctx.exec.rBPI.loc - GameManagement.VisiblePlayerLocation).magnitude < units;
        }
    }

    public class WithoutC : Challenge {
        public readonly float units;
        public readonly float yield = 4;

        public override LocalizedString Description(BossConfig boss) =>
            $"Social distance from {boss.CasualName}".Locale($"{boss.CasualName}に近寄らないで");

        public WithoutC(float units) {
            this.units = units;
        }

        private static readonly ReflWrap<TP4> StayOutColor = new ReflWrap<TP4>("witha lerpt 0 1 0 0.3 red");

        private static ReflWrap<TaskPattern> StayOutRange(BehaviorEntity beh, float f) =>
            ReflWrap.FromFunc($"Challenge.StayOutRange.{f}", () => SMReflection.Sync("_", GCXFRepo.RV2Zero,
                AtomicPatterns.RelCirc("_", new BEHPointer("_", beh), _ => V2RV2.Rot(f, f), StayOutColor)));

        public override void SetupPhase(SMHandoff smh) {
            StayOutRange(smh.Exec, units).Value(smh);
        }

        public override bool FrameCheck(ChallengeManager.TrackingContext ctx) {
            return ctx.t < yield || (ctx.exec.rBPI.loc - GameManagement.VisiblePlayerLocation).magnitude > units;
        }
    }

    public class DestroyC : Challenge {
        public override LocalizedString Description(BossConfig boss) =>
            $"Defeat {boss.CasualName}".Locale($"{boss.CasualName}を倒せ");

        public override bool EndCheck(ChallengeManager.TrackingContext ctx, PhaseCompletion pc) => pc.Cleared == true;
    }

    public class GrazeC : Challenge {
        public readonly int graze;
        public override LocalizedString Description(BossConfig boss) => $"Get {graze} graze".Locale($"グレイズを{graze}回しろ");

        public override bool EndCheck(ChallengeManager.TrackingContext ctx, PhaseCompletion pc) =>
            GameManagement.Instance.Graze >= graze;

        public GrazeC(int g) {
            graze = g;
        }
    }

    public class DestroyTimedC : DestroyC {
        public readonly float time;

        public override LocalizedString Description(BossConfig boss) =>
            $"Defeat {boss.CasualName} within {time}s".Locale($"{boss.CasualName}を{time}秒以内で倒せ");

        public DestroyTimedC(float t) {
            time = t;
        }
    }
}

}