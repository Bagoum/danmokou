using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;

namespace Danmokou.Behavior.Functions {
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