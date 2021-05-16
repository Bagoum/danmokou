using System;
using UnityEditor;
using UnityEngine;
using Danmokou.DMath;
using Danmokou.Scriptables;
using JetBrains.Annotations;

namespace Danmokou.Danmaku.Descriptors {
/// This script is primarily used to send simple-bullet collider info to BulletManager.
/// Alternatively, you can use it to debug collision algorithms.
/// You should not stick this directly on objects; it is not optimized at all.
public class GenericColliderInfo : MonoBehaviour {
    public enum ColliderType {
        Circle,
        Rectangle,
        RectPtColl,
        Line,
        Segments,
        None,
    }

    public enum DebugColliderType {
        None,
        AABB,
        WeakAABB
    }

    public ColliderType colliderType;
    [Header("Circle/Line/Segments")] 
    public float radius;
    [Header("Line")] 
    public Vector2 point1;
    public Vector2 point2;
    [Header("Line/Rect/Segments (Not yet implemented in AsCollider)")] 
    public float rotationDeg;
    [Header("Rect")] 
    public float rectHalfX;
    public float rectHalfY;
    [Header("Segments")]
    public int start;
    public int skip;
    public int end;
    public Vector2[] points = null!;

    public ICollider AsCollider => colliderType switch {
        ColliderType.Circle => new CircleCollider(radius),
        ColliderType.Rectangle => new RectCollider(rectHalfX, rectHalfY),
        ColliderType.Line => new LineCollider(point1, point2, radius),
        ColliderType.None => new NoneCollider(),
        _ => throw new Exception($"No AsCollider handling for type {colliderType}")
    };
    
    #if UNITY_EDITOR
    
    [Tooltip("Only fill for debugging in scene use")]
    public SOPlayerHitbox? target;
    public DebugColliderType debug;
    public void DoLiveCollisionTest() {
        Vector3 trp = transform.position;
        CollisionResult cr = new CollisionResult();
        var hitbox = target!.Hitbox;
        if (debug == DebugColliderType.AABB) {
            cr = new CollisionResult(false,
                CollisionMath.CircleOnAABB(
                    new AABB(trp, new Vector2(rectHalfX, rectHalfY))
                    , target.location, target.largeRadius));
        } else if (debug == DebugColliderType.WeakAABB) {
            cr = new CollisionResult(false,
                CollisionMath.WeakCircleOnAABB(-rectHalfX, -rectHalfY, rectHalfX, rectHalfY,
                    target.location.x - trp.x, target.location.y - trp.y, target.largeRadius));
        } else if (colliderType == ColliderType.Circle) {
            cr = CollisionMath.GrazeCircleOnCircle(hitbox, trp, radius);
        } else if (colliderType == ColliderType.Line) {
            float maxdist = Mathf.Max(point2.magnitude, point1.magnitude) + radius;
            cr = CollisionMath.GrazeCircleOnRotatedSegment(hitbox, trp, radius,
                point1, point2 - point1, 1f, (point2 - point1).sqrMagnitude, maxdist * maxdist,
                Mathf.Cos(rotationDeg * Mathf.PI / 180f), Mathf.Sin(rotationDeg * Mathf.PI / 180f));
        } else if (colliderType == ColliderType.Rectangle) {
            cr = CollisionMath.GrazeCircleOnRect(hitbox, trp, rectHalfX, rectHalfY, rectHalfX * rectHalfX + rectHalfY * rectHalfY, 1f,
                Mathf.Cos(rotationDeg * Mathf.PI / 180f), Mathf.Sin(rotationDeg * Mathf.PI / 180f));
        } else if (colliderType == ColliderType.RectPtColl) {
            cr = new CollisionResult(CollisionMath.PointInRect(target.location, new CRect(
                trp.x, trp.y,
                rectHalfX, rectHalfY, rotationDeg
            )), false);
        } else if (colliderType == ColliderType.Segments) {
            cr = CollisionMath.GrazeCircleOnSegments(hitbox, transform.position, points, start, skip, end, radius,
                Mathf.Cos(rotationDeg * Mathf.PI / 180f), Mathf.Sin(rotationDeg * Mathf.PI / 180f));
        }
        if (cr.graze) {
            Debug.Break();
        }
    }
    
    private void OnDrawGizmos() {
        Handles.color = Color.magenta;
        Vector2 p = transform.position;
        Handles.color = Color.red;
        if (colliderType == ColliderType.Circle) {
            Handles.DrawWireDisc(p, Vector3.forward, radius);
        } else if (colliderType == ColliderType.Line) {
            Vector2 d = point2 - point1;
            Vector2 tp1 = M.RotateVectorDeg(point1, rotationDeg);
            d = M.RotateVectorDeg(d, rotationDeg);
            Vector2 dt = M.RotateVectorDeg(radius * d.normalized, 90f);
            Handles.DrawLine(p + tp1, p + tp1 + d);
            Handles.DrawLine(p + tp1 + dt, p + tp1 + dt + d);
            Handles.DrawLine(p + tp1 - dt, p + tp1 - dt + d);
            Handles.DrawWireDisc(p + tp1, Vector3.forward, radius);
            Handles.DrawWireDisc(p + tp1 + d, Vector3.forward, radius);
        } else if (colliderType == ColliderType.Rectangle || colliderType == ColliderType.RectPtColl) {
            Vector2 c1 = p + M.RotateVectorDeg(new Vector2(rectHalfX, rectHalfY), rotationDeg);
            Vector2 c2 = p + M.RotateVectorDeg(new Vector2(rectHalfX, -rectHalfY), rotationDeg);
            Vector2 c3 =
                p + M.RotateVectorDeg(new Vector2(-rectHalfX, -rectHalfY), rotationDeg);
            Vector2 c4 = p + M.RotateVectorDeg(new Vector2(-rectHalfX, rectHalfY), rotationDeg);
            Handles.DrawLine(c1, c2);
            Handles.DrawLine(c2, c3);
            Handles.DrawLine(c1, c4);
            Handles.DrawLine(c4, c3);
        } else if (colliderType == ColliderType.Segments) {
            DrawGizmosForSegments(points, start, skip, end, p, radius, rotationDeg);
        }
    }

    public static void DrawGizmosForSegments(Vector2[] points, int start, int skip, int end, Vector2 basePos,
        float radius, float rotationDeg = 0f) {
        if (end <= start) return;
        Vector2 toRot;
        void DrawFromTo(int i1, int i2) {
            Vector2 fromRot = basePos + M.RotateVectorDeg(points[i1], rotationDeg);
            toRot = basePos + M.RotateVectorDeg(points[i2], rotationDeg);
            Handles.DrawLine(fromRot, toRot);
            Vector2 d = toRot - fromRot;
            Vector2 dt = M.RotateVectorDeg(d.normalized * radius, 90f);
            Handles.DrawLine(fromRot + dt, toRot + dt);
            Handles.DrawLine(fromRot - dt, toRot - dt);
            Handles.DrawWireDisc(fromRot, Vector3.forward, radius);
        }
        int ii = skip;
        var c = Handles.color;
        Handles.color = Color.white;
        for (; ii < start + skip; ii += skip) {
            DrawFromTo(ii - skip, ii);
        }
        ii = start + skip;
        Handles.color = c;
        for (; ii < end - 1; ii += skip) {
            DrawFromTo(ii - skip, ii);
        }
        DrawFromTo(ii - skip, end - 1);
        Handles.color = Color.white;
        DrawFromTo(end - 1, points.Length - 1);
        Handles.DrawWireDisc(toRot, Vector3.forward, radius);

    }
#endif
}
}