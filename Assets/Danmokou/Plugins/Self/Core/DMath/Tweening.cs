using System;
using System.Collections;
using DMK.Core;
using UnityEngine;

namespace DMK.DMath {
public static class Tweening {
    public static readonly FXY Identity = x => x;
    private static IEnumerator Ease<T>(T start, T end, Action<T> apply, 
        Func<T, T, float, T> lerp, float time, FXY? ease = null, ICancellee? cT = null) {
        if (cT?.Cancelled ?? false) yield break;
        ease ??= Identity;
        for (float t = 0; t < time; t += ETime.FRAME_TIME) {
            apply(lerp(start, end, ease(t / time)));
            yield return null;
            if (cT?.Cancelled ?? false) yield break;
        }
        apply(end);
    }

    private static IEnumerator Ease(float start, float end, Action<float> apply, float time,
        FXY? ease = null, ICancellee? cT = null) =>
        Ease(start, end, apply, Mathf.LerpUnclamped, time, ease, cT);
    private static IEnumerator Ease(Vector3 start, Vector3 end, Action<Vector3> apply, float time,
        FXY? ease = null, ICancellee? cT = null) =>
        Ease(start, end, apply, Vector3.LerpUnclamped, time, ease, cT);
    
    
    public static IEnumerator GoTo(this Transform tr, Vector3 loc, float time, 
        FXY? ease = null, ICancellee? cT = null)
        => Ease(tr.localPosition, loc, x => tr.localPosition = x, time, ease, cT);
    public static IEnumerator ScaleTo(this Transform tr, float target, float time, 
        FXY? ease = null, ICancellee? cT = null)
        => Ease(tr.localScale.x, target, x => tr.localScale = new Vector3(x, x, x), time, ease, cT);
    public static IEnumerator RotateTo(this Transform tr, Vector3 eulers, float time, 
        FXY? ease = null, ICancellee? cT = null)
        => Ease(tr.localEulerAngles, eulers, x => tr.localEulerAngles = x, time, ease, cT);
    public static IEnumerator OpacityTo(this SpriteRenderer sr, float target, float time, 
        FXY? ease = null, ICancellee? cT = null)
        => Ease(sr.color.a, target, sr.SetAlpha, time, ease, cT);
    public static IEnumerator ColorTo(this SpriteRenderer sr, Color target, float time, 
        FXY? ease = null, ICancellee? cT = null)
        => Ease(sr.color, target, c => sr.color = c, Color.LerpUnclamped, time, ease, cT);
    
    
    public static IEnumerator ScaleBy(this Transform tr, float multiplier, float time, 
        FXY? ease = null, ICancellee? cT = null)
        => Ease(tr.localScale, tr.localScale * multiplier, x => tr.localScale = x, time, ease, cT);
}
}