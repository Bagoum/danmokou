using UnityEditor;
using UnityEngine;
using DMath;
using JetBrains.Annotations;
using Collision = DMath.Collision;

namespace Danmaku {
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
        None
    }

    public ColliderType colliderType;
    [Tooltip("Player bullet collision, does not need to be accurate.")]
    public float effectiveCircleRadius;
    [Header("Circle/Line/Rect")] public float scale;
    [Header("Circle/Line/Segments")] public float radius;
    [Header("Line")] public Vector2 point1;
    public Vector2 point2;
    [Header("Line/Rect/Segments")] public float rotationDeg;
    [Header("Rect")] public float rectHalfX;
    public float rectHalfY;
    [Header("Segments")] public int start;
    public int skip;
    public int end;
    public Vector2[] points;
    [Tooltip("Only fill for debugging in scene use")] [CanBeNull]
    public SOCircleHitbox target;

    // Update is called once per frame
    void Update() {
        Vector3 trp = transform.position;
        CollisionResult cr = new CollisionResult();
        if (colliderType == ColliderType.Circle) {
            cr = Collision.GrazeCircleOnCircle(target, trp, radius * scale);
        } else if (colliderType == ColliderType.Line) {
            float maxdist = Mathf.Max(point2.magnitude, point1.magnitude) + radius;
            cr = Collision.GrazeCircleOnRotatedSegment(target, trp, radius,
                point1, point2 - point1, scale, (point2 - point1).sqrMagnitude, maxdist * maxdist,
                Mathf.Cos(rotationDeg * Mathf.PI / 180f), Mathf.Sin(rotationDeg * Mathf.PI / 180f));
        } else if (colliderType == ColliderType.Rectangle) {
            cr = Collision.GrazeCircleOnRect(target, trp, rectHalfX, rectHalfY, rectHalfX * rectHalfX + rectHalfY * rectHalfY, scale,
                Mathf.Cos(rotationDeg * Mathf.PI / 180f), Mathf.Sin(rotationDeg * Mathf.PI / 180f));
        } else if (colliderType == ColliderType.RectPtColl) {
            cr = new CollisionResult(Collision.PointInRect(target.location, new CRect(
                trp.x, trp.y,
                rectHalfX, rectHalfY, rotationDeg
            )), false);
        } else if (colliderType == ColliderType.Segments) {
            cr = Collision.GrazeCircleOnSegments(target, transform.position, points, start, skip, end, radius,
                Mathf.Cos(rotationDeg * Mathf.PI / 180f), Mathf.Sin(rotationDeg * Mathf.PI / 180f));
        }
        if (cr.graze) {
            Debug.Break();
        }
    }
#if UNITY_EDITOR
    private void OnDrawGizmos() {
        Handles.color = Color.magenta;
        Vector2 p = transform.position;
        Handles.DrawWireDisc(p, Vector3.forward, effectiveCircleRadius * scale);
        Handles.color = Color.red;
        if (colliderType == ColliderType.Circle) {
            Handles.DrawWireDisc(p, Vector3.forward, radius * scale);
        } else if (colliderType == ColliderType.Line) {
            Vector2 d = point2 - point1;
            Vector2 tp1 = M.RotateVectorDeg(point1 * scale, rotationDeg);
            d = M.RotateVectorDeg(d * scale, rotationDeg);
            Vector2 dt = M.RotateVectorDeg(scale * radius * d.normalized, 90f);
            Handles.DrawLine(p + tp1, p + tp1 + d);
            Handles.DrawLine(p + tp1 + dt, p + tp1 + dt + d);
            Handles.DrawLine(p + tp1 - dt, p + tp1 - dt + d);
            Handles.DrawWireDisc(p + tp1, Vector3.forward, scale * radius);
            Handles.DrawWireDisc(p + tp1 + d, Vector3.forward, scale * radius);
        } else if (colliderType == ColliderType.Rectangle || colliderType == ColliderType.RectPtColl) {
            Vector2 c1 = p + M.RotateVectorDeg(new Vector2(scale * rectHalfX, scale * rectHalfY), rotationDeg);
            Vector2 c2 = p + M.RotateVectorDeg(new Vector2(scale * rectHalfX, scale * -rectHalfY), rotationDeg);
            Vector2 c3 =
                p + M.RotateVectorDeg(new Vector2(scale * -rectHalfX, scale * -rectHalfY), rotationDeg);
            Vector2 c4 = p + M.RotateVectorDeg(new Vector2(scale * -rectHalfX, scale * rectHalfY), rotationDeg);
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