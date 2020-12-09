using System.Collections;
using System.Collections.Generic;
using DMK.Behavior;
using DMK.Core;
using UnityEngine;

namespace DMK.Testing {
public class InputAlignmentDebug : RegularUpdater {
    public override void RegularUpdate() {
        if (Input.GetKeyDown(KeyCode.H)) Log.Unity("KeyDown event");
        if (Input.GetKey(KeyCode.H)) Log.Unity("Key event");
        if (Input.GetKeyUp(KeyCode.H)) Log.Unity("KeyUp event");
        if (InputManager.Bomb.Active) Log.Unity("Bomb event");
    }
}
}