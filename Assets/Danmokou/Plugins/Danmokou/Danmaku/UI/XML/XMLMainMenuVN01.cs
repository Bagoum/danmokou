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
        OptionsScreen, 
        MainScreen
    }.NotNull();
    
    public VisualTreeAsset MainScreenV = null!;
    public VisualTreeAsset OptionsScreenV = null!;

    public override void FirstFrame() {
        FixedDifficulty dfc = FixedDifficulty.Normal;
        var defaultPlayer = References.campaign.players[0];
        var defaultShot = defaultPlayer.shots2[0];
        var defaultSupport = defaultPlayer.supports[0];

        TeamConfig Team() => new TeamConfig(0, Subshot.TYPE_D, defaultSupport.ability, (defaultPlayer, defaultShot.shot));
        SharedInstanceMetadata Meta() => new SharedInstanceMetadata(Team(), new DifficultySettings(dfc));
        
        OptionsScreen = new UIScreen(this, XMLPauseMenu.GetOptions(true).ToArray())
            .With(OptionsScreenV).OnExit(SaveData.AssignSettingsChanges);
        MainScreen = new UIScreen(this,
            new FuncNode(() => InstanceRequest.RunCampaign(MainCampaign, null, Meta()), 
                main_gamestart, true).With(large1Class),
            new OptionNodeLR<string?>(main_lang, l => {
                    SaveData.UpdateLocale(l);
                    SaveData.AssignSettingsChanges();
                }, new[] {
                    (new LString("English"), Locales.EN),
                    (new LString("日本語"), Locales.JP)
                }, SaveData.s.Locale)
                .With(large1Class),
            new TransferNode(OptionsScreen, main_options)
                .With(large1Class),
            new FuncNode(Application.Quit, main_quit)
                .With(large1Class),
            new OpenUrlNode("https://twitter.com/rdbatz", main_twitter)
                .With(large1Class)
            ).With(MainScreenV);
        ResetCurrentNode();

        base.FirstFrame();
        _ = uiRenderer.Slide(new Vector2(3, 0), Vector2.zero, 1f, DMath.M.EOutSine);
        _ = uiRenderer.Fade(0, 1, 1f, null);
    }
}
}