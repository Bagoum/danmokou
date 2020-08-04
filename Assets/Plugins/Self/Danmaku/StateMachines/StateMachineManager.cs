using System;
using System.Collections.Generic;
using Danmaku;
using JetBrains.Annotations;
using UnityEngine;

namespace SM {


//This class serves as a loader and repository for state machines.
//Recall that state machines are stateless; each kind is instantiated only once for all BEH that use it.
public static class StateMachineManager  {
    private static readonly Dictionary<int, StateMachine> SMMapByFile = new Dictionary<int, StateMachine>();
    private static readonly Dictionary<string, TextAsset> SMFileByName = new Dictionary<string, TextAsset>() {
        { "", null }
    };
    private static readonly Dictionary<string, Dictionary<Locale, TextAsset>> Dialogue = new Dictionary<string, Dictionary<Locale, TextAsset>>();
    
    static StateMachineManager() {
        var (sms, dialogue) = GameManagement.StateMachineAssets;
        if (sms != null) {
            foreach (var group in sms.assetGroups) {
                foreach (var sminfo in group.assets) {
                    SMFileByName[sminfo.name] = sminfo.file;
                    //Don't load SMs on init
                }
            }
        }
        if (dialogue != null) {
            foreach (var group in dialogue.assetGroups) {
                Dialogue[group.name] = new Dictionary<Locale, TextAsset>();
                foreach (var lc in group.files) {
                    Dialogue[group.name][lc.locale] = lc.file;
                }
            }
        }
    }

    public static StateMachine LoadDialogue(string file, Locale lc = Locale.EN) {
        if (!Dialogue.TryGetValue(file, out var locales)) 
            throw new Exception($"No dialogue file by name {file}");
        if (!locales.TryGetValue(lc, out var tx) && !locales.TryGetValue(Locale.EN, out tx)) 
            throw new Exception($"Dialogue file has no applicable localization");
        //No need to cache this, since dialogue files are effectively never referenced from more than one place
        return StateMachine.CreateFromDump(tx.text);
    }
    
    public static StateMachine GetSMFromName(string name) {
        if (SMFileByName.TryGetValue(name, out TextAsset txt)) return GetSMFromTextAsset(txt);
        throw new Exception($"No StateMachine file by name {name} exists.");
    }

    public static void ClearCachedSMs() {
        SMMapByFile.Clear();
    }

    //SMs can be lazy-loaded over the course of a game, but most are loaded on the InitOnLoad method.
    [CanBeNull]
    public static StateMachine GetSMFromTextAsset([CanBeNull] TextAsset t) {
        if (t == null) return null;
        int id = t.GetInstanceID();
        if (!SMMapByFile.ContainsKey(id)) {
            SMMapByFile[id] = StateMachine.CreateFromDump(t.text);
        }
        return SMMapByFile[id];
    }
}
}
