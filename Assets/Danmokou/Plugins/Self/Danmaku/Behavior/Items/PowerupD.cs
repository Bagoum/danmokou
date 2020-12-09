using DMK.Core;

namespace DMK.Behavior.Items {
public abstract class PowerupItem : BouncyItem {
    protected abstract Subshot Type { get; }
    protected override void CollectMe() {
        GameManagement.instance.SetSubshot(Type);
        base.CollectMe();
    }
}
public class PowerupD : PowerupItem {
    protected override Subshot Type => Subshot.TYPE_D;
}
}