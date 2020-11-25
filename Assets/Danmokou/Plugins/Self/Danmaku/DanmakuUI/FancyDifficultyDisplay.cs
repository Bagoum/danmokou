using System;
using System.Collections;
using System.Collections.Generic;
using Danmaku;
using DMath;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public abstract class FancyDisplay : CoroutineRegularUpdater {
    protected Transform tr;
    protected SpriteRenderer sr;
    public float selectedScale = 1f;
    public float unselectedScale = 1f;
    
    [CanBeNull] protected Cancellable canceller;
    
    protected virtual void Awake() {
        tr = transform;
        sr = GetComponent<SpriteRenderer>();
    }

    public void Show(bool show) => gameObject.SetActive(show);


    private IEnumerator Ease<T>(T start, T end, Action<T> apply, 
        Func<T, T, float, T> lerp, float time, FXY ease, ICancellee cT) {
        for (float t = 0; t < time; t += ETime.FRAME_TIME) {
            apply(lerp(start, end, ease(t / time)));
            if (cT.Cancelled) yield break;
            yield return null;
        }
        apply(end);
    }
    protected IEnumerator GoTo(Vector3 loc, float time, FXY ease, ICancellee cT)
        => Ease(tr.localPosition, loc, x => tr.localPosition = x, Vector3.Lerp, time, ease, cT);
    protected IEnumerator ScaleTo(float target, float time, FXY ease, ICancellee cT)
        => Ease(tr.localScale.x, target, x => tr.localScale = new Vector3(x, x, x), Mathf.Lerp, time, ease, cT);
    protected IEnumerator OpacityTo(float target, float time, FXY ease, ICancellee cT)
        => Ease(sr.color.a, target, sr.SetAlpha, Mathf.Lerp, time, ease, cT);
}

public class FancyDifficultyDisplay : FancyDisplay {
    
    private static readonly Vector2 axis = new Vector2(2.4f, -1.6f).normalized;

    public void SetRelative(int thisIndex, int selectedIndex, int total, bool first) {
        int center = total / 2;
        var selLoc = axis * ((selectedIndex - center) * 0.8f);
        var dist = Mathf.Abs(thisIndex - selectedIndex);
        var effectiveDist = Mathf.Sign(thisIndex - selectedIndex) * Mathf.Pow(dist, 0.6f);
        var myLoc = selLoc + axis * (effectiveDist * 4.2f);
        var isSel = thisIndex == selectedIndex;
        var scale = isSel ? selectedScale : unselectedScale;
        var alpha = isSel ? 1 : 0.7f;
        canceller?.Cancel();
        canceller = new Cancellable();
        float time = 0.4f;
        if (first) {
            tr.localPosition = myLoc;
            tr.localScale = new Vector3(scale, scale, scale);
            sr.SetAlpha(alpha);
        } else {
            RunDroppableRIEnumerator(GoTo(myLoc, time, M.EOutSine, canceller));
            RunDroppableRIEnumerator(ScaleTo(scale, time, M.EOutSine, canceller));
            RunDroppableRIEnumerator(OpacityTo(alpha, time, M.EOutSine, canceller));
        }
    }

}