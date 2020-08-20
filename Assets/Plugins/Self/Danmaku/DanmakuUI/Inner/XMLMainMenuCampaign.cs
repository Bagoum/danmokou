using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using Danmaku.DanmakuUI;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;
using static GameManagement;
using static Danmaku.MainMenuCampaign;

/// <summary>
/// Class to manage the main menu UI for campaign-type games.
/// </summary>
[Preserve]
public class XMLMainMenuCampaign : XMLMenu {
    [CanBeNull] private static List<int> _returnTo;
    protected override List<int> ReturnTo {
        [CanBeNull] get => _returnTo;
        set => _returnTo = value;
    }

    private UIScreen CampaignSelectScreen;
    private UIScreen ExtraSelectScreen;
    private UIScreen StagePracticeScreen;
    private UIScreen BossPracticeScreen;
    private UIScreen ShotSelectScreen;

    protected override UIScreen[] Screens => new[] { CampaignSelectScreen, ExtraSelectScreen, StagePracticeScreen, BossPracticeScreen,
        ShotSelectScreen, MainScreen };

    public VisualTreeAsset GenericUIScreen;
    public VisualTreeAsset GenericUINode;
    public VisualTreeAsset PracticeUIScreen;
    public VisualTreeAsset ShotScreen;
    public VisualTreeAsset MainScreenV;

    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), GenericUIScreen},
        {typeof(UINode), GenericUINode},
    };

    private const string smallDescrClass = "small";
    private const string shotDescrClass = "descriptor";
    private static UINode[] DifficultyNodes(Func<DifficultySet, UINode> map) =>
        VisibleDifficulties.Select(map).ToArray();

    private static UINode[] DifficultyFuncNodes(Func<DifficultySet, Action> map) =>
        DifficultyNodes(d => new FuncNode(map(d), d.Describe()));
    protected override void Awake() {
        if (!Application.isPlaying) return;
        Func<ShotConfig, bool> shotCont = null;
        var shots = MainMenuCampaign.main.shotOptions.Select(s => new FuncNode(() => shotCont(s), s.title, false, new InheritNode(s.description).With(shotDescrClass))).ToArray();
        ShotSelectScreen = new UIScreen(shots.Select(x => (UINode) x).ToArray()).With(ShotScreen);
        var shotTop = ShotSelectScreen.top[0];
        UINode[] DifficultyThenShot(Action<DifficultySet, ShotConfig> cb) {
            if (MainMenuCampaign.main.shotOptions.Length == 1) {
                return DifficultyFuncNodes(d => () => cb(d, MainMenuCampaign.main.shotOptions[0]));
            }
            return DifficultyNodes(d => new FuncNode(() => shotCont = s => {
                    cb(d, s);
                    return true;
                }, d.Describe(), shotTop));
        }

        CampaignSelectScreen = new UIScreen(DifficultyThenShot((d, sh) => 
            MainScenario(new GameReq(CampaignMode.MAIN, null, d, shot: sh))));
        ExtraSelectScreen = new UIScreen(DifficultyFuncNodes(d => 
            () => ExtraScenario(new GameReq(CampaignMode.MAIN, null, d))));
        StagePracticeScreen =
            new LazyUIScreen(() => Stages.Select(s =>
                (UINode)new NavigateUINode($"Stage {s.stage.stageNumber}", s.phases.Select(p =>
                    (UINode)new CacheNavigateUINode(TentativeCache, p.Title, DifficultyThenShot((d, sh) => {
                        ConfirmCache();
                        new GameReq(CampaignMode.STAGE_PRACTICE, DefaultReturn, d, toPhase: p.index, shot: sh).SelectStageContinue(s.stage);
                    }))
                ).ToArray())
            ).ToArray()).With(PracticeUIScreen);
        BossPracticeScreen = 
            new LazyUIScreen(() => Bosses.Select(b => 
                (UINode)new NavigateUINode(b.boss.CardPracticeName, b.phases.Select(p =>
                    new CacheNavigateUINode(TentativeCache, p.Title, DifficultyThenShot((d, sh) => {
                        ConfirmCache();
                        SelectBossSinglePhase(b.boss, new GameReq(CampaignMode.CARD_PRACTICE, 
                            DefaultReturn, d, toPhase: p.index, shot: sh), p.type);
                    })).With(smallDescrClass)
                ).ToArray())
            ).ToArray()).With(PracticeUIScreen);
        MainScreen = new UIScreen(
            new TransferNode(CampaignSelectScreen.top[1], "Main Scenario"),
            new TransferNode(ExtraSelectScreen.top[1], "Extra Stage").EnabledIf(SaveData.r.MainCampaignCompleted),
            new TransferNode(StagePracticeScreen, "Stage Practice").EnabledIf(Stages.Length > 0),
            new TransferNode(BossPracticeScreen, "Boss Card Practice").EnabledIf(Bosses.Length > 0),
            new FuncNode(RunTutorial, "Tutorial"),
            new FuncNode(Application.Quit, "Quit"),
            new OpenUrlNode("https://twitter.com/rdbatz", "Twitter (Browser)"),
            new OpenUrlNode("https://github.com/Bagoum/danmokou", "Github (Browser)"),
            new OpenUrlNode("https://www.youtube.com/watch?v=cBNnNJrA5_w&list=PLkd4SjCCKjq6B5u_5DrSU4Qz0QgZfgnh7", "OST (Browser)")
            ).With(MainScreenV);
        base.Awake();
    }
}