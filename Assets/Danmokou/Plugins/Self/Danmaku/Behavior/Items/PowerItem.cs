using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using UnityEngine;

namespace DMK.Behavior.Items {
public class PowerItem : Item {
    protected override short RenderOffsetIndex => 1;
    protected override float RotationTurns => 1;

    protected override void CollectMe() {
        GameManagement.Instance.AddPowerItems(1);
        base.CollectMe();
    }
}
}