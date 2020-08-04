using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmaku {
public class LifeItem : Item {

    protected override void CollectMe() {
        GameManagement.campaign.AddLifeItems(1);
        base.CollectMe();
    }
}
}