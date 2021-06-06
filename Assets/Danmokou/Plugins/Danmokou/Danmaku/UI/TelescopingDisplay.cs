using System;
using System.Collections;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.UI {
public abstract class FancyDisplay : CoroutineRegularUpdater {
    protected Transform tr = null!;
    protected SpriteRenderer sr = null!;
    public float selectedScale = 1f;
    public float unselectedScale = 1f;
    
    protected Cancellable? canceller;
    
    protected virtual void Awake() {
        tr = transform;
        sr = GetComponent<SpriteRenderer>();
    }

    public void Show(bool show) => gameObject.SetActive(show);
}

public class TelescopingDisplay : FancyDisplay {
    public void SetRelative(Vector2 baseLoc, Vector2 axis, int thisIndex, int selectedIndex, int total, bool setImmediate, bool locked = false) {
        int center = total / 2;
        var selLoc = axis * ((selectedIndex - center) * 0.8f);
        var dist = Mathf.Abs(thisIndex - selectedIndex);
        var effectiveDist = Mathf.Sign(thisIndex - selectedIndex) * Mathf.Pow(dist, 0.6f);
        var myLoc = baseLoc + selLoc + axis * (effectiveDist * 4.2f);
        var isSel = thisIndex == selectedIndex;
        var scale = isSel ? selectedScale : unselectedScale;
        var alpha = isSel ? 1 : 0.7f;
        var color = (locked ? Color.gray : Color.white).WithA(alpha);
        canceller?.Cancel();
        canceller = new Cancellable();
        float time = 0.4f;
        if (setImmediate) {
            tr.localPosition = myLoc;
            tr.localScale = new Vector3(scale, scale, scale);
            sr.color = color;
        } else {
            tr.GoTo(myLoc, time, M.EOutSine, canceller).Run(this, new CoroutineOptions(true));
            tr.ScaleTo(scale, time, M.EOutSine, canceller).Run(this, new CoroutineOptions(true));
            sr.ColorTo(color, time, M.EOutSine, canceller).Run(this, new CoroutineOptions(true));
        }
    }
}
}