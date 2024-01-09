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
        gameObject.layer = opts.layer ?? beh.DefaultLayer;
        pb.SetFloat(PropConsts.speed, opts.iterations / opts.totalTime);
        beh.rBPI.t = opts.initialTime;
    }

    public override void UpdateRender(bool isFirstFrame) {
        sprite.color = ColorHelpers.V4C(options.color(beh.rBPI));
        base.UpdateRender(isFirstFrame);
        if (options.cT.Cancelled) {
            InvokeCull();
        } else if (beh.rBPI.t > options.totalTime) {
            options.continuation?.Invoke();
            InvokeCull();
        }
    }

    public void InvokeCull() => beh.InvokeCull();
}
}