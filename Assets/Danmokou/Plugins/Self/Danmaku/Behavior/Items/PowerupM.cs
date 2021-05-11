using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using UnityEngine;

namespace DMK.Behavior.Items {
public class PowerupM : PowerupItem {
    protected override ItemType Type => ItemType.POWERUP_M;
    protected override Subshot SType => Subshot.TYPE_M;
}
}