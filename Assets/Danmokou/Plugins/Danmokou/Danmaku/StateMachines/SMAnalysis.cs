using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;
using static Danmokou.Core.LocalizedStrings.UI;

namespace Danmokou.SM {

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
    private Action? callback;

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
    /// <br/>After the phase is run, GoToNextPhase will always return -1.
    /// </summary>
    /// <param name="gotoPhase">Target phase</param>
    /// <param name="cb">Callback</param>
    /// <param name="forceZeroOverride">True iff the zero phase should also be skipped</param>
    public void Override(int gotoPhase, Action? cb = null, bool forceZeroOverride = false) {
        externalOverride = gotoPhase;
        callback = cb;
        typ = forceZeroOverride ? ControllerType.EXTERNAL_OVERRIDE_SKIP : ControllerType.EXTERNAL_OVERRIDE;
    }

    /// <summary>
    /// Run a single phase, then continue the script, and hit a callback when the script is done normally.
    /// </summary>
    public void SetGoTo(int gotoPhase, Action? cb) {
        externalOverride = gotoPhase;
        callback = cb;
        typ = ControllerType.EXTERNAL_OVERRIDE_CONTINUE;
    }

    /// <summary>
    /// Set an override for a GoTo, but only if one is not already set.
    /// </summary>
    public void LowPriorityGoTo(int gotoPhase) {
        if (typ == ControllerType.DEFAULT) SetGoTo(gotoPhase, callback);
    }

    /// <summary>
    /// OK to call twice
    /// </summary>
    public void RunEndingCallback() {
        callback?.Invoke();
        callback = null;
    }

    public void SetDesiredNext(int nxt) => normalNextPhase = nxt;
    
