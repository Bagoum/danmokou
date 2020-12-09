using DMK.Behavior;
using DMK.Core;
using DMK.DMath;
using DMK.Reflection;
using UnityEngine;

namespace DMK.Behavior.Functions {
public class MoveMe : RegularUpdater {
    public string location;
    private TP3 locF;
    private Transform tr;
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