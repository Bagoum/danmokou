using DMK.Core;
using DMK.DMath;
using DMK.Reflection;

namespace DMK.Behavior.Functions {
public class RotateMe : BehaviorEntity {

    [ReflectInto(typeof(TP3))]
    public string rotator = "";
    private TP3 rotate = null!;

    protected override void Awake() {
        base.Awake();
        rotate = ReflWrap<TP3>.Wrap(rotator);
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        tr.localEulerAngles = rotate(bpi);
    }
}
}