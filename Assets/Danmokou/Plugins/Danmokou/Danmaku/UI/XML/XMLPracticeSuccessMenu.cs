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

    protected override string HeaderOverride => "YOU HUNTED";

    protected override void ResetCurrentNode() {
        Current = MainScreen.top[0];
    }

    protected override void Awake() {
        MainScreen = new UIScreen(
            new FuncNode(GameManagement.Restart, restart, true)
                .EnabledIf(() => GameManagement.CanRestart),
            new FuncNode(GameManagement.GoToReplayScreen, save_replay, true)
                .EnabledIf(() => GameManagement.Instance.Replay is ReplayRecorder),
            new FuncNode(GameManagement.GoToMainMenu, to_menu, true)
        ).With(UIScreen);
        MainScreen.ExitNode = MainScreen.top[2];
        base.Awake();
    }

    protected override void Start() {
        base.Start();
        HideMe();
        MenuActive = false;
        UI.style.right = UIManager.MenuRightOffset;
    }
    
    protected override void BindListeners() {
        base.BindListeners();
        Listen(InstanceData.PracticeSuccess, ShowMe);
    }


}
}