using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using static Danmokou.Core.GameManagement;
using static Danmokou.UI.XML.XMLUtils;
using static Danmokou.Core.LocalizedStrings.UI;

namespace Danmokou.UI.XML {


public abstract class XMLMainMenu : XMLMenu { }

/// <summary>
/// Class to manage the main menu UI for campaign-type games.
/// </summary>
[Preserve]
public class XMLMainMenuCampaign : XMLMainMenu {
    private static List<CacheInstruction>? _returnTo;
    protected override List<CacheInstruction>? ReturnTo {
        get => _returnTo;
        set => _returnTo = value;
    }

    private UIScreen PlaymodeScreen = null!;
    private UIScreen DifficultyScreen = null!;
    public UIScreen CustomDifficultyScreen { get; private set; } = null!;
    private UIScreen CampaignShotScreen = null!;
    private UIScreen? ExtraShotScreen;
    public UIScreen StagePracticeScreen { get; private set; }= null!;
    public UIScreen BossPracticeScreen { get; private set; } = null!;
    private UIScreen OptionsScreen = null!;
    private UIScreen ReplayScreen = null!;
    private UIScreen HighScoreScreen = null!;
    private UIScreen StatsScreen = null!;
    private UIScreen MusicRoomScreen = null!;
    private UIScreen? AchievementsScreen;

    protected override IEnumerable<UIScreen> Screens => new[] {
        PlaymodeScreen, DifficultyScreen, CustomDifficultyScreen, CampaignShotScreen, ExtraShotScreen,
        StagePracticeScreen, BossPracticeScreen, OptionsScreen, ReplayScreen, HighScoreScreen,
        StatsScreen, AchievementsScreen, MusicRoomScreen,
        MainScreen
    }.NotNull();

    public VisualTreeAsset ShotScreen = null!;
    public VisualTreeAsset MainScreenV = null!;
    public VisualTreeAsset CustomDifficultyScreenV = null!;
    public VisualTreeAsset BossPracticeScreenV = null!;
    public VisualTreeAsset StagePracticeScreenV = null!;
    public VisualTreeAsset OptionsScreenV = null!;
    public VisualTreeAsset ReplayScreenV = null!;
    public VisualTreeAsset HighScoreScreenV = null!;
    public VisualTreeAsset StatsScreenV = null!;
    public VisualTreeAsset SpellPracticeNodeV = null!;
    public VisualTreeAsset AchievementsScreenV = null!;
    public VisualTreeAsset AchievementsNodeV = null!;
    public VisualTreeAsset MusicRoomScreenV = null!;

    public DifficultySubmenu difficultySubmenu = null!;
    public PlayModeSubmenu playmodeSubmenu = null!;
    public Transform shotDisplayContainer = null!;
    public BehaviorEntity? shotSetup;
    public GameObject? demoPlayerSetup;

        protected override void Start() {
        if (ReturnTo == null) {
            uiRenderer.Slide(new Vector2(3, 0), Vector2.zero, 1f, DMath.M.EOutSine, null);
            uiRenderer.Fade(0, 1, 1f, x => x, null);
        }
        base.Start();
    }

