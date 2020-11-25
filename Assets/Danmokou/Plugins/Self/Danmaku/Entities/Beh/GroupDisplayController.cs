
using System;
using Danmaku;
using DMath;
using JetBrains.Annotations;
using UnityEngine;

public class GroupDisplayController : DisplayController {
    public DisplayController recvSprite;
    public DisplayController[] all;
    
    public override void ResetV(BehaviorEntity parent) {
        base.ResetV(parent);
        for (int ii = 0; ii < all.Length; ++ii) all[ii].ResetV(parent);
    }
    public override MaterialPropertyBlock CreatePB() {
        return new MaterialPropertyBlock();
    }

    public override void Hide() {
        for (int ii = 0; ii < all.Length; ++ii) all[ii].Hide();
    }

    public override void SetSortingOrder(int x) {
        for (int ii = 0; ii < all.Length; ++ii) all[ii].SetSortingOrder(x);
    }
    
    public override void UpdateRender() {
        base.UpdateRender();
        for (int ii = 0; ii < all.Length; ++ii) all[ii].UpdateRender();
    }

    public override void FadeSpriteOpacity(BPY fader01, float over, ICancellee cT, Action done) =>
        recvSprite.FadeSpriteOpacity(fader01, over, cT, done);

    public override void Animate(AnimationType typ, bool loop, [CanBeNull] Action done) =>
        recvSprite.Animate(typ, loop, done);

}
