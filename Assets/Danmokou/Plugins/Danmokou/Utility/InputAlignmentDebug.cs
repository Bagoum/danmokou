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
        
        var downKeys = new HashSet<KeyCode>();
        var holdKeys = new HashSet<KeyCode>();
        var upKeys = new HashSet<KeyCode>();
        foreach (var kc in Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>()) {
            if (Input.GetKeyDown(kc)) downKeys.Add(kc);
            if (Input.GetKey(kc)) holdKeys.Add(kc);
            if (Input.GetKeyUp(kc)) upKeys.Add(kc);
        }
        /*
        if (downKeys.Count > 0 || holdKeys.Count > 0 || upKeys.Count > 0 || InputManager.TextInput is { })
            Logs.Log($"KeyDown: {string.Join(", ", downKeys)} KeyHold: {string.Join(", ", holdKeys)} " +
                     $"KeyUp: {string.Join(", ", upKeys)} Text: {InputManager.TextInput} or {Input.inputString}");
                     */

    }

    public void Update() {
        //Input.GetJoystickNames();
        //Logs.Log($"{Input.inputString}");
    }
}
}