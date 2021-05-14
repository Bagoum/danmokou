using Danmokou.DMath;

namespace Danmokou.Behavior.Display {
public class RectDrawer : ShapeDrawer {
    private BPRV2 locate = null!;

    public void Initialize(TP4 colorizer, BPRV2 locater) {
        locate = locater;
        base.Initialize(colorizer);
    }

    protected override V2RV2 GetLocScaleRot() => locate(beh.rBPI);
}
}

