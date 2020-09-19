using System.Collections;
using System.Collections.Generic;
using Danmaku;
using TMPro;
using UnityEngine;

public class DemoScriptButton : MonoBehaviour {
    private TextAsset script;
    private DemoScriptManager manager;
    public TextMeshProUGUI text;
    public void Initialize(TextAsset file, DemoScriptManager parent, string label) {
        script = file;
        manager = parent;
        text.text = label;
    }

    public void SetMe() {
        manager.RequestScript(script);
    }
}