using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using Danmaku.DanmakuUI;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using static XMLUtils;


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
            new FuncNode(() => {
                if (GameManagement.Restart()) {
                    HideMe();
                    return true;
                } else return false;
            }, "Restart", true).With(small1Class),
            new FuncNode(() => {
                HideMe();
                GameManagement.GoToReplayScreen();
            }, "Save Replay", true).With(small1Class).EnabledIf(() => Replayer.PostedReplay != null),
            new FuncNode(() => {
                HideMe();
                GameManagement.GoToMainMenu();
            }, "Return to Menu", true).With(small1Class)
            );
        base.Awake();
    }

    protected override void Start() {
        base.Start();
        HideMe();
        MenuActive = false;
        UI.style.right = UIManager.MenuRightOffset;
    }
    public void HideMe(bool sfx=false) {
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