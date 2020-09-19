using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmaku {
public class OneUpItem : Item {
    protected override short RenderOffsetIndex => 5;
    protected override float RotationTurns => 6;
    protected override float RotationTime => 2.5f;
    protected override float peakt => 1.4f;
    protected override float speed0 => 1.7f;
    protected override float speed1 => -1.5f;
    protected override float CollectRadiusBonus => 0.1f;

    protected override void CollectMe() {
        GameManagement.campaign.LifeExtend();
        base.CollectMe();
    }
}
}