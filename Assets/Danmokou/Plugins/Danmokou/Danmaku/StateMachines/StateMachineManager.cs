using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Danmokou.Core;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.SM {

public class LoadedStateMachine {
    public EFStateMachine? SM { get; set; } = null;
    //As of DMK v10.1 it is possible to preserve state machines and reflected objects between scenes.
    // However, it is generally good hygeine to destroy them in order to prevent hanging allocations.
    public bool Preserve { get; set; } = false;
}

//This class serves as a loader and repository for state machines.
//Recall that state machines are stateless; each kind is instantiated only once for all BEH that use it.
public static class StateMachineManager  {
    private static readonly Dictionary<int, LoadedStateMachine> SMMapByFile = new();
    private static readonly Dictionary<string, TextAsset?> SMFileByName = new() {
        { "", null }
    };
    private static readonly Dictionary<string, Dictionary<string, TextAsset>> Dialogue = new();
    
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

    public static EFStateMachine LoadDialogue(string file, string? lc = null) {
        var locale = lc ?? SaveData.s.TextLocale.Value ?? "";
        if (!Dialogue.TryGetValue(file, out var locales)) 
            throw new Exception($"No dialogue file by name {file}");
        if (!locales.TryGetValue(locale, out var tx) && !locales.TryGetValue("", out tx)) 
            throw new Exception($"Dialogue file has no applicable localization");
        //No need to cache this, since dialogue files are effectively never referenced from more than one place
        return StateMachine.CreateFromDump(tx.text);
    }
    
    public static EFStateMachine? FromName(string name) {
        if (SMFileByName.TryGetValue(name, out TextAsset? txt)) return FromText(txt);
        throw new Exception($"No StateMachine file by name {name} exists.");
    }

    public static void ClearCachedSMs() {
        foreach (var lsm in SMMapByFile.Values)
            if (!lsm.Preserve) {
                lsm.SM?.RootFrame?.Free();
                lsm.SM = null;
            }
    }
    
    /// <summary>
    /// Mark that the TextAsset SM with the given ID should not be cleared when loading a new scene.
    /// </summary>
    public static void Preserve(int id) {
        GetLSM(id).Preserve = true;
    }
    private static LoadedStateMachine GetLSM(int id) {
        if (SMMapByFile.TryGetValue(id, out var lsm))
            return lsm;
        return SMMapByFile[id] = new();
    }

    public static EFStateMachine? FromText(TextAsset? t) {
        if (t == null) return null;
        return FFromText(t);
    }

    public static EFStateMachine FFromText(TextAsset t) {
        return FromText(t.GetInstanceID(), t.text, t.name);
    }

    /// <summary>
    /// Return the state machine for the given TextAsset only if it has already been loaded
    /// (via <see cref="FromText(UnityEngine.TextAsset?)"/>). Otherwise return null.
    /// </summary>
    public static EFStateMachine? GetIfAlreadyLoaded(TextAsset? t) {
        if (t == null) return null;
        return GetLSM(t.GetInstanceID()).SM;
    }

    //some underlying reflection handling in c# is single-threaded, so we lock this to prevent
    // background loading from fucking stuff up
    //note that this can still theoretically interfere with other top-level loaders such as reflwrap
    private static readonly string topLevelLock = "";
    public static EFStateMachine FromText(int id, string text, string name) {
        lock (topLevelLock) {
        var lsm = GetLSM(id);
            if (lsm.SM == null) {
                try {
                    lsm.SM = StateMachine.CreateFromDump(text);
                } catch (Exception e) {
                    Logs.DMKLogs.Error(e, $"Failed to parse StateMachine from text file `{name}`.");
                    throw;
                }
            }
            return lsm.SM;
        }
    }
}
}
