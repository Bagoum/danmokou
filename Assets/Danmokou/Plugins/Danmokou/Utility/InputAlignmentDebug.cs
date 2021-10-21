using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Testing {
public class InputAlignmentDebug : RegularUpdater {
    public override void RegularUpdate() {
        if (Input.GetKeyDown(KeyCode.H)) Logs.Log("KeyDown event");
        if (Input.GetKey(KeyCode.H)) Logs.Log("Key event");
        if (Input.GetKeyUp(KeyCode.H)) Logs.Log("KeyUp event");
        if (InputManager.Bomb.Active) Logs.Log("Bomb event");
    }
}
}