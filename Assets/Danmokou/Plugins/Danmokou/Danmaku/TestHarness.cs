using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Player;
using Danmokou.SM;
using UnityEngine;

namespace Danmokou.Testing {
public class TestHarness : RegularUpdater {
    public TextAsset[] behaviorScripts = null!;
    private static readonly Dictionary<string, TextAsset> scriptsByName = new Dictionary<string, TextAsset>();

    private void Awake() {
        scriptsByName.Clear();
        foreach (var script in behaviorScripts) scriptsByName[script.name] = script;
    }

    public override int UpdatePriority => UpdatePriorities.SOF;

    public override void RegularUpdate() {
        int ndelinv = checks.Count;
        for (int ii = 0; ii < ndelinv; ++ii) {
            var (assertion, delay) = checks.Dequeue();
            if (delay == 0) assertion();
            else checks.Enqueue((assertion, delay - 1));
        }
    }

    private static readonly Queue<(Action assertion, int delay)> checks = new Queue<(Action assertion, int delay)>();
    public static void Check(int delay, Action assertion) => checks.Enqueue((assertion, delay));
    public static void OnSOF(Action doThing) => Check(0, doThing);

    public static bool Running => checks.Count > 0;
    public static StateMachine? LoadBehaviorScript(string sname) {
        if (!scriptsByName.TryGetValue(sname, out var script)) {
            foreach (var key in scriptsByName.Keys) {
                if (key.ToLower().StartsWith(sname.ToLower())) {
                    script = scriptsByName[key];
                    break;
                }
            }
        }
        if (script == null) throw new Exception($"Couldn't find testing script {sname}");
        return StateMachineManager.FromText(script);
    }
    
    #if UNITY_EDITOR
    public static void RunBehaviorScript(string sname, string behid) {
        var sm = LoadBehaviorScript(sname);
        BehaviorEntity.GetExecForID(behid).RunPatternSM(sm);
    }
    
    
    #endif
}
}