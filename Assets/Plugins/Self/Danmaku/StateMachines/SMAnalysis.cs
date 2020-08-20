using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using static Danmaku.Enums;

namespace SM {

public struct SMPhaseController {
    private enum ControllerType {
        /// <summary>
        /// Go to whatever phase is specified.
        /// </summary>
        DEFAULT,
        /// <summary>
        /// Go to the override phase, and then invoke a callback when the SCRIPT is done.
        /// However, if the normal phase specified is 0 (the setup phase) then go there.
        /// </summary>
        EXTERNAL_OVERRIDE_CONTINUE,
        /// <summary>
        /// Go to the override phase, and then invoke a callback when the PHASE is done.
        /// However, if the normal phase specified is 0 (the setup phase) then go there.
        /// </summary>
        EXTERNAL_OVERRIDE,
        /// <summary>
        /// Same as EXTERNAL_OVERRIDE without allowance for 0.
        /// </summary>
        EXTERNAL_OVERRIDE_SKIP,
        /// <summary>
        /// End execution and run a callback.
        /// </summary>
        WAITING_OVERRIDE_RETURN
    }

    private ControllerType typ;
    private int externalOverride;
    private int normalNextPhase;
    [CanBeNull] private Action callback;

    private SMPhaseController(int normalNext) {
        typ = ControllerType.DEFAULT;
        externalOverride = 0;
        normalNextPhase = normalNext;
        callback = null;
    }
    
    public static SMPhaseController Normal(int firstPhase) => new SMPhaseController(firstPhase);

    /// <summary>
    /// Run a single phase and then hit the callback.
    /// By default, the zero phase (setup phase by convention) is run first, and then it goes to the target phase.
    /// </summary>
    /// <param name="gotoPhase">Target phase</param>
    /// <param name="cb">Callback</param>
    /// <param name="forceZeroOverride">True iff the zero phase should also be skipped</param>
    public void Override(int gotoPhase, [CanBeNull] Action cb = null, bool forceZeroOverride = false) {
        externalOverride = gotoPhase;
        callback = cb;
        typ = forceZeroOverride ? ControllerType.EXTERNAL_OVERRIDE_SKIP : ControllerType.EXTERNAL_OVERRIDE;
    }

    /// <summary>
    /// Run a single phase, then continue the script, and hit a callback when the script is done normally.
    /// </summary>
    public void SetGoTo(int gotoPhase, [CanBeNull] Action cb) {
        externalOverride = gotoPhase;
        callback = cb;
        typ = ControllerType.EXTERNAL_OVERRIDE_CONTINUE;
    }
    /// <summary>
    /// Set a callback that can be run on script end, or on phase end if using override.
    /// </summary>
    public void SetCallback([CanBeNull] Action cb) => callback = cb;

    /// <summary>
    /// </summary>
    /// <returns>-1</returns>
    public int RunEndingCallback() {
        callback?.Invoke();
        callback = null;
        return -1;
    }

    /// <summary>
    /// Set an override, but only if one is not already set.
    /// </summary>
    public void LowPriorityOverride(int gotoPhase, bool forceZeroOverride = false) {
        if (typ == ControllerType.DEFAULT) Override(gotoPhase, callback, forceZeroOverride);
    }

    public void SetDesiredNext(int nxt) => normalNextPhase = nxt;
    
