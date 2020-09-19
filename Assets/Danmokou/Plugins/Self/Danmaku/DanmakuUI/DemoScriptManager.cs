using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using SM;
using TMPro;
using UnityEngine;

public class DemoScriptManager : MonoBehaviour {
    public TextMeshPro scriptDisplay;
    public Transform buttonGroup;
    public GameObject buttonPrefab;
    public TextAsset[] scripts;
    public BehaviorEntity boss;
    public void Awake() {
        scriptDisplay.richText = false;
        foreach (var (textAsset, phases) in scripts
            .Select(s => (s, StateMachineManager.FromText(s)))
            .Select(s => (s.Item1, SMAnalysis.Analyze(null, s.Item2 as PatternSM, false)))) {
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