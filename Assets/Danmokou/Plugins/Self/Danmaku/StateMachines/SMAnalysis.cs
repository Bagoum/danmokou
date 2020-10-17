using System;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
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
        /// <summary>
        /// Index of this phase in the original state machine.
        /// </summary>
        public readonly int index;
        private readonly AnalyzedPhaseConstruct parent;
        public int IndexInParentPhases => parent.Phases.IndexOf(this) + 1;
        public string Title {
            get {
                if (type == PhaseType.STAGE) return $"Stage Section {IndexInParentPhases}";
                if (type == PhaseType.STAGEMIDBOSS) return "Midboss";
                if (type == PhaseType.STAGEENDBOSS) return "Endboss";
                if (type == PhaseType.DIALOGUE) return title ?? "Dialogue";
                return title ?? "!!!UNTITLED PHASE (REPORT ME)!!!";
            }
        }

        public Phase(AnalyzedPhaseConstruct parent, PhaseType c, int phaseNum, [CanBeNull] string name) {
            type = c;
            title = name;
            index = phaseNum;
            this.parent = parent;
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
        //Raw index
        public readonly int cardIndex;
        //Index among analyzed only
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
        public string Title(GameMetadata meta) => (boss.Enabled(meta)) ? (_Title ?? phase.Title) : "??? Locked ???";
        public readonly AnalyzedDayBoss boss;
        public bool Completed(int cIndex, GameMetadata meta) => SaveData.r.ChallengeCompleted(this, cIndex, meta);
        public bool CompletedOne(GameMetadata meta) => SaveData.r.PhaseCompletedOne(this, meta);
        public bool CompletedAll(GameMetadata meta) => SaveData.r.PhaseCompletedAll(this, meta);
        public bool Enabled(GameMetadata meta) {
            if (!boss.Enabled(meta)) return false;
            else if (type == DayPhaseType.DIALOGUE_INTRO) {
                return boss.bossIndex == 0 || boss.day.bosses[boss.bossIndex - 1].FirstPhaseCompletedOne(meta);
            } else if (type == DayPhaseType.CARD) return boss.phases[0].CompletedOne(meta);
            else if (type == DayPhaseType.DIALOGUE_END) return boss.phases.All(p => p == this || p.CompletedOne(meta));
            else return false;
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
        
        public (((string, string), int), int) Key => (boss.Key, cardIndex);

        public static DayPhase Reconstruct((((string, string), int), int) key) =>
            AnalyzedDayBoss.Reconstruct(key.Item1).phases.First(p => p.cardIndex == key.Item2);
        
    }

    public static List<Phase> Analyze(AnalyzedPhaseConstruct parent, [CanBeNull] PatternSM pat, bool ignoreZero = true) {
        var ret = new List<Phase>();
        if (pat == null) return ret;
        foreach (var (i, p) in pat.phases.Enumerate()) {
            if (ignoreZero && i == 0) continue;
            if (p.props.phaseType.HasValue) {
                ret.Add(new Phase(parent, p.props.phaseType.Value, i, p.props.cardTitle));
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
                ret.Add(new DayPhase(boss, new Phase(boss, p.props.phaseType.Value, i, p.props.cardTitle),
                    p.props.challenges, typ, cardNumber++, combatCardNumber));
            }
        }
        return ret;
    }

    public interface AnalyzedPhaseConstruct {
        List<Phase> Phases { get; }
    }
    public class AnalyzedStage : AnalyzedPhaseConstruct {
        public readonly StageConfig stage;
        /// <summary>
        /// List of active nontrivial phases only
        /// </summary>
        public readonly List<Phase> phases;
        public List<Phase> Phases => phases;
        public readonly int stageIndex;
        public readonly AnalyzedCampaign campaign;
        public AnalyzedStage(AnalyzedCampaign campaign, int index) {
            stage = (this.campaign = campaign).campaign.stages[stageIndex = index];
            phases = SMAnalysis.Analyze(this, StateMachineManager.FromText(stage.stateMachine) as PatternSM);
        }
        public (string, int) Key => (campaign.Key, stageIndex);
        public static AnalyzedStage Reconstruct((string, int) key) =>
            AnalyzedCampaign.Reconstruct(key.Item1).stages[key.Item2];
    }
    public class AnalyzedBoss : AnalyzedPhaseConstruct {
        public readonly BossConfig boss;
        /// <summary>
        /// List of active nontrivial phases only
        /// </summary>
        public readonly List<Phase> phases;
        public List<Phase> Phases => phases;
        public readonly int bossIndex;
        public readonly AnalyzedCampaign campaign;

        public AnalyzedBoss(AnalyzedCampaign campaign, int index) {
            boss = (this.campaign = campaign).campaign.practiceBosses[bossIndex = index];
            phases = SMAnalysis.Analyze(this, StateMachineManager.FromText(boss.stateMachine) as PatternSM);
        }

        public (string, int) Key => (campaign.Key, bossIndex);
        public static AnalyzedBoss Reconstruct((string, int) key) =>
            AnalyzedCampaign.Reconstruct(key.Item1).bosses[key.Item2];
    }

    public class AnalyzedCampaign {
        public readonly CampaignConfig campaign;
        public readonly AnalyzedBoss[] bosses;
        public readonly AnalyzedStage[] stages;
        public IEnumerable<AnalyzedStage> practiceStages => stages.Where(s => s.stage.practiceable);

        public AnalyzedCampaign(CampaignConfig campaign) {
            bosses = (this.campaign = campaign).practiceBosses.Length.Range().Select(i => new AnalyzedBoss(this, i)).ToArray();
            stages = campaign.stages.Length.Range().Select(i => new AnalyzedStage(this, i)).ToArray();
        }

        public string Key => campaign.key;
        public static AnalyzedCampaign Reconstruct(string key) =>
            GameManagement.Campaigns.First(c => c.campaign.key == key);
    }
    public class AnalyzedDayBoss : AnalyzedPhaseConstruct {
        public readonly BossConfig boss;
        public readonly List<DayPhase> phases;
        public List<Phase> Phases => phases.Select(x => x.phase).ToList();
        public readonly AnalyzedDay day;
        public readonly int bossIndex;
        public bool Enabled(GameMetadata meta) => day.Enabled(meta);
        public bool Concluded(GameMetadata meta) => phases.All(p => p.CompletedOne(meta));
        public bool FirstPhaseCompletedOne(GameMetadata meta) => phases[0].CompletedOne(meta);

        public AnalyzedDayBoss(AnalyzedDay day, int index) {
            boss = (this.day = day).day.bosses[bossIndex = index];
            phases = SMAnalysis.AnalyzeDay(this, StateMachineManager.FromText(boss.stateMachine) as PatternSM);
        }
        
        public ((string, string), int) Key => (day.Key, bossIndex);
        public static AnalyzedDayBoss Reconstruct(((string, string), int) key) => 
            AnalyzedDay.Reconstruct(key.Item1).bosses[key.Item2];
    }

    public class AnalyzedDay {
        public readonly DayConfig day;
        public readonly AnalyzedDayBoss[] bosses;
        public IEnumerable<DayPhase> Phases => bosses.SelectMany(b => b.phases);
        public bool Enabled(GameMetadata meta) => dayIndex == 0 || campaign.days[dayIndex - 1].OneBossesConcluded(meta);
        public bool OneBossesConcluded(GameMetadata meta) => bosses.Any(b => b.Concluded(meta));
        public bool AllBossesConcluded(GameMetadata meta) => bosses.All(b => b.Concluded(meta));
        public readonly int dayIndex;
        public readonly AnalyzedDayCampaign campaign;

        public AnalyzedDay(AnalyzedDayCampaign campaign, DayConfig[] days, int index) {
            this.campaign = campaign;
            this.day = days[dayIndex = index];
            bosses = day.bosses.Length.Range().Select(i => new AnalyzedDayBoss(this, i)).ToArray();
        }
        
        public (string, string) Key => (campaign.campaign.key, day.key);
        public static AnalyzedDay Reconstruct((string, string) key) => 
            GameManagement.DayCampaign.days.First(c => c.day.key == key.Item2);
    }

    public class AnalyzedDayCampaign {
        public readonly AnalyzedDay[] days;
        public readonly DayCampaignConfig campaign;
        public AnalyzedDayCampaign(DayCampaignConfig campaign) {
            this.campaign = campaign;
            this.days = campaign.days.Length.Range().Select(i => new AnalyzedDay(this, campaign.days, i)).ToArray();
        }
    }
}
}