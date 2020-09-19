using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmaku {
public abstract class PowerupItem : BouncyItem {
    protected abstract Enums.Subshot Type { get; }
    protected override void CollectMe() {
        GameManagement.campaign.SetSubshot(Type);
        base.CollectMe();
    }
}
public class PowerupD : PowerupItem {
    protected override Enums.Subshot Type => Enums.Subshot.TYPE_D;
}
}