using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Tasks;
using Danmokou.ADV;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Scenes;
using Danmokou.Services;
using Suzunoya.ADV;
using UnityEngine;

/// <summary>
/// Helper script that runs the linked ADVGameDef directly in a working scene (instead of going through the main menu).
/// </summary>
public class EditorOnly_FirstSceneExecutionHelper : CoroutineRegularUpdater {
    /// 1-indexed. If 0 or less, starts from nothing
    public int saveSlot = 0;
    
    
    #if UNITY_EDITOR
    
    public override void FirstFrame() {
        var gameDef = GameManagement.References.gameDefinition;
        if (SceneIntermediary.IsFirstScene && gameDef is IADVGameDef adv) {
            Logs.Log($"Running ADV game in working scene with save slot {saveSlot}");
            var advMan = ServiceLocator.Find<ADVManager>();
            var save = saveSlot <= 0 ?
                adv.NewGameData() :
                SaveData.v.Saves.TryGetValue(saveSlot - 1, out var s) ?
                    s.GetData() :
                    throw new Exception($"Save slot {saveSlot} not found");
            _ = new ADVInstanceRequest(advMan, adv, save).RunInScene().ContinueWithSync();
        }
    }

    public override int UpdatePriority => UpdatePriorities.SYSTEM;


#endif
}
