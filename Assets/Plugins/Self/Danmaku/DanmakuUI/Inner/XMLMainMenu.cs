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
using static Danmaku.MainMenu;

/// <summary>
/// Class to manage the main menu UI.
/// </summary>
[Preserve]
public class XMLMainMenu : XMLMenu {
    [CanBeNull] private static List<int> returnTo;
    [CanBeNull] private List<int> tentativeReturnTo;

    private void TentativeCache(List<int> indices) {
        tentativeReturnTo = indices;
    }

    private void ConfirmCache() {
        if (tentativeReturnTo != null) Log.Unity($"Caching menu position with indices {string.Join(", ", tentativeReturnTo)}");
        returnTo = tentativeReturnTo;
    }

    private UIScreen CampaignSelectScreen;
    private UIScreen StagePracticeScreen;
    private UIScreen BossPracticeScreen;
    private UIScreen ShotSelectScreen;

    protected override UIScreen[] Screens => new[] { CampaignSelectScreen, StagePracticeScreen, BossPracticeScreen,
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
        var shots = MainMenu.main.shotOptions.Select(s => new ContinuationNode<ShotConfig>(s, s.title, false, 
                new InheritNode(s.description).With(shotDescrClass)
            )).ToArray();
        ShotSelectScreen = new UIScreen(shots.Select(x => (UINode) x).ToArray()).With(ShotScreen);
        var shotTop = ShotSelectScreen.top[0];
        void SetShotContinuation(Func<ShotConfig, bool> cont) {
            foreach (var s in shots) s.continuation = cont;
        }
        void SetShotContinuationA(Action<ShotConfig> cont) => SetShotContinuation(s => {
            cont(s);
            return true;
        });
        UINode[] DifficultyThenShot(Action<DifficultySet, ShotConfig> cb) => DifficultyNodes(d =>
            new FuncNode(() => SetShotContinuationA(s => cb(d, s)), d.Describe(), shotTop));
        
        CampaignSelectScreen = new UIScreen(DifficultyThenShot((d, sh) => 
            MainScenario(new GameReq(CampaignMode.MAIN, null, d, shot: sh))));
        StagePracticeScreen =
            new LazyUIScreen(() => Stages.Select(s =>
                (UINode)new NavigateUINode($"Stage {s.stage.stageNumber}", s.phases.Select(p =>
                    (UINode)new CacheNavigateUINode(TentativeCache, p.Title, DifficultyThenShot((d, sh) => {
                        ConfirmCache();
                        SelectStageContinue(s.stage,
                            new GameReq(CampaignMode.STAGE_PRACTICE, DefaultReturn, d, toPhase: p.index, shot: sh));
                    }))
                ).ToArray())
            ).ToArray()).With(PracticeUIScreen);
        BossPracticeScreen = 
            new LazyUIScreen(() => Bosses.Select(b => 
                (UINode)new NavigateUINode(b.boss.CardPracticeName, b.phases.Select(p =>
                    new CacheNavigateUINode(TentativeCache, p.Title, DifficultyThenShot((d, sh) => {
                        ConfirmCache();
                        SelectBossSinglePhase(b.boss,
                            new GameReq(CampaignMode.CARD_PRACTICE, DefaultReturn, d, toPhase: p.index, shot: sh));
                    })).With(smallDescrClass)
                ).ToArray())
            ).ToArray()).With(PracticeUIScreen);
        MainScreen = new UIScreen(
            new TransferNode(CampaignSelectScreen.top[2], "Main Scenario"),
            new TransferNode(StagePracticeScreen, "Stage Practice").EnabledIf(SaveData.r.CompletedMain),
            new TransferNode(BossPracticeScreen, "Boss Card Practice").EnabledIf(SaveData.r.CompletedMain),
            new FuncNode(RunTutorial, "Tutorial"),
            new FuncNode(Application.Quit, "Quit"),
            new OpenUrlNode("https://twitter.com/rdbatz", "Twitter (Browser)"),
            new OpenUrlNode("https://github.com/Bagoum/danmokou", "Github (Browser)")
            ).With(MainScreenV);
        base.Awake();
    }

    protected override IEnumerable<Object> Rebind() {
        var x = base.Rebind();
        if (returnTo != null) {
            foreach (var ind in returnTo) SimulateSelectIndex(ind);
        }
        returnTo = null;
        return x;
    }
}