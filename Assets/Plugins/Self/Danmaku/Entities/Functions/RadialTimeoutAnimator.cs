using System;
using System.Collections;
using DMath;
using UnityEngine;

/// <summary>
/// Graphical only. Not on RU loop.
/// </summary>
public class RadialTimeoutAnimator : TimeBoundAnimator {
    private float time;
    public float startScale = 0f;
    public float midScale = 3f;
    public float endScale = 0f;
    public float time1Ratio = 0.4f;
    public float time2Ratio = 0.3f;
    private float time1;
    private float time2;
    private float holdtime = 0f;
    public string ease1;
    public string ease2;

    public float lerpInTimeRatio;
    public float endEarlyRatio;
    private Transform tr;
    private SpriteRenderer sr;
    private MaterialPropertyBlock pb;

    public override void Initialize(ICancellee cT, float total) {
        time = total;
        time1 = time * time1Ratio;
        time2 = time * time2Ratio;
        RunDroppableRIEnumerator(DoRadialize(cT));
        RunDroppableRIEnumerator(DoScale(cT));
    }

    private void Awake() {
        (sr = GetComponent<SpriteRenderer>()).GetPropertyBlock(pb = new MaterialPropertyBlock());
        pb.SetFloat(PropConsts.fillRatio, 0);
        sr.SetPropertyBlock(pb);
        tr = transform;
    }


    private void AssignScale(float x) {
        tr.localScale = new Vector3(x, x, 1f);
    }
    private IEnumerator DoScale(ICancellee cT) {
        AssignScale(startScale);
        FXY e1 = EaseHelpers.GetFuncOrRemoteOrLinear(ease1);
        FXY e2 = EaseHelpers.GetFuncOrRemoteOrLinear(ease2);
        for (float t = 0; t < time1; t += ETime.FRAME_TIME) {
            if (cT.Cancelled) break;
            yield return null;
            AssignScale(startScale + (midScale - startScale) * e1(t/time1));
        }
        AssignScale(midScale);
        for (float t = 0; t < holdtime; t += ETime.FRAME_TIME) {
            if (cT.Cancelled) break;
            yield return null;
        }
        for (float t = 0; t < time2; t += ETime.FRAME_TIME) {
            if (cT.Cancelled) break;
            yield return null;
            AssignScale(midScale + (endScale - midScale) * e2(t/time2));
        }
        AssignScale(endScale);
    }
    private IEnumerator DoRadialize(ICancellee cT) {
        float t = 0;
        var lit = time * lerpInTimeRatio;
        for (; t < time * lerpInTimeRatio; t += ETime.FRAME_TIME) {
            pb.SetFloat(PropConsts.fillRatio, M.SinDeg(90 * t / lit));
            sr.SetPropertyBlock(pb);
            if (cT.Cancelled) break;
            yield return null;
        }
        for (; t < time; t += ETime.FRAME_TIME) {
            pb.SetFloat(PropConsts.fillRatio, 1 - (t - lit) / (time * (1 - endEarlyRatio) - lit));
            sr.SetPropertyBlock(pb);
            if (cT.Cancelled) break;
            yield return null;
        }
        if (destroyOnDone) {
            Destroy(gameObject);
        }
    }

}