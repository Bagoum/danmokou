using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.ADV;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.UI.XML;
using Suzunoya.ADV;
using UnityEngine;
using UnityEngine.UIElements;
using static Danmokou.Services.GameManagement;
using static Danmokou.UI.XML.XMLUtils;
using static Danmokou.Core.LocalizedStrings.UI;

namespace MiniProjects.PJ24 {
public class PJ24MainMenuUXML : XMLMainMenu {

    private UIScreen LoadGameScreen = null!;
    private UIScreen OptionsScreen = null!;
    private UIScreen LicenseScreen = null!;

    public ADVGameDef GameDef = null!;

    public VisualTreeAsset mainMenuNodeVTA = null!;
    
    protected override UIScreen?[] Screens => new[] {
        MainScreen,
        LoadGameScreen,
        OptionsScreen, 
        LicenseScreen
    };
    

    public override void FirstFrame() {
        var advMan = ServiceLocator.Find<ADVManager>();

        LoadGameScreen = this.SaveLoadVNScreen(s => 
            new ADVInstanceRequest(advMan, GameDef, s.GetData()).Run(), null, false).WithBG(SecondaryBGConfig);
        OptionsScreen = this.OptionsScreen(true).WithBG(SecondaryBGConfig);
        LicenseScreen = this.LicenseScreen(References.licenses).WithBG(SecondaryBGConfig);
        
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined){ Builder = (s, ve) => {
            ve.AddColumn().WithAbsolutePosition(60, 400).SetWidthHeight(new(800, 1200));
        }, SceneObjects = MainScreenOnlyObjects}.WithBG(PrimaryBGConfig);
        _ = new UIColumn(MainScreen, null,
            new FuncNode(main_newgame, () => 
                new ADVInstanceRequest(advMan, GameDef, GameDef.NewGameData()).Run())
                { Prefab = mainMenuNodeVTA },
            new FuncNode(main_continue, () => 
                new ADVInstanceRequest(advMan, GameDef, SaveData.v.MostRecentSave.GetData()).Run()) {
                EnabledIf = () => SaveData.v.Saves.Count > 0,
                Prefab = mainMenuNodeVTA
            },
            new TransferNode(main_load, LoadGameScreen)
                { Prefab = mainMenuNodeVTA },
            new TransferNode(main_options, OptionsScreen)
                { Prefab = mainMenuNodeVTA },
            new TransferNode(main_licenses, LicenseScreen)
                { Prefab = mainMenuNodeVTA }
#if !WEBGL
            , new FuncNode(main_quit, Application.Quit)
                { Prefab = mainMenuNodeVTA }
#endif
        ) {
            ExitIndexOverride = -2
        };

        base.FirstFrame();
        UIRoot.style.opacity = 0;
        _ = UIRoot.FadeTo(1, 0.8f, x => x).Run(this);
    }
}
}