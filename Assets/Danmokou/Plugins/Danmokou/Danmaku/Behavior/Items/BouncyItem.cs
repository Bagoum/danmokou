using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public abstract class BouncyItem : Item {
    protected override bool Autocollectible => false;
    protected override bool Attractible => false;
    protected override float CollectRadiusBonus => 0.1f;
    protected override short RenderOffsetIndex => 7;
    protected virtual float Speed => 2f;
    protected virtual float LRUMargin => 0.3f;
    protected virtual float BottomMargin => 1.5f;
    protected virtual float StopBouncingAfter => 15f;

    private float Left => LocationHelpers.Left + LRUMargin;
    private float Top => LocationHelpers.Top - LRUMargin;
    private float Right => LocationHelpers.Right - LRUMargin;
    private float Bottom => LocationHelpers.Bot + BottomMargin;

    private Vector2 velocity;

    protected override Vector2 Velocity(float t) => velocity;

    public override void Initialize(Vector2 root, Vector2 targetOffset, PoC? collectionPoint = null) {
        base.Initialize(root, targetOffset, collectionPoint);
        float startAngle = 45f + RNG.GetInt(0, 4) * 90f;
        velocity = Speed * M.CosSinDeg(startAngle);
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        if (State == HomingState.NO && time < StopBouncingAfter) {
            if (loc.x > Right) {
                loc.x = 2 * Right - loc.x;
                velocity.x *= -1;
            } else if (loc.x < Left) {
                loc.x = 2 * Left - loc.x;
                velocity.x *= -1;
            }
            if (loc.y > Top) {
                loc.y = 2 * Top - loc.y;
                velocity.y *= -1;
            } else if (loc.y < Bottom) {
                loc.y = 2 * Bottom - loc.y;
                velocity.y *= -1;
            }
        }
    }
}
}