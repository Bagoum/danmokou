using Danmokou.Core;
using Danmokou.Player;

namespace Danmokou.Behavior.Items {
public class OneUpItem : Item {
    protected override ItemType Type => ItemType.ONEUP;
    protected override short RenderOffsetIndex => 6;
    protected override float RotationTurns => 6;
    protected override float RotationTime => 2.5f;
    protected override float peakt => 1.4f;
    protected override float speed0 => 1.7f;
    protected override float speed1 => -1.5f;
    protected override float CollectRadiusBonus => 0.1f;

    protected override void CollectMe(PlayerController collector) {
        collector.AddOneUpItem();
        base.CollectMe(collector);
    }
}
}