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
using UnityEngine;
using UnityEngine.UIElements;
using static Danmokou.Services.GameManagement;
using static Danmokou.UI.XML.XMLUtils;
using static Danmokou.Core.LocalizedStrings.UI;

namespace Danmokou.UI.XML {
public class XMLMainMenuVN01 : XMLMainMenu {

    private UIScreen LoadGameScreen = null!;
    private UIScreen OptionsScreen = null!;
    private UIScreen LicenseScreen = null!;

    public ADVGameDef GameDef = null!;
    
    protected override UIScreen?[] Screens => new[] {
        MainScreen,
        LoadGameScreen,
        OptionsScreen, 
        LicenseScreen
    };
    

    public override void FirstFrame() {
        var advMan = ServiceLocator.Find<ADVManager>();

        LoadGameScreen = this.SaveLoadVNScreen(s => 
            advMan.RunCampaign(GameDef, s.GetData()), null, false).WithBG(SecondaryBGConfig);
        OptionsScreen = this.OptionsScreen(true).WithBG(SecondaryBGConfig);
        LicenseScreen = this.LicenseScreen(References.licenses).WithBG(SecondaryBGConfig);
        
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined){ Builder = (s, ve) => {
            s.Margin.SetLRMargin(480, 480);
            var c = ve.AddColumn();
            c.style.maxWidth = 20f.Percent();
            c.style.paddingTop = 640;
        }, SceneObjects = MainScreenOnlyObjects}.WithBG(PrimaryBGConfig);
        _ = new UIColumn(MainScreen, null, new[] {
            new FuncNode(main_newgame, () => 
                advMan.RunCampaign(GameDef, GameDef.NewGameData())),
            new FuncNode(main_continue, () => advMan.RunCampaign(GameDef, 
                SaveData.v.MostRecentSave.GetData())) {
                EnabledIf = () => SaveData.v.Saves.Count > 0
            },
            new TransferNode(main_load, LoadGameScreen),
            new TransferNode(main_options, OptionsScreen),
            new TransferNode(main_licenses, LicenseScreen),
            new FuncNode(main_quit, Application.Quit),
            new OpenUrlNode(main_twitter, "https://twitter.com/rdbatz")
        }.Select(x => x.With(large1Class, centerTextClass))) {
            ExitIndexOverride = -2
        };

        base.FirstFrame();
        UIRoot.style.opacity = 0;
        _ = UIRoot.FadeTo(1, 0.8f, x => x).Run(this);
    }
}
}