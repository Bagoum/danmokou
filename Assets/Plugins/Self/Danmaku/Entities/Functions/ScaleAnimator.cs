using System;
using System.Collections;
using DMath;
using UnityEngine;

public class ScaleAnimator : TimeBoundAnimator {
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

    private Transform tr;

    public override void AssignTime(float total) {
        time1 = time1Ratio * total;
        time2 = time2Ratio * total;
        holdtime = total - time1 - time2;
        //Share(ref time1, out holdtime, ref time2, total);
    }

    public void AssignRatios(float? t1r, float? t2r) {
        time1Ratio = t1r ?? time1Ratio;
        time2Ratio = t2r ?? time2Ratio;
    }
    public void AssignScales(float start, float mid, float end) {
        startScale = start;
        midScale = mid;
        endScale = end;
    }

    private void Start() {
        tr = transform;
        StartCoroutine(DoScale());
    }

    private void AssignScale(float x) {
        tr.localScale = new Vector3(x, x, 1f);
    }

    private IEnumerator DoScale() {
        AssignScale(startScale);
        FXY e1 = EaseHelpers.GetFuncOrRemoteOrLinear(ease1);
        FXY e2 = EaseHelpers.GetFuncOrRemoteOrLinear(ease2);
        for (float t = 0; t < time1; t += ETime.dT) {
            if (cT.Cancelled) break;
            yield return null;
            AssignScale(startScale + (midScale - startScale) * e1(t/time1));
        }
        AssignScale(midScale);
        for (float t = 0; t < holdtime; t += ETime.dT) {
            if (cT.Cancelled) break;
            yield return null;
        }
        for (float t = 0; t < time2; t += ETime.dT) {
            if (cT.Cancelled) break;
            yield return null;
            AssignScale(midScale + (endScale - midScale) * e2(t/time2));
        }
        AssignScale(endScale);
        if (destroyOnDone) {
            Destroy(gameObject);
        }
    }

}