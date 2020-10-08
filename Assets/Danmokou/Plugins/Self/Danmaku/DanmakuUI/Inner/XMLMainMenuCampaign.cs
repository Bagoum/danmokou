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
    private UIScreen OptionsScreen;
    private UIScreen ReplayScreen;

    protected override IEnumerable<UIScreen> Screens => new[] { CampaignSelectScreen, ExtraSelectScreen, 
        StagePracticeScreen, BossPracticeScreen, OptionsScreen, ReplayScreen, MainScreen }.NotNull();

    public VisualTreeAsset GenericUIScreen;
    public VisualTreeAsset GenericUINode;
    public VisualTreeAsset PracticeUIScreen;
    public VisualTreeAsset ShotScreen;
    public VisualTreeAsset MainScreenV;
    public VisualTreeAsset OptionsScreenV;
    public VisualTreeAsset ReplayScreenV;
    public VisualTreeAsset GenericOptionNodeV;

    public Transform shotDisplayContainer;
    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), GenericUIScreen},
        {typeof(UINode), GenericUINode},
    };
    private static UINode[] DifficultyNodes(Func<FixedDifficulty, UINode> map) =>
        VisibleDifficulties.Select(map).ToArray();

    private static UINode[] DifficultyFuncNodes(Func<FixedDifficulty, Action> map) =>
        DifficultyNodes(d => new FuncNode(map(d), d.Describe()));
    protected override void Awake() {
        if (!Application.isPlaying) return;
        
        Dictionary<PlayerConfig, FancyShotDisplay> shotDisplays = new Dictionary<PlayerConfig, FancyShotDisplay>();
        void HideShots() {
            foreach (var f in shotDisplays.Values) f.Show(false);
        }
        void ShowShot(PlayerConfig p, int shotIndex, ShotConfig s, Subshot sub) {
            HideShots();
            shotDisplays[p].SetShot(p, shotIndex, s, sub);
            shotDisplays[p].Show(true);
        }
        
        Func<DifficultySettings, PlayerTeam, bool> shotCont2 = null;
        UIScreen CreateShotScreen2(CampaignConfig c) {
            FixedDifficulty? dfc = FixedDifficulty.Normal;
            int dfcSlider = DifficultySettings.DEFAULT_SLIDER;
            if (c == null) return null;
            foreach (var p in c.players) {
                if (!shotDisplays.ContainsKey(p)) {
                    var d = shotDisplays[p] = Instantiate(p.shotDisplay, shotDisplayContainer)
                        .GetComponent<FancyShotDisplay>();
                    d.gameObject.SetActive(false);
                }
            }
            
            var optFixedDiff = new OptionNodeLR<FixedDifficulty?>("", d => dfc = d, 
                VisibleDifficultiesAndCustomDescribed.ToArray(), dfc);
            var optSliderDiff = new OptionNodeLR<int>("", d => dfcSlider = d,
                (DifficultySettings.MIN_SLIDER, DifficultySettings.MAX_SLIDER + 1).Range()
                    .Select(x => ($"{x}", x)).ToArray(), dfcSlider);
            var optSliderHelper = new PassthroughNode(() => 
                DifficultySettings.FancifySlider(dfcSlider));
            
            
            OptionNodeLR<PlayerConfig> playerSelect = null;
            DynamicOptionNodeLR<ShotConfig> shotSelect = null;
            OptionNodeLR<Subshot> subshotSelect = null;
            void _ShowShot() {
                ShowShot(playerSelect.Value, shotSelect.Index, shotSelect.Value, subshotSelect.Value);
            }
            playerSelect = new OptionNodeLR<PlayerConfig>("", _ => _ShowShot(), 
                c.players.Select(p => (p.shortTitle, p)).ToArray(), c.players[0]);
            //Place a fixed node in the second column for shot description
            shotSelect = new DynamicOptionNodeLR<ShotConfig>("", _ => _ShowShot(), () =>
                    playerSelect.Value.shots.Select((s, i) => 
                        (s.isMultiShot ? $"Multishot {i.ToABC()}" : $"Type {i.ToABC()}", s)).ToArray(),
                    playerSelect.Value.shots[0]);
            subshotSelect = new OptionNodeLR<Subshot>("", _ => _ShowShot(), 
                Subshots.Select(x => ($"Variant {x.Describe()}", x)).ToArray(), Subshot.TYPE_D);
            return new UIScreen(
                new PassthroughNode("\nDIFFICULTY"),
                optFixedDiff.With(GenericOptionNodeV).With(optionNoKeyClass, smallClass),
                optSliderDiff.With(GenericOptionNodeV).With(optionNoKeyClass, smallClass)
                    .VisibleIf(() => dfc == null),
                optSliderHelper.With(small1Class).VisibleIf(() => dfc == null),
                new PassthroughNode("").With(smallClass),
                new PassthroughNode("PLAYER"),
                playerSelect.With(GenericOptionNodeV).With(optionNoKeyClass, smallClass), 
                new PassthroughNode("").With(smallClass),
                new PassthroughNode("SHOT"),
                shotSelect.With(GenericOptionNodeV).With(optionNoKeyClass, smallClass), 
                subshotSelect.With(GenericOptionNodeV).With(optionNoKeyClass, smallClass)
                    .VisibleIf(() => shotSelect.Value.isMultiShot), 
                new PassthroughNode("").With(smallClass),
                new FuncNode(() => shotCont2(new DifficultySettings(dfc, dfcSlider), 
                    new PlayerTeam(0, subshotSelect.Value, (playerSelect.Value, shotSelect.Value))), "Play!", false)
                //new UINode(() => shotSelect.Value.title).SetAlwaysVisible().FixDepth(1),
                //new UINode(() => shotSelect.Value.description)
                //    .With(shotDescrClass).With(smallClass)
                //    .SetAlwaysVisible().FixDepth(1)
                
                ).With(ShotScreen)
                .OnEnter(_ShowShot)
                .OnExit(HideShots);
        }

        CampaignSelectScreen = CreateShotScreen2(References.campaign);
        ExtraSelectScreen = CreateShotScreen2(References.exCampaign);
        var shotTopMap = new Dictionary<CampaignConfig, UINode>() {{ References.campaign, CampaignSelectScreen.top[1] }};
        if (References.exCampaign != null) shotTopMap[References.exCampaign] = ExtraSelectScreen.top[1];
        
        Func<(bool, UINode)> GoToShotScreen(CampaignConfig c, Func<DifficultySettings, PlayerTeam, bool> cont) {
            return () => {
                shotCont2 = cont;
                return (true, shotTopMap[c]);
            };
        }
        
        StagePracticeScreen = new LazyUIScreen(() => PStages.Select(stage =>
            (UINode)new NavigateUINode($"Stage {stage.stage.stageNumber}", 
                stage.phases.Select(phase =>
                    new CacheNavigateUINode(TentativeCache, phase.Title).SetConfirmOverride(
                        GoToShotScreen(stage.campaign.campaign, (d, p) => {
                            ConfirmCache();
                            return new GameRequest(GameRequest.WaitDefaultReturn, d, 
                                stage: new StagePracticeRequest(stage, phase.index),
                                player: p).Run();
                        })
                    )
                ).ToArray()
            )
        ).ToArray()).With(PracticeUIScreen);
        BossPracticeScreen = new LazyUIScreen(() => PBosses.Select(boss => 
                (UINode)new NavigateUINode(boss.boss.CardPracticeName, boss.phases.Select(phase =>
                    new CacheNavigateUINode(TentativeCache, phase.Title).SetConfirmOverride(
                        GoToShotScreen(boss.campaign.campaign, (d, p) => {
                            ConfirmCache();
                            return new GameRequest(GameRequest.WaitShowPracticeSuccessMenu, d, 
                                boss: new BossPracticeRequest(boss, phase),
                                player: p).Run();
                        })
                    ).With(smallClass)
                ).ToArray()
            )
        ).ToArray()).With(PracticeUIScreen);
        OptionsScreen = new UIScreen(XMLPauseMenu.GetOptions(true, x => x.With(GenericOptionNodeV)).ToArray())
            .With(OptionsScreenV).OnExit(SaveData.AssignSettingsChanges);
        ReplayScreen = XMLUtils.ReplayScreen(true, TentativeCache, ConfirmCache).With(ReplayScreenV);
        MainScreen = new UIScreen(
            new UINode("Main Scenario").SetConfirmOverride(GoToShotScreen(References.campaign, 
                (d, p) => GameRequest.RunCampaign(MainCampaign, null, d, p))),
            References.exCampaign == null ? null :
                new UINode("Extra Stage").SetConfirmOverride(GoToShotScreen(References.exCampaign, 
                    (d, p) => GameRequest.RunCampaign(ExtraCampaign, null, d, p)))
                    .EnabledIf(SaveData.r.MainCampaignCompleted),
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