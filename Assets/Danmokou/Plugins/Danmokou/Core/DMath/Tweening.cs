using System;
using System.Collections;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using BagoumLib.Transitions;
using Danmokou.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static BagoumLib.Transitions.TransitionHelpers;

namespace Danmokou.DMath {
public static class Tweening {
    public static Tweener<Vector3> GoTo(this Transform tr, Vector3 loc, float time, 
        Easer? ease = null, ICancellee? cT = null) =>
        TweenTo(tr.localPosition, loc, time, x => tr.localPosition = x, ease, cT);
    public static Tweener<float> ScaleTo(this Transform tr, float target, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenTo(tr.localScale.x, target, time, x => tr.localScale = new Vector3(x, x, x), ease, cT);
    public static Tweener<Vector3> RotateTo(this Transform tr, Vector3 eulers, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenTo(tr.localEulerAngles, eulers, time, x => tr.localEulerAngles = x, ease, cT);
    public static Tweener<float> OpacityTo(this SpriteRenderer sr, float target, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenTo(sr.color.a, target, time, sr.SetAlpha, ease, cT);
    public static Tweener<Color> ColorTo(this SpriteRenderer sr, Color target, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenTo(sr.color, target, time, c => sr.color = c, ease, cT);
    public static Tweener<Vector3> ScaleBy(this Transform tr, float multiplier, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenBy(tr.localScale, multiplier, time, x => tr.localScale = x, ease, cT);
    
    
    public static Tweener<Vector3> GoTo(this ITransform tr, Vector3 loc, float time, 
        Easer? ease = null, ICancellee? cT = null) =>
        TweenTo(tr.position, loc, time, x => tr.position = x, ease, cT);
    public static DeltaTweener<Vector3> TranslateBy(this ITransform tr, Vector3 delta, float time, 
        Easer? ease = null, ICancellee? cT = null) =>
        TweenDelta(tr.position, delta, time, x => tr.position = x, ease, cT);
    public static Tweener<float> ScaleTo(this ITransform tr, float target, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenTo(tr.scale.x, target, time, x => tr.scale = new Vector3(x, x, x), ease, cT);
    public static Tweener<Vector3> ScaleTo(this ITransform tr, Vector3 target, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenTo(tr.scale, target, time, x => tr.scale = x, ease, cT);
    public static Tweener<float> ScaleBy(this ITransform tr, float target, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenBy(tr.scale.x, target, time, x => tr.scale = new Vector3(x, x, x), ease, cT);
    public static Tweener<Vector3> RotateTo(this ITransform tr, Vector3 eulers, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenTo(tr.rotation.eulerAngles, eulers, time, x => tr.rotation = Quaternion.Euler(x), ease, cT);
    public static DeltaTweener<Vector3> RotateBy(this ITransform tr, Vector3 eulerDelta, float time, 
        Easer? ease = null, ICancellee? cT = null)
        => TweenDelta(tr.rotation.eulerAngles, eulerDelta, time, x => tr.rotation = Quaternion.Euler(x), ease, cT);

    public static Tweener<float> FadeTo(this IStyle style, float opacity, float time,
        Easer? ease = null, ICancellee? cT = null)
        => TweenTo(style.opacity.value, opacity, time, x => style.opacity = x, ease, cT);

    public static Tweener<float> FadeTo(this VisualElement tr, float opacity, float time, 
        Easer? ease = null, ICancellee? cT = null) =>
        TweenTo(tr.style.opacity.keyword == StyleKeyword.Null ? 1 : tr.style.opacity.value, 
            opacity, time, x => tr.style.opacity = x, ease, cT);
}
}