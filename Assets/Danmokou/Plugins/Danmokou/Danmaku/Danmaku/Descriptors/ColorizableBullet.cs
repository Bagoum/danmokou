﻿using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Danmaku.Descriptors {
public class ColorizableBullet : Bullet {

    public override void Initialize(StyleMetadata? style, RealizedBehOptions options,
        BehaviorEntity? parent, in Movement mov, ParametricInfo pi, out int layer) {
        if (style?.recolor != null) Colorize(style.recolor.GetOrLoadRecolor());
        base.Initialize(style, options, parent, in mov, pi, out layer);
    }

    protected virtual void Colorize(FrameAnimBullet.Recolor r) {
        SetMaterial(r.material);
        if (r.sprites == null) return;
        SetSprite(r.sprites[0].s);
    }

    public virtual void ColorizeOverwrite(FrameAnimBullet.Recolor r) => Colorize(r);

    protected virtual void SetSprite(Sprite s) {
        Dependent<DisplayController>().SetSprite(s);
    }
}
}