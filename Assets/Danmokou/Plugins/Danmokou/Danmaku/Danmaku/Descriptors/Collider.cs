using System.Runtime.CompilerServices;
using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.Danmaku.Descriptors {
/// <summary>
/// Interface for two-dimensional colliders that can collide against a circle.
/// </summary>
public interface ICollider {
    /// <summary>
    /// The maximum distance from this shape's center at which a collision can occur.
    /// </summary>
    float MaxRadius { get; }
    
    /// <summary>
    /// Check if this shape and a circle overlap.
    /// </summary>
    /// <param name="x">X-coordinate of this shape</param>
    /// <param name="y">Y-coordinate of this shape</param>
    /// <param name="dir">Orientation of this shape (as cos/sin pair)</param>
    /// <param name="scale">Scale of this shape</param>
    /// <param name="cx">X-coordinate of circle</param>
    /// <param name="cy">Y-coordinate of circle</param>
    /// <param name="cRad">Circle radius</param>
    /// <returns>True iff there is a collision</returns>
    bool CheckCollision(in float x, in float y, in Vector2 dir, in float scale, in float cx, in float cy, in float cRad);
    
    /// <summary>
    /// Check if this shape and a circular hitbox overlap.
    /// </summary>
    /// <param name="x">X-coordinate of this shape</param>
    /// <param name="y">Y-coordinate of this shape</param>
    /// <param name="dir">Orientation of this shape (as cos/sin pair)</param>
    /// <param name="scale">Scale of this shape</param>
    /// <param name="target">Hitbox information</param>
    CollisionResult CheckGrazeCollision(in float x, in float y, in Vector2 dir, in float scale, in Hurtbox target);
}

public class NoneCollider : ICollider {
    public float MaxRadius => 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CheckCollision(in float x, in float y, in Vector2 dir, in float scale, in float cx, in float cy, in float cRad) =>
        false;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CollisionResult CheckGrazeCollision(in float x, in float y, in Vector2 dir, in float scale, in Hurtbox target) =>
        CollisionMath.NoCollision;
}

public class CircleCollider : ICollider {
    public readonly float Radius;
    public virtual float MaxRadius => Radius;

    public CircleCollider(float radius) {
        this.Radius = radius;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CheckCollision(in float x, in float y, in Vector2 dir, in float scale, in float cx, in float cy, in float cRad) =>
        CollisionMath.CircleOnCircle(in cx, in cy, in cRad, in x, in y, Radius * scale);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CollisionResult CheckGrazeCollision(in float x, in float y, in Vector2 dir, in float scale, in Hurtbox target) => CollisionMath.GrazeCircleOnCircle(in target, in x, in y, in Radius, in scale);
}

public class ApproximatedCircleCollider : CircleCollider {
    public override float MaxRadius { get; }
    /// <summary>
    /// The measure of how non-circular this approximation is, as the ratio MaxRadius/Radius.
    /// <br/>1 for a circle approximated by itself, infinity for a line approximated by a point.
    /// </summary>
    public float Irregularity => MaxRadius / Radius;

    public ApproximatedCircleCollider(float radius, float maxRadius) : base(radius) {
        this.MaxRadius = maxRadius;
    }
}

public class RectCollider : ICollider {
    private readonly float halfRectX;
    private readonly float halfRectY;
    public readonly Vector2 halfRect;
    private readonly float maxDist2;
    public float MaxRadius => Mathf.Sqrt(maxDist2);

    public RectCollider(float halfRectX, float halfRectY) {
        this.halfRectX = halfRectX;
        this.halfRectY = halfRectY;
        halfRect = new(halfRectX, halfRectY);
        maxDist2 = halfRectX * halfRectX + halfRectY * halfRectY;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CheckCollision(in float x, in float y, in Vector2 dir, in float scale, in float cx, in float cy, in float cRad) =>
        CollisionMath.CircleOnRect(in cx, in cy, in cRad, in x, in y, in halfRectX, in halfRectY, in maxDist2, in scale, in dir.x, in dir.y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CollisionResult CheckGrazeCollision(in float x, in float y, in Vector2 dir, in float scale, in Hurtbox target) =>
        CollisionMath.GrazeCircleOnRect(in target, in x, in y, in halfRect, in maxDist2, in scale, in dir);
}


public class LineCollider : ICollider {
    private readonly Vector2 pt1;
    private readonly Vector2 delta;
    private readonly float radius;
    private readonly float deltaMag2;
    private readonly float maxDist2;
    public float MaxRadius => Mathf.Sqrt(Mathf.Max(pt1.sqrMagnitude, (pt1 + delta).sqrMagnitude)) + radius;

    public LineCollider(Vector2 pt1, Vector2 pt2, float radius) {
        this.pt1 = pt1;
        delta = pt2 - pt1;
        deltaMag2 = delta.sqrMagnitude;
        this.radius = radius;
        var md = Mathf.Max(pt1.magnitude, pt2.magnitude) + radius;
        maxDist2 = md * md;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CheckCollision(in float x, in float y, in Vector2 dir, in float scale, in float cx, in float cy, in float cRad) =>
        CollisionMath.CircleOnRotatedSegment(in cx, in cy, in cRad, in x, in y, in radius, in pt1,
            in delta, in scale, in deltaMag2, in maxDist2, in dir.x, in dir.y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CollisionResult CheckGrazeCollision(in float x, in float y, in Vector2 dir, in float scale, in Hurtbox target) =>
        CollisionMath.GrazeCircleOnRotatedSegment(in target, in x, in y, in radius, in pt1,
            in delta, in scale, in deltaMag2, in maxDist2, in dir);
}


}