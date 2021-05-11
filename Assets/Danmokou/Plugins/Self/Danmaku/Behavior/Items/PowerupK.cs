using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using UnityEngine;

namespace DMK.Behavior.Items {
public class PowerupK : PowerupItem {
    protected override ItemType Type => ItemType.POWERUP_K;
    protected override Subshot SType => Subshot.TYPE_K;
}
}