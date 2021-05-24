using System;
using System.Collections;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using BagoumLib.Tweening;
using Danmokou.Core;
using UnityEngine;
using static BagoumLib.Tweening.Tween;

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
}
}