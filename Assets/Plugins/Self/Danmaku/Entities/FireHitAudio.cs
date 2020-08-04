using System.Collections;
using System.Collections.Generic;
using DMath;
using UnityEngine;

public class FireHitAudio : ContinuousAudio {
    protected override FXY SpeedScale => x => base.SpeedScale(x) * 
        LowHPMultiplier(Counter.ReadResetLowHPBoss()) *
        ShotgunMultiplier(Counter.ReadResetShotgun());

    private static float LowHPMultiplier(int lowHPRequests) => (lowHPRequests > 0) ? 1.3f : 1f;
    private static float ShotgunMultiplier(float ratio) => Mathf.Lerp(1f, 0.7f, ratio);
}