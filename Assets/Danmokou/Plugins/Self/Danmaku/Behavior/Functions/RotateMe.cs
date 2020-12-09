using DMK.DMath;
using DMK.Reflection;

namespace DMK.Behavior.Functions {
public class RotateMe : BehaviorEntity {

    public string rotator;
    private TP3 rotate;

    protected override void Awake() {
        base.Awake();
        rotate = rotator.Into<TP3>();
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        tr.localEulerAngles = rotate(bpi);
    }
}
}