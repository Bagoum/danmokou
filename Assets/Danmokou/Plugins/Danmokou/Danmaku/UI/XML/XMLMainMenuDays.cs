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
using Danmokou.SM;
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

        TeamConfig Team() => new(0, Subshot.TYPE_D, (defaultPlayer, defaultShot.shot, defaultSupport.ability));
        SharedInstanceMetadata Meta() => new(Team(), new(dfc));

        var photoBoard = ServiceLocator.FindOrNull<IAyaPhotoBoard>();

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
                        return new UINode(p.Title(Meta())) {EnabledIf = () => false};
                    var vm = new DayPhaseViewModel(this, p, Meta);
                    var binder = new PropTwoWayBinder<Challenge>(vm, nameof(vm.c));
                    return new UINode(p.Title(Meta())) {
                        CacheOnEnter = true, ShowHideGroup = new UIColumn(sceneChallengeCol, 
                            new UINode { OnConfirm = vm.OnConfirm }
                                .WithCSS(large1Class, centerTextClass)
                                .Bind(new LabelView<Challenge>(new(() => vm.c, c => c.Description(p.boss.boss)))),
                            new ComplexLROptionNode<Challenge>(LString.Empty, binder, p.challenges, (_, c, on) => {
                                    var ve = VTALR2Option.CloneTreeNoContainer();
                                    ve.Query(null!, "bracket")
                                        .ForEach(x => x.style.display = on ? DisplayStyle.Flex : DisplayStyle.None);
                                    ve.Q("Star").style.unityBackgroundImageTintColor = p.Completed(c, Meta()) ?
                                        p.boss.boss.colors.uiHPColor :
                                        new Color(1, 1, 1, 0.52f);
                                    return ve;
                                }).Bind(new DayPhaseView(vm, photoBoard, photoSize)).WithCSS(optionNoKeyClass),
                            new UINode(main_gamestart) 
                                    { OnConfirm = vm.OnConfirm }
                                .WithCSS(large1Class, centerTextClass)
                        )
                    }.WithCSS(large1Class, 
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
                .WithCSS(large1Class),
            new LROptionNode<string?>(main_lang, SaveData.s.TextLocale, new[] {
                    ((LString)("English"), Locales.EN),
                    ((LString)("日本語"), Locales.JP)
                })
                .WithCSS(large1Class),
            new TransferNode(main_replays, ReplayScreen) {
                EnabledIf = () => (SaveData.p.ReplayData.Count > 0)
            }.WithCSS(large1Class),
            new TransferNode(main_options, OptionsScreen)
                .WithCSS(large1Class),
#if !WEBGL
            new FuncNode(main_quit, Application.Quit)
                .WithCSS(large1Class),
#endif
            new OpenUrlNode(main_twitter, "https://twitter.com/rdbatz")
                .WithCSS(large1Class));

        bool doAnim = ReturnTo == null;
        base.FirstFrame();
        if (doAnim) {
            //_ = TransitionHelpers.TweenTo(720f, 0f, 1f, x => UIRoot.style.left = x, Easers.EOutSine).Run(this);
            _ = TransitionHelpers.TweenTo(0f, 1f, 0.8f, x => UIRoot.style.opacity = x, x => x).Run(this);
        }
    }

    private class DayPhaseViewModel : VersionedUIViewModel, IUIViewModel {
        private XMLMainMenuDays src { get; }
        public SMAnalysis.DayPhase p { get; }
        public Challenge c { get; set; } //written via PropBinder
        public Func<SharedInstanceMetadata> Meta { get; }
        
        public DayPhaseViewModel(XMLMainMenuDays src, SMAnalysis.DayPhase p, Func<SharedInstanceMetadata> meta) {
            this.src = src;
            this.p = p;
            c = p.challenges[0];
            Meta = meta;
        }

        public UIResult? OnConfirm(UINode node, ICursorState cs) {
            src.ConfirmCache();
            new InstanceRequest(InstanceRequest.PracticeSuccess, Meta(), new PhaseChallengeRequest(p, c)).Run();
            return new UIResult.StayOnNode();
        }
    }

    private class DayPhaseView : UIView<DayPhaseViewModel>, IUIView {
        private IDisposable? photoBoardToken;
        private readonly IAyaPhotoBoard? photoBoard;
        private readonly float photoSize;

        public DayPhaseView(DayPhaseViewModel data, IAyaPhotoBoard? photoBoard, float photoSize) : base(data) {
            this.photoBoard = photoBoard;
            this.photoSize = photoSize;
        }

        private void ShowPhotos() {
            var completion = SaveData.r.ChallengeCompletion(VM.p, VM.c, VM.Meta());
            photoBoardToken?.Dispose();
            photoBoardToken = photoBoard?.ConstructPhotos(completion?.Photos, photoSize);
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) => ShowPhotos();

        protected override BindingResult Update(in BindingContext context) {
            ShowPhotos();
            return base.Update(in context);
        }

        void IUIView.OnLeave(UINode node, ICursorState cs, bool animate, bool isEnteringPopup) {
            photoBoardToken?.Dispose();
        }
    }
    
}
}