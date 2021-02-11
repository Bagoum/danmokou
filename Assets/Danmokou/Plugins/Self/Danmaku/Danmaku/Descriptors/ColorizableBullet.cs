using DMK.Behavior;
using DMK.Danmaku.Options;
using DMK.DMath;
using DMK.Scriptables;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Danmaku.Descriptors {
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