    /// <summary>
    /// </summary>
    /// <param name="requestedNormal">The phase desired by the SM</param>
    /// <returns>The phase the SM should go to. This number may be negative or greater than the phase length,
    /// in which case the SM should stop executing.</returns>
    public int WhatIsNextPhase(int? requestedNormal = null) {
        normalNextPhase = requestedNormal ?? normalNextPhase;
        if (typ == ControllerType.EXTERNAL_OVERRIDE_SKIP ||
            (typ == ControllerType.EXTERNAL_OVERRIDE && normalNextPhase > 0)) {
            typ = ControllerType.WAITING_OVERRIDE_RETURN;
            return externalOverride;
        } else if (typ == ControllerType.EXTERNAL_OVERRIDE_CONTINUE && normalNextPhase > 0) {
            typ = ControllerType.DEFAULT;
            return externalOverride;
        } else if (typ == ControllerType.WAITING_OVERRIDE_RETURN) {
            typ = ControllerType.DEFAULT;
            if (callback != null) return RunEndingCallback();
        }
        return normalNextPhase;
    }

}
public static class SMAnalysis {
    /// <summary>
    /// Analyzed phase construct for normal game card selection.
    /// </summary>
    public readonly struct Phase {
        public readonly PhaseType type;
        [CanBeNull] private readonly string title;
        public readonly int index;
        public string Title {
            get {
                if (type == PhaseType.STAGE) return $"Stage Section {index}";
                if (type == PhaseType.STAGEMIDBOSS) return "Midboss";
                if (type == PhaseType.STAGEENDBOSS) return "Endboss";
                if (type == PhaseType.DIALOGUE) return title ?? "Dialogue";
                return title ?? "!!!UNTITLED PHASE (REPORT ME)!!!";
            }
        }

        public Phase(PhaseType c, int phaseNum, [CanBeNull] string name) {
            type = c;
            title = name;
            index = phaseNum;
        }
    }

    /// <summary>
    /// Analyzed phase construct for scene game menus.
    /// </summary>
    public class DayPhase {
        public enum DayPhaseType {
            DIALOGUE_INTRO,
            CARD,
            DIALOGUE_END
        }

        public readonly Phase phase;
        public readonly Challenge[] challenges;
        public readonly DayPhaseType type;
        private readonly int cardIndex;
        private readonly int combatCardIndex;
        private string Introduction => "Introduction".Locale("紹介");
        private string Conclusion => "Conclusion".Locale("結末");
        [CanBeNull]
        private string _Title {
            get {
                if (type == DayPhaseType.DIALOGUE_INTRO) return $"{boss.boss.CasualName} {Introduction}";
                else if (type == DayPhaseType.DIALOGUE_END) return $"{boss.boss.CasualName} {Conclusion}";
                else return $"{boss.boss.CasualName} {combatCardIndex}";
                
            }
        }
        public string Title => (boss.Enabled) ? (_Title ?? phase.Title) : "??? Locked ???";
        public readonly AnalyzedDayBoss boss;
        public bool Completed(int cIndex) => SaveData.r.ChallengeCompleted(this, cIndex);
        public bool CompletedOne => SaveData.r.PhaseCompletedOne(this);
        public bool CompletedAll => SaveData.r.PhaseCompletedAll(this);
        public bool Enabled {
            get {
                if (!boss.Enabled) return false;
                else if (type == DayPhaseType.DIALOGUE_INTRO) {
                    return boss.bossIndex == 0 || boss.day.bosses[boss.bossIndex - 1].FirstPhaseCompletedOne;
                } else if (type == DayPhaseType.CARD) return boss.phases[0].CompletedOne;
                else if (type == DayPhaseType.DIALOGUE_END) return boss.phases.All(p => p == this || p.CompletedOne);
                else return false;
            }
        }
        [CanBeNull] public DayPhase Next => boss.phases.Try(cardIndex + 1);

        public DayPhase(AnalyzedDayBoss b, Phase p, 
            IEnumerable<Challenge> challenges, DayPhaseType type, int cardIndex, int combatCardIndex) {
            this.phase = p;
            this.challenges = challenges.ToArray();
            this.type = type;
            this.cardIndex = cardIndex;
            this.combatCardIndex = combatCardIndex;
            boss = b;
        }
    }

