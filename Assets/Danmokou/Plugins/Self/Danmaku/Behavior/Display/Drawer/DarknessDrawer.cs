using DMK.DMath;
using DMK.Graphics;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Behavior.Display {
public class DarknessDrawer : SpriteDisplayController {
    private TP4 color;
    private TP locate;
    private BPY scale;

    public void Initialize(TP locator, BPY scaler, [CanBeNull] TP4 colorizer) {
        locate = locator;
        scale = scaler;
        color = colorizer ?? (_ => Color.black);
        sprite.color = new Color(0, 0, 0, 0);
        pb.SetFloat(PropConsts.ScaleX, sprite.transform.lossyScale.x);
    }

    public override void UpdateRender() {
        sprite.color = color(BPI);
        tr.localPosition = locate(BPI);
        pb.SetFloat(PropConsts.radius, scale(BPI));
        base.UpdateRender();
    }
}
}