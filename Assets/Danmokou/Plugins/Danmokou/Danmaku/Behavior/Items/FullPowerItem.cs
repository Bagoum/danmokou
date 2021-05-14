using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public class FullPowerItem : Item {
    protected override ItemType Type => ItemType.FULLPOWER;
    protected override short RenderOffsetIndex => 6;
    protected override float RotationTurns => 0;

    protected override void CollectMe() {
        GameManagement.Instance.AddFullPowerItems(1);
        base.CollectMe();
    }
}
}