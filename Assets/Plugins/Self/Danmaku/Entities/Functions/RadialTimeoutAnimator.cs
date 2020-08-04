using System;
using System.Collections;
using DMath;
using UnityEngine;

public class RadialTimeoutAnimator : TimeBoundAnimator {
    public float time;

    public float lerpInTimeRatio;
    public float endEarlyRatio;
    private Transform tr;
    private SpriteRenderer sr;
    private MaterialPropertyBlock pb;

    public override void AssignTime(float total) {
        time = total;
    }

    private void Awake() {
        (sr = GetComponent<SpriteRenderer>()).GetPropertyBlock(pb = new MaterialPropertyBlock());
        pb.SetFloat(PropConsts.fillRatio, 0);
        sr.SetPropertyBlock(pb);
    }

    private void Start() {
        StartCoroutine(DoRadialize());
    }

    private IEnumerator DoRadialize() {
        float t = 0;
        var lit = time * lerpInTimeRatio;
        for (; t < time * lerpInTimeRatio; t += ETime.dT) {
            pb.SetFloat(PropConsts.fillRatio, M.SinDeg(90 * t / lit));
            sr.SetPropertyBlock(pb);
            if (cT.IsCancellationRequested) break;
            yield return null;
        }
        for (; t < time; t += ETime.dT) {
            pb.SetFloat(PropConsts.fillRatio, 1 - (t - lit) / (time * (1 - endEarlyRatio) - lit));
            sr.SetPropertyBlock(pb);
            if (cT.IsCancellationRequested) break;
            yield return null;
        }
        if (destroyOnDone) {
            Destroy(gameObject);
        }
    }

}