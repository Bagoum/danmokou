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
                new UINode(() => r.metadata.Record.AsDisplay(true, true, true)).With(small1Class),
                new PassthroughNode(""),
                replayName,
                new FuncNode(() => {
                    r.metadata.Record.AssignName(replayName.DataWIP);
                    //The name edit changes the name under the record 
                    SaveData.SaveRecord();
                    SaveData.p.SaveNewReplay(r);
                    save = "Saved!";
                    return true;
                }, () => save, true),
                new FuncNode(GameManagement.GoToMainMenu, "Return to Menu")
            ).With(UIScreenV);
        }
        base.Awake();
        if (MainScreen.top.Length > 2) Current = MainScreen.top[2];
    }
}