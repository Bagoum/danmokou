using DMK.Core;
using DMK.Danmaku.Options;
using DMK.DMath;
using DMK.Graphics;
using DMK.Services;
using UnityEngine;

namespace DMK.Behavior.Display {
/// <summary>
/// Class for basic powerup effects constructed from a circular aura going in or out.
/// </summary>
public class PowerAura : SpriteDisplayController {
    private RealizedPowerAuraOptions options;

    protected override Vector3 GetScale => options.scale.Try(out var s) ? new Vector3(s, s, s) : base.GetScale;

    public void Initialize(in RealizedPowerAuraOptions opts) {
        options = opts;
        DependencyInjection.SFXService.Request(options.sfx);
        sprite.color = new Color(0, 0, 0, 0);
        gameObject.layer = opts.layer ?? beh.DefaultLayer;
        pb.SetFloat(PropConsts.speed, opts.iterations / opts.time);
    }

    public override void UpdateRender() {
        sprite.color = ColorHelpers.V4C(options.color(beh.rBPI));
        base.UpdateRender();
        if (options.cT.Cancelled) {
            InvokeCull();
        } else if (beh.rBPI.t > options.time) {
            options.continuation?.Invoke();
            InvokeCull();
        }
    }

    public void InvokeCull() => beh.InvokeCull();
}
}