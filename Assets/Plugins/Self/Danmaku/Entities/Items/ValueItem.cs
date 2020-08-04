using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmaku {
public class ValueItem : Item {

    protected override void CollectMe() {
        GameManagement.campaign.AddValueItems(1);
        base.CollectMe();
    }
}
}