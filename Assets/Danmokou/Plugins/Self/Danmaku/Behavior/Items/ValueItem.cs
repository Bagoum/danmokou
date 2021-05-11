using DMK.Core;
using DMK.DMath;

namespace DMK.Behavior.Items {
public class ValueItem : Item {
    protected override ItemType Type => ItemType.VALUE;
    protected override short RenderOffsetIndex => 2;
    protected override float RotationTurns => -1;

    private const double MAX_BONUS = 2;

    protected virtual void AddMe(double bonus) => GameManagement.Instance.AddValueItems(1, bonus);
    
    protected override void CollectMe() {
        var bonus = (autocollected || collection == null) ?
            MAX_BONUS :
            M.Lerp(1, MAX_BONUS, M.Ratio(
                LocationHelpers.BotPlayerBound + 1,
                collection.Bound.y, tr.position.y));
        AddMe(bonus);
        base.CollectMe();
    }
}
}