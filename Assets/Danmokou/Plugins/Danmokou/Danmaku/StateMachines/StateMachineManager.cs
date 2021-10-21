using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Danmokou.Core;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.SM {


//This class serves as a loader and repository for state machines.
//Recall that state machines are stateless; each kind is instantiated only once for all BEH that use it.
public static class StateMachineManager  {
    private static readonly Dictionary<int, StateMachine> SMMapByFile = new Dictionary<int, StateMachine>();
    private static readonly Dictionary<string, TextAsset?> SMFileByName = new Dictionary<string, TextAsset?>() {
        { "", null }
    };
    private static readonly Dictionary<string, Dictionary<string, TextAsset>> Dialogue = new Dictionary<string, Dictionary<string, TextAsset>>();
    
    static StateMachineManager() {
        if (!Application.isPlaying) return;
        foreach (var sm in GameManagement.References.fileStateMachines.SelectMany(x => x.assetGroups)
            .SelectMany(x => x.assets)) {
            SMFileByName[sm.name] = sm.file;
            //Don't load SMs on init
        }
        foreach (var group in GameManagement.References.dialogue.SelectMany(d => d.assetGroups)) {
            Dialogue[group.name] = new Dictionary<string, TextAsset>();
            foreach (var lc in group.files) {
                Dialogue[group.name][
                    Locales.AllLocales.Contains(lc.locale) ? (lc.locale ?? "") : ""] = lc.file;
            }
        }
    }

    public static StateMachine LoadDialogue(string file, string? lc = null) {
        var locale = lc ?? SaveData.s.Locale ?? "";
        if (!Dialogue.TryGetValue(file, out var locales)) 
            throw new Exception($"No dialogue file by name {file}");
        if (!locales.TryGetValue(locale, out var tx) && !locales.TryGetValue("", out tx)) 
            throw new Exception($"Dialogue file has no applicable localization");
        //No need to cache this, since dialogue files are effectively never referenced from more than one place
        return StateMachine.CreateFromDump(tx.text);
    }
    
    public static StateMachine? FromName(string name) {
        if (SMFileByName.TryGetValue(name, out TextAsset? txt)) return FromText(txt);
        throw new Exception($"No StateMachine file by name {name} exists.");
    }

    public static void ClearCachedSMs() {
        SMMapByFile.Clear();
    }

    public static StateMachine? FromText(TextAsset? t) {
        if (t == null) return null;
        int id = t.GetInstanceID();
        if (!SMMapByFile.ContainsKey(id)) {
            SMMapByFile[id] = StateMachine.CreateFromDump(t.text);
        }
        return SMMapByFile[id];
    }
}
}
