using System;
using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using DMK.Scriptables;
using DMK.Services;
using UnityEngine.UIElements;
using UnityEngine.Scripting;


namespace DMK.UI.XML {
/// <summary>
/// Class to manage the death menu UI.
/// </summary>
[Preserve]
public class XMLDeathMenu : XMLMenu {

    public VisualTreeAsset UIScreen;
    public VisualTreeAsset UINode;

    public SFXConfig openPauseSound;
    public SFXConfig closePauseSound;

    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), UIScreen},
        {typeof(UINode), UINode},
    };
    protected override string HeaderOverride => "YOU DIED";

    protected override void ResetCurrentNode() {
        Current = MainScreen.top[GameManagement.instance.Continues > 0 ? 0 : 1];
    }

    protected override void Awake() {
        MainScreen = new UIScreen(
            new FuncNode(() => {
                if (GameManagement.instance.TryContinue()) {
                    EngineStateManager.AnimatedUnpause();
                    return true;
                } else return false;
            }, () => $"Continue ({GameManagement.instance.Continues})", true),
            new ConfirmFuncNode(GameManagement.Restart, "Restart", true)
                .EnabledIf(() => GameManagement.CanRestart),
            GameManagement.MainMenuExists ?
                new ConfirmFuncNode(GameManagement.GoToMainMenu, "Return to Menu", true) :
                null
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