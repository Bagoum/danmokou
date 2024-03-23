using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Transitions;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Player;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using static Danmokou.Services.GameManagement;
using static Danmokou.UI.XML.XMLUtils;
using static Danmokou.Core.LocalizedStrings.UI;

namespace Danmokou.UI.XML {
/// <summary>
/// Class to manage the main menu UI for scene challenge-type games.
/// </summary>
[Preserve]
public class XMLMainMenuDays : XMLMainMenu {
    private static List<CacheInstruction>? _returnTo;
    protected override List<CacheInstruction>? ReturnTo {
        get => _returnTo;
        set => _returnTo = value;
    }

    private UIScreen SceneSelectScreen = null!;
    private UIScreen OptionsScreen = null!;
    private UIScreen GameDetailsScreen = null!;
    private UIScreen ReplayScreen = null!;

    protected override UIScreen?[] Screens => new[] {
        MainScreen,
        SceneSelectScreen, OptionsScreen, ReplayScreen, GameDetailsScreen
    };

    public VisualTreeAsset VTALR2Option = null!;
    public float photoSize;


    private const string completed1Class = "lblue";
    private const string completedAllClass = "lgreen";

    private static UINode[] DifficultyNodes(Func<FixedDifficulty, UINode> map) =>
        GameManagement.VisibleDifficulties.Select(map).ToArray();

    private static UINode[] DifficultyFuncNodes(Func<FixedDifficulty, Action> map) =>
        DifficultyNodes(d => new FuncNode(d.Describe(), map(d)));

    public override void FirstFrame() {
        var game = References.SceneGameDef;
        FixedDifficulty dfc = FixedDifficulty.Normal;
        var defaultPlayer = game.DayCampaign.players[0];
        var defaultShot = defaultPlayer.shots2[0];
        var defaultSupport = defaultPlayer.supports[0];

        TeamConfig Team() => new(0, Subshot.TYPE_D, defaultSupport.ability, (defaultPlayer, defaultShot.shot));
        SharedInstanceMetadata Meta() => new(Team(), new(dfc));

        var photoBoard = ServiceLocator.FindOrNull<IAyaPhotoBoard>();
        IDisposable? photoBoardToken = null;

        SceneSelectScreen = new UIScreen(this, "SCENE SELECT") {Builder = (s, ve) => {
            s.Margin.SetLRMargin(600, 600);
            ve.AddScrollColumn().style.flexGrow = 2.3f;
            ve.AddScrollColumn().style.flexGrow = 5;
        }};
        var sceneChallengeCol = new UIRenderColumn(SceneSelectScreen, 1);
        _ = new UIColumn(SceneSelectScreen, null) {
            LazyNodes = () => DayCampaign.days[0].bosses.SelectMany(b =>
                b.phases.Select(p => {
                    //TODO: this return is not safe if you change the difficulty.
                    if (!p.Enabled(Meta()))
                        return new UINode(() => p.Title(Meta())) {EnabledIf = () => false};
                    Challenge c = p.challenges[0];
                    void SetChallenge(int idx) {
                        c = p.challenges[idx];
                        var completion = SaveData.r.ChallengeCompletion(p, idx, Meta());
                        photoBoardToken = photoBoard?.ConstructPhotos(completion?.Photos, photoSize);
                    }
                    UIResult Confirm(UINode _) {
                        ConfirmCache();
                        new InstanceRequest(InstanceRequest.PracticeSuccess, Meta(), new PhaseChallengeRequest(p, c)).Run();
                        return new UIResult.StayOnNode();
                    }
                    return new UINode(() => p.Title(Meta())) {
                        CacheOnEnter = true, ShowHideGroup = new UIColumn(sceneChallengeCol, 
                            new UINode(() => c.Description(p.boss.boss)) 
                                    { OnConfirm = Confirm }
                                .With(large1Class, centerTextClass),
                            new ComplexLROptionUINode<int>(LString.Empty, VTALR2Option, SetChallenge,
                                p.challenges.Length.Range().ToArray, (i, v, on) => {
                                    v.Query(null!, "bracket")
                                        .ForEach(x => x.style.display = on ? DisplayStyle.Flex : DisplayStyle.None);
                                    v.Q("Star").style.unityBackgroundImageTintColor = new StyleColor(p.Completed(i, Meta()) ?
                                        p.boss.boss.colors.uiHPColor :
                                        new Color(1, 1, 1, 0.52f));
                                }) {
                                OnConfirm = Confirm,
                                OnEnter = n => SetChallenge((n as IBaseOptionNodeLR)!.Index),
                                OnLeave = _ => photoBoardToken?.Dispose()
                            }.With(optionNoKeyClass),
                            new UINode(() => new LText("Press Z to start level", (Locales.JP, "Zキー押すとレベルスタート"))) 
                                    { OnConfirm = Confirm }
                                .With(large1Class, centerTextClass)
                        )
                    }.With(large1Class, 
                        p.CompletedAll(Meta()) ? completedAllClass : 
                        p.CompletedOne(Meta()) ? completed1Class :
                        null
                    );
                }))
        };

        OptionsScreen = this.OptionsScreen(true);
        GameDetailsScreen = new UIScreen(this, "GAME DETAILS") { Builder = XMLHelpers.GameResultsScreenBuilder };
        ReplayScreen = this.ReplayScreen(GameDetailsScreen);

        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined)
        { Builder = (s, ve) => {
            s.Margin.SetLRMargin(720, null);
            var c = ve.AddColumn();
            c.style.maxWidth = 40f.Percent();
            c.style.paddingTop = 500;
        }, SceneObjects = MainScreenOnlyObjects};
        _ = new UIColumn(MainScreen, null,
            new TransferNode(main_gamestart, SceneSelectScreen)
                .With(large1Class),
            new OptionNodeLR<string?>(main_lang, l => {
                    SaveData.s.TextLocale.OnNext(l);
                    SaveData.AssignSettingsChanges();
                }, new[] {
                    ((LString)("English"), Locales.EN),
                    ((LString)("日本語"), Locales.JP)
                }, SaveData.s.TextLocale)
                .With(large1Class),
            new TransferNode(main_replays, ReplayScreen) {
                EnabledIf = () => (SaveData.p.ReplayData.Count > 0)
            }.With(large1Class),
            new TransferNode(main_options, OptionsScreen)
                .With(large1Class),
#if !WEBGL
            new FuncNode(main_quit, Application.Quit)
                .With(large1Class),
#endif
            new OpenUrlNode(main_twitter, "https://twitter.com/rdbatz")
                .With(large1Class));

        bool doAnim = ReturnTo == null;
        base.FirstFrame();
        if (doAnim) {
            //_ = TransitionHelpers.TweenTo(720f, 0f, 1f, x => UIRoot.style.left = x, Easers.EOutSine).Run(this);
            _ = TransitionHelpers.TweenTo(0f, 1f, 0.8f, x => UIRoot.style.opacity = x, x => x).Run(this);
        }
    }
}
}