    protected override void Awake() {
        if (!Application.isPlaying) return;
        
        Func<DifficultySettings, (bool, UINode)> dfcCont = null!;
        Func<TeamConfig, bool> shotCont = null!;
        
        UIScreen? CreatePlayerScreen(SMAnalysis.AnalyzedCampaign? c, bool enableDemo) {
            if (c == null) return null;
            foreach (var sc in c.campaign.players
                .SelectMany(p => p.shots2)
                .Select(s2 => s2.shot)) {
                if (sc.prefab != null)
                    sc.prefab.GetComponentsInChildren<FireOption>()
                        .ForEach(fo => fo.Preload());
            }
            Player.PlayerController? demoPlayer = null;
            Cancellable? demoCT = null;
            OptionNodeLR<ShipConfig> playerSelect = null!;
            DynamicOptionNodeLR<ShotConfig> shotSelect = null!;
            OptionNodeLR<Subshot> subshotSelect = null!;
            var team = new TeamConfig(0, Subshot.TYPE_D,
                c.campaign.players
                    .SelectMany(p => p.shots2
                        .Select(s => (p, s.shot)))
                    .ToArray());
            var smeta = new SharedInstanceMetadata(team, new DifficultySettings(FixedDifficulty.Normal));
            
            void CleanupDemo() {
                if (demoPlayer != null) {
                    demoPlayer.InvokeCull();
                    demoPlayer = null;
                }
                demoCT?.Cancel();
                Replayer.End(null);
            }
            void UpdateDemo() {
                if (!enableDemo || shotSetup == null || demoPlayerSetup == null) return;
                GameManagement.NewInstance(InstanceMode.NULL, null, 
                    new InstanceRequest(() => true, smeta, new CampaignRequest(c!)));
                if (demoPlayer == null) {
                    demoPlayer = Instantiate(demoPlayerSetup).GetComponent<Player.PlayerController>();
                }
                demoPlayer.UpdateTeam((playerSelect.Value, shotSelect.Value), subshotSelect.Value);
                demoPlayer.transform.position = new Vector2(0, -3);
                Replayer.End(null);
                var effShot = shotSelect.Value.GetSubshot(subshotSelect.Value);
                if (effShot.demoReplay != null) {
                    Replayer.BeginReplaying(new Replayer.ReplayerConfig(
                        Replayer.ReplayerConfig.FinishMethod.REPEAT, 
                        effShot.demoReplay.Frames,
                        () => demoPlayer.transform.position = new Vector2(0, -3)
                    ));
                    demoCT?.Cancel();
                    demoCT = new Cancellable();
                    if (effShot.demoSetupSM != null) {
                        StateMachineManager.FromText(effShot.demoSetupSM)?.Start(new SMHandoff(shotSetup, demoCT));
                    }
                } else {
                    Replayer.BeginReplaying(new Replayer.ReplayerConfig(
                        Replayer.ReplayerConfig.FinishMethod.REPEAT,
                        () => new []{new InputManager.FrameInput(0, 0, false, false, false, false, false, false, false)}
                    ));
                }
            }
            
            (ShipConfig player, FancyShotDisplay display)[] displays = c.campaign.players.Select(p =>
                (p, Instantiate(p.shotDisplay, shotDisplayContainer).GetComponent<FancyShotDisplay>())).ToArray();

            void HidePlayers() {
                foreach (var f in displays) f.display.Show(false);
            }
            void ShowShot(ShipConfig p, ShotConfig s, Subshot sub, bool first) {
                if (!first) UpdateDemo();
                var index = displays.IndexOf(sd => sd.player == p);
                displays[index].display.SetShot(p, s, sub);
                displays.ForEachI((i, x) => {
                    //Only show the selected player on entry so the others don't randomly appear on screen during swipe
                    if (!first || i == index) x.display.Show(true);
                    x.display.SetRelative(i, index, first);
                });
            }

            HidePlayers();
            void _ShowShot(bool first = false) {
                ShowShot(playerSelect.Value, shotSelect.Value, subshotSelect.Value, first);
            }
            
            playerSelect = new OptionNodeLR<ShipConfig>(LocalizedString.Empty, _ => _ShowShot(),
                c.campaign.players.Select(p => (p.ShortTitle, p)).ToArray(), c.campaign.players[0]);

            //Place a fixed node in the second column for shot description
            shotSelect = new DynamicOptionNodeLR<ShotConfig>(LocalizedString.Empty, _ => _ShowShot(), () =>
                    playerSelect.Value.shots2.Select(s => (s.shot.isMultiShot ? 
                            shotsel_multi(s.ordinal) : 
                            shotsel_type(s.ordinal), s.shot)).ToArray(),
                playerSelect.Value.shots2[0].shot);
            subshotSelect = new OptionNodeLR<Subshot>(LocalizedString.Empty, _ => _ShowShot(),
                EnumHelpers2.Subshots.Select(x => (shotsel_variant_ls(x.Describe()), x)).ToArray(), Subshot.TYPE_D);
            return new UIScreen(
                    new PassthroughNode(shotsel_player).With(centerTextClass),
                    playerSelect.With(optionNoKeyClass),
                    new PassthroughNode(LocalizedString.Empty),
                    new PassthroughNode(shotsel_shot).With(centerTextClass),
                    shotSelect.With(optionNoKeyClass),
                    subshotSelect.With(optionNoKeyClass)
                        .VisibleIf(() => shotSelect.Value.isMultiShot),
                    new PassthroughNode(LocalizedString.Empty),
                    new FuncNode(() => shotCont(new TeamConfig(0, subshotSelect.Value,
                        (playerSelect.Value, shotSelect.Value))), play_game, false).With(centerTextClass)
                    //new UINode(() => shotSelect.Value.title).SetAlwaysVisible().FixDepth(1),
                    //new UINode(() => shotSelect.Value.description)
                    //    .With(shotDescrClass).With(smallClass)
                    //    .SetAlwaysVisible().FixDepth(1)

                ).With(ShotScreen)
                .OnEnter(() => _ShowShot(true))
                .OnPostEnter(UpdateDemo)
                .OnPreExit(() => {
                    CleanupDemo();
                    displays.ForEach(x => {
                        if (x.player != playerSelect.Value) x.display.Show(false);
                    });
                })
                .OnExit(HidePlayers);
        }

        DifficultyScreen = difficultySubmenu.Initialize(this, x => dfcCont(x));
        CustomDifficultyScreen = CreateCustomDifficultyEdit(CustomDifficultyScreenV, x => dfcCont(x));
        CampaignShotScreen = CreatePlayerScreen(GameManagement.MainCampaign, shotSetup != null)!;
        ExtraShotScreen = CreatePlayerScreen(GameManagement.ExtraCampaign, shotSetup != null);
        var shotTopMap = new Dictionary<CampaignConfig, UINode>() {{References.campaign, CampaignShotScreen.top[1]}};
        if (ExtraShotScreen != null && References.exCampaign != null) 
            shotTopMap[References.exCampaign] = ExtraShotScreen.top[1];

        (bool, UINode) GetShot(CampaignConfig c, Func<TeamConfig, bool> cont) {
            shotCont = cont;
            return (true, shotTopMap![c]);
        }

        Func<(bool, UINode?)> GetDifficultyThenShot(CampaignConfig c, Func<SharedInstanceMetadata, bool> cont) {
            return () => {
                dfcCont = d => GetShot(c, p => cont(new SharedInstanceMetadata(p, d)));
                return (true, DifficultyScreen.top[0]);
            };
        }

        StagePracticeScreen = new LazyUIScreen(() => PStages.Select(stage =>
            (UINode) new NavigateUINode(practice_stage_ls(stage.stage.stageNumber),
                stage.Phases.Select(phase =>
                    new CacheNavigateUINode(TentativeCache, phase.Title).SetConfirmOverride(
                        GetDifficultyThenShot(stage.campaign.campaign, meta => {
                            ConfirmCache();
                            return new InstanceRequest(InstanceRequest.ShowPracticeSuccessMenu, meta,
                                stage: new StagePracticeRequest(stage, phase.index)).Run();
                        })
                    )
                ).Prepend(
                    new CacheNavigateUINode(TentativeCache, practice_fullstage).SetConfirmOverride(
                        GetDifficultyThenShot(stage.campaign.campaign, meta => {
                            ConfirmCache();
                            return new InstanceRequest(InstanceRequest.ShowPracticeSuccessMenu, meta,
                                stage: new StagePracticeRequest(stage, 1)).Run();
                        })
                    )
                ).ToArray()
            )
        ).ToArray()).With(StagePracticeScreenV);
        var cmpSpellHist = SaveData.r.GetCampaignSpellHistory();
        var prcSpellHist = SaveData.r.GetPracticeSpellHistory();
        BossPracticeScreen = new LazyUIScreen(() => PBosses.Select(boss =>
            (UINode) new NavigateUINode(boss.boss.BossPracticeName, boss.Phases.Select(phase => {
                    var req = new BossPracticeRequest(boss, phase);
                    return new CacheNavigateUINode(TentativeCache, phase.Title).SetConfirmOverride(
                        GetDifficultyThenShot(boss.campaign.campaign, meta => {
                            ConfirmCache();
                            return new InstanceRequest(InstanceRequest.ShowPracticeSuccessMenu, meta,
                                boss: req).Run();
                        })
                    ).With(SpellPracticeNodeV).With(ev => {
                        var (cs, ct) = cmpSpellHist.GetOrDefault(req.Key);
                        var (ps, pt) = prcSpellHist.GetOrDefault(req.Key);
                        ev.Q<Label>("CampaignHistory").text = $"{cs}/{ct}";
                        ev.Q<Label>("PracticeHistory").text = $"{ps}/{pt}";
                    });
                }).ToArray()
            )
        ).ToArray()).With(BossPracticeScreenV);
        PlaymodeScreen = playmodeSubmenu.Initialize(this, GetDifficultyThenShot);
        OptionsScreen = new UIScreen(XMLPauseMenu.GetOptions(true).ToArray())
            .With(OptionsScreenV).OnExit(SaveData.AssignSettingsChanges);
        ReplayScreen = XMLUtils.ReplayScreen(TentativeCache, ConfirmCache).With(ReplayScreenV);
        HighScoreScreen = XMLUtils.HighScoreScreen(ReplayScreen, FinishedCampaigns.ToArray())
            .With(HighScoreScreenV);
        StatsScreen = XMLUtils.StatisticsScreen(StatsScreenV, SaveData.r.FinishedCampaignGames, Campaigns);
        MusicRoomScreen = XMLUtils.MusicRoomScreen(MusicRoomScreenV, References.tracks);
        if (GameManagement.Achievements != null)
            AchievementsScreen = XMLUtils.AchievementsScreen(
                AchievementsScreenV, AchievementsNodeV, GameManagement.Achievements);
        MainScreen = new UIScreen(
            new TransferNode(PlaymodeScreen, main_gamestart).With(large1Class),
            new OptionNodeLR<Locale>(main_lang, l => {
                    SaveData.UpdateLocale(l);
                    SaveData.AssignSettingsChanges();
                }, new[] {
                    (new LocalizedString("English"), Locale.EN),
                    (new LocalizedString("日本語"), Locale.JP)
                }, SaveData.s.Locale)
                .With(large1Class),
            new TransferNode(HighScoreScreen, main_scores)
                .EnabledIf(FinishedCampaigns.Any())
                .With(large1Class),
            new TransferNode(StatsScreen, main_stats)
                .EnabledIf(FinishedCampaigns.Any())
                .With(large1Class),
            new TransferNode(MusicRoomScreen, main_musicroom)
                .EnabledIf(MusicRoomScreen.top.Length > 0)
                .With(large1Class),
            AchievementsScreen == null ? null :
                new TransferNode(AchievementsScreen, main_achievements).With(large1Class),
            new TransferNode(ReplayScreen, main_replays)
                .EnabledIf(SaveData.p.ReplayData.Count > 0)
                .With(large1Class),
            new TransferNode(OptionsScreen, main_options)
                .With(large1Class),
            new FuncNode(Application.Quit, main_quit)
                .With(large1Class),
            new OpenUrlNode("https://twitter.com/rdbatz", main_twitter)
                .With(large1Class)
        ).With(MainScreenV);
        MainScreen.ExitNode = MainScreen.top[MainScreen.top.Length - 2];
        base.Awake();
    }
}
}