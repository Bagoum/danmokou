using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public class PointPPItem : Item {
    protected override ItemType Type => ItemType.PPP;
    protected override short RenderOffsetIndex => 4;
    protected override float RotationTurns => -2;
    protected override float MinTimeBeforeHome => 0.8f;

    protected override void CollectMe(PlayerController collector) {
        collector.AddPointPlusItems(1);
        base.CollectMe(collector);
    }
}
}