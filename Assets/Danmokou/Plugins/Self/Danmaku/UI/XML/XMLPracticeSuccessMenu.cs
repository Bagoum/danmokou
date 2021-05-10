using System;
using System.Collections.Generic;
using DMK.Core;
using DMK.Scriptables;
using DMK.Services;
using UnityEngine.UIElements;
using UnityEngine.Scripting;
using static DMK.Core.LocalizedStrings.UI;


namespace DMK.UI.XML {
/// <summary>
/// Class to manage the success menu UI. This is invoked when a scene or boss practice is finished.
/// </summary>
[Preserve]
public class XMLPracticeSuccessMenu : XMLMenu {
    public VisualTreeAsset UIScreen = null!;
    
    public SFXConfig? openPauseSound;
    public SFXConfig? closePauseSound;

    protected override string HeaderOverride => "YOU HUNTED";

    protected override void ResetCurrentNode() {
        Current = MainScreen.top[0];
    }

    protected override void Awake() {
        MainScreen = new UIScreen(
            new FuncNode(GameManagement.Restart, restart, true)
                .EnabledIf(() => GameManagement.CanRestart),
            new FuncNode(GameManagement.GoToReplayScreen, save_replay, true)
                .EnabledIf(() => Replayer.PostedReplay != null),
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

    public void HideMe(bool sfx = false) {
        if (UITop != null) {
            if (sfx) 
                DependencyInjection.SFXService.Request(closePauseSound);
            UITop.style.display = DisplayStyle.None;
            MenuActive = false;
        }
    }

    public void ShowMe() {
        if (UITop != null && !MenuActive) {
            MenuActive = true;
            DependencyInjection.SFXService.Request(openPauseSound);
            UITop.style.display = DisplayStyle.Flex;
            ResetCurrentNode();
            Redraw();
        }
    }
}
}