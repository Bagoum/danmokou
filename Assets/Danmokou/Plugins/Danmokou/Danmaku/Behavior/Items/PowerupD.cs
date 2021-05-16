using Danmokou.Core;
using Danmokou.Player;

namespace Danmokou.Behavior.Items {
public abstract class PowerupItem : BouncyItem {
    protected abstract Subshot SType { get; }
    protected override void CollectMe(PlayerController collector) {
        collector.SetSubshot(SType);
        base.CollectMe(collector);
    }
}
public class PowerupD : PowerupItem {
    protected override ItemType Type => ItemType.POWERUP_D;
    protected override Subshot SType => Subshot.TYPE_D;
}
}