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
using static Danmokou.UI.XML.XMLHelpers;
using static Danmokou.Core.LocalizedStrings.UI;
using static Danmokou.Core.LocalizedStrings.Generic;

namespace Danmokou.UI.XML {
/// <summary>
/// Class to manage the main menu UI for campaign-type games.
/// </summary>
[Preserve]
public class XMLReplaySaveMenu : UIController {
    public override void FirstFrame() {
        MainScreen = new UIScreen(this, "GAME RESULTS") { Builder = GameResultsScreenBuilder };
        if (InstanceRequest.InstanceCompleted.LastPublished.Try(out var inst)) {
            var options = new List<UINode>() {
                new FuncNode(to_menu, GameManagement.GoToMainMenu)
            };
            if (inst.data.Replay is ReplayRecorder rr) {
                var rec = rr.Compile(inst.record);
                var didSave = false;
                options.Add(new FuncNode(() => !didSave ? save_replay : replay_saved, n => {
                    if (didSave) return new UIResult.StayOnNode(true);
                    var nameEntry = new TextInputNode(LString.Empty);
                    return PopupUIGroup.LRB2(n, () => save_replay,
                        r => new UIColumn(r, new UINode(replay_name) {
                            Prefab = GameManagement.References.uxmlDefaults.PureTextNode, Passthrough = true
                        }, nameEntry.With(noSpacePrefixClass, centerTextClass)),
                        null, new UINode[] {
                            UIButton.Save(() => {
                                rec.Metadata.Record.AssignName(nameEntry.DataWIP);
                                //The name edit changes the name under the record 
                                SaveData.SaveRecord();
                                SaveData.p.SaveNewReplay(rec);
                                return didSave = true;
                            }, n.ReturnGroup),
                        });
                }));
            }
            var (lNodes, rNodes) = GameMetadataDisplay(inst.record);
            MainScreen.SetFirst(new LRBGroup(
                new UIColumn(new UIRenderExplicit(MainScreen, s => s.Q("Left")), lNodes),
                new UIColumn(new UIRenderExplicit(MainScreen, s => s.Q("Right")), rNodes),
                new UIRow(new UIRenderExplicit(MainScreen, s => s.Q("Bottom")), options) 
                    { EntryIndexOverride = () => -1 }
            ) {
                ExitNodeOverride = options[0],
                EntryNodeOverride = options.Count > 1 ? options[1] : options[0]
            });
        } else {
            Logs.UnityError("Arrived at replay save screen with no instance definition");
            GameManagement.GoToMainMenu();
        }
        base.FirstFrame();
    }
}
}