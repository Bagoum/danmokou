using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using DMath;
using JetBrains.Annotations;
using UnityEngine;

public class RaikoCamera : CoroutineRegularUpdater {
    // This can and will be reset every level by a new camera controller
    private static RaikoCamera main;
    private Transform tr;

    private void Awake() {
        main = this;
        tr = transform;
    }

    private const float ScreenshakeMultiplier = 0.04f;

    public static void ShakeExtra(float time, float magMul) {
        if (main.cancelTokens.Count == 0) Shake(time, null, magMul, null, () => { });
    }
    public static void Shake(float time, [CanBeNull] FXY magnitude, float magMul, [CanBeNull] ICancellee cT, Action done) {
        foreach (var cts in main.cancelTokens) {
            cts.Cancel();
        }
        main.cancelTokens.Clear();
        magnitude = magnitude ?? (t => M.Sin(M.PI * (0.4f + 0.6f * t / time)));
        var x = new Cancellable();
        main.cancelTokens.Add(x);
        var joint = (cT == null) ? (ICancellee)x : new JointCancellee(x, cT);
        main.RunDroppableRIEnumerator(main.IShake(time, magnitude, magMul, joint, () => {
            main.cancelTokens.Remove(x);
            done();
        }));
    }
    private readonly HashSet<Cancellable> cancelTokens = new HashSet<Cancellable>();

    private IEnumerator IShake(float time, FXY magnitude, float magMul, ICancellee cT, Action done) {
        Vector3 pp = transform.localPosition; // Should be (0, 0, ?)
        for (float elapsed = 0; elapsed < time;) {
            float m = magnitude(elapsed) * magMul * ScreenshakeMultiplier * SaveData.s.Screenshake;
            float deg = RNG.GetFloatOffFrame(0, 360);
            Vector2 quake = M.PolarToXY(m, deg);
            transform.localPosition = pp + (Vector3) quake;
            yield return null;
            if (cT.Cancelled) break;
            elapsed += ETime.FRAME_TIME;
            yield return null;
            if (cT.Cancelled) break;
            elapsed += ETime.FRAME_TIME;
        }
        //Future: If you want to move the camera, do so by moving the Camera Container object.
        transform.localPosition = pp;
        done();
    }
    
    
}