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
    }
    public static IEnumerator Ease<T>(T start, T end, Action<T> apply, 
        Func<T, T, float, T> lerp, float time, [CanBeNull] FXY ease = null) {
        ease = ease ?? (x => x);
        for (float t = 0; t < time; t += ETime.FRAME_TIME) {
            apply(lerp(start, end, ease(t / time)));
            yield return null;
        }
        apply(end);
    }

    public static IEnumerator Rotate(Transform t, Vector3 end, float over, [CanBeNull] FXY ease) =>
        Ease(Vector3.zero, end, x => t.localEulerAngles = x, Vector3.Lerp, over, ease);
    public static IEnumerator Scale(Transform t, float end, float over, [CanBeNull] FXY ease) {
        var scale1 = t.localScale;
        return Ease(1f, end, x => t.localScale = scale1 * x, Mathf.Lerp, over, ease);
    }
}
}