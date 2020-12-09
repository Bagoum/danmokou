using System;
using System.Collections.Generic;
using DMK.Core;
using DMK.Scriptables;
using DMK.Services;
using UnityEngine.UIElements;
using UnityEngine.Scripting;


namespace DMK.UI.XML {
/// <summary>
/// Class to manage the success menu UI. This is invoked when a scene or boss practice is finished.
/// </summary>
[Preserve]
public class XMLPracticeSuccessMenu : XMLMenu {

    public VisualTreeAsset UIScreen;
    public VisualTreeAsset UINode;

    public SFXConfig openPauseSound;
    public SFXConfig closePauseSound;

    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), UIScreen},
        {typeof(UINode), UINode},
    };
    protected override string HeaderOverride => "YOU HUNTED";

    protected override void ResetCurrentNode() {
        Current = MainScreen.top[0];
    }

    protected override void Awake() {
        MainScreen = new UIScreen(
            new FuncNode(GameManagement.Restart, "Restart", true)
                .EnabledIf(() => GameManagement.CanRestart),
            new FuncNode(GameManagement.GoToReplayScreen, "Save Replay", true)
                .EnabledIf(() => Replayer.PostedReplay != null),
            new FuncNode(GameManagement.GoToMainMenu, "Return to Menu", true)
        );
        base.Awake();
    }

    protected override void Start() {
        base.Start();
        HideMe();
        MenuActive = false;
        UI.style.right = UIManager.MenuRightOffset;
    }

    public void HideMe(bool sfx = false) {
        if (UITop != null) {
            if (sfx) SFXService.Request(closePauseSound);
            UITop.style.display = DisplayStyle.None;
            MenuActive = false;
        }
    }

    public void ShowMe() {
        if (UITop != null && !MenuActive) {
            MenuActive = true;
            SFXService.Request(openPauseSound);
            UITop.style.display = DisplayStyle.Flex;
            ResetCurrentNode();
            Redraw();
        }
    }
}
}