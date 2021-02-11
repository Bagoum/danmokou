using System;
using System.Collections;
using DMK.Core;
using DMK.DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Behavior.Display {
public static class CutinHelpers {
    public static IEnumerator Velocity(Transform t, TP3 eq) {
        var bpi = new ParametricInfo() {
            loc = t.position
        };
        for (bpi.t = 0f;; bpi.t += ETime.FRAME_TIME) {
            t.localPosition += eq(bpi) * ETime.FRAME_TIME;
            bpi.loc = t.position;
            yield return null;
        }
        // ReSharper disable once IteratorNeverReturns
    }
    public static IEnumerator Offset(Transform t, TP3 eq) {
        var bpi = new ParametricInfo() {
            loc = t.position
        };
        for (bpi.t = 0f;; bpi.t += ETime.FRAME_TIME) {
            t.localPosition = eq(bpi);
            bpi.loc = t.position;
            yield return null;
        }
        // ReSharper disable once IteratorNeverReturns
    }
}
}