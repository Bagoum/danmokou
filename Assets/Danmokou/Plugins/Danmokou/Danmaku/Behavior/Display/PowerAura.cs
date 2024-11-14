using System;
using BagoumLib;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.Behavior.Display {
/// <summary>
/// Class for basic powerup effects constructed from a circular aura going in or out.
/// </summary>
public class PowerAura : SpriteDisplayController {
    private RealizedPowerAuraOptions options;

    protected override Vector3 BaseScale => options.scale.Try(out var s) ? new Vector3(s, s, s) : base.BaseScale;

    public void Initialize(in RealizedPowerAuraOptions opts) {
        options = opts;
        ISFXService.SFXService.Request(options.sfx);
        sprite.color = new Color(0, 0, 0, 0);
        gameObject.layer = opts.layer ?? Beh.DefaultLayer;
        pb.SetFloat(PropConsts.speed, opts.iterations / opts.totalTime);
        Beh.rBPI.t = opts.initialTime;
    }

    public override void OnRender(bool isFirstFrame, Vector2 lastDesiredDelta) {
        try {
            sprite.color = ColorHelpers.V4C(options.color(Beh.rBPI));
        } catch (Exception e) {
            int k = 5;
        }
        base.OnRender(isFirstFrame, lastDesiredDelta);
        if (options.cT.Cancelled) {
            InvokeCull();
        } else if (Beh.rBPI.t > options.totalTime) {
            options.continuation?.Invoke();
            InvokeCull();
        }
    }

    public void InvokeCull() => Beh.InvokeCull();
}
}