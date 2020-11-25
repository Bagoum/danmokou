using System;
using System.Collections;
using System.Collections.Generic;
using DMath;
using UnityEngine;

namespace Danmaku {
public class ValueItem : Item {
    protected override short RenderOffsetIndex => 2;
    protected override float RotationTurns => -1;

    private const double MAX_BONUS = 2;
    protected override void CollectMe() {
        var bonus = autocollected ?
            2 :
            M.Lerp(1, MAX_BONUS, M.Ratio(
                LocationService.BotPlayerBound + 1,
                PoC.Bound.y, tr.position.y));
        GameManagement.campaign.AddValueItems(1, bonus);
        base.CollectMe();
    }
}
}