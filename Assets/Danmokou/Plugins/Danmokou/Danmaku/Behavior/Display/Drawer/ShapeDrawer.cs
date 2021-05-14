using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.Behavior.Display {
public abstract class ShapeDrawer : SpriteDisplayController {
    private TP4 color = default!;
    protected V2RV2 lastRV2 { get; private set; }

    public void Initialize(TP4 colorizer) {
        color = colorizer;
        sprite.color = new Color(0, 0, 0, 0);
    }

    //Scale specified in radius
    protected override Vector3 GetScale => new Vector3(lastRV2.rx / 0.5f, lastRV2.ry / 0.5f, 1);

    protected abstract V2RV2 GetLocScaleRot();

    public override void UpdateRender() {
        sprite.color = ColorHelpers.V4C(color(beh.rBPI));
        lastRV2 = GetLocScaleRot();
        tr.localPosition = lastRV2.NV;
        tr.localEulerAngles = new Vector3(0, 0, M.Mod(360, lastRV2.angle));
        base.UpdateRender();
    }
}
}
