using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Transitions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Graphics.Backgrounds;
using Danmokou.Player;
using Danmokou.Scriptables;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;
using static Danmokou.Services.GameManagement;
using static Danmokou.UI.XML.XMLUtils;
using static Danmokou.Core.LocalizedStrings.UI;
using static Danmokou.UI.PlayModeCommentator;

namespace Danmokou.UI.XML {


public abstract class XMLMainMenu : UIController {
    public GameObject MainBackground = null!;
    public GameObject? SecondaryBackground;
    public SOBgTransition BGTransition = null!;
    public GameObject? MainScreenOnlyObjects;
    public GameObject? ShotScreenObjects;

    protected (GameObject, BackgroundTransition) PrimaryBGConfig => 
        (MainBackground, BGTransition.value);
    protected (GameObject, BackgroundTransition)? SecondaryBGConfig =>
        SecondaryBackground == null ? null : (SecondaryBackground, BGTransition.value);

}

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
    private bool onlyOnePlaymode = false;
    private UIScreen DifficultyScreen = null!;
    public UIScreen CustomDifficultyScreen { get; private set; } = null!;
    private UIScreen CampaignShotScreen = null!;
    private UIScreen? ExtraShotScreen;
    public UIScreen StagePracticeScreen { get; private set; }= null!;
    public UIScreen BossPracticeScreen { get; private set; } = null!;
    private UIScreen OptionsScreen = null!;
    private UIScreen ReplayScreen = null!;
    private UIScreen RecordsScreen = null!;
    private UIScreen StatsScreen = null!;
    private UIScreen MusicRoomScreen = null!;
    private UIScreen GameDetailsScreen = null!;
    private UIScreen? AchievementsScreen;
    private UIScreen PlayerDataScreen = null!;
    private UIScreen LicenseScreen = null!;

    protected override UIScreen?[] Screens => new[] {
        MainScreen,
        PlaymodeScreen, DifficultyScreen, CustomDifficultyScreen, CampaignShotScreen, ExtraShotScreen,
        StagePracticeScreen, BossPracticeScreen, OptionsScreen, ReplayScreen, RecordsScreen,
        StatsScreen, AchievementsScreen, MusicRoomScreen, GameDetailsScreen, PlayerDataScreen,
        LicenseScreen
    };
    
    public VisualTreeAsset SpellPracticeNodeV = null!;
    public VisualTreeAsset AchievementsNodeV = null!;

    public Transform shotDisplayContainer = null!;
    public BehaviorEntity? shotSetup;
    public GameObject? demoPlayerSetup;

    public DifficultyCommentator? difficultyCommentator;
    public Sprite easyDifficulty = null!;
    public Sprite normalDifficulty = null!;
    public Sprite hardDifficulty = null!;
    public Sprite lunaticDifficulty = null!;
    public Sprite? customDifficulty;

    public PlayModeCommentator? modeCommentator;
    public Sprite mainMode = null!;
    public Sprite exMode = null!;
    public Sprite bossMode = null!;
    public Sprite stageMode = null!;
    public Sprite tutorialMode = null!;

    public override void FirstFrame() {
        var game = References.CampaignGameDef;
        Func<DifficultySettings, UIResult> dfcContinuation = null!;
        Func<TeamConfig, bool> shotContinuation = null!;
        var campaignToShotScreenMap = new Dictionary<CampaignConfig, UIScreen>();

        var floater = References.uxmlDefaults.FloatingNode;
        Sprite? SpriteForDFC(FixedDifficulty? fd) => fd switch {
            FixedDifficulty.Easy => easyDifficulty,
            FixedDifficulty.Normal => normalDifficulty,
            FixedDifficulty.Hard => hardDifficulty,
            FixedDifficulty.Lunatic => lunaticDifficulty,
            _ => customDifficulty
        };
        (UINode?, FixedDifficulty?) MakeDifficultyNode(FixedDifficulty? fd) =>
            SpriteForDFC(fd) == null ? (null, fd) : 
                (new UINode {
                    OnConfirm = (_, _) => fd == null ? 
                        new UIResult.GoToNode(CustomDifficultyScreen) : dfcContinuation(new(fd)),
                    Prefab = floater,
                    OnBuilt = n => XMLUtils.ConfigureFloatingImage(n.NodeHTML, SpriteForDFC(fd)!)
                }, fd);
            
        DifficultyScreen = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Builder = (s, ve) => ve.CenterElements()
        };
        DifficultyScreen.SetFirst(new CommentatorAxisColumn<FixedDifficulty?>(DifficultyScreen, 
            DifficultyScreen, CustomAndVisibleDifficulties.Select(MakeDifficultyNode).ToArray()) {
            Commentator = difficultyCommentator,
            BaseLoc = new(-2.9f * 240f, 0),
            Axis = new Vector2(1.4f, 2.6f).normalized * (0.7f * 240f),
            EntryIndexOverride = () => 2
        });
        
