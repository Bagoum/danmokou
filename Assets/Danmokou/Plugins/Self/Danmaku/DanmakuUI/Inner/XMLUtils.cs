using System;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using Danmaku.DanmakuUI;
using JetBrains.Annotations;
using SM;
using UnityEngine.UIElements;
using static GameManagement;
using static SM.SMAnalysis;

public static class XMLUtils {
    public const string monospaceClass = "monospace";
    public const string large1Class = "large1";
    public const string small1Class = "small1";
    public const string small2Class = "small2";
    public const string small3Class = "small3";
    public const string visibleAdjacentClass = "visibleadjacent";
    public const string optionNoKeyClass = "nokey";
    public const string hideClass = "hide";
    public const string descriptorClass = "descriptor";
    public const string centerTextClass = "centertext";
    
    public static UIScreen ReplayScreen(bool showScore, Action<List<int>> cacheTentative, Action cacheConfirm) => 
        new UIScreen(SaveData.p.ReplayData.Count.Range().Select(i => 
            new CacheNavigateUINode(cacheTentative, () => 
                    SaveData.p.ReplayData.TryN(i)?.metadata.Record.AsDisplay(showScore, true) ?? "---Deleted Replay---",
                new FuncNode(() => {
                    cacheConfirm();
                    return GameRequest.ViewReplay(SaveData.p.ReplayData.TryN(i));
                }, "View"),
                new ConfirmFuncNode(() => SaveData.p.TryDeleteReplay(i), "Delete", true)
            ).With(monospaceClass).With(small3Class)
        ).ToArray());
    
    
    public static UIScreen HighScoreScreen(VisualTreeAsset optionNode, UIScreen replayScreen, 
        AnalyzedCampaign[] campaigns, [CanBeNull] AnalyzedDayCampaign days = null) {
        if (campaigns.Length == 0) return new UIScreen(new UINode("Finish a campaign to view scores."));
        var replays = new Dictionary<string, int>();
        var key = new GameRecord().GameKey;
        var cmpIndex = 0;
        void AssignCampaign(int cmpInd) {
            cmpIndex = cmpInd;
            key.campaign = key.boss.Item1.campaign = key.challenge.Item1.Item1.Item1.campaign = 
                    key.stage.Item1.campaign = campaigns[cmpIndex].Key;
        }
        void AssignBoss(int boss) {
            key.boss.Item1.boss = key.challenge.Item1.Item1.boss = boss;
        }
        void AssignStage(int stage) { //Better not to mix with AssignBoss to avoid invalid assignments.
            key.stage.Item1.stage = stage;
        }
        void AssignBossPhase(int phase) {
            key.boss.phase = key.challenge.Item1.phase = phase;
        }
        void AssignStagePhase(int phase) {
            key.stage.phase = phase;
        }
        key.stage.phase = 1; //only show full-stage practice
        AssignCampaign(0);
        AssignBoss(0);
        AssignStage(0);
        key.type = 0;
        SaveData.p.ReplayData.ForEachI((i, r) => replays[r.metadata.RecordUuid] = i);
        var scoreNodes = SaveData.r.FinishedGames.Values
            .Where(g => !string.IsNullOrWhiteSpace(g.CustomNameOrPartial) && g.Score > 0)
            .OrderByDescending(g => g.Score).Select(g => {
            var node = new UINode(g.AsDisplay(true, false));
            if (replays.TryGetValue(g.Uuid, out var i)) node.SetConfirmOverride(() => (true, replayScreen.top[i]));
            return node.With(monospaceClass).With(small2Class)
                .With(replays.ContainsKey(g.Uuid) ? "checked" : "unchecked")
                .VisibleIf(() => DUHelpers.Tuple4Eq(key, g.GameKey));
        });
        var optnodes = new UINode[] {
            new OptionNodeLR<short>("Game Type", i => key.type = i, new (string, short)?[] {
                ("Campaign", 0),
                ("Boss Practice", 1),
                days == null ? ((string, short)?)null : ("Scene Challenge", 2),
                ("Stage Practice", 3)
            }.FilterNone().ToArray(), key.type),
            new OptionNodeLR<int>("Campaign", AssignCampaign, 
                campaigns.Select((c, i) => (c.campaign.shortTitle, i)).ToArray(), cmpIndex),
            new DynamicOptionNodeLR<int>("Boss", AssignBoss, () => 
                key.type == 1 ? 
                    campaigns[cmpIndex].bosses.Select((b, i) => (b.boss.CardPracticeName, i)).ToArray()
                    : new[] { ("_null_", 0 )} //required to avoid errors with the option node
                , 0).VisibleIf(() => key.type == 1),
            new DynamicOptionNodeLR<int>("Stage", AssignStage, () => 
                    key.type == 3 ? 
                        campaigns[cmpIndex].stages.Select((s, i) => (s.stage.stageNumber, i)).ToArray()
                        : new[] { ("_null_", 0 )} //required to avoid errors with the option node
                , 0).VisibleIf(() => key.type == 3),
            new DynamicOptionNodeLR<int>("Phase", AssignBossPhase, () => 
                    key.type == 1 ? 
                        campaigns[cmpIndex].bosses[key.boss.Item1.boss].Phases.Select(
                            //p.index is used as request key
                            (p, i) => ($"{i + 1}. {p.Title}", p.index)).ToArray()
                        : new[] { ("_null_", 0 )}, 0)
                .With(ve => ve.Q("ValueContainer").style.width = new StyleLength(new Length(80, LengthUnit.Percent)))
                .VisibleIf(() => key.type == 1), 
            new DynamicOptionNodeLR<int>("Phase", AssignStagePhase, () => 
                    key.type == 3 ? 
                        campaigns[cmpIndex].stages[key.stage.Item1.stage].Phases.Select(
                            p => ($"{p.Title}", p.index)).Prepend(("Full Stage", 1)).ToArray()
                        : new[] { ("_null_", 0 )}, 0)
                .VisibleIf(() => key.type == 3), 
        }.Select(x => x.With(optionNode));
        return new UIScreen(optnodes.Append(new PassthroughNode("")).Concat(scoreNodes).ToArray());
    }
}