    /// <summary>
    /// Calculates the next phase to execute, and modifies internal state to move to that state.
    /// </summary>
    /// <param name="requestedNormal">The phase desired by the SM</param>
    /// <returns>The phase the SM should go to. This number may be negative or greater than the phase length,
    /// in which case the SM should stop executing.</returns>
    public int GoToNextPhase(int? requestedNormal = null) {
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
            RunEndingCallback();
            return -1;
        }
        return normalNextPhase;
    }

    /// <summary>
    /// Calculates the next phase to execute. Does not modify internal state (this is a pure function).
    /// </summary>
    /// <param name="requestedNormal">The phase desired by the SM</param>
    /// <returns>The phase the SM should go to. This number may be negative or greater than the phase length,
    /// in which case the SM should stop executing.</returns>
    [Pure]
    public int ScanNextPhase(int? requestedNormal = null) {
        var nnp = requestedNormal ?? normalNextPhase;
        if (typ == ControllerType.EXTERNAL_OVERRIDE_SKIP ||
            (typ == ControllerType.EXTERNAL_OVERRIDE && nnp > 0)) {
            return externalOverride;
        } else if (typ == ControllerType.EXTERNAL_OVERRIDE_CONTINUE && nnp > 0) {
            return externalOverride;
        } else if (typ == ControllerType.WAITING_OVERRIDE_RETURN) {
            return -1;
        }
        return nnp;
    }

}
public static class SMAnalysis {
    /// <summary>
    /// Analyzed phase construct for normal game card selection.
    /// </summary>
    public readonly struct Phase {
        public readonly PhaseType type;
        private readonly LString? title;
        /// <summary>
        /// Index of this phase in the original state machine.
        /// </summary>
        public readonly int index;
        private readonly AnalyzedPhasedConstruct parent;
        /// <summary>
        /// 1-indexed index of this phase in the parent's list of nontrivial phases.
        /// </summary>
        public int NontrivialPhaseIndex => parent.Phases.IndexOf(this) + 1;
        public LString Title =>
            type switch {
                PhaseType.STAGE => practice_stage_section_ls(NontrivialPhaseIndex),
                PhaseType.STAGEMIDBOSS => practice_midboss,
                PhaseType.STAGEENDBOSS => practice_endboss,
                PhaseType.DIALOGUE => title ?? practice_dialogue,
                _ => title ?? new LString("!!!UNTITLED PHASE (REPORT ME)!!!")
            };

        public Phase(AnalyzedPhasedConstruct parent, PhaseType c, int phaseNum, LString? name) {
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
        //Index among analyzed only
        private readonly int combatCardIndex;
        
        private LString _Title =>
            type switch {
                DayPhaseType.DIALOGUE_INTRO => challenge_day_intro_ls(boss.ChallengeName),
                DayPhaseType.DIALOGUE_END => challenge_day_end_ls(boss.ChallengeName),
                _ => challenge_day_card_ls(boss.ChallengeName, combatCardIndex)
            };
        public LString Title(SharedInstanceMetadata meta) => (boss.Enabled(meta)) ? _Title : new LString("??? Locked ???");
        public readonly AnalyzedDayBoss boss;
        public bool Completed(int cIndex, SharedInstanceMetadata meta) => SaveData.r.ChallengeCompleted(this, cIndex, meta);
        public bool CompletedOne(SharedInstanceMetadata meta) => SaveData.r.PhaseCompletedOne(this, meta);
        public bool CompletedAll(SharedInstanceMetadata meta) => SaveData.r.PhaseCompletedAll(this, meta);
        public bool Enabled(SharedInstanceMetadata meta) {
            if (!boss.Enabled(meta)) return false;
            else
                return type switch {
                    DayPhaseType.DIALOGUE_INTRO => 
                        boss.bossIndex == 0 || boss.day.bosses[boss.bossIndex - 1].FirstPhaseCompletedOne(meta),
                    DayPhaseType.CARD => 
                        boss.phases[0].CompletedOne(meta),
                    DayPhaseType.DIALOGUE_END => 
                        boss.phases.All(p => p == this || p.CompletedOne(meta)),
                    _ => false
                };
        }
        public DayPhase? Next {
            get {
                var idx = boss.phases.IndexOf(this);
                if (idx > 0)
                    return boss.phases.Try(idx + 1);
                else
                    return null;
            }
        }

        public DayPhase(AnalyzedDayBoss b, Phase p, 
            IEnumerable<Challenge> challenges, DayPhaseType type, int cardIndex, int combatCardIndex) {
            this.phase = p;
            this.challenges = challenges.ToArray();
            this.type = type;
            this.combatCardIndex = combatCardIndex;
            boss = b;
        }

        public static DayPhase Reconstruct(string campaign, int dayIndex, string boss, int phaseIndex) =>
            AnalyzedDayBoss.Reconstruct(campaign, dayIndex, boss).phases.First(p => p.phase.index == phaseIndex);
        
    }

    /// <summary>
    /// Returns nontrivial phases only (ie. skips 0th phase and untyped phases)
    /// </summary>
    public static List<Phase> Analyze(AnalyzedPhasedConstruct parent, PatternSM? pat, bool ignoreZero = true) {
        var ret = new List<Phase>();
        if (pat == null) return ret;
        foreach (var (i, p) in pat.Phases.Enumerate()) {
            if (ignoreZero && i == 0) continue;
            if (p.props.phaseType.Try(out var pt) && pt.AppearsInPractice()) {
                ret.Add(new Phase(parent, pt, i, p.props.cardTitle));
            }
        }
        return ret;
    }

    /// <summary>
    /// Assumes that there is exactly one untyped phase (phase 0) and all other phases are typed.
    /// Phase 0 may have or not have properties.
    /// </summary>
    public static List<Phase> Analyze(AnalyzedPhasedConstruct parent, List<PhaseProperties> phases) {
        var ret = new List<Phase>();
        int deficit = phases[0].phaseType == null ? 0 : 1;
        for (int ii = 1 - deficit; ii < phases.Count; ++ii) {
            var p = phases[ii];
            if (p.phaseType == null)
                throw new Exception("Fast parsing: found an untyped phase.");
            if (p.phaseType.Value.AppearsInPractice()) {
                ret.Add(new Phase(parent, p.phaseType.Value, ii + deficit, p.cardTitle));
            }
        }
        return ret;
    }

    public static List<Phase> Analyze(AnalyzedPhasedConstruct parent, TextAsset? sm) {
        if (sm == null) return new List<Phase>();
#if !EXBAKE_LOAD
        if (GameManagement.References.fastParsing)
            return Analyze(parent, StateMachine.ParsePhases(sm.text));
#endif
        return Analyze(parent, StateMachineManager.FromText(sm) as PatternSM);
    }

    public static List<DayPhase> AnalyzeDay(AnalyzedDayBoss boss, PatternSM pat, bool ignoreZero = true) {
        var ret = new List<DayPhase>();
        int combatCardNumber = 0;
        int cardNumber = 0;
        foreach (var (i, p) in pat.Phases.Enumerate()) {
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

    public interface AnalyzedPhasedConstruct {
        List<Phase> Phases { get; }
    }
    public class AnalyzedStage : AnalyzedPhasedConstruct {
        public readonly StageConfig stage;
        private List<Phase>? phases;
        /// <summary>
        /// List of active nontrivial phases only (ie. skips 0th phase)
        /// </summary>
        public List<Phase> Phases => phases ??= SMAnalysis.Analyze(this, stage.stateMachine);
        public readonly int stageIndex;
        public readonly AnalyzedCampaign campaign;
        public AnalyzedStage(AnalyzedCampaign campaign, int index) {
            stage = (this.campaign = campaign).campaign.stages[stageIndex = index];
        }
        public static AnalyzedStage Reconstruct(string campaign, int stageIndex) =>
            AnalyzedCampaign.Reconstruct(campaign).stages[stageIndex];
    }
    public class AnalyzedBoss : AnalyzedPhasedConstruct {
        public readonly BossConfig boss;
        private List<Phase>? phases;
        /// <summary>
        /// List of active nontrivial phases only (ie. skips 0th phase)
        /// </summary>
        public List<Phase> Phases => phases ??= SMAnalysis.Analyze(this, boss.stateMachine);
        public readonly int bossIndex;
        public readonly AnalyzedCampaign campaign;

        public AnalyzedBoss(AnalyzedCampaign campaign, int index) {
            boss = (this.campaign = campaign).campaign.practiceBosses[bossIndex = index];
        }

        public static AnalyzedBoss Reconstruct(string campaign, string boss) =>
            AnalyzedCampaign.Reconstruct(campaign).bossKeyMap[boss];
    }

    public class AnalyzedCampaign {
        public readonly CampaignConfig campaign;
        public readonly AnalyzedBoss[] bosses;
        public readonly AnalyzedStage[] stages;
        public readonly Dictionary<string, AnalyzedBoss> bossKeyMap = new Dictionary<string, AnalyzedBoss>();
        public IEnumerable<AnalyzedStage> practiceStages => stages.Where(s => s.stage.practiceable);

        public AnalyzedCampaign(CampaignConfig campaign) {
            bosses = (this.campaign = campaign).practiceBosses.Length.Range().Select(i => new AnalyzedBoss(this, i)).ToArray();
            for (int ii = 0; ii < bosses.Length; ++ii) {
                bossKeyMap[bosses[ii].boss.key] = bosses[ii];
            }
            stages = campaign.stages.Length.Range().Select(i => new AnalyzedStage(this, i)).ToArray();
        }

        public string Key => campaign.key;
        public static AnalyzedCampaign Reconstruct(string key) =>
            GameManagement.Campaigns.First(c => c.campaign.key == key);
    }
    public class AnalyzedDayBoss : AnalyzedPhasedConstruct {
        public readonly BossConfig boss;
        public readonly List<DayPhase> phases;
        public List<Phase> Phases => phases.Select(x => x.phase).ToList();
        public readonly AnalyzedDay day;
        public readonly int bossIndex;
        public LString ChallengeName => boss.CasualName;
        public bool Enabled(SharedInstanceMetadata meta) => day.Enabled(meta);
        public bool Concluded(SharedInstanceMetadata meta) => phases.All(p => p.CompletedOne(meta));
        public bool FirstPhaseCompletedOne(SharedInstanceMetadata meta) => phases[0].CompletedOne(meta);

        public AnalyzedDayBoss(AnalyzedDay day, int index) {
            boss = (this.day = day).day.bosses[bossIndex = index];
            phases = SMAnalysis.AnalyzeDay(this, (StateMachineManager.FromText(boss.stateMachine) as PatternSM)!);
        }
        public static AnalyzedDayBoss Reconstruct(string campaign, int dayIndex, string boss) {
            return AnalyzedDay.Reconstruct(campaign, dayIndex).bossKeyMap[boss];
        }
    }

    public class AnalyzedDay {
        public readonly DayConfig day;
        public readonly AnalyzedDayBoss[] bosses;
        public readonly int dayIndex;
        public readonly AnalyzedDayCampaign campaign;
        public readonly Dictionary<string, AnalyzedDayBoss> bossKeyMap = new Dictionary<string, AnalyzedDayBoss>();
        
        public IEnumerable<DayPhase> Phases => bosses.SelectMany(b => b.phases);
        public bool Enabled(SharedInstanceMetadata meta) => dayIndex == 0 || campaign.days[dayIndex - 1].OneBossesConcluded(meta);
        public bool OneBossesConcluded(SharedInstanceMetadata meta) => bosses.Any(b => b.Concluded(meta));
        public bool AllBossesConcluded(SharedInstanceMetadata meta) => bosses.All(b => b.Concluded(meta));

        public AnalyzedDay(AnalyzedDayCampaign campaign, DayConfig[] days, int index) {
            this.campaign = campaign;
            this.day = days[dayIndex = index];
            bosses = day.bosses.Length.Range().Select(i => new AnalyzedDayBoss(this, i)).ToArray();
            for (int ii = 0; ii < bosses.Length; ++ii) {
                bossKeyMap[bosses[ii].boss.key] = bosses[ii];
            }
        }
        public static AnalyzedDay Reconstruct(string campaign, int dayIndex) => 
            GameManagement.DayCampaign.days[dayIndex];
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