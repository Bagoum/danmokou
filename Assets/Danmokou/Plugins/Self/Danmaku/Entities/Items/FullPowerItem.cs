using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmaku {
public class FullPowerItem : Item {
    protected override short RenderOffsetIndex => 5;
    protected override float RotationTurns => 0;

    protected override void CollectMe() {
        GameManagement.campaign.AddFullPowerItems(1);
        base.CollectMe();
    }
}
}