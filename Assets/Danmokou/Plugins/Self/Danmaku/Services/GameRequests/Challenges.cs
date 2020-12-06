using System;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using DMath;
using JetBrains.Annotations;
using SM;
using static SM.SMAnalysis;
using GameLowRequest = DU<Danmaku.CampaignRequest, Danmaku.BossPracticeRequest, 
    PhaseChallengeRequest, Danmaku.StagePracticeRequest>;


public interface IChallengeRequest {
    
    string Description { get; }
    Challenge[] Challenges { get; }

    bool ControlsBoss([CanBeNull] BossConfig boss);
    
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
    public readonly GameRequest gr;
    public readonly Enums.FixedDifficulty difficulty;

    public SceneChallengeReqest(GameRequest gr, PhaseChallengeRequest cr) {
        this.gr = gr;
        this.cr = cr;
        difficulty = gr.metadata.difficulty.standard ?? 
                     throw new Exception("Scene challenges must used fixed difficulties");
    }

    public void Initialize() {
        //Prevents load lag if this is executed on scene change while camera transition is up
        StateMachineManager.FromText(cr.Boss.stateMachine);
        UIManager.RequestChallengeDisplay(cr, gr.metadata);
    }

    public void Start(BehaviorEntity exec) {
        exec.phaseController.Override(cr.phase.phase.index, () => { });
        exec.RunSMFromScript(cr.Boss.stateMachine);
    }

    public bool OnSuccess(ChallengeManager.TrackingContext ctx) {
        Log.Unity($"PASSED challenge {cr.Description}");
        if (gr.Saveable) {
            Log.Unity("Committing challenge to save data");
            SaveData.r.CompleteChallenge(gr, ctx.cm.ChallengePhotos);
        }
        
        if (gr.replay == null && cr.NextChallenge(gr.metadata).Try(out var next)) {
            Replayer.Cancel(); //can't replay both scenes together
            Log.Unity($"Autoproceeding to next challenge: {next.Description}");
            StaticNullableStruct.LastGame = new GameRequest(gr.cb, gr.metadata, new GameLowRequest(next), 
                true, null);
            ctx.cm.TrackChallenge(new SceneChallengeReqest(
                StaticNullableStruct.LastGame.Value, next));
            ctx.cm.LinkBoss(ctx.exec);
            return false;
        } else {
            UIManager.MessageChallengeEnd(true, out float t);
            WaitingUtils.WaitThenCB(ctx.cm, Cancellable.Null, t, false, gr.vFinishAndPostReplay);
            return true;
        }
    }

    public void OnFail(ChallengeManager.TrackingContext ctx) {
        Log.Unity($"FAILED challenge {cr.Description}");
        UIManager.MessageChallengeEnd(false, out float t);
        if (ctx.exec != null) ctx.exec.ShiftPhase();
        WaitingUtils.WaitThenCB(ctx.cm, Cancellable.Null, t, false, () => {
            BulletManager.PlayerTarget.Player.Hit(999, true);
        });
    }

    public string Description => cr.Description;

    public Challenge[] Challenges => new[] {cr.challenge};

    public bool ControlsBoss([CanBeNull] BossConfig boss) => cr.Boss == boss;

}

