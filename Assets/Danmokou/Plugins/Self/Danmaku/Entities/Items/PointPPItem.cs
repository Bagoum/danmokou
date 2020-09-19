using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmaku {
public class PointPPItem : Item {
    protected override short RenderOffsetIndex => 3;
    protected override float RotationTurns => -2;

    protected override void CollectMe() {
        GameManagement.campaign.AddPointPlusItems(1);
        base.CollectMe();
    }
}
}