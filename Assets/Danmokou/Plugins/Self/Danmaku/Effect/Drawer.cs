using System.Collections;
using System.Collections.Generic;
using Danmaku;
using DMath;
using UnityEngine;

public abstract class Drawer : BehaviorEntity {
    private TP4 color;
    protected V2RV2 lastRV2 { get; private set; }

    public void Initialize(TP4 colorizer) {
        color = colorizer;
        sr.color = new Color(0, 0, 0, 0);
    }

    protected abstract V2RV2 GetLocScaleRot();
    protected override void RegularUpdateRender() {
        sr.color = ColorHelpers.V4C(color(bpi));
        bpi.loc = tr.position;
        lastRV2 = GetLocScaleRot();
        tr.localPosition = lastRV2.NV;
        //Scale specified in radius
        tr.localScale = new Vector3(lastRV2.rx / 0.5f, lastRV2.ry / 0.5f, 1);
        tr.localEulerAngles = new Vector3(0, 0, M.Mod(360, lastRV2.angle));
        base.RegularUpdateRender();
    }
}
