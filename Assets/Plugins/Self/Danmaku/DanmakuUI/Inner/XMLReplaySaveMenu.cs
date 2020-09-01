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
using Object = UnityEngine.Object;
using static GameManagement;
using static XMLUtils;

/// <summary>
/// Class to manage the main menu UI for campaign-type games.
/// </summary>
[Preserve]
public class XMLReplaySaveMenu : XMLMenu {
    public VisualTreeAsset UIScreenV;
    public VisualTreeAsset GenericUINodeV;
    
    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), UIScreenV},
        {typeof(UINode), GenericUINodeV},
    };
    
    protected override void Awake() {
        if (!Application.isPlaying) return;
        if (Replayer.PostedReplay == null) {
            //This shouldn't happen, but here's trivial handling to avoid catastrophic problems
            MainScreen = new UIScreen(
                new FuncNode(GameManagement.GoToMainMenu, "Return to Menu")
            ).With(UIScreenV);
        } else {
            var r = Replayer.PostedReplay.Value;
            var replayName = new TextInputNode("Name");
            var save = "Save";
            MainScreen = new UIScreen(
                new UINode(() => r.metadata.AsDisplay),
                replayName,
                new FuncNode(() => {
                    r.metadata.AssignName(replayName.DataWIP);
                    SaveData.p.SaveNewReplay(r);
                    save = "Saved!";
                    return true;
                }, () => save, true),
                new FuncNode(GameManagement.GoToMainMenu, "Return to Menu")
            ).With(UIScreenV);
        }
        base.Awake();
        if (MainScreen.top.Length > 1) Current = MainScreen.top[1];
    }
}