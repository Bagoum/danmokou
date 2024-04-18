using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection2;
using Danmokou.Scenes;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;
using Helpers = Danmokou.Reflection2.Helpers;

namespace Danmokou.SM {

public class LoadedStateMachine {
    public StateMachine? SM { get; set; } = null;
    public EnvFrame? ScriptEF { get; set; } = null;
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
            .SelectMany(x => x.assets)
            .Concat(GameManagement.References.importableScripts)) {
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
        SceneIntermediary.SceneUnloaded.Subscribe(_ => ClearCachedSMs());
    }

    public static StateMachine LoadDialogue(string file, string? lc = null) {
        var locale = lc ?? SaveData.s.TextLocale.Value ?? "";
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
        foreach (var lsm in SMMapByFile.Values)
            if (!lsm.Preserve) {
                lsm.SM = null;
                lsm.ScriptEF?.Free();
                lsm.ScriptEF = null;
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

    public static StateMachine? FromText(TextAsset? t) {
        if (t == null) return null;
        return FFromText(t);
    }

    public static StateMachine FFromText(TextAsset t) {
        if (t == null)
            throw new Exception("Received a null TextAsset - couldn't parse a StateMachine!");
        return FromText(t.GetInstanceID(), t.text, t.name);
    }

    /// <summary>
    /// Return the state machine for the given TextAsset only if it has already been loaded
    /// (via <see cref="FromText(UnityEngine.TextAsset?)"/>). Otherwise return null.
    /// </summary>
    public static StateMachine? GetIfAlreadyLoaded(TextAsset? t) {
        if (t == null) return null;
        return GetLSM(t.GetInstanceID()).SM;
    }

    //some underlying reflection handling in c# is single-threaded, so we lock this to prevent
    // background loading from fucking stuff up
    //note that this can still theoretically interfere with other top-level loaders such as reflwrap
    private static readonly string topLevelLock = "";
    public static StateMachine FromText(int id, string text, string name) {
        lock (topLevelLock) {
            var lsm = GetLSM(id);
            if (lsm.SM == null) {
                try {
                    lsm.SM = StateMachine.CreateFromDump(text, out var ef);
                    lsm.ScriptEF = ef;
                } catch (Exception e) {
                    Logs.DMKLogs.Error(e, $"Failed to parse StateMachine from text file `{name}`.");
                    throw;
                }
            }
            return lsm.SM ?? throw new Exception($"Couldn't load StateMachine from text file `{name}`");
        }
    }

    private static readonly Stack<string> importStack = new();

    public static EnvFrame LoadImport(string name) {
        if (!SMFileByName.TryGetValue(name, out var txt) || txt == null)
            throw new Exception($"No SM is loaded with the name `{name}`.");
        var lsm = GetLSM(txt.GetInstanceID());
        //Imported files don't actually have to compile to StateMachine. We only need the EnvFrame and its scope.
        if (lsm.ScriptEF == null) {
            if (importStack.Contains(name)) {
                var sb = new StringBuilder();
                sb.Append(
                    $"There is a circular import for `{name}`. Circular imports are not permitted. The import stack is as follows:\n{name}");
                foreach (var x in importStack) {
                    sb.Append($"\nis imported by {x}");
                    if (x == name) break;
                }
                throw new Exception(sb.ToString());
            }
            importStack.Push(name);
            try {
                using var _ = BakeCodeGenerator.OpenContext(CookingContext.KeyType.SM_IMPORT, txt.text);
                lsm.ScriptEF = Helpers.ParseAndCompileErased(txt.text);
            } catch (Exception e) {
                Logs.DMKLogs.Error(e, $"Failed to parse import from text file `{name}`.");
                throw;
            } finally {
                importStack.Pop();
            }
        }
        return lsm.ScriptEF;
    }
    
    
}
}
