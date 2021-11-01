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

    protected override void ResetCurrentNode() {
        Current = MainScreen.Top[0];
    }

    public override void FirstFrame() {
        MainScreen = new UIScreen(this,
            new FuncNode(GameManagement.Restart, restart, true)
                .EnabledIf(() => GameManagement.CanRestart),
            new FuncNode(GameManagement.GoToReplayScreen, save_replay, true)
                .EnabledIf(() => GameManagement.Instance.Replay is ReplayRecorder),
            new FuncNode(GameManagement.GoToMainMenu, to_menu, true)
        ).With(UIScreen);
        MainScreen.ExitNode = MainScreen.Top[2];
        
        base.FirstFrame();
        UI.Q<Label>("Header").text = "YOU HUNTED";
        HideMe();
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(GameManagement.EvInstance, i => i.PracticeSuccess, ShowMe);
    }


}
}