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
        BagoumLib.Mathematics.GenericOps.RegisterLerper<Vector2>(Vector2.Lerp);
        var t = new Tweener<Vector2>(Vector2.one * -1, Vector2.one, 2f, v => transform.localPosition = v, M.EInSine);
        t.Then(t.Reverse()).Loop().Run(this);
    }
}