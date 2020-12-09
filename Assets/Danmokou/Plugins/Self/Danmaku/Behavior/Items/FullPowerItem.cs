using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using UnityEngine;

namespace DMK.Behavior.Items {
public class FullPowerItem : Item {
    protected override short RenderOffsetIndex => 6;
    protected override float RotationTurns => 0;

    protected override void CollectMe() {
        GameManagement.instance.AddFullPowerItems(1);
        base.CollectMe();
    }
}
}