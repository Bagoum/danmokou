using System;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Player;
// ReSharper disable Unity.InefficientPropertyAccess

namespace Danmokou.Behavior.Items {
public class ValueItem : Item {
    protected override ItemType Type => ItemType.VALUE;
    protected override short RenderOffsetIndex => 2;
    protected override float RotationTurns => -1;

    private const double MAX_BONUS = 2;

    protected virtual void AddMe(PlayerController collector, double bonus) => 
        collector.AddValueItems(1, bonus);
    
    protected override void CollectMe(PlayerController collector) {
        var bonus = MAX_BONUS;
        if (!autocollected && collection != null) {
            bonus = M.Lerp(1, MAX_BONUS, 
                collection.direction switch {
                    LRUD.RIGHT => M.Ratio(LocationHelpers.LeftPlayerBound + 1, collection.Bound.x, tr.position.x),
                    LRUD.UP => M.Ratio(LocationHelpers.BotPlayerBound + 1, collection.Bound.y, tr.position.y),
                    LRUD.LEFT => M.Ratio(LocationHelpers.RightPlayerBound - 1, collection.Bound.x, tr.position.x),
                    _ => M.Ratio(LocationHelpers.TopPlayerBound - 1, collection.Bound.y, tr.position.y),
                });
        }
        AddMe(collector, bonus);
        base.CollectMe(collector);
    }
}
}