        CustomDifficultyScreen = this.CustomDifficultyScreen(x => dfcContinuation(x));
        CampaignShotScreen = campaignToShotScreenMap[MainCampaign.campaign] = this.CreatePlayerScreen(MainCampaign, shotSetup, demoPlayerSetup, shotDisplayContainer, x => shotContinuation(x));
        CampaignShotScreen.SceneObjects = ShotScreenObjects;
        if (ExtraCampaign != null) {
            ExtraShotScreen = campaignToShotScreenMap[ExtraCampaign.campaign] =
                this.CreatePlayerScreen(ExtraCampaign, shotSetup, demoPlayerSetup, shotDisplayContainer, x => shotContinuation(x));
            ExtraShotScreen.SceneObjects = ShotScreenObjects;
        }

        Func<UINode, ICursorState, UIResult> GetMetadata(CampaignConfig c, Func<SharedInstanceMetadata, bool> cont) => (_, _) => {
            dfcContinuation = dfc => {
                if (c.HasOneShotConfig(out var team)) {
                    cont(new SharedInstanceMetadata(team, dfc));
                    return new UIResult.StayOnNode();
                } else {
                    shotContinuation = shot => cont(new SharedInstanceMetadata(shot, dfc));
                    return new UIResult.GoToNode(campaignToShotScreenMap[c]);
                }
            };
            return new UIResult.GoToNode(DifficultyScreen);
        };

        StagePracticeScreen = this.StagePracticeScreen(GetMetadata);
        BossPracticeScreen = this.BossPracticeScreen(SpellPracticeNodeV, GetMetadata);
        PlaymodeScreen = this.PlaymodeScreen(game, BossPracticeScreen, StagePracticeScreen, new() {
            { Mode.MAIN, mainMode },
            { Mode.EX, exMode },
            {Mode.BOSSPRAC, bossMode},
            {Mode.STAGEPRAC, stageMode},
            {Mode.TUTORIAL, tutorialMode}
        }, modeCommentator, GetMetadata, out onlyOnePlaymode);

        OptionsScreen = this.OptionsScreen(true);
        GameDetailsScreen = new UIScreen(this, "GAME DETAILS") { Builder = XMLHelpers.GameResultsScreenBuilder };
        MusicRoomScreen = this.MusicRoomScreen(References.tracks);
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined)
            { Builder = (s, ve) => {
                s.Margin.SetLRMargin(400, 560);
                var c = ve.AddColumn();
                c.style.maxWidth = 20f.Percent();
                c.style.paddingTop = 640;
            }, SceneObjects = MainScreenOnlyObjects}.WithBG(PrimaryBGConfig);
        
        PlayerDataScreen = this.AllPlayerDataScreens(game, GameDetailsScreen, out ReplayScreen, out StatsScreen,
            out AchievementsScreen, out RecordsScreen, AchievementsNodeV);
        Profiler.BeginSample("Licenses");
        LicenseScreen = this.LicenseScreen(References.licenses);
        Profiler.EndSample();
        
        foreach (var s in Screens)
            if (s != MainScreen)
                s?.WithBG(SecondaryBGConfig);

        MainScreen.SetFirst(new UIColumn(MainScreen, null, new UINode[] {
            onlyOnePlaymode ?
                new FuncNode(main_gamestart, GetMetadata(game.Campaign, meta => 
                    InstanceRequest.RunCampaign(MainCampaign, null, meta))) :
                new TransferNode(main_gamestart, PlaymodeScreen) ,
            new TransferNode(main_playerdata, PlayerDataScreen),
            //new TransferNode(main_musicroom, MusicRoomScreen)
            //        {EnabledIf = () => MusicRoomScreen.Groups[0].Nodes.Count > 0}
            new TransferNode(main_options, OptionsScreen),
            new TransferNode(main_licenses, LicenseScreen),
        #if !WEBGL
            new FuncNode(main_quit, Application.Quit),
        #endif
            new OpenUrlNode(main_twitter, "https://twitter.com/rdbatz")
        }.Select(x => x.With(large1Class, centerTextClass))) {
            ExitIndexOverride = -2
        });

        bool doAnim = ReturnTo == null;
        base.FirstFrame();
        if (doAnim) {
            //_ = TransitionHelpers.TweenTo(720f, 0f, 1f, x => UIRoot.style.left = x, Easers.EOutSine).Run(this);
            _ = TransitionHelpers.TweenTo(0f, 1f, 0.8f, x => UIRoot.style.opacity = x, x => x).Run(this);
        }
    }
}
}