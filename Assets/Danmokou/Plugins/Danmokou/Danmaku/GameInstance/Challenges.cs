using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.GameInstance;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using Danmokou.SM;
using Danmokou.UI;


namespace Danmokou.GameInstance {
public interface IChallengeRequest {

    InstanceRequest Requester { get; }
    LString Description { get; }
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
        ServiceLocator.Find<IUIManager>().DisplayChallenge(cr, Requester.metadata);
    }

    public void Start(BehaviorEntity exec) {
        exec.phaseController.Override(cr.phase.phase.index, () => { });
        exec.RunSMFromScript(cr.Boss.stateMachine);
    }

    public bool OnSuccess(ChallengeManager.TrackingContext ctx) {
        Logs.Log($"PASSED challenge {cr.Description}");
        //This saves completion. Needs to be done locally in case of continuations.
        //The callback will handle replaying.
        var record = Requester.MakeGameRecord(ctx.cm.ChallengePhotos.ToArray());
        Requester.TrySave(record);

        if (!(Requester.replay is ReplayMode.Replaying) && cr.NextChallenge(Requester.metadata).Try(out var nextC)) {
            Logs.Log($"Autoproceeding to next challenge: {nextC.Description}");
            var nextGr = new InstanceRequest(Requester.cb, Requester.metadata, nextC);
            nextGr.SetupInstance();
            GameManagement.Instance.Replay?.Cancel(); //can't replay both scenes together,
            //or even just the second scene due to time-dependency of world objects such as shots
            ctx.cm.TrackChallenge(new SceneChallengeReqest(nextGr, nextC), ctx.onSuccess);
            ctx.cm.LinkBoss(ctx.exec);
            return false;
        } else {
            ServiceLocator.Find<IUIManager>().MessageChallengeEnd(true, out _);
            //The callback should have a wait procedure in it
            ctx.onSuccess(record);
            //WaitingUtils.WaitThenCB(ctx.cm, Cancellable.Null, t, false, () => ctx.onSuccess(record));
            return true;
        }
    }

    public void OnFail(ChallengeManager.TrackingContext ctx) {
        Logs.Log($"FAILED challenge {cr.Description}");
        ServiceLocator.Find<IUIManager>().MessageChallengeEnd(false, out float t);
        if (ctx.exec != null) ctx.exec.ShiftPhase();
        WaitingUtils.WaitThenCB(ctx.cm, Cancellable.Null, t, false,
            () => { BulletManager.PlayerTarget.Player.Hit(999, true); });
    }

    public LString Description => cr.Description;

    public Challenge[] Challenges => new[] {cr.challenge};

    public bool ControlsBoss(BossConfig? boss) => cr.Boss == boss;

}

public class PhaseChallengeRequest : ILowInstanceRequest {
    public DayCampaignConfig DayCampaign => phase.boss.day.campaign.campaign;
    public ICampaignMeta Campaign => DayCampaign;
    public DayConfig Day => phase.boss.day.day;
    public BossConfig Boss => phase.boss.boss;
    public readonly SMAnalysis.DayPhase phase;
    public readonly Challenge challenge;
    public int ChallengeIdx => phase.challenges.IndexOf(challenge);
    public LString Description => challenge.Description(Boss);

    public PhaseChallengeRequest? NextChallenge(SharedInstanceMetadata d) {
        if (challenge is Challenge.DialogueC {point: Challenge.DialogueC.DialoguePoint.INTRO}) {
            if (phase.Next?.CompletedOne(d) == false) return new PhaseChallengeRequest(phase.Next);
        } else if (phase.Next?.challenges.Try(0) is Challenge.DialogueC {point: Challenge.DialogueC.DialoguePoint.CONCLUSION} 
                   && phase.Next?.Enabled(d) == true && phase.Next?.CompletedOne(d) == false) {
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

    public ILowInstanceRequestKey Key => new PhaseChallengeRequestKey() {
        Campaign = DayCampaign.key,
        Boss = Boss.key,
        PhaseIndex = phase.phase.index
    };
    public InstanceMode Mode => InstanceMode.SCENE_CHALLENGE;
}

[Reflect]
public abstract class Challenge {
    public abstract LString Description(BossConfig boss);
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
        public override LString Description(BossConfig boss) => "Don't die".Locale("死なないで");
    }

    public class NoHorizC : Challenge {
        public override LString Description(BossConfig boss) => "You cannot move left/right".Locale("左右の動きは出来ない");
    }

    public class NoVertC : Challenge {
        public override LString Description(BossConfig boss) => "You cannot move up/down".Locale("上下の動きは出来ない");
    }

    public class NoFocusC : Challenge {
        public override LString Description(BossConfig boss) => "You cannot use slow movement".Locale("低速移動は出来ない");
    }

    public class AlwaysFocusC : Challenge {
        public override LString Description(BossConfig boss) => "You cannot use fast movement".Locale("高速移動は出来ない");
    }

    public class DialogueC : Challenge {
        public enum DialoguePoint {
            INTRO,
            CONCLUSION
        }

        public override LString Description(BossConfig boss) =>
            $"Have a chat with {boss.CasualName}".Locale($"{boss.CasualName}との会話");

        public readonly DialoguePoint point;

        public DialogueC(DialoguePoint point) {
            this.point = point;
        }
    }

    public class WithinC : Challenge {
        public readonly float units;
        public readonly float yield = 4;

        public override LString Description(BossConfig boss) =>
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

        public override LString Description(BossConfig boss) =>
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
        public override LString Description(BossConfig boss) =>
            $"Defeat {boss.CasualName}".Locale($"{boss.CasualName}を倒せ");

        public override bool EndCheck(ChallengeManager.TrackingContext ctx, PhaseCompletion pc) => pc.Cleared == true;
    }

    public class GrazeC : Challenge {
        public readonly int graze;
        public override LString Description(BossConfig boss) => $"Get {graze} graze".Locale($"グレイズを{graze}回しろ");

        public override bool EndCheck(ChallengeManager.TrackingContext ctx, PhaseCompletion pc) =>
            GameManagement.Instance.Graze >= graze;

        public GrazeC(int g) {
            graze = g;
        }
    }

    public class DestroyTimedC : DestroyC {
        public readonly float time;

        public override LString Description(BossConfig boss) =>
            $"Defeat {boss.CasualName} within {time}s".Locale($"{boss.CasualName}を{time}秒以内で倒せ");

        public DestroyTimedC(float t) {
            time = t;
        }
    }
}

}