using System;
using System.Collections;
using BagoumLib.Cancellation;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;

namespace Danmokou.Behavior.Display {
public class SpriteDisplayController: DisplayController {
    public SpriteRenderer sprite = null!;

    public override void OnLinkOrResetValues(bool isLink) {
        base.OnLinkOrResetValues(isLink);
        sprite.GetPropertyBlock(pb);
    }

    public override void SetMaterial(Material mat) {
        sprite.sharedMaterial = mat;
        sprite.GetPropertyBlock(pb);
    }

    public override void OnRender(bool isFirstFrame, Vector2 lastDesiredDelta) {
        base.OnRender(isFirstFrame, lastDesiredDelta);
        if (ETime.LastUpdateForScreen || isFirstFrame) {
            sprite.SetPropertyBlock(pb);
        }
    }

    public override void Show() {
        sprite.enabled = true;
        base.Show();
    }

    public override void Hide() {
        sprite.enabled = false;
        base.Hide();
    }

    public override void SetSortingOrder(int x) {
        sprite.sortingOrder = x;
    }

    public override void SetSprite(Sprite? s) {
        if (s != null) {
            sprite.sprite = s;
            pb.SetTexture(PropConsts.mainTex, s.texture);
        }
    }
    
    public override void FadeSpriteOpacity(BPY fader01, float over, ICancellee cT, Action done) {
        var tbpi = Beh.rBPI;
        tbpi.t = 0;
        sprite.color = sprite.color.WithA(fader01(tbpi));
        Beh.RunRIEnumerator(_FadeSpriteOpacity(fader01, tbpi, over, cT, done));
    }
    private IEnumerator _FadeSpriteOpacity(BPY fader01, ParametricInfo tbpi, float over, ICancellee cT, Action done) {
        if (cT.Cancelled) { done(); yield break; }
        Color c = sprite.color;
        for (tbpi.t = 0f; tbpi.t < over - ETime.FRAME_YIELD; tbpi.t += ETime.FRAME_TIME) {
            yield return null;
            if (cT.Cancelled) { break; } //Set to target and then leave
            tbpi.loc = Beh.rBPI.loc;
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