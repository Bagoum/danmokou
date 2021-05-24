using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Pooling;
using TMPro;
using UnityEngine;

namespace Danmokou.Behavior.Display {
public class DropLabel : Pooled<DropLabel> {

    public static IGradient MakeGradient(Color c1, Color c2) =>
        ColorHelpers.FromKeys(new[] {
            new GradientColorKey(c1, 0.1f),
            new GradientColorKey(c2, 0.8f),
        }, DropLabel.defaultAlpha);

    public static readonly GradientAlphaKey[] defaultAlpha = {
        new GradientAlphaKey(0, 0),
        new GradientAlphaKey(1, 0.1f),
        new GradientAlphaKey(1, 0.7f),
        new GradientAlphaKey(0, 1f),
    };
    private Vector2 loc;
    private float time;
    private Vector2 velocity0;
    private Vector2 Velocity => velocity0 - new Vector2(0, time * 0.8f);
    private TextMeshPro text = null!;
    private LabelRequestContext ctx;

    private static ushort rendererPriority = 0;

    protected override void Awake() {
        base.Awake();
        this.text = GetComponent<TextMeshPro>();
    }

    public void Initialize(LabelRequestContext ctx_) {
        this.ctx = ctx_;
        text.sortingOrder = ++rendererPriority;
        velocity0 = ctx.speed * M.CosSinDeg(ctx.angle);
        tr.localPosition = loc = ctx.root + ctx.radius * M.CosSinDeg(ctx.angle);
        time = 0;
        text.text = ctx.text;
        SetColor();
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        time += ETime.FRAME_TIME;
        if (time > ctx.timeToLive) {
            PooledDone();
        } else {
            loc += Velocity * ETime.dT;
            tr.localPosition = loc;
            var scale = ctx.Scale(time / ctx.timeToLive);
            tr.localScale = new Vector3(scale, scale, scale);
            SetColor();
        }
    }

    private void SetColor() {
        text.color = ctx.color.Evaluate32(time / ctx.timeToLive);

    }
}
}
