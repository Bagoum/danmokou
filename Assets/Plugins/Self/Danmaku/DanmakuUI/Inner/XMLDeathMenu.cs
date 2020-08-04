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
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using static GameManagement;
using static Danmaku.MainMenu;


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
        Current = MainScreen.top[GameManagement.campaign.Continues > 0 ? 0 : 1];
    }

    protected override void Awake() {
        MainScreen = new UIScreen(
            new FuncNode(() => {
                if (GameManagement.campaign.TryContinue()) {
                    HideMe();
                    GameStateManager.ForceUnpause();
                    return true;
                } else return false;
            }, () => $"Continue ({GameManagement.campaign.Continues})", true),
            new ConfirmFuncNode(() => {
                HideMe();
                GameManagement.ReloadLevel();
            }, "Reload Level", true),
            new ConfirmFuncNode(() => {
                HideMe();
                GameManagement.GoToMainMenu();
            }, "Quit to Menu", true));
        base.Awake();
    }

    protected override void Start() {
        base.Start();
        HideMe();
        MenuActive = false;
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