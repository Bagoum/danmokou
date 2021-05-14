using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public class PowerupK : PowerupItem {
    protected override ItemType Type => ItemType.POWERUP_K;
    protected override Subshot SType => Subshot.TYPE_K;
}
}