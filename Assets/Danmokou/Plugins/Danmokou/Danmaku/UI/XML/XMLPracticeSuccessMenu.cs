using System;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine.UIElements;
using UnityEngine.Scripting;
using static Danmokou.Core.LocalizedStrings.UI;


namespace Danmokou.UI.XML {
/// <summary>
/// Class to manage the success menu UI. This is invoked when a scene or boss practice is finished.
/// </summary>
[Preserve]
public class XMLPracticeSuccessMenu : PausedGameplayMenu {
    public override void FirstFrame() {
        MainScreen = new UIScreen(this, "YOU HUNTED", UIScreen.Display.OverlayTH)  { Builder = (s, ve) => {
            ve.AddColumn();
        }, MenuBackgroundOpacity = 0.8f  };
        _ = new UIColumn(MainScreen, null,
            new FuncNode(restart, () => GameManagement.Restart())
                {EnabledIf = (() => GameManagement.CanRestart)},
            new FuncNode(save_replay, GameManagement.GoToReplayScreen)
                {EnabledIf = (() => GameManagement.Instance.Replay is ReplayRecorder)},
            new FuncNode(to_menu, GameManagement.GoToMainMenu)) {
            EntryIndexOverride = () => 1
        };
        base.FirstFrame();
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(GameManagement.EvInstance, i => i.PracticeSuccess, ShowMe);
    }


}
}