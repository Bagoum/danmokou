using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.Danmaku.Descriptors {
public interface ICollider {
    bool CheckCollision(Vector2 loc, Vector2 dir, float scale, Vector2 targetLoc, float targetRad);
    CollisionResult CheckGrazeCollision(Vector2 loc, Vector2 dir, float scale, in Hitbox target);
}

public class NoneCollider : ICollider {
    public bool CheckCollision(Vector2 loc, Vector2 dir, float scale, Vector2 targetLoc, float targetRad) =>
        false;
    public CollisionResult CheckGrazeCollision(Vector2 loc, Vector2 dir, float scale, in Hitbox target) =>
        CollisionResult.noColl;
}

public class CircleCollider : ICollider {
    private readonly float radius;

    public CircleCollider(float radius) {
        this.radius = radius;
    }

    public bool CheckCollision(Vector2 loc, Vector2 dir, float scale, Vector2 targetLoc, float targetRad) =>
        CollisionMath.CircleOnCircle(loc, radius * scale, targetLoc, targetRad);
    public CollisionResult CheckGrazeCollision(Vector2 loc, Vector2 dir, float scale, in Hitbox target) =>
        CollisionMath.GrazeCircleOnCircle(in target, loc, radius * scale);
}

public class RectCollider : ICollider {
    private readonly float halfRectX;
    private readonly float halfRectY;
    private readonly float maxDist2;
    
    public RectCollider(float halfRectX, float halfRectY) {
        this.halfRectX = halfRectX;
        this.halfRectY = halfRectY;
        maxDist2 = halfRectX * halfRectX + halfRectY * halfRectY;
    }

    public bool CheckCollision(Vector2 loc, Vector2 dir, float scale, Vector2 targetLoc, float targetRad) =>
        CollisionMath.CircleOnRect(targetLoc, targetRad, loc, halfRectX, halfRectY, maxDist2, scale, dir.x, dir.y);
    public CollisionResult CheckGrazeCollision(Vector2 loc, Vector2 dir, float scale, in Hitbox target) =>
        CollisionMath.GrazeCircleOnRect(in target, loc, halfRectX, halfRectY, maxDist2, scale, dir.x, dir.y);
}


public class LineCollider : ICollider {
    private readonly Vector2 pt1;
    private readonly Vector2 delta;
    private readonly float radius;
    private readonly float deltaMag2;
    private readonly float maxDist2;

    public LineCollider(Vector2 pt1, Vector2 pt2, float radius) {
        this.pt1 = pt1;
        delta = pt2 - pt1;
        deltaMag2 = delta.sqrMagnitude;
        this.radius = radius;
        var md = Mathf.Max(pt1.magnitude, pt2.magnitude) + radius;
        maxDist2 = md * md;
    }

    public bool CheckCollision(Vector2 loc, Vector2 dir, float scale, Vector2 targetLoc, float targetRad) =>
        CollisionMath.CircleOnRotatedSegment(targetLoc, targetRad, loc, radius, pt1,
            delta, scale, deltaMag2, maxDist2, dir.x, dir.y);
    public CollisionResult CheckGrazeCollision(Vector2 loc, Vector2 dir, float scale, in Hitbox target) =>
        CollisionMath.GrazeCircleOnRotatedSegment(in target, loc, radius, pt1,
            delta, scale, deltaMag2, maxDist2, dir.x, dir.y);
}


}