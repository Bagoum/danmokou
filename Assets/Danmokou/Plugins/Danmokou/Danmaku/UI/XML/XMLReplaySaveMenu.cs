using System;
using System.Collections.Generic;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.GameInstance;
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

    public override void FirstFrame() {
        if (InstanceRequest.InstanceCompleted.LastPublished.Try(out var inst) && inst.data.Replay is ReplayRecorder rr) {
            var r = rr.Compile(inst.record);
            var replayName = new TextInputNode(replay_name);
            var save = replay_save;
            MainScreen = new UIScreen(this,
                new UINode(() => r.metadata.Record.AsDisplay(true, true, true)).With(small1Class),
                new PassthroughNode(LString.Empty),
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
        } else {
            //This shouldn't happen, but here's trivial handling to avoid catastrophic problems
            MainScreen = new UIScreen(this, 
                new FuncNode(GameManagement.GoToMainMenu, to_menu)
            ).With(UIScreenV);
        }
        MainScreen.ExitNode = MainScreen.Top[MainScreen.Top.Length - 1];
        
        base.FirstFrame();
        if (MainScreen.Top.Length > 2) Current = MainScreen.Top[2];
    }
}
}