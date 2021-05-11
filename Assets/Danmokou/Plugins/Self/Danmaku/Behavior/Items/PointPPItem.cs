using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using UnityEngine;

namespace DMK.Behavior.Items {
public class PointPPItem : Item {
    protected override ItemType Type => ItemType.PPP;
    protected override short RenderOffsetIndex => 4;
    protected override float RotationTurns => -2;
    protected override float MinTimeBeforeHome => 0.8f;

    protected override void CollectMe() {
        GameManagement.Instance.AddPointPlusItems(1);
        base.CollectMe();
    }
}
}