using System.Collections;
using System.Collections.Generic;
using Danmaku;
using DMath;
using UnityEngine;
using Collision = DMath.Collision;

public class RectDrawer : Drawer {
    private BPRV2 locate;

    public void Initialize(TP4 colorizer, BPRV2 locater) {
        base.Initialize(colorizer);
        locate = locater;
    }

    protected override V2RV2 GetLocScaleRot() => locate(bpi);

    protected override bool Contains(Vector2 pt) => Collision.PointInRect(pt, lastRV2);
}
