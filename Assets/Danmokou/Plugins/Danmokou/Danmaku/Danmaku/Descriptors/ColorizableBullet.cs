using Danmokou.Behavior;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Danmaku.Descriptors {
public class ColorizableBullet : Bullet {

    public override void Initialize(BEHStyleMetadata? style, RealizedBehOptions options,
        BehaviorEntity? parent, Movement mov, ParametricInfo pi, SOPlayerHitbox _target,
        out int layer) {
        if (style?.recolor != null) Colorize(style.recolor.GetOrLoadRecolor());
        base.Initialize(style, options, parent, mov, pi, _target, out layer);
    }

    protected virtual void Colorize(FrameAnimBullet.Recolor r) {
        SetMaterial(r.material);
        if (r.sprites == null) return;
        SetSprite(r.sprites[0].s);
    }

    public virtual void ColorizeOverwrite(FrameAnimBullet.Recolor r) => Colorize(r);

    protected virtual void SetSprite(Sprite s) {
        displayer!.SetSprite(s);
    }
}
}