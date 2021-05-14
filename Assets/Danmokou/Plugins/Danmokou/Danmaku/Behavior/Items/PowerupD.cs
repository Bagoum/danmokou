using Danmokou.Core;

namespace Danmokou.Behavior.Items {
public abstract class PowerupItem : BouncyItem {
    protected abstract Subshot SType { get; }
    protected override void CollectMe() {
        GameManagement.Instance.SetSubshotViaItem(SType);
        base.CollectMe();
    }
}
public class PowerupD : PowerupItem {
    protected override ItemType Type => ItemType.POWERUP_D;
    protected override Subshot SType => Subshot.TYPE_D;
}
}