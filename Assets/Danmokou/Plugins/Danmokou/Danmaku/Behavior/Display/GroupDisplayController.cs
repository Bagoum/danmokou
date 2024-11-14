using System;
using BagoumLib.Cancellation;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Behavior.Display {

public interface IMultiDisplayController {
    BehaviorEntity Beh { get; }
}

public class GroupDisplayController : DisplayController, IMultiDisplayController {
    public DisplayController recvSprite = null!;
    public DisplayController[] all = null!;

    protected override void Awake() {
        foreach (var controller in all)
            controller.IsPartOf(this);
        base.Awake();
    }

    public override void OnLinkOrResetValues(bool isLink) {
        base.OnLinkOrResetValues(isLink);
        for (int ii = 0; ii < all.Length; ++ii) all[ii].OnLinkOrResetValues(isLink);
    }

    public override void SetMaterial(Material mat) {
        recvSprite.SetMaterial(mat);
    }

    public override void StyleChanged(BehaviorEntity.StyleMetadata style) {
        for (int ii = 0; ii < all.Length; ++ii) all[ii].StyleChanged(style);
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

    public override void OnRender(bool isFirstFrame, Vector2 lastDesiredDelta) {
        base.OnRender(isFirstFrame, lastDesiredDelta);
        for (int ii = 0; ii < all.Length; ++ii) all[ii].OnRender(isFirstFrame, lastDesiredDelta);
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

    public override void Culled(bool allowFinalize, Action done) {
        foreach (var c in all)
            c.Culled(allowFinalize, c == recvSprite ? done : WaitingUtils.NoOp);
        base.Culled(allowFinalize, WaitingUtils.NoOp);
    }
}
}
