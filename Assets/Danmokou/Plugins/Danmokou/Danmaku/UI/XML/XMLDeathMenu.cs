using System;
using System.Collections;
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
/// Class to manage the death menu UI.
/// </summary>
[Preserve]
public class XMLDeathMenu : PausedGameplayMenu {

    protected override void ResetCurrentNode() {
        Current = MainScreen.Top[GameManagement.Instance.Continues > 0 ? 0 : 1];
    }

    protected override void Awake() {
        MainScreen = new UIScreen(this,
            new FuncNode(() => {
                if (GameManagement.Instance.TryContinue()) {
                    ProtectHide(HideMe);
                    return true;
                } else return false;
                }, () => death_continue_ls(GameManagement.Instance.Continues), true
            ).EnabledIf(() => GameManagement.Instance.Continues > 0),
            new ConfirmFuncNode(GameManagement.Restart, restart, true)
                .EnabledIf(() => GameManagement.CanRestart),
            new ConfirmFuncNode(GameManagement.GoToMainMenu, to_menu, true)
        ).With(UIScreen);
        MainScreen.ExitNode = MainScreen.Top[0];
        base.Awake();
    }

    protected override void Start() {
        base.Start();
        UI.Q<Label>("Header").text = death_header;
        HideMe();
    }

    protected override void BindListeners() {
        base.BindListeners();
        
        Listen(GameManagement.EvInstance, i => i.GameOver, ShowMe);
    }
}
}