using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {
public class DemoScriptButton : MonoBehaviour {
    private TextAsset script = null!;
    private DemoScriptManager manager = null!;
    public TextMeshProUGUI text = null!;

    public void Initialize(TextAsset file, DemoScriptManager parent, string label) {
        script = file;
        manager = parent;
        text.text = label;
    }

    public void SetMe() {
        manager.RequestScript(script);
    }
}
}