    public static List<Phase> Analyze(PatternSM pat, bool ignoreZero = true) {
        var ret = new List<Phase>();
        foreach (var (i, p) in pat.phases.Enumerate()) {
            if (ignoreZero && i == 0) continue;
            if (p.props.phaseType.HasValue) {
                ret.Add(new Phase(p.props.phaseType.Value, i, p.props.cardTitle));
            }
        }
        return ret;
    }

    public static List<DayPhase> AnalyzeDay(AnalyzedDayBoss boss, PatternSM pat, bool ignoreZero = true) {
        var ret = new List<DayPhase>();
        int combatCardNumber = 0;
        int cardNumber = 0;
        foreach (var (i, p) in pat.phases.Enumerate()) {
            if (ignoreZero && i == 0) continue;
            if (p.props.phaseType.HasValue && p.props.challenges.Count > 0) {
                var asDp = (p.props.challenges.Try(0) as Challenge.DialogueC)?.point;
                var typ = asDp == Challenge.DialogueC.DialoguePoint.INTRO ? DayPhase.DayPhaseType.DIALOGUE_INTRO :
                    asDp == Challenge.DialogueC.DialoguePoint.CONCLUSION ? DayPhase.DayPhaseType.DIALOGUE_END :
                    DayPhase.DayPhaseType.CARD;
                if (typ == DayPhase.DayPhaseType.CARD) ++combatCardNumber;
                ret.Add(new DayPhase(boss, new Phase(p.props.phaseType.Value, i, p.props.cardTitle),
                    p.props.challenges, typ, cardNumber++, combatCardNumber));
            }
        }
        return ret;
    }
    
    
    public readonly struct AnalyzedStage {
        public readonly StageConfig stage;
        public readonly List<Phase> phases;
        public AnalyzedStage(StageConfig s) {
            stage = s;
            phases = SMAnalysis.Analyze(StateMachineManager.FromText(s.stateMachine) as PatternSM);
        }
    }
    public readonly struct AnalyzedBoss {
        public readonly BossConfig boss;
        public readonly List<Phase> phases;

        public AnalyzedBoss(BossConfig sb) {
            boss = sb;
            phases = SMAnalysis.Analyze(StateMachineManager.FromText(sb.stateMachine) as PatternSM);
        }
    }
    public class AnalyzedDayBoss {
        public readonly BossConfig boss;
        public readonly List<DayPhase> phases;
        public readonly AnalyzedDay day;
        public readonly int bossIndex;
        public bool Enabled => day.Enabled;
        public bool Concluded => phases.All(p => p.CompletedOne);
        public bool FirstPhaseCompletedOne => phases[0].CompletedOne;

        public AnalyzedDayBoss(AnalyzedDay day, DayConfig d, int index) {
            boss = d.bosses[bossIndex = index];
            this.day = day;
            phases = SMAnalysis.AnalyzeDay(this, StateMachineManager.FromText(boss.stateMachine) as PatternSM);
        }
    }

    public class AnalyzedDay {
        public readonly DayConfig day;
        public readonly AnalyzedDayBoss[] bosses;
        public IEnumerable<DayPhase> Phases => bosses.SelectMany(b => b.phases);
        public bool Enabled => dayIndex == 0 || all.days[dayIndex - 1].OneBossesConcluded;
        public bool OneBossesConcluded => bosses.Any(b => b.Concluded);
        public bool AllBossesConcluded => bosses.All(b => b.Concluded);
        public readonly int dayIndex;
        private readonly AnalyzedDays all;

        public AnalyzedDay(AnalyzedDays all, DayConfig[] days, int index) {
            this.all = all;
            this.day = days[dayIndex = index];
            bosses = day.bosses.Length.Range().Select(i => new AnalyzedDayBoss(this, day, i)).ToArray();
        }
    }

    public class AnalyzedDays {
        public readonly AnalyzedDay[] days;
        public AnalyzedDays(DayConfig[] days) {
            this.days = days.Length.Range().Select(i => new AnalyzedDay(this, days, i)).ToArray();
        }
    }
}
}