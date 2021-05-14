using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using UnityEngine;

namespace Danmokou.Behavior.Functions {
public class MoveMe : RegularUpdater {
    [ReflectInto(typeof(TP3))]
    public string location = "";
    private TP3 locF = null!;
    private Transform tr = null!;
    private float t;

    private void Awake() {
        locF = location.Into<TP3>();
        tr = transform;
    }

    public override void RegularUpdate() {
        tr.localPosition = locF(new ParametricInfo(tr.localPosition, 0, 0, t += ETime.FRAME_TIME));
    }
}
}