using System;
using BagoumLib.Cancellation;
using Danmokou.Core;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Behavior.Display {
public class GroupDisplayController : DisplayController {
    public DisplayController recvSprite = null!;
    public DisplayController[] all = null!;

    public override void LinkAndReset(BehaviorEntity parent) {
        base.LinkAndReset(parent);
        for (int ii = 0; ii < all.Length; ++ii) all[ii].LinkAndReset(parent);
    }

    public override MaterialPropertyBlock CreatePB() {
        return new();
    }

    public override void SetMaterial(Material mat) {
        recvSprite.SetMaterial(mat);
    }

    public override void UpdateStyle(BehaviorEntity.BEHStyleMetadata style) {
        for (int ii = 0; ii < all.Length; ++ii) all[ii].UpdateStyle(style);
    }

    public override void Show() {
        for (int ii = 0; ii < all.Length; ++ii) all[ii].Show();
    }
    public override void Hide() {
        for (int ii = 0; ii < all.Length; ++ii) all[ii].Hide();
    }

    public override void SetSortingOrder(int x) {
        for (int ii = 0; ii < all.Length; ++ii) all[ii].SetSortingOrder(x);
    }

    public override void SetProperty(int id, float val) => recvSprite.SetProperty(id, val);

    public override void UpdateRender(bool isFirstFrame) {
        base.UpdateRender(isFirstFrame);
        for (int ii = 0; ii < all.Length; ++ii) all[ii].UpdateRender(isFirstFrame);
    }

    public override void FaceInDirection(Vector2 delta) {
        base.FaceInDirection(delta);
        for (int ii = 0; ii < all.Length; ++ii) all[ii].FaceInDirection(delta);
    }

    public override void SetSprite(Sprite? s) {
        recvSprite.SetSprite(s);
    }

    public override void FadeSpriteOpacity(BPY fader01, float over, ICancellee cT, Action done) =>
        recvSprite.FadeSpriteOpacity(fader01, over, cT, done);

    public override void Animate(AnimationType typ, bool loop, Action? done) =>
        recvSprite.Animate(typ, loop, done);
}
}
