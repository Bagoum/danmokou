using System;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Services;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using static Danmokou.UI.XML.XMLUtils;
using static Danmokou.Core.LocalizedStrings.UI;

namespace Danmokou.UI.XML {
/// <summary>
/// Class to manage the main menu UI for campaign-type games.
/// </summary>
[Preserve]
public class XMLReplaySaveMenu : XMLMenu {
    public VisualTreeAsset UIScreenV = null!;

    protected override void Awake() {
        if (!Application.isPlaying) return;
        if (Replayer.PostedReplay == null) {
            //This shouldn't happen, but here's trivial handling to avoid catastrophic problems
            MainScreen = new UIScreen(
                new FuncNode(GameManagement.GoToMainMenu, to_menu)
            ).With(UIScreenV);
        } else {
            var r = Replayer.PostedReplay.Value;
            var replayName = new TextInputNode(replay_name);
            var save = replay_save;
            MainScreen = new UIScreen(
                new UINode(() => r.metadata.Record.AsDisplay(true, true, true)).With(small1Class),
                new PassthroughNode(LocalizedString.Empty),
                replayName,
                new FuncNode(() => {
                    r.metadata.Record.AssignName(replayName.DataWIP);
                    //The name edit changes the name under the record 
                    SaveData.SaveRecord();
                    SaveData.p.SaveNewReplay(r);
                    save = replay_saved;
                    return true;
                }, () => save, true),
                new FuncNode(GameManagement.GoToMainMenu, to_menu)
            ).With(UIScreenV);
        }
        MainScreen.ExitNode = MainScreen.top[MainScreen.top.Length - 1];
        base.Awake();
        if (MainScreen.top.Length > 2) Current = MainScreen.top[2];
    }
}
}