using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Core.DInput;
using System.Linq;
using UnityEngine;

namespace Danmokou.Testing {
public class InputAlignmentDebug : RegularUpdater {
    public override void RegularUpdate() {
        foreach (var kc in Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>()) {
            if (Input.GetKeyDown(kc)) Logs.Log($"{kc} KeyDown event");
            if (Input.GetKey(kc)) Logs.Log($"{kc} Key event");
            if (Input.GetKeyUp(kc)) Logs.Log($"{kc} KeyUp event");
        }
    }
}
}