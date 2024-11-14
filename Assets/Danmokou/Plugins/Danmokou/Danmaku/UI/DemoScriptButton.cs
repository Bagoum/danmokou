using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {
public class DemoScriptButton : MonoBehaviour {
    private NamedTextAsset script = default!;
    private DemoScriptManager manager = null!;
    public TextMeshProUGUI text = null!;

    public void Initialize(NamedTextAsset file, DemoScriptManager parent, string label) {
        script = file;
        manager = parent;
        text.text = label;
    }

    public void SetMe() {
        manager.RequestScript(script);
    }
}
}