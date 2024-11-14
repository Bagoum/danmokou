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
using Danmokou.UI;
using Danmokou.UI.XML;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;
using static Danmokou.Services.GameManagement;
using static Danmokou.UI.XML.XMLUtils;
using static Danmokou.Core.LocalizedStrings.UI;
using static Danmokou.UI.PlayModeCommentator;

namespace MiniProjects {


/// <summary>
/// Class to manage the main menu UI for campaign-type games.
/// </summary>
[Preserve]
public class XMLMainMenuCampaignPlasticHakkero : XMLMainMenu {
    private UIScreen DifficultyScreen = null!;
    private UIScreen OptionsScreen = null!;
    private UIScreen LicenseScreen = null!;

    protected override UIScreen?[] Screens => new[] {
        MainScreen, DifficultyScreen, OptionsScreen, LicenseScreen
    };

    public Sprite easyDifficulty = null!;
    public Sprite normalDifficulty = null!;
    public Sprite hardDifficulty = null!;
    public Sprite lunaticDifficulty = null!;
    
    private Func<DifficultySettings, UIResult> dfcContinuation = null!;
    Sprite SpriteForDFC(FixedDifficulty fd) => fd switch {
        FixedDifficulty.Easy => easyDifficulty,
        FixedDifficulty.Normal => normalDifficulty,
        FixedDifficulty.Hard => hardDifficulty,
        FixedDifficulty.Lunatic => lunaticDifficulty,
        _ => throw new ArgumentOutOfRangeException(nameof(fd), fd, null)
    };
    public override void FirstFrame() {
        var game = References.CampaignGameDef;
        
        var axisVM = new AxisViewModel {
            BaseLoc = new(-2.9f, 0),
            Axis = new Vector2(1.4f, -2.6f).normalized * 0.7f,
        };
        DifficultyScreen = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Builder = (s, ve) => ve.CenterElements(),
        };
        DifficultyScreen.SetFirst(new UIColumn(new UIRenderConstructed(DifficultyScreen, new(x => x.AddVE(null))),
            VisibleDifficulties.Select(fd => new UINode(new AxisView(axisVM), new DFCView(new(this, fd)))).ToArray()) {
            EntryIndexOverride = () => 2
        });

        Func<UINode, ICursorState, UIResult> GetMetadata(CampaignConfig c, Func<SharedInstanceMetadata, bool> cont) => (_, _) => {
            dfcContinuation = dfc => {
                if (c.HasOneShotConfig(out var team)) {
                    cont(new SharedInstanceMetadata(team, dfc));
                    return new UIResult.StayOnNode();
                } else
                    throw new Exception("Fixed shot required");
            };
            return new UIResult.GoToScreen(DifficultyScreen);
        };

        OptionsScreen = this.OptionsScreen(true);
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined)
            { Builder = (s, ve) => {
                s.Margin.SetLRMargin(300, 560);
                var c = ve.AddColumn();
                c.style.maxWidth = 20f.Percent();
                c.style.paddingTop = 800;
            }, SceneObjects = MainScreenOnlyObjects}.WithBG(PrimaryBGConfig);
        LicenseScreen = this.LicenseScreen(References.licenses);
        
        foreach (var s in Screens)
            if (s != MainScreen)
                s?.WithBG(SecondaryBGConfig);

        MainScreen.SetFirst(new UIColumn(MainScreen, null, new UINode[] {
            new FuncNode("Level 1", GetMetadata(game.Campaign, meta => 
                new InstanceRequest(InstanceRequest.PracticeSuccess, meta, 
                    new StagePracticeRequest(Campaigns[0].stages[0], 1)).Run())),
            new FuncNode("Level 2", GetMetadata(game.Campaign, meta => 
                new InstanceRequest(InstanceRequest.PracticeSuccess, meta, 
                    new StagePracticeRequest(Campaigns[0].stages[1], 1)).Run())) {
                EnabledIf = () => SaveData.r.FinishedGames.Any(x => 
                    x is { Completed: true, RequestKey : StagePracticeRequestKey { StageIndex:0 } })
            },
            new TransferNode(main_options, OptionsScreen),
            new TransferNode(main_licenses, LicenseScreen)
        #if !WEBGL
            , new FuncNode(main_quit, Application.Quit)
        #endif
        }.Select(x => x.WithCSS(large1Class, centerText))) {
            ExitIndexOverride = -2
        });

        bool doAnim = ReturnTo == null;
        base.FirstFrame();
        if (doAnim) {
            //_ = TransitionHelpers.TweenTo(720f, 0f, 1f, x => UIRoot.style.left = x, Easers.EOutSine).Run(this);
            _ = TransitionHelpers.TweenTo(0f, 1f, 0.8f, x => UIRoot.style.opacity = x, x => x).Run(this);
        }
    }
    
    private class DFCViewModel : IConstUIViewModel {
        public XMLMainMenuCampaignPlasticHakkero src { get; }
        public FixedDifficulty fd { get; }
        public DFCViewModel(XMLMainMenuCampaignPlasticHakkero src, FixedDifficulty fd) {
            this.src = src;
            this.fd = fd;
        }

        UIResult? IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
            return src.dfcContinuation(new(fd));
        }
    }
    private class DFCView : UIView<DFCViewModel>, IUIView {
        public override VisualTreeAsset? Prefab => References.uxmlDefaults.FloatingNode;
        public DFCView(DFCViewModel viewModel) : base(viewModel) { }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            HTML.ConfigureFloatingImage(VM.src.SpriteForDFC(VM.fd)!);
        }
    }
}
}