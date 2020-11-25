using System;
using System.Collections;
using DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmaku {
public class SpriteDisplayController: DisplayController {
    public SpriteRenderer sprite;
    public override MaterialPropertyBlock CreatePB() {
        var m = new MaterialPropertyBlock();
        sprite.GetPropertyBlock(m);
        return m;
    }

    public override void UpdateRender() {
        base.UpdateRender();
        sprite.SetPropertyBlock(pb);
    }

    public override void SetSortingOrder(int x) {
        sprite.sortingOrder = x;
    }

    protected void SetSprite([CanBeNull] Sprite s) {
        if (s != null) {
            sprite.sprite = s;
            pb.SetTexture(PropConsts.mainTex, s.texture);
        }
    }
    
    public override void FadeSpriteOpacity(BPY fader01, float over, ICancellee cT, Action done) {
        Color c = sprite.color;
        var tbpi = ParametricInfo.WithRandomId(beh.rBPI.loc, beh.rBPI.index);
        c.a = fader01(tbpi);
        sprite.color = c;
        beh.RunRIEnumerator(_FadeSpriteOpacity(fader01, tbpi, over, cT, done));
    }
    private IEnumerator _FadeSpriteOpacity(BPY fader01, ParametricInfo tbpi, float over, ICancellee cT, Action done) {
        if (cT.Cancelled) { done(); yield break; }
        Color c = sprite.color;
        for (tbpi.t = 0f; tbpi.t < over - ETime.FRAME_YIELD; tbpi.t += ETime.FRAME_TIME) {
            yield return null;
            if (cT.Cancelled) { break; } //Set to target and then leave
            tbpi.loc = beh.rBPI.loc;
            c.a = fader01(tbpi);
            sprite.color = c;
        }
        tbpi.t = over;
        c.a = fader01(tbpi);
        sprite.color = c;
        done();
    }

}
}