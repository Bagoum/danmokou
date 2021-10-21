using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Services {
public interface IRaiko {
    void Shake(float time, FXY? magnitude, float magMul, ICancellee? cT = null, Action? done = null);
}

public class RaikoCamera : RegularUpdater, IRaiko {
    private Transform tr = null!;
    private Vector3 baseLoc;

    private void Awake() {
        tr = transform;
        baseLoc = tr.localPosition;
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IRaiko>(this);
    }

    private const float ScreenshakeMultiplier = 0.04f;

    private class ShakeInProgress {
        public readonly float time;
        public readonly FXY magnitude;
        public readonly float magMul;
        public readonly ICancellee? cT;
        public readonly Action? done;
        public float elapsed;

        public ShakeInProgress(float time, FXY magnitude, float magMul, ICancellee? cT, Action? done) {
            this.time = time;
            this.magnitude = magnitude;
            this.magMul = magMul;
            this.cT = cT;
            this.done = done;
            this.elapsed = 0;
        }

        public (bool isFinished, Vector3 quake) Update(float dT) {
            elapsed += dT;
            if (elapsed >= time || cT?.Cancelled == true) {
                done?.Invoke();
                return (true, Vector3.zero);
            } else {
                float m = magnitude(elapsed) * magMul * ScreenshakeMultiplier * SaveData.s.Screenshake;
                float deg = RNG.GetFloatOffFrame(0, 360);
                return (false, M.PolarToXY(m, deg));
            }
        }
    }

    private readonly DMCompactingArray<ShakeInProgress> shakers = new DMCompactingArray<ShakeInProgress>();
    public override void RegularUpdate() {
        Vector3 totalQuake = Vector3.zero;
        for (int ii = 0; ii < shakers.Count; ++ii) {
            if (!shakers.Data[ii].MarkedForDeletion) {
                var (finished, quake) = shakers[ii].Update(ETime.FRAME_TIME);
                if (finished)
                    shakers.Data[ii].MarkForDeletion();
                else
                    totalQuake += quake;
            }
        }
        tr.localPosition = baseLoc + totalQuake;
        shakers.Compact();
    }

    /// <summary>
    /// Note that this may call DONE earlier than TIME if it is cancelled by another SHAKE call.
    /// </summary>
    public void Shake(float time, FXY? magnitude, float magMul, ICancellee? cT = null, Action? done = null) {
        magnitude ??= (t => M.Sin(M.PI * (0.4f + 0.6f * t / time)));
        shakers.Add(new ShakeInProgress(time, magnitude, magMul, cT, done));
    }


}
}