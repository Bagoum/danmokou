using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmaku {
public class PowerItem : Item {
    protected override short RenderOffsetIndex => 1;
    protected override float RotationTurns => 1;

    protected override void CollectMe() {
        GameManagement.campaign.AddPowerItems(1);
        base.CollectMe();
    }
}
}