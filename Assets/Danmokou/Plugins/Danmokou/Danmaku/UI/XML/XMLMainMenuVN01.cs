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
    
    private UIScreen OptionsScreen = null!;
    
    protected override IEnumerable<UIScreen> Screens => new[] {
        MainScreen,
        OptionsScreen, 
    }.NotNull();
    

    public override void FirstFrame() {
        FixedDifficulty dfc = FixedDifficulty.Normal;
        var defaultPlayer = References.campaign.players[0];
        var defaultShot = defaultPlayer.shots2[0];
        var defaultSupport = defaultPlayer.supports[0];

        TeamConfig Team() => new(0, Subshot.TYPE_D, defaultSupport.ability, (defaultPlayer, defaultShot.shot));
        SharedInstanceMetadata Meta() => new(Team(), new DifficultySettings(dfc));
        
        OptionsScreen = this.OptionsScreen();
        
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined){ Builder = (s, ve) => {
            s.Margin.SetLRMargin(720, null);
            var c = ve.AddColumn();
            c.style.maxWidth = 40f.Percent();
            c.style.paddingTop = 500;
        }, SceneObjects = MainScreenOnlyObjects};
        _ = new UIColumn(MainScreen, null,
            new FuncNode(main_gamestart, () => InstanceRequest.RunCampaign(MainCampaign, null, Meta()))
                .With(large1Class),
            new OptionNodeLR<string?>(main_lang, l => {
                    SaveData.UpdateLocale(l);
                    SaveData.AssignSettingsChanges();
                }, new[] {
                    (new LString("English"), Locales.EN),
                    (new LString("日本語"), Locales.JP)
                }, SaveData.s.Locale)
                .With(large1Class),
            new TransferNode(main_options, OptionsScreen)
                .With(large1Class),
            new FuncNode(main_quit, Application.Quit)
                .With(large1Class),
            new OpenUrlNode(main_twitter, "https://twitter.com/rdbatz")
                .With(large1Class)
        );

        base.FirstFrame();
        _ = uiRenderer.Slide(new Vector2(3, 0), Vector2.zero, 1f, DMath.M.EOutSine);
        _ = uiRenderer.Fade(0, 1, 1f, null);
    }
}
}