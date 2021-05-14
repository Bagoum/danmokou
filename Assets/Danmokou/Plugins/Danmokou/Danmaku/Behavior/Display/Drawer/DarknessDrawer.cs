using Danmokou.DMath;
using Danmokou.Graphics;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Behavior.Display {
public class DarknessDrawer : SpriteDisplayController {
    private TP4 color = null!;
    private TP locate = null!;
    private BPY scale = null!;

    public void Initialize(TP locator, BPY scaler, TP4? colorizer) {
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