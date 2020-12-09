using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using UnityEngine;

namespace DMK.Behavior.Functions {
public class DestroyAfter : RegularUpdater {
    private float t = 0;
    public float maxTime = 6f;

    public override void RegularUpdate() {
        t += ETime.FRAME_TIME;
        if (t > maxTime) Destroy(gameObject);
    }
}
}