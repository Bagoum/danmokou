using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Danmaku;
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
    
    protected override IEnumerable<UIScreen> Screens => new[] {
        MainScreen,
        LoadGameScreen,
        OptionsScreen, 
        LicenseScreen
    }.NotNull();
    

    public override void FirstFrame() {
        FixedDifficulty dfc = FixedDifficulty.Normal;
        var defaultPlayer = References.campaign.players[0];
        var defaultShot = defaultPlayer.shots2[0];
        var defaultSupport = defaultPlayer.supports[0];

        TeamConfig Team() => new(0, Subshot.TYPE_D, defaultSupport.ability, (defaultPlayer, defaultShot.shot));
        SharedInstanceMetadata Meta() => new(Team(), new DifficultySettings(dfc));

        LoadGameScreen = this.SaveLoadVNScreen(s => 
            InstanceRequest.RunCampaign(MainCampaign, null, Meta(), s.GetData()), null, false).WithBG(SecondaryBGConfig);
        OptionsScreen = this.OptionsScreen(true).WithBG(SecondaryBGConfig);
        LicenseScreen = this.LicenseScreen(References.licenses).WithBG(SecondaryBGConfig);
        
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined){ Builder = (s, ve) => {
            s.Margin.SetLRMargin(480, null);
            var c = ve.AddColumn();
            c.style.maxWidth = 20f.Percent();
            c.style.paddingTop = 640;
        }, SceneObjects = MainScreenOnlyObjects}.WithBG(PrimaryBGConfig);
        _ = new UIColumn(MainScreen, null,
            new UINode[] {
                new FuncNode(main_newgame, () => InstanceRequest.RunCampaign(MainCampaign, null, Meta())),
                new FuncNode(main_continue, () => InstanceRequest.RunCampaign(MainCampaign, null, Meta(), 
                        SaveData.v.MostRecentSave.GetData())) {
                    EnabledIf = () => SaveData.v.Saves.Count > 0
                },
                new TransferNode(main_load, LoadGameScreen),
                new TransferNode(main_options, OptionsScreen),
                new TransferNode("Licenses", LicenseScreen),
                new FuncNode(main_quit, Application.Quit),
                new OpenUrlNode(main_twitter, "https://twitter.com/rdbatz")
            }.Select(x => x.With(large1Class, centerTextClass))
        );

        base.FirstFrame();
        //_ = uiRenderer.Slide(new Vector2(3, 0), Vector2.zero, 1f, DMath.M.EOutSine);
        _ = uiRenderer.Fade(0, 1, 1f, null);
    }
}
}