using System;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using JetBrains.Annotations;
using static SM.SMAnalysis;


public readonly struct ChallengeRequest {
    public DayConfig Day => phase.boss.day.day;
    public BossConfig Boss => phase.boss.boss;
    public readonly DayPhase phase;
    public readonly Challenge challenge;
    public int ChallengeIdx => phase.challenges.IndexOf(challenge);
    public string Description => challenge.Description(Boss);
    public ChallengeRequest? NextChallenge {
        get {
            if (challenge is Challenge.DialogueC dc && dc.point == Challenge.DialogueC.DialoguePoint.INTRO) {
                if (phase.Next?.CompletedOne == false) return new ChallengeRequest(phase.Next);
            } else if (phase.Next?.challenges?.Try(0) is Challenge.DialogueC dce &&
                       dce.point == Challenge.DialogueC.DialoguePoint.CONCLUSION && phase.Next?.Enabled == true
                       && phase.Next?.CompletedOne == false) {
                return new ChallengeRequest(phase.Next);
            }
            return null;
        }
    }

    public ChallengeRequest(DayPhase p) {
        phase = p;
        challenge = p.challenges[0];
    }
    public ChallengeRequest(DayPhase p, Challenge c) {
        phase = p;
        challenge = c;
    }

    public (((string, int), int), int) Key => (phase.Key, ChallengeIdx);

    public static ChallengeRequest Reconstruct((((string, int), int), int) key) {
        var phase = DayPhase.Reconstruct(key.Item1);
        return new ChallengeRequest(phase, phase.challenges[key.Item2]);
    }
}
public abstract class Challenge {
    public abstract string Description(BossConfig boss);
    
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

    public abstract class TrivialConditionC : Challenge {
    }

    public class SurviveC : TrivialConditionC {
        public override string Description(BossConfig boss) => "Don't die".Locale("死なないで");
    }

    public class NoHorizC : TrivialConditionC {
        public override string Description(BossConfig boss) => "You cannot move left/right".Locale("左右の動きは出来ない");
    }
    public class NoVertC : TrivialConditionC {
        public override string Description(BossConfig boss) => "You cannot move up/down".Locale("上下の動きは出来ない");
    }

    public class NoFocusC : TrivialConditionC {
        public override string Description(BossConfig boss) => "You cannot use slow movement".Locale("低速移動は出来ない");
    }
    public class AlwaysFocusC : TrivialConditionC {
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
    }
    public class WithoutC : Challenge {
        public readonly float units;
        public readonly float yield = 4;
        public override string Description(BossConfig boss) => $"Social distance from {boss.CasualName}".Locale($"{boss.CasualName}に近寄らないで");
        public WithoutC(float units) {
            this.units = units;
        }
    }
    public class DestroyC : Challenge {
        public override string Description(BossConfig boss) => $"Defeat {boss.CasualName}".Locale($"{boss.CasualName}を倒せ");
    }

    public class GrazeC : Challenge {
        public readonly int graze;
        public override string Description(BossConfig boss) => $"Get {graze} graze".Locale($"グレイズを{graze}回しろ");

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


public struct ChallengeCompletion {
}

