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
using static GameManagement;
using static XMLUtils;
using static Danmaku.Enums;

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
    private UIScreen OptionsScreen;
    private UIScreen ReplayScreen;

    protected override UIScreen[] Screens => new[] { CampaignSelectScreen, ExtraSelectScreen, StagePracticeScreen, BossPracticeScreen, ShotSelectScreen, OptionsScreen, ReplayScreen, MainScreen };

    public VisualTreeAsset GenericUIScreen;
    public VisualTreeAsset GenericUINode;
    public VisualTreeAsset PracticeUIScreen;
    public VisualTreeAsset ShotScreen;
    public VisualTreeAsset MainScreenV;
    public VisualTreeAsset OptionsScreenV;
    public VisualTreeAsset ReplayScreenV;
    public VisualTreeAsset GenericOptionNodeV;
    
    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), GenericUIScreen},
        {typeof(UINode), GenericUINode},
    };
    private static UINode[] DifficultyNodes(Func<DifficultySet, UINode> map) =>
        VisibleDifficulties.Select(map).ToArray();

    private static UINode[] DifficultyFuncNodes(Func<DifficultySet, Action> map) =>
        DifficultyNodes(d => new FuncNode(map(d), d.Describe()));
    protected override void Awake() {
        if (!Application.isPlaying) return;
        Func<ShotConfig, bool> shotCont = null;
        var shots = GameManagement.References.shots.Select(s => new FuncNode(() => shotCont(s), s.title, false, new InheritNode(s.description).With(shotDescrClass))).ToArray();
        ShotSelectScreen = new UIScreen(shots.Select(x => (UINode) x).ToArray()).With(ShotScreen);
        var shotTop = ShotSelectScreen.top[0];
        UINode[] DifficultyThenShot(Action<DifficultySet, ShotConfig> cb) {
            if (GameManagement.References.shots.Length == 1) {
                return DifficultyFuncNodes(d => () => cb(d, GameManagement.References.shots[0]));
            }
            return DifficultyNodes(d => new FuncNode(() => shotCont = s => {
                    cb(d, s);
                    return true;
                }, d.Describe(), shotTop));
        }

        CampaignSelectScreen = new UIScreen(DifficultyThenShot((d, sh) => 
            GameRequest.RunCampaign(MainCampaign, null, d, sh)));
        ExtraSelectScreen = new UIScreen(DifficultyFuncNodes(d => 
            () => GameRequest.RunCampaign(ExtraCampaign, null, d, null)));
        StagePracticeScreen =
            new LazyUIScreen(() => PStages.Select(s =>
                (UINode)new NavigateUINode($"Stage {s.stage.stageNumber}", s.phases.Select(p =>
                    (UINode)new CacheNavigateUINode(TentativeCache, p.Title, DifficultyThenShot((d, sh) => {
                        ConfirmCache();
                        new GameRequest(GameRequest.WaitDefaultReturn, d, 
                            stage: new StagePracticeRequest(s, p.index), shot: sh).Run();
                    }))
                ).ToArray())
            ).ToArray()).With(PracticeUIScreen);
        BossPracticeScreen = 
            new LazyUIScreen(() => PBosses.Select(b => 
                (UINode)new NavigateUINode(b.boss.CardPracticeName, b.phases.Select(p =>
                    new CacheNavigateUINode(TentativeCache, p.Title, DifficultyThenShot((d, sh) => {
                        ConfirmCache();
                        new GameRequest(GameRequest.WaitShowPracticeSuccessMenu, d, 
                            boss: new BossPracticeRequest(b, p), shot: sh).Run();
                    })).With(smallClass)
                ).ToArray())
            ).ToArray()).With(PracticeUIScreen);
        OptionsScreen = new UIScreen(XMLPauseMenu.GetOptions(true, x => x.With(GenericOptionNodeV)).ToArray())
            .With(OptionsScreenV).OnExit(SaveData.AssignSettingsChanges);
        ReplayScreen = XMLUtils.ReplayScreen(TentativeCache, ConfirmCache).With(ReplayScreenV);
        MainScreen = new UIScreen(
            new TransferNode(CampaignSelectScreen.top[1], "Main Scenario"),
            References.exCampaign == null ? null :
                new TransferNode(ExtraSelectScreen.top[1], "Extra Stage").EnabledIf(SaveData.r.MainCampaignCompleted),
            new TransferNode(StagePracticeScreen, "Stage Practice").EnabledIf(PStages.Length > 0),
            new TransferNode(BossPracticeScreen, "Boss Practice").EnabledIf(PBosses.Length > 0),
            new TransferNode(ReplayScreen, "Replays").EnabledIf(SaveData.p.ReplayData.Count > 0),
            References.tutorial == null ? null :
                new FuncNode(GameRequest.RunTutorial, "Tutorial"),
            new TransferNode(OptionsScreen, "Options"),
            new FuncNode(Application.Quit, "Quit"),
            new OpenUrlNode("https://twitter.com/rdbatz", "Twitter (Browser)")
            //new OpenUrlNode("https://www.youtube.com/watch?v=cBNnNJrA5_w&list=PLkd4SjCCKjq6B5u_5DrSU4Qz0QgZfgnh7", "OST (Browser)")
            ).With(MainScreenV);
        base.Awake();
    }
}