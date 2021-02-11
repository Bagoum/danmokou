using System;
using System.Collections;
using System.Collections.Generic;
using DMK.Behavior;
using DMK.Core;
using DMK.DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Services {
public interface IRaiko {
    void ShakeExtra(float time, float magMul);
    void Shake(float time, FXY? magnitude, float magMul, ICancellee? cT, Action? done);
}

public class RaikoCamera : CoroutineRegularUpdater, IRaiko {
    private Transform tr = null!;

    private void Awake() {
        tr = transform;
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterDI<IRaiko>(this);
    }

    private const float ScreenshakeMultiplier = 0.04f;

    public void ShakeExtra(float time, float magMul) {
        if (cancelTokens.Count == 0) Shake(time, null, magMul, null, () => { });
    }

    /// <summary>
    /// Note that this may call DONE earlier than TIME if it is cancelled by another SHAKE call.
    /// </summary>
    public void Shake(float time, FXY? magnitude, float magMul, ICancellee? cT,
        Action? done) {
        foreach (var cts in cancelTokens) {
            cts.Cancel();
        }
        cancelTokens.Clear();
        magnitude ??= (t => M.Sin(M.PI * (0.4f + 0.6f * t / time)));
        var x = new Cancellable();
        cancelTokens.Add(x);
        var joint = new JointCancellee(x, cT);
        RunDroppableRIEnumerator(IShake(time, magnitude, magMul, joint, () => {
            cancelTokens.Remove(x);
            done?.Invoke();
        }));
    }

    private readonly HashSet<Cancellable> cancelTokens = new HashSet<Cancellable>();

    private IEnumerator IShake(float time, FXY magnitude, float magMul, ICancellee cT, Action? done) {
        Vector3 pp = tr.localPosition; // Should be (0, 0, ?)
        for (float elapsed = 0; elapsed < time;) {
            float m = magnitude(elapsed) * magMul * ScreenshakeMultiplier * SaveData.s.Screenshake;
            float deg = RNG.GetFloatOffFrame(0, 360);
            Vector2 quake = M.PolarToXY(m, deg);
            tr.localPosition = pp + (Vector3) quake;
            yield return null;
            if (cT.Cancelled) break;
            elapsed += ETime.FRAME_TIME;
            yield return null;
            if (cT.Cancelled) break;
            elapsed += ETime.FRAME_TIME;
        }
        //Future: If you want to move the camera, do so by moving the Camera Container object.
        tr.localPosition = pp;
        done?.Invoke();
    }

}
}