public readonly struct PhaseChallengeRequest {
    public DayCampaignConfig Campaign => phase.boss.day.campaign.campaign;
    public DayConfig Day => phase.boss.day.day;
    public BossConfig Boss => phase.boss.boss;
    public readonly DayPhase phase;
    public readonly Challenge challenge;
    public int ChallengeIdx => phase.challenges.IndexOf(challenge);
    public string Description => challenge.Description(Boss);
    public PhaseChallengeRequest? NextChallenge(GameMetadata d) {
        if (challenge is Challenge.DialogueC dc && dc.point == Challenge.DialogueC.DialoguePoint.INTRO) {
            if (phase.Next?.CompletedOne(d) == false) return new PhaseChallengeRequest(phase.Next);
        } else if (phase.Next?.challenges?.Try(0) is Challenge.DialogueC dce &&
                   dce.point == Challenge.DialogueC.DialoguePoint.CONCLUSION && phase.Next?.Enabled(d) == true
                   && phase.Next?.CompletedOne(d) == false) {
            return new PhaseChallengeRequest(phase.Next);
        }
        return null;
    }

    public PhaseChallengeRequest(DayPhase p, int index = 0) {
        phase = p;
        challenge = p.challenges[index];
    }
    public PhaseChallengeRequest(DayPhase p, Challenge c) {
        phase = p;
        challenge = c;
    }

    public ((((string, int), int), int), int) Key => (phase.Key, ChallengeIdx);

    public static PhaseChallengeRequest Reconstruct(((((string, int), int), int), int) key) {
        var phase = DayPhase.Reconstruct(key.Item1);
        return new PhaseChallengeRequest(phase, phase.challenges[key.Item2]);
    }
}
public abstract class Challenge {
    public abstract string Description(BossConfig boss);
    public virtual void SetupPhase(SMHandoff smh) {}
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
        public override string Description(BossConfig boss) => "Don't die".Locale("死なないで");
    }

    public class NoHorizC : Challenge {
        public override string Description(BossConfig boss) => "You cannot move left/right".Locale("左右の動きは出来ない");
    }
    public class NoVertC : Challenge {
        public override string Description(BossConfig boss) => "You cannot move up/down".Locale("上下の動きは出来ない");
    }

    public class NoFocusC : Challenge {
        public override string Description(BossConfig boss) => "You cannot use slow movement".Locale("低速移動は出来ない");
    }
    public class AlwaysFocusC : Challenge {
        public override string Description(BossConfig boss) => "You cannot use fast movement".Locale("高速移動は出来ない");
    }
    
    public class DialogueC : Challenge {
        public enum DialoguePoint {
            INTRO,
            CONCLUSION
        }
        public override string Description(BossConfig boss) => $"Have a chat with {boss.CasualName}".Locale($"{boss.CasualName}との会話");
        public readonly DialoguePoint point;
        public DialogueC(DialoguePoint point) {
            this.point = point;
        }
    }

    public class WithinC : Challenge {
        public readonly float units;
        public readonly float yield = 4;
        public override string Description(BossConfig boss) => $"Stay close to {boss.CasualName}".Locale($"{boss.CasualName}から離れないで");
        public WithinC(float units) {
            this.units = units;
        }
        private static ReflWrap<TP4> StayInColor => (Func<TP4>)"witha lerpt 0 1 0 0.3 green".Into<TP4>;

        private static ReflWrap<TaskPattern> StayInRange(BehaviorEntity beh, float f) => 
            (Func<TaskPattern>) (() => SMReflection.Sync("_", GCXFRepo.RV2Zero,
            AtomicPatterns.RelCirc("_", new BEHPointer(beh), 
                _ => ExMRV2.RXY(f, f), StayInColor)));

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
        public override string Description(BossConfig boss) => $"Social distance from {boss.CasualName}".Locale($"{boss.CasualName}に近寄らないで");
        public WithoutC(float units) {
            this.units = units;
        }
        
        private static ReflWrap<TP4> StayOutColor => (Func<TP4>)"witha lerpt 0 1 0 0.3 red".Into<TP4>;

        private static ReflWrap<TaskPattern> StayOutRange(BehaviorEntity beh, float f) => 
            (Func<TaskPattern>) (() => SMReflection.Sync("_", GCXFRepo.RV2Zero,
                AtomicPatterns.RelCirc("_", new BEHPointer(beh), 
                    _ => ExMRV2.RXY(f, f), StayOutColor)));

        public override void SetupPhase(SMHandoff smh) {
            StayOutRange(smh.Exec, units).Value(smh);
        }
        public override bool FrameCheck(ChallengeManager.TrackingContext ctx) {
            return ctx.t < yield || (ctx.exec.rBPI.loc - GameManagement.VisiblePlayerLocation).magnitude > units;
        }
    }
    public class DestroyC : Challenge {
        public override string Description(BossConfig boss) => $"Defeat {boss.CasualName}".Locale($"{boss.CasualName}を倒せ");

        public override bool EndCheck(ChallengeManager.TrackingContext ctx, PhaseCompletion pc) => pc.Cleared == true;
    }

    public class GrazeC : Challenge {
        public readonly int graze;
        public override string Description(BossConfig boss) => $"Get {graze} graze".Locale($"グレイズを{graze}回しろ");

        public override bool EndCheck(ChallengeManager.TrackingContext ctx, PhaseCompletion pc) =>
            GameManagement.campaign.Graze >= graze;
        public GrazeC(int g) {
            graze = g;
        }
    }

    public class DestroyTimedC : DestroyC {
        public readonly float time;
        public override string Description(BossConfig boss) => $"Defeat {boss.casualName} within {time}s".Locale($"{boss.CasualName}を{time}秒以内で倒せ");

        public DestroyTimedC(float t) {
            time = t;
        }
    }
}

public class ChallengeCompletion {
    public AyaPhoto[] photos = new AyaPhoto[0];

    public ChallengeCompletion() { } //JSON constructor
    public ChallengeCompletion(IEnumerable<AyaPhoto> ps) {
        photos = ps.ToArray();
    }
}

