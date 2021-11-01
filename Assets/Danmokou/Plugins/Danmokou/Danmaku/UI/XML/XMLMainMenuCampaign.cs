using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
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
using static Danmokou.Services.GameManagement;
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

    public override void FirstFrame() {
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
            PlayerController? demoPlayer = null;
            Cancellable? demoCT = null;
            OptionNodeLR<ShipConfig> playerSelect = null!;
            DynamicOptionNodeLR<ISupportAbilityConfig> supportSelect = null!;
            DynamicOptionNodeLR<ShotConfig> shotSelect = null!;
            OptionNodeLR<Subshot> subshotSelect = null!;
            ReplayActor? r = null;
            
            
            var team = new TeamConfig(0, Subshot.TYPE_D, null,
                c.campaign.players
                    .SelectMany(p => p.shots2
                        .Select(s => (p, s.shot)))
                    .ToArray());
            var smeta = new SharedInstanceMetadata(team, new DifficultySettings(FixedDifficulty.Normal));
            
            bool ContinueAfterShot(TeamConfig tc) {
                r?.Cancel();
                return shotCont(tc);
            }
            void CleanupDemo() {
                Logs.Log("Cleaning up demo");
                r?.Cancel();
                if (demoPlayer != null) {
                    demoPlayer.InvokeCull();
                    demoPlayer = null;
                }
                demoCT?.Cancel();
                GameManagement.DeactivateInstance();
            }
            void UpdateDemo() {
                if (!enableDemo || shotSetup == null || demoPlayerSetup == null) return;
                GameManagement.DeactivateInstance();
                var effShot = shotSelect.Value.GetSubshot(subshotSelect.Value);
                r?.Cancel();
                if (effShot.demoReplay != null) {
                    r = Replayer.BeginReplaying(new Replayer.ReplayerConfig(
                        Replayer.ReplayerConfig.FinishMethod.REPEAT, 
                        effShot.demoReplay.Frames,
                        () => demoPlayer!.transform.position = new Vector2(0, -3)
                    ));
                    demoCT?.Cancel();
                    demoCT = new Cancellable();
                    if (effShot.demoSetupSM != null) {
                        StateMachineManager.FromText(effShot.demoSetupSM)?.Start(new SMHandoff(shotSetup, demoCT));
                    }
                } else {
                    r = Replayer.BeginReplaying(new Replayer.ReplayerConfig(
                        Replayer.ReplayerConfig.FinishMethod.REPEAT,
                        () => new []{new InputManager.FrameInput(0, 0, false, false, false, false, false, false, false)}
                    ));
                }
                GameManagement.NewInstance(InstanceMode.NULL, null, 
                    new InstanceRequest(_ => true, smeta, new CampaignRequest(c!)), r);
                if (demoPlayer == null) {
                    demoPlayer = Instantiate(demoPlayerSetup).GetComponent<PlayerController>();
                }
                demoPlayer.UpdateTeam((playerSelect.Value, shotSelect.Value), subshotSelect.Value, true);
                demoPlayer.transform.position = new Vector2(0, -3);
            }
            
            (ShipConfig player, FancyShotDisplay display)[] displays = c.campaign.players.Select(p =>
                (p, Instantiate(p.shotDisplay, shotDisplayContainer).GetComponent<FancyShotDisplay>())).ToArray();

            void HidePlayers() {
                foreach (var f in displays) f.display.Show(false);
            }
            void ShowShot(ShipConfig p, ShotConfig s, Subshot sub, ISupportAbilityConfig support, bool first) {
                if (!first) UpdateDemo();
                var index = displays.IndexOf(sd => sd.player == p);
                displays[index].display.SetShot(p, s, sub, support);
                displays.ForEachI((i, x) => {
                    //Only show the selected player on entry so the others don't randomly appear on screen during swipe
                    if (!first || i == index) x.display.Show(true);
                    x.display.SetRelative(i, index, first);
                });
            }

            HidePlayers();
            void _ShowShot(bool first = false) {
                ShowShot(playerSelect.Value, shotSelect.Value, subshotSelect.Value, supportSelect.Value, first);
            }
            
            playerSelect = new OptionNodeLR<ShipConfig>(LString.Empty, _ => _ShowShot(),
                c.campaign.players.Select(p => (p.ShortTitle, p)).ToArray(), c.campaign.players[0]);

            supportSelect = new DynamicOptionNodeLR<ISupportAbilityConfig>(LString.Empty, _ => _ShowShot(),
                () => playerSelect.Value.supports.Select(s => 
                    (s.ordinal, (ISupportAbilityConfig)s.ability)).ToArray(), 
                playerSelect.Value.supports[0].ability);
            shotSelect = new DynamicOptionNodeLR<ShotConfig>(LString.Empty, _ => _ShowShot(), () =>
                    playerSelect.Value.shots2.Select(s => (s.shot.isMultiShot ? 
                            shotsel_multi(s.ordinal) : 
                            shotsel_type(s.ordinal), s.shot)).ToArray(),
                playerSelect.Value.shots2[0].shot);
            subshotSelect = new OptionNodeLR<Subshot>(LString.Empty, _ => _ShowShot(),
                EnumHelpers2.Subshots.Select(x => (shotsel_variant_ls(x.Describe()), x)).ToArray(), Subshot.TYPE_D);
            return new UIScreen(this,
                    new PassthroughNode(shotsel_player).With(centerTextClass),
                    playerSelect.With(optionNoKeyClass),
                    new PassthroughNode(LString.Empty),
                    new PassthroughNode(shotsel_shot).With(centerTextClass),
                    shotSelect.With(optionNoKeyClass),
                    subshotSelect.With(optionNoKeyClass)
                        .VisibleIf(() => shotSelect.Value.isMultiShot),
                    new PassthroughNode(LString.Empty),
                    new PassthroughNode(shotsel_support).With(centerTextClass),
                    supportSelect.With(optionNoKeyClass),
                    new PassthroughNode(LString.Empty),
                    new FuncNode(() => ContinueAfterShot(new TeamConfig(0, subshotSelect.Value, supportSelect.Value,
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
        CustomDifficultyScreen = CreateCustomDifficultyEdit(this, CustomDifficultyScreenV, x => dfcCont(x));
        CampaignShotScreen = CreatePlayerScreen(GameManagement.MainCampaign, shotSetup != null)!;
        ExtraShotScreen = CreatePlayerScreen(GameManagement.ExtraCampaign, shotSetup != null);
        var shotTopMap = new Dictionary<CampaignConfig, UINode>() {{References.campaign, CampaignShotScreen.Top[1]}};
        if (ExtraShotScreen != null && References.exCampaign != null) 
            shotTopMap[References.exCampaign] = ExtraShotScreen.Top[1];

        (bool, UINode) GetShot(CampaignConfig c, Func<TeamConfig, bool> cont) {
            shotCont = cont;
            return (true, shotTopMap![c]);
        }

        Func<(bool, UINode?)> GetDifficultyThenShot(CampaignConfig c, Func<SharedInstanceMetadata, bool> cont) {
            return () => {
                dfcCont = d => GetShot(c, p => cont(new SharedInstanceMetadata(p, d)));
                return (true, DifficultyScreen.Top[0]);
            };
        }

        StagePracticeScreen = new LazyUIScreen(this, () => PStages.Select(stage =>
            (UINode) new NavigateUINode(practice_stage_ls(stage.stage.stageNumber),
                stage.Phases.Select(phase =>
                    new CacheNavigateUINode(TentativeCache, phase.Title).SetConfirmOverride(
                        GetDifficultyThenShot(stage.campaign.campaign, meta => {
                            ConfirmCache();
                            return new InstanceRequest(InstanceRequest.PracticeSuccess, meta,
                                new StagePracticeRequest(stage, phase.index)).Run();
                        })
                    )
                ).Prepend(
                    new CacheNavigateUINode(TentativeCache, practice_fullstage).SetConfirmOverride(
                        GetDifficultyThenShot(stage.campaign.campaign, meta => {
                            ConfirmCache();
                            return new InstanceRequest(InstanceRequest.PracticeSuccess, meta,
                                new StagePracticeRequest(stage, 1)).Run();
                        })
                    )
                ).ToArray()
            )
        ).ToArray()).With(StagePracticeScreenV);
        var cmpSpellHist = SaveData.r.GetCampaignSpellHistory();
        var prcSpellHist = SaveData.r.GetPracticeSpellHistory();
        BossPracticeScreen = new LazyUIScreen(this, () => PBosses.Select(boss =>
            (UINode) new NavigateUINode(boss.boss.BossPracticeName, boss.Phases.Select(phase => {
                    var req = new BossPracticeRequest(boss, phase);
                    var key = (req.Key as BossPracticeRequestKey)!;
                    return new CacheNavigateUINode(TentativeCache, phase.Title).SetConfirmOverride(
                        GetDifficultyThenShot(boss.campaign.campaign, meta => {
                            ConfirmCache();
                            return new InstanceRequest(InstanceRequest.PracticeSuccess, meta,
                                req).Run();
                        })
                    ).With(SpellPracticeNodeV).OnBound(ev => {
                        var (cs, ct) = cmpSpellHist.GetOrDefault(key);
                        var (ps, pt) = prcSpellHist.GetOrDefault(key);
                        ev.Q<Label>("CampaignHistory").text = $"{cs}/{ct}";
                        ev.Q<Label>("PracticeHistory").text = $"{ps}/{pt}";
                    });
                }).ToArray()
            )
        ).ToArray()).With(BossPracticeScreenV);
        PlaymodeScreen = playmodeSubmenu.Initialize(this, GetDifficultyThenShot);
        OptionsScreen = new UIScreen(this, XMLPauseMenu.GetOptions(true).ToArray())
            .With(OptionsScreenV).OnExit(SaveData.AssignSettingsChanges);
        ReplayScreen = XMLUtils.ReplayScreen(this, TentativeCache, ConfirmCache).With(ReplayScreenV);
        HighScoreScreen = XMLUtils.HighScoreScreen(this, ReplayScreen, FinishedCampaigns.ToArray())
            .With(HighScoreScreenV);
        StatsScreen = XMLUtils.StatisticsScreen(this, StatsScreenV, SaveData.r.FinishedCampaignGames, Campaigns);
        MusicRoomScreen = XMLUtils.MusicRoomScreen(this, MusicRoomScreenV, References.tracks);
        if (GameManagement.Achievements != null)
            AchievementsScreen = XMLUtils.AchievementsScreen(this, 
                AchievementsScreenV, AchievementsNodeV, GameManagement.Achievements);
        MainScreen = new UIScreen(this,
            new TransferNode(PlaymodeScreen, main_gamestart).With(large1Class),
            new OptionNodeLR<string?>(main_lang, l => {
                    SaveData.UpdateLocale(l);
                    SaveData.AssignSettingsChanges();
                }, new[] {
                    (new LString("English"), Locales.EN),
                    (new LString("日本語"), Locales.JP)
                }, SaveData.s.Locale)
                .With(large1Class),
            new TransferNode(HighScoreScreen, main_scores)
                .EnabledIf(FinishedCampaigns.Any())
                .With(large1Class),
            new TransferNode(StatsScreen, main_stats)
                .EnabledIf(FinishedCampaigns.Any())
                .With(large1Class),
            new TransferNode(MusicRoomScreen, main_musicroom)
                .EnabledIf(MusicRoomScreen.Top.Length > 0)
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
        MainScreen.ExitNode = MainScreen.Top[MainScreen.Top.Length - 2];
        
        base.FirstFrame();
        if (ReturnTo == null) {
            _ = uiRenderer.Slide(new Vector2(3, 0), Vector2.zero, 1f, DMath.M.EOutSine);
            _ = uiRenderer.Fade(0, 1, 1f, null);
        } else
            uiRenderer.Fade(1, 1, 0, null);
    }
}
}