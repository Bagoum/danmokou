using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
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
    /// Set a callback that can be run on script end, or on phase end if using override.
    /// </summary>
    public void SetCallback(Action? cb) => callback = cb;

    /// <summary>
    /// OK to call twice
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
        //Raw index
        public readonly int cardIndex;
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
        public DayPhase? Next => boss.phases.Try(cardIndex + 1);

        public DayPhase(AnalyzedDayBoss b, Phase p, 
            IEnumerable<Challenge> challenges, DayPhaseType type, int cardIndex, int combatCardIndex) {
            this.phase = p;
            this.challenges = challenges.ToArray();
            this.type = type;
            this.cardIndex = cardIndex;
            this.combatCardIndex = combatCardIndex;
            boss = b;
        }
        
        public (((string, int), string), int) Key => (boss.Key, cardIndex);

        public static DayPhase Reconstruct((((string, int), string), int) key) =>
            AnalyzedDayBoss.Reconstruct(key.Item1).phases.First(p => p.cardIndex == key.Item2);
        
    }

    /// <summary>
    /// Returns nontrivial phases only (ie. skips 0th phase and untyped phases)
    /// </summary>
    public static List<Phase> Analyze(AnalyzedPhasedConstruct parent, PatternSM? pat, bool ignoreZero = true) {
        var ret = new List<Phase>();
        if (pat == null) return ret;
        foreach (var (i, p) in pat.phases.Enumerate()) {
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
        if (GameManagement.References.fastParsing)
            return Analyze(parent, StateMachine.ParsePhases(sm.text));
        return Analyze(parent, StateMachineManager.FromText(sm) as PatternSM);
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
        public (string, int) Key => (campaign.Key, stageIndex);
        public static AnalyzedStage Reconstruct((string, int) key) =>
            AnalyzedCampaign.Reconstruct(key.Item1).stages[key.Item2];
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

        public (string campaign, string bossKey) Key => (campaign.Key, boss.key);

        public static AnalyzedBoss Reconstruct((string campaign, string bossKey) key) =>
            AnalyzedCampaign.Reconstruct(key.campaign).bossKeyMap[key.bossKey];
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
        
        public ((string, int), string) Key => (day.Key, boss.key);
        public static AnalyzedDayBoss Reconstruct(((string, int), string) key) => 
            AnalyzedDay.Reconstruct(key.Item1).bossKeyMap[key.Item2];
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
        
        public (string, int) Key => (campaign.campaign.key, dayIndex);
        public static AnalyzedDay Reconstruct((string, int) key) => 
            GameManagement.DayCampaign.days[key.Item2];
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