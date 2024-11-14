using System;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.Player {
public class PlayerHPBarDisplay: RegularUpdater {
    public PlayerController player = null!;
    public SpriteRenderer renderTo = null!;
    private MaterialPropertyBlock pb = null!;

    private PushLerper<float> DisplayRatio = new(1f, (x, y, t) => (float)M.Lerp(x, y, 1-Math.Exp(-14 * t)));

    private void Awake() {
        renderTo.GetPropertyBlock(pb = new());
    }

    public override void RegularUpdate() {
        var ft = GameManagement.Instance.BasicF;
        DisplayRatio.PushIfNotSame(ft.Lives.Value*1f/ft.StartLives);
        DisplayRatio.Update(ETime.FRAME_TIME);
        pb.SetColor(PropConsts.fillColor, player.Ship.uiColor);
        pb.SetFloat(PropConsts.FillRatio, DisplayRatio);
        renderTo.SetPropertyBlock(pb);
    }

    private bool takeHitNextFrame;
    public override void RegularUpdateCollision() {
        base.RegularUpdateCollision();
        if (takeHitNextFrame) {
            player.TakeHit();
            takeHitNextFrame = false;
        }
    }

    [ContextMenu("Take hit")]
    public void TakeHit() => takeHitNextFrame = true;
}
}