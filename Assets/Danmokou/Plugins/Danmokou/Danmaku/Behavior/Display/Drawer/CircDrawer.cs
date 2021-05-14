using Danmokou.DMath;

namespace Danmokou.Behavior.Display {
public class CircDrawer : ShapeDrawer {
    private BPRV2 locate = null!;

    public void Initialize(TP4 colorizer, BPRV2 locater) {
        base.Initialize(colorizer);
        locate = locater;
    }

    protected override V2RV2 GetLocScaleRot() => locate(beh.rBPI);
}
}
