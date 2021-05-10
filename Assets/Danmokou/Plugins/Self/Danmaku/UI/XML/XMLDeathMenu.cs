using System;
using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using DMK.Scriptables;
using DMK.Services;
using UnityEngine.UIElements;
using UnityEngine.Scripting;
using static DMK.Core.LocalizedStrings.UI;

namespace DMK.UI.XML {
/// <summary>
/// Class to manage the death menu UI.
/// </summary>
[Preserve]
public class XMLDeathMenu : XMLMenu {
    public VisualTreeAsset UIScreen = null!;
    public SFXConfig? openPauseSound;
    public SFXConfig? closePauseSound;

    protected override string HeaderOverride => death_header;

    protected override void ResetCurrentNode() {
        Current = MainScreen.top[GameManagement.Instance.Continues > 0 ? 0 : 1];
    }

    protected override void Awake() {
        MainScreen = new UIScreen(
            new FuncNode(() => {
                if (GameManagement.Instance.TryContinue()) {
                    EngineStateManager.AnimatedUnpause();
                    return true;
                } else return false;
            }, () => death_continue_ls(GameManagement.Instance.Continues), true),
            new ConfirmFuncNode(GameManagement.Restart, restart, true)
                .EnabledIf(() => GameManagement.CanRestart),
            new ConfirmFuncNode(GameManagement.GoToMainMenu, to_menu, true)
        ).With(UIScreen);
        MainScreen.ExitNode = MainScreen.top[0];
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