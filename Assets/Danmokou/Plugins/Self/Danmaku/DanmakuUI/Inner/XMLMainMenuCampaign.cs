using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using Danmaku.DanmakuUI;
using JetBrains.Annotations;
using SM;
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
    private UIScreen cmpPlayerSelectScreen;
    [CanBeNull] private UIScreen exPlayerSelectScreen;
    private UIScreen OptionsScreen;
    private UIScreen ReplayScreen;

    protected override IEnumerable<UIScreen> Screens => new[] { CampaignSelectScreen, ExtraSelectScreen, 
        StagePracticeScreen, BossPracticeScreen, cmpPlayerSelectScreen, exPlayerSelectScreen, OptionsScreen, 
        ReplayScreen, MainScreen }.NotNull();

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
        Func<PlayerConfig, ShotConfig, bool> shotCont = null;
        UIScreen CreateShotScreen(CampaignConfig c) => c == null ? null : new UIScreen(c.players.Select(
            p => (UINode) new NavigateUINode(p.title, p.shots.Select(
                    (s,i) => (UINode) new FuncNode(() => shotCont(p, s), $"Type {i.ToABC()}", false, 
                        new InheritNode(s.title).With(visibleAdjacentClass).With(largeClass),
                        new InheritNode(s.description).With(shotDescrClass).With(visibleAdjacentClass))).ToArray()
        )).ToArray()).With(ShotScreen);
        
        cmpPlayerSelectScreen = CreateShotScreen(References.campaign);
        exPlayerSelectScreen = CreateShotScreen(References.exCampaign);
        
        UINode[] DifficultyThenShot(UINode to, Action<DifficultySet, PlayerConfig, ShotConfig> cb) => 
            DifficultyNodes(d => new FuncNode(() => shotCont = (p, s) => {
                    cb(d, p, s);
                    return true;
                }, d.Describe(), to));

        var cmpShotTop = cmpPlayerSelectScreen.top[0];
        var exShotTop = exPlayerSelectScreen?.top[0];
        var shotTopMap = new Dictionary<CampaignConfig, UINode>() {{ References.campaign, cmpShotTop }};
        if (References.exCampaign != null) shotTopMap[References.exCampaign] = exShotTop;
        

        CampaignSelectScreen = new UIScreen(DifficultyThenShot(cmpShotTop, (d, p, s) => 
            GameRequest.RunCampaign(MainCampaign, null, d, p, s)));
        ExtraSelectScreen = new UIScreen(DifficultyThenShot(exShotTop, (d, p, s) => 
            GameRequest.RunCampaign(ExtraCampaign, null, d, p, s)));
        StagePracticeScreen = new LazyUIScreen(() => PStages.Select(s =>
                (UINode)new NavigateUINode($"Stage {s.stage.stageNumber}", s.phases.Select(p =>
                    (UINode)new CacheNavigateUINode(TentativeCache, p.Title, DifficultyThenShot(
                        shotTopMap[s.campaign.campaign], (d, pl, sh) => {
                            ConfirmCache();
                            new GameRequest(GameRequest.WaitDefaultReturn, d, 
                                stage: new StagePracticeRequest(s, p.index), player: pl, shot: sh).Run();
                        }))
                ).ToArray())
            ).ToArray()).With(PracticeUIScreen);
        BossPracticeScreen = new LazyUIScreen(() => PBosses.Select(b => 
                (UINode)new NavigateUINode(b.boss.CardPracticeName, b.phases.Select(p =>
                    new CacheNavigateUINode(TentativeCache, p.Title, DifficultyThenShot(
                        shotTopMap[b.campaign.campaign], (d, pl, sh) => {
                            ConfirmCache();
                            new GameRequest(GameRequest.WaitShowPracticeSuccessMenu, d, 
                                boss: new BossPracticeRequest(b, p), player: pl, shot: sh).Run();
                        })).With(smallClass)
                ).ToArray())
            ).ToArray()).With(PracticeUIScreen);
        OptionsScreen = new UIScreen(XMLPauseMenu.GetOptions(true, x => x.With(GenericOptionNodeV)).ToArray())
            .With(OptionsScreenV).OnExit(SaveData.AssignSettingsChanges);
        ReplayScreen = XMLUtils.ReplayScreen(true, TentativeCache, ConfirmCache).With(ReplayScreenV);
        MainScreen = new UIScreen(
            new TransferNode(CampaignSelectScreen.top[2], "Main Scenario"),
            References.exCampaign == null ? null :
                new TransferNode(ExtraSelectScreen.top[2], "Extra Stage").EnabledIf(SaveData.r.MainCampaignCompleted),
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