using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;

namespace DMath {
public readonly struct CollisionResult {
    public readonly bool collide;
    public readonly bool graze;

    public CollisionResult(bool collide, bool graze) {
        this.collide = collide;
        this.graze = graze;
    }
    public static readonly CollisionResult noColl = new CollisionResult(false, false);
}

public static class Collision {
    private static readonly Type t = typeof(Collision);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnCircle(Vector2 c1t, Vector2 c2t, float c1rc2r) {
        c2t.x -= c1t.x;
        c2t.y -= c1t.y;
        return c2t.x * c2t.x + c2t.y * c2t.y < c1rc2r * c1rc2r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnCircle(Vector2 c1t, float c1r, Vector2 c2t, float c2r) {
        c2t.x -= c1t.x;
        c2t.y -= c1t.y;
        c2r += c1r;
        return c2t.x * c2t.x + c2t.y * c2t.y < c2r * c2r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnCircle(SOCircle c1, Vector2 c2t, float c2r) {
        c2t.x -= c1.location.x;
        c2t.y -= c1.location.y;
        c2r += c1.radius;
        return c2t.x * c2t.x + c2t.y * c2t.y < c2r * c2r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnPoint(Vector2 c1, float r, Vector2 c2) {
        c2.x -= c1.x;
        c2.y -= c1.y;
        return c2.x * c2.x + c2.y * c2.y < r * r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnCircle(SOCircle c1, Vector2 c2t, float c2r) {
        c2t.x -= c1.location.x;
        c2t.y -= c1.location.y;
        float lr = c2r + c1.largeRadius;
        c2r += c1.radius;
        float d2 = c2t.x * c2t.x + c2t.y * c2t.y;
        return new CollisionResult(d2 < c2r * c2r,  d2 < lr * lr);
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnSegments(Vector2 c_src, float c_rad, Vector2 src, Vector2[] points, int start, int skip, int end, float radius, float cos_rot, float sin_rot) {
        if (start >= end) return false;
        // use src.x to store delta vector to target, derotated.
        src.x = c_src.x - src.x;
        src.y = c_src.y - src.y;
        float _gbg = cos_rot * src.x + sin_rot * src.y;
        src.y = cos_rot * src.y - sin_rot * src.x;
        src.x = _gbg;

        float radius2 = (radius + c_rad) * (radius + c_rad);
        Vector2 delta; Vector2 g;
        float projection_unscaled; float d2;
        --end; //Now end refers to the index we will look at for the final check; ie it is inclusive.
        int ii = start + skip;
        for (; ii < end; ii += skip) {
            delta.x = points[ii].x - points[ii - skip].x;
            delta.y = points[ii].y - points[ii - skip].y;
            g.x = src.x - points[ii - skip].x;
            g.y = src.y - points[ii - skip].y;
            projection_unscaled = g.x * delta.x + g.y * delta.y;
            d2 = g.x * g.x + g.y * g.y;
            if (projection_unscaled < 0) {
                //We only check endpoint collision on the first point;
                //due to segmenting we will end by checking on all points except the last, which is handled outside.
                if (d2 < radius2) return true;
            } else {
                float dmag2 = delta.x * delta.x + delta.y * delta.y;
                if (projection_unscaled < dmag2) {
                    float norm2 = d2 - projection_unscaled * projection_unscaled / dmag2;
                    if (norm2 < radius2) return true;
                }
            }
        }
        //Now perform the last point check
        ii -= skip;
        delta.x = points[end].x - points[ii].x;
        delta.y = points[end].y - points[ii].y;
        g.x = src.x - points[ii].x;
        g.y = src.y - points[ii].y;
        projection_unscaled = g.x * delta.x + g.y * delta.y;
        d2 = g.x * g.x + g.y * g.y;
        if (projection_unscaled < 0) {
            if (d2 < radius2) return true;
        } else {
            float dmag2 = delta.x * delta.x + delta.y * delta.y;
            if (projection_unscaled < dmag2) {
                float norm2 = d2 - projection_unscaled * projection_unscaled / dmag2;
                if (norm2 < radius2) return true;
            }
        }
        //Last point circle collision
        g.x = src.x - points[end].x;
        g.y = src.y - points[end].y;
        d2 = g.x * g.x + g.y * g.y;
        return d2 < radius2;
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnSegments(SOCircle c1, Vector2 src, Vector2[] points, int start, int skip, int end, float radius, float cos_rot, float sin_rot) {
        if (start >= end) return CollisionResult.noColl;
        bool grazed = false;
        // use src.x to store delta vector to target, derotated.
        src.x = c1.location.x - src.x;
        src.y = c1.location.y - src.y;
        float _gbg = cos_rot * src.x + sin_rot * src.y;
        src.y = cos_rot * src.y - sin_rot * src.x;
        src.x = _gbg;

        float lradius2 = (radius + c1.largeRadius) * (radius + c1.largeRadius);
        float radius2 = (radius + c1.radius) * (radius + c1.radius);
        Vector2 delta; Vector2 g;
        float projection_unscaled; float d2;
        --end; //Now end refers to the index we will look at for the final check; ie it is inclusive.
        int ii = start + skip;
        for (; ii < end; ii += skip) {
            delta.x = points[ii].x - points[ii - skip].x;
            delta.y = points[ii].y - points[ii - skip].y;
            g.x = src.x - points[ii - skip].x;
            g.y = src.y - points[ii - skip].y;
            projection_unscaled = g.x * delta.x + g.y * delta.y;
            d2 = g.x * g.x + g.y * g.y;
            if (projection_unscaled < 0) {
                //We only check endpoint collision on the first point;
                //due to segmenting we will end by checking on all points except the last, which is handled outside.
                grazed |= d2 < lradius2;
                if (d2 < radius2) {
                    return new CollisionResult(true, grazed);
                }
            } else {
                float dmag2 = delta.x * delta.x + delta.y * delta.y;
                if (projection_unscaled < dmag2) {
                    float norm2 = d2 - projection_unscaled * projection_unscaled / dmag2;
                    grazed |= norm2 < lradius2;
                    if (norm2 < radius2) {
                        return new CollisionResult(true, grazed);
                    }
                }
            }
        }
        //Now perform the last point check
        ii -= skip;
        delta.x = points[end].x - points[ii].x;
        delta.y = points[end].y - points[ii].y;
        g.x = src.x - points[ii].x;
        g.y = src.y - points[ii].y;
        projection_unscaled = g.x * delta.x + g.y * delta.y;
        d2 = g.x * g.x + g.y * g.y;
        if (projection_unscaled < 0) {
            grazed |= d2 < lradius2;
            if (d2 < radius2) {
                return new CollisionResult(true, grazed);
            }
        } else {
            float dmag2 = delta.x * delta.x + delta.y * delta.y;
            if (projection_unscaled < dmag2) {
                float norm2 = d2 - projection_unscaled * projection_unscaled / dmag2;
                grazed |= norm2 < lradius2;
                if (norm2 < radius2) {
                    return new CollisionResult(true, grazed);
                }
            }
        }
        //Last point circle collision
        g.x = src.x - points[end].x;
        g.y = src.y - points[end].y;
        d2 = g.x * g.x + g.y * g.y;
        grazed |= d2 < lradius2;
        return new CollisionResult(d2 < radius2, grazed);
    }


    //We use this for pill-like bullets
    //NOTE: the calling mechanism is different. You pass node1 and delta=node2-node1. The reason for this is because 
    //delta can be precomputed.
    //NOTE: it's also more efficient to compute scale stuff in here.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnRotatedSegment(SOCircle circ, Vector2 src, float radius, Vector2 node1, Vector2 delta, float scale, float delta_mag2, float max_dist2, float cos_rot, float sin_rot) {
        max_dist2 *= scale * scale;
        //First, we get target - src, then DErotate it. This means we only need one rotation operation
        src.x = circ.location.x - src.x;
        src.y = circ.location.y - src.y;
        //Early exit condition: ||src -> target||^2 > 2(max_dist^2 + Lrad^2)
        //The extra 2 is because 2(x^2+y^2) is an upper bound for (x+y)^2.
        if (src.x * src.x + src.y * src.y > 2f * (max_dist2 + circ.lradius2)) return CollisionResult.noColl;
        
        //Derotation and subtract by node1 to get the G vector.
        float _gbg = cos_rot * src.x + sin_rot * src.y - node1.x * scale;
        src.y = cos_rot * src.y - sin_rot * src.x - node1.y * scale;
        src.x = _gbg;
        
        delta.x *= scale;
        delta.y *= scale;
        delta_mag2 *= scale * scale;
        radius *= scale;
        
        float radius2 = (radius + circ.radius) * (radius + circ.radius);
        float lradius2 = (radius + circ.largeRadius) * (radius + circ.largeRadius);

        float projection_unscaled = src.x * delta.x + src.y * delta.y;
        if (projection_unscaled < 0) {
            float d2 = src.x * src.x + src.y * src.y;
            return new CollisionResult(d2 < radius2, d2 < lradius2);
        } else if (projection_unscaled > delta_mag2) {
            src.x -= delta.x;
            src.y -= delta.y;
            float d2 = src.x * src.x + src.y * src.y;
            return new CollisionResult(d2 < radius2, d2 < lradius2);
        } else {
            float norm = src.x * src.x + src.y * src.y - projection_unscaled * projection_unscaled / delta_mag2;
            return new CollisionResult(norm < radius2, norm < lradius2);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [UsedImplicitly]
    public static bool PointInCircle(Vector2 pt, CCircle c) {
        pt.x -= c.x;
        pt.y -= c.y;
        return pt.x * pt.x + pt.y * pt.y < c.r * c.r;
    }
    public static readonly ExFunction pointInCircle = ExUtils.Wrap(t, "PointInCircle", new[] {ExUtils.tv2, ExUtils.tcc});
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointInRect(Vector2 pt, CRect rect) {
        pt.x -= rect.x;
        pt.y -= rect.y;
        float px = rect.cos_rot * pt.x + rect.sin_rot * pt.y;
        pt.y = rect.cos_rot * pt.y - rect.sin_rot * pt.x;
        if (px < 0) {
            px *= -1;
        }
        if (pt.y < 0) {
            pt.y *= -1;
        }
        return px < rect.halfW && pt.y < rect.halfH;
    }
    public static readonly ExFunction pointInRect = ExUtils.Wrap(t, "PointInRect", new[] {ExUtils.tv2, ExUtils.tcr});
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnAABB(float dx, float dy, float rhx, float rhy, float r, float r2) {
        //Inlined absolutes are much faster
        if (dx < 0) dx *= -1;
        if (dy < 0) dy *= -1;
        //Then we are in one of three locations:
        if (dy < rhy) {
            //In "front" of the rectangle.
            return dx - rhx < r;
        } else if (dx < rhx) {
            // On "top" of the rectangle. 
            return dy - rhy < r;
        } else {
            //In front and on top.
            dx -= rhx;
            dy -= rhy;
            return (dx * dx + dy * dy) < r2;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnRect(SOCircle circ, Vector2 rect, float rectHalfX, float rectHalfY, float diag2, float scale, float cos_rot, float sin_rot) {
        diag2 *= scale * scale;
        rect.x = circ.location.x - rect.x;
        rect.y = circ.location.y - rect.y;
        //Early exit condition: ||src -> target||^2 > 2*(diag^2 + Lrad^2)
        //The extra 2 is because 2(x^2+y^2) is an upper bound for (x+y)^2.
        if (rect.x * rect.x + rect.y * rect.y > 2f * (diag2 + circ.lradius2)) return CollisionResult.noColl;
        rectHalfX *= scale;
        rectHalfY *= scale;
        //First DErotate the delta vector and get its absolutes. Note we use -sin_rot
        //Store delta vector in Rect for efficiency
        float gbg = cos_rot * rect.x + sin_rot * rect.y;
        rect.y = cos_rot * rect.y - sin_rot * rect.x;
        rect.x = gbg;
        //Inlined absolutes are much faster
        if (rect.x < 0) rect.x *= -1;
        if (rect.y < 0) rect.y *= -1;
        //Then we are in one of three locations:
        if (rect.y < rectHalfY) {
            //In "front" of the rectangle.
            return new CollisionResult(rect.x - rectHalfX < circ.radius,
                rect.x - rectHalfX < circ.largeRadius);
        }
        if (rect.x < rectHalfX) {
            // On "top" of the rectangle
            return new CollisionResult(rect.y - rectHalfY < circ.radius,
                rect.y - rectHalfY < circ.largeRadius);
        }
        //In front and on top.
        rect.x -= rectHalfX;
        rect.y -= rectHalfY;
        float dsqr = rect.x * rect.x + rect.y * rect.y;
        return new CollisionResult(dsqr < circ.radius2, dsqr < circ.lradius2);
    }
}
}
