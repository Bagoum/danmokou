using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Behavior;
using Danmokou.Services;
using Danmokou.SM;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {
#if UNITY_EDITOR || ALLOW_RELOAD
public class DemoScriptManager : MonoBehaviour {
    public TextMeshPro scriptDisplay = null!;
    public Transform buttonGroup = null!;
    public GameObject buttonPrefab = null!;
    public TextAsset[] scripts = null!;
    public BehaviorEntity boss = null!;

    public void Awake() {
        scriptDisplay.richText = false;
        foreach (var (textAsset, phases) in scripts
            .Select(s => (s, StateMachineManager.FromText(s)))
            .Select(s => (s.Item1, SMAnalysis.Analyze(null!, s.Item2 as PatternSM, false)))) {
            Instantiate(buttonPrefab, buttonGroup)
                .GetComponent<DemoScriptButton>()
                .Initialize(textAsset, this, phases[0].Title);
        }
    }

    public void Start() {
        RequestScript(scripts[0]);
    }

    public void RequestScript(TextAsset script) {
        boss.behaviorScript = script;
        scriptDisplay.text = script.text.Replace("\t", "  ");
        GameManagement.LocalReset();
    }
}
#endif
}