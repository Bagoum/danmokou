using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public class LifeItem : Item {
    protected override ItemType Type => ItemType.LIFE;
    protected override short RenderOffsetIndex => 5;
    protected override float RotationTurns => 2;

    protected override void CollectMe(PlayerController collector) {
        collector.AddLifeItems(1);
        base.CollectMe(collector);
    }
}
}