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

[Serializable]
public struct DifficultyDisplay {
    public FixedDifficulty dfc;
    public FancyDifficultyDisplay display;
}
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

    private UIScreen DifficultyScreen;
    private UIScreen CustomDifficultyScreen;
    private UIScreen CampaignShotScreen;
    private UIScreen ExtraShotScreen;
    private UIScreen StagePracticeScreen;
    private UIScreen BossPracticeScreen;
    private UIScreen OptionsScreen;
    private UIScreen ReplayScreen;
    private UIScreen HighScoreScreen;

    protected override IEnumerable<UIScreen> Screens => new[] { 
        DifficultyScreen, CustomDifficultyScreen, CampaignShotScreen, ExtraShotScreen, 
        StagePracticeScreen, BossPracticeScreen, OptionsScreen, ReplayScreen, HighScoreScreen,
        MainScreen }.NotNull();

    public VisualTreeAsset GenericUIScreen;
    public VisualTreeAsset GenericUINode;
    public VisualTreeAsset ShotScreen;
    public VisualTreeAsset MainScreenV;
    public VisualTreeAsset CustomDifficultyScreenV;
    public VisualTreeAsset BossPracticeScreenV;
    public VisualTreeAsset StagePracticeScreenV;
    public VisualTreeAsset OptionsScreenV;
    public VisualTreeAsset ReplayScreenV;
    public VisualTreeAsset HighScoreScreenV;
    public VisualTreeAsset GenericOptionNodeV;
    public VisualTreeAsset SpellPracticeNodeV;

    public Transform shotDisplayContainer;
    public DifficultyDisplay[] difficultyDisplays;
    public FancyDifficultyDisplay customDifficulty;
    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), GenericUIScreen},
        {typeof(UINode), GenericUINode},
    };
    private static UINode[] DifficultyNodes(Func<FixedDifficulty, UINode> map) =>
        VisibleDifficulties.Select(map).ToArray();

    private static UINode[] DifficultyFuncNodes(Func<FixedDifficulty, Action> map) =>
        DifficultyNodes(d => new FuncNode(map(d), d.Describe()));

    protected override void Start() {
        if (ReturnTo == null) {
            UIBuilderRenderer.Slide(new Vector2(3, 0), Vector2.zero, 1f, DMath.M.EOutSine, null);
            UIBuilderRenderer.Fade(0, 1, 1f, x => x, null);
        }
        base.Start();
    }

    protected override void Awake() {
        if (!Application.isPlaying) return;
        
        List<(FixedDifficulty? key, FancyDifficultyDisplay display)> dfcDisplays 
            = new List<(FixedDifficulty?, FancyDifficultyDisplay)>();
        if (customDifficulty != null) dfcDisplays.Add((null, customDifficulty));
        foreach (var dfc in difficultyDisplays) {
            dfcDisplays.Add((dfc.dfc, dfc.display));
            dfc.display.Show(false);
        }
        
        void HideDfcs() {
            dfcDisplays.ForEach(x => x.display.Show(false));
        }
        Func<DifficultySettings, (bool, UINode)> dfcCont = null;
        UIScreen CreateDifficultySelect() {
            var options = CustomAndVisibleDifficulties
                .Where(d => dfcDisplays.IndexOf(x => x.key == d) > -1).ToArray();
            FixedDifficulty? dfc = FixedDifficulty.Normal;
            void _ShowDfc(bool first = false) {
                var index = dfcDisplays.IndexOf(x => x.key == dfc);
                dfcDisplays.ForEachI((i, x) => {
                    x.display.Show(true);
                    x.display.SetRelative(i, index, options.Length, first);
                });
            }
            void SetDfc(FixedDifficulty? newDfc) {
                dfc = newDfc;
                _ShowDfc();
            }
            
            var optFixedDiff = new OptionNodeLR<FixedDifficulty?>("", SetDfc, 
                options.Select(x => (x?.Describe() ?? "", x)).ToArray(), dfc);
            return new UIScreen(
                    optFixedDiff.With(GenericOptionNodeV).With(hideClass)
                        .SetUpOverride(() => optFixedDiff.Left())
                        .SetDownOverride(() => optFixedDiff.Right())
                        .SetConfirmOverride(() => dfc.Try(out var fd) 
                            ? dfcCont(new DifficultySettings(fd)) 
                            : (true, CustomDifficultyScreen.top[0]))
                ).OnEnter(() => _ShowDfc(true))
                .OnExit(HideDfcs);
        }

        UIScreen CreateCustomDifficultyEdit() {
            var dfc = new DifficultySettings(null);
            string[] descr = {descriptorClass};
            double[] _pctMods = {
                0.31, 0.45, 0.58, 0.7, 0.85, 1, 1.2, 1.4, 1.6, 1.8, 2
            };
            var pctMods = _pctMods.Select(x => {
                var offset = (x - 1) * 100;
                var prefix = (offset >= 0) ? "+" : "";
                return ($"{prefix}{offset}%", x);
            }).ToArray();
            (string, bool)[] yesNo = {("Yes", true), ("No", false)};
            IEnumerable<(string, double)> AddPlus(IEnumerable<double> arr) => arr.Select(x => {
                var prefix = (x >= 0) ? "+" : "";
                return ($"{prefix}{x}", x);
            });
            UINode MakeOption<T>(string title, IEnumerable<(string, T)> options, T deflt, Action<T> apply, string description) {
                return new OptionNodeLR<T>(title, apply, options.ToArray(),
                    deflt, new UINode("\n\n" + description).With(descr)).With(GenericOptionNodeV).With(small1Class);
            }
            UINode MakePctOption(string title, double deflt, Action<double> apply, string description)
                => MakeOption(title, pctMods, deflt, apply, description);
            UINode MakeYesNoOption(string title, bool deflt, Action<bool> apply, string description)
                => MakeOption(title, yesNo, deflt, apply, description);
            UINode MakeOptionAuto<T>(string title, IEnumerable<T> options, T deflt, Action<T> apply, string description)
                => MakeOption(title, options.Select(x => (x.ToString(), x)), deflt, apply, description);
            
            
            var optSliderHelper = new PassthroughNode(() =>     
                $"   Effective difficulty: {DifficultySettings.FancifySlider(dfc.customValueSlider)}");
            return new UIScreen(
                    MakeOption("Scaling", (DifficultySettings.MIN_SLIDER, DifficultySettings.MAX_SLIDER + 1).Range()
                        .Select(x => ($"{x}", x)), dfc.customValueSlider, x => dfc.customValueSlider = x, 
                            "Set the base difficulty scaling variable." +
                            "\nThis primarily affects the number and firing rate of bullets " +
                            "at an exponential rate."),
                    optSliderHelper.With(small2Class),
                    MakeOptionAuto("Suicide Bullets", new[] { 0, 1, 3, 5, 7 }, dfc.numSuicideBullets, x => dfc.numSuicideBullets = x,
                            "Stage enemies fire suicide bullets in a spread pattern " +
                            "aimed at the player when they are killed."
                        ),
                    MakePctOption("Player Damage Mod", dfc.playerDamageMod, x => dfc.playerDamageMod = x, 
                        "Change the amount of damage dealt by the player's shots."),
                    MakePctOption("Boss HP Mod", dfc.bossHPMod, x => dfc.bossHPMod = x, 
                        "Change the amount of health that boss enemies have."),
                    MakeYesNoOption("Respawn on Death", dfc.respawnOnDeath, x => dfc.respawnOnDeath = x,
                        "Set whether or not the player respawns from the bottom of the screen when dying."),
                    MakePctOption("Faith Decay Mod", dfc.faithDecayMultiplier, x => dfc.faithDecayMultiplier = x,
                        "Change the rate at which the faith meter automatically empties over time."),
                    MakePctOption("Faith Acquire Mod", dfc.faithAcquireMultiplier, x => dfc.faithAcquireMultiplier = x,
                        "Change the rate at which you gain faith by defeating enemies, collecting items, etc."),
                    MakePctOption("Meter Usage Mod", dfc.meterUsageMultiplier, x => dfc.meterUsageMultiplier = x,
                        "Change the rate at which the special meter empties while it is in use."),
                    MakePctOption("Meter Acquire Mod", dfc.meterAcquireMultiplier, x => dfc.meterAcquireMultiplier = x,
                        "Change the rate at which you gain special meter by collecting gems."),
                    MakeYesNoOption("Bombs Enabled", dfc.bombsEnabled, x=> dfc.bombsEnabled = x,
                        "Set whether or not the player can use bombs."),
                    MakeYesNoOption("Meter Enabled", dfc.meterEnabled, x => dfc.meterEnabled = x,
                        "Set whether or not the player can use the special meter."),
                    MakePctOption("Player Speed Mod", dfc.playerSpeedMultiplier, x => dfc.playerSpeedMultiplier = x,
                        "Change the speed of the player character."),
                    MakePctOption("Player Hitbox Mod", dfc.playerHitboxMultiplier, x => dfc.playerHitboxMultiplier = x,
                        "Change the size of the player hitbox. " +
                        "If a bullet comes in contact with the hitbox, the player will lose a life."),
                    MakePctOption("Player Grazebox Mod", dfc.playerGrazeboxMultiplier, x => dfc.playerGrazeboxMultiplier = x,
                        "Change the size of the player grazebox. " +
                        "If a bullet comes in contact with the grazebox, the player will gain some graze."),
                    MakeOption("Starting Lives", (1, 14).Range().Select(x => ($"{x}", (int?) x)).Prepend(("Default", null)),
                        dfc.startingLives, x => dfc.startingLives = x, 
                        "Change the number of lives the player has when starting the game."),
                    MakeOption("PoC Offset", AddPlus(new[] {
                            //can't use addition to generate these because -6 + 0.4 =/= -5.6...
                            -6, -5.6, -5.2, -4.8, -4.4, -4, -3.6, -3.2, -2.8, -2.4, -2, -1.6, -1.2, -0.8, -0.4,
                            0, 0.4, 0.8, 1.2, 1.6, 2
                        }), dfc.pocOffset, x => dfc.pocOffset = x,
                        "Change the vertical height of the point of collection."),
                    //new PassthroughNode(""),
                    new UINode("To Player Select").SetConfirmOverride(() => dfcCont(dfc))
            ).With(CustomDifficultyScreenV);
        }
        
        
        Func<PlayerTeam, bool> shotCont = null;
        UIScreen CreatePlayerScreen(CampaignConfig c) {
            if (c == null) return null;
            (PlayerConfig player, FancyShotDisplay display)[] displays = c.players.Select(p =>
                (p, Instantiate(p.shotDisplay, shotDisplayContainer).GetComponent<FancyShotDisplay>())).ToArray();
            
            void HidePlayers() {
                foreach (var f in displays) f.display.Show(false);
            }
            void ShowShot(PlayerConfig p, int shotIndex, ShotConfig s, Subshot sub, bool first) {
                var index = displays.IndexOf(sd => sd.player == p);
                displays[index].display.SetShot(p, shotIndex, s, sub);
                displays.ForEachI((i, x) => {
                    //Only show the selected player on entry so the others don't randomly appear on screen during swipe
                    if (!first || i == index) x.display.Show(true);
                    x.display.SetRelative(i, index, first);
                });
            }

            HidePlayers();
            OptionNodeLR<PlayerConfig> playerSelect = null;
            DynamicOptionNodeLR<ShotConfig> shotSelect = null;
            OptionNodeLR<Subshot> subshotSelect = null;
            void _ShowShot(bool first = false) {
                ShowShot(playerSelect.Value, shotSelect.Index, shotSelect.Value, subshotSelect.Value, first);
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
                new PassthroughNode("PLAYER").With(centerTextClass),
                playerSelect.With(GenericOptionNodeV).With(optionNoKeyClass), 
                new PassthroughNode(""),
                new PassthroughNode("SHOT").With(centerTextClass),
                shotSelect.With(GenericOptionNodeV).With(optionNoKeyClass), 
                subshotSelect.With(GenericOptionNodeV).With(optionNoKeyClass)
                    .VisibleIf(() => shotSelect.Value.isMultiShot), 
                new PassthroughNode(""),
                new FuncNode(() => shotCont(new PlayerTeam(0, subshotSelect.Value, 
                    (playerSelect.Value, shotSelect.Value))), "Play!", false).With(centerTextClass)
                //new UINode(() => shotSelect.Value.title).SetAlwaysVisible().FixDepth(1),
                //new UINode(() => shotSelect.Value.description)
                //    .With(shotDescrClass).With(smallClass)
                //    .SetAlwaysVisible().FixDepth(1)
                
                ).With(ShotScreen)
                .OnEnter(() => _ShowShot(true))
                .OnPreExit(() => displays.ForEach(x => {
                    if (x.player != playerSelect.Value) x.display.Show(false);
                }))
                .OnExit(HidePlayers);
        }

        DifficultyScreen = CreateDifficultySelect();
        CustomDifficultyScreen = CreateCustomDifficultyEdit();
        CampaignShotScreen = CreatePlayerScreen(References.campaign);
        ExtraShotScreen = CreatePlayerScreen(References.exCampaign);
        var shotTopMap = new Dictionary<CampaignConfig, UINode>() {{ References.campaign, CampaignShotScreen.top[1] }};
        if (References.exCampaign != null) shotTopMap[References.exCampaign] = ExtraShotScreen.top[1];

        (bool, UINode) GetShot(CampaignConfig c, Func<PlayerTeam, bool> cont) {
            shotCont = cont;
            return (true, shotTopMap[c]);
        }
        
        Func<(bool, UINode)> GetDifficultyThenShot(CampaignConfig c, Func<GameMetadata, bool> cont) {
            return () => {
                dfcCont = d => GetShot(c, p => cont(new GameMetadata(p, d)));
                return (true, DifficultyScreen.top[0]);
            };
        }
        
        StagePracticeScreen = new LazyUIScreen(() => PStages.Select(stage =>
            (UINode)new NavigateUINode($"Stage {stage.stage.stageNumber}", 
                stage.phases.Select(phase =>
                    new CacheNavigateUINode(TentativeCache, phase.Title).SetConfirmOverride(
                        GetDifficultyThenShot(stage.campaign.campaign, meta => {
                            ConfirmCache();
                            return new GameRequest(GameRequest.WaitDefaultReturn, meta, 
                                stage: new StagePracticeRequest(stage, phase.index)).Run();
                        })
                    )
                ).Prepend(
                    new CacheNavigateUINode(TentativeCache, "Full Stage").SetConfirmOverride(
                        GetDifficultyThenShot(stage.campaign.campaign, meta => {
                            ConfirmCache();
                            return new GameRequest(GameRequest.WaitDefaultReturn, meta, 
                                stage: new StagePracticeRequest(stage, 1)).Run();
                        })
                    )
                ).ToArray()
            )
        ).ToArray()).With(StagePracticeScreenV);
        var cmpSpellHist = SaveData.r.GetCampaignSpellHistory();
        var prcSpellHist = SaveData.r.GetPracticeSpellHistory();
        BossPracticeScreen = new LazyUIScreen(() => PBosses.Select(boss => 
                (UINode)new NavigateUINode(boss.boss.CardPracticeName, boss.phases.Select(phase =>
                    new CacheNavigateUINode(TentativeCache, phase.Title).SetConfirmOverride(
                        GetDifficultyThenShot(boss.campaign.campaign, meta => {
                            ConfirmCache();
                            return new GameRequest(GameRequest.WaitShowPracticeSuccessMenu, meta, 
                                boss: new BossPracticeRequest(boss, phase)).Run();
                        })
                    ).With(SpellPracticeNodeV).With(ev => {
                        var key = (boss.campaign.Key, boss.boss.key, phase.index);
                        var (cs, ct) = cmpSpellHist.GetOrDefault(key);
                        var (ps, pt) = prcSpellHist.GetOrDefault(key);
                        ev.Q<Label>("CampaignHistory").text = $"{cs}/{ct}";
                        ev.Q<Label>("PracticeHistory").text = $"{ps}/{pt}";
                    })
                ).ToArray()
            )
        ).ToArray()).With(BossPracticeScreenV);
        OptionsScreen = new UIScreen(XMLPauseMenu.GetOptions(true, x => x.With(GenericOptionNodeV)).ToArray())
            .With(OptionsScreenV).OnExit(SaveData.AssignSettingsChanges);
        ReplayScreen = XMLUtils.ReplayScreen(true, TentativeCache, ConfirmCache).With(ReplayScreenV);
        HighScoreScreen = XMLUtils.HighScoreScreen(GenericOptionNodeV, ReplayScreen, FinishedCampaigns.ToArray()).With(HighScoreScreenV);
        MainScreen = new UIScreen(
            new UINode("Main Scenario")
                .SetConfirmOverride(GetDifficultyThenShot(References.campaign, 
                    meta => GameRequest.RunCampaign(MainCampaign, null, meta)))
                .With(large1Class),
            References.exCampaign == null ? null :
                new UINode("Extra Stage")
                    .SetConfirmOverride(GetDifficultyThenShot(References.exCampaign, 
                        meta => GameRequest.RunCampaign(ExtraCampaign, null, meta)))
                    .EnabledIf(SaveData.r.MainCampaignCompleted)
                    .With(large1Class),
            new TransferNode(StagePracticeScreen, "Stage Practice")
                .EnabledIf(PStages.Length > 0)
                .With(large1Class),
            new TransferNode(BossPracticeScreen, "Boss Practice")
                .EnabledIf(PBosses.Length > 0)
                .With(large1Class),
            new TransferNode(HighScoreScreen, "Scores")
                .EnabledIf(FinishedCampaigns.Any())
                .With(large1Class),
            new TransferNode(ReplayScreen, "Replays")
                .EnabledIf(SaveData.p.ReplayData.Count > 0)
                .With(large1Class),
            References.tutorial == null ? null :
                new FuncNode(GameRequest.RunTutorial, "Tutorial")
                    .With(large1Class),
            new TransferNode(OptionsScreen, "Options")
                .With(large1Class),
            new FuncNode(Application.Quit, "Quit")
                .With(large1Class),
            new OpenUrlNode("https://twitter.com/rdbatz", "Twitter (Browser)")
                .With(large1Class)
            //new OpenUrlNode("https://www.youtube.com/watch?v=cBNnNJrA5_w&list=PLkd4SjCCKjq6B5u_5DrSU4Qz0QgZfgnh7", "OST (Browser)")
            ).With(MainScreenV);
        base.Awake();
    }
}