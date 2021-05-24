using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Tweening;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using UnityEngine;

public class DebugTemp : CoroutineRegularUpdater {
    void Start() {
        Tween.RegisterLerper<Vector2>(Vector2.Lerp);
        Func<float, float> f = M.EInSine;
        var t = new Tweener<Vector2>(Vector2.one * -1, Vector2.one, 2f, v => transform.localPosition = v).WithEaser(M.EInSine);
        t.Then(t.Reverse()).Loop().Run(this);
    }
}