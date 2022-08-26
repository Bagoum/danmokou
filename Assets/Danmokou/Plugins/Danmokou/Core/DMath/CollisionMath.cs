﻿using System;
using System.Runtime.CompilerServices;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Expressions;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.DMath {
public readonly struct CollisionResult {
    public readonly bool collide;
    public readonly bool graze;

    public CollisionResult(bool collide, bool graze) {
        this.collide = collide;
        this.graze = graze;
    }
}

public static class CollisionMath {
    public static readonly CollisionResult noColl = new(false, false);
    private static readonly Type t = typeof(CollisionMath);

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
    public static bool CircleOnCircle(in Hitbox c1, Vector2 c2t, float c2r) {
        c2t.x -= c1.x;
        c2t.y -= c1.y;
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
    public static CollisionResult GrazeCircleOnCircle(in Hitbox h, Vector2 c2t, float c2r) {
        c2t.x -= h.x;
        c2t.y -= h.y;
        float lr = c2r + h.largeRadius;
        c2r += h.radius;
        float d2 = c2t.x * c2t.x + c2t.y * c2t.y;
        return new(d2 < c2r * c2r,  d2 < lr * lr);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnCircle(in Hitbox h, in float cx, in float cy, in float cr) {
        var dx = cx - h.x;
        var dy = cy - h.y;
        float lr = cr + h.largeRadius;
        var r = cr + h.radius;
        float d2 = dx * dx + dy * dy;
        return new(d2 < r * r,  d2 < lr * lr);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GrazeCircleOnCircle(in Hitbox h, Vector2 c2t, float c2r, out bool collide, out bool graze) {
        c2t.x -= h.x;
        c2t.y -= h.y;
        float lr = c2r + h.largeRadius;
        c2r += h.radius;
        float d2 = c2t.x * c2t.x + c2t.y * c2t.y;
        collide = d2 < c2r * c2r;
        graze = d2 < lr * lr;
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnSegments(Vector2 c_src, float c_rad, Vector2 src, Vector2[] points, int start, int skip, int end, float radius, float cos_rot, float sin_rot, out int segment) {
        segment = 0;
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
            //Check circle collision at every point for accurate out segment
            if (d2 < radius2) {
                segment = ii;
                return true;
            } else if (projection_unscaled > 0) {
                float dmag2 = delta.x * delta.x + delta.y * delta.y;
                if (projection_unscaled < dmag2) {
                    float norm2 = d2 - projection_unscaled * projection_unscaled / dmag2;
                    if (norm2 < radius2) {
                        segment = ii;
                        return true;
                    }
                }
            }
        }
        //Now perform the last point check
        ii -= skip;
        segment = end;
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
    public static CollisionResult GrazeCircleOnSegments(in Hitbox c1, Vector2 src, Vector2[] points, int start, int skip, int end, float radius, float cos_rot, float sin_rot) {
        if (start >= end) return noColl;
        bool grazed = false;
        // use src.x to store delta vector to target, derotated.
        src.x = c1.x - src.x;
        src.y = c1.y - src.y;
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
    public static CollisionResult GrazeCircleOnRotatedSegment(in Hitbox h, in float cx, in float cy, in float radius, in Vector2 node1, in Vector2 delta, in float scale, in float delta_mag2, in float max_dist2, in float cos_rot, in float sin_rot) {
        //First, we get src -> target and descale it, so we don't need any other scaling
        var x = (h.x - cx) / scale;
        var y = (h.y - cy) / scale;
        
        //Early exit condition: ||src -> target||^2 > 2(max_dist^2 + Lrad^2)
        //The extra 2 is because 2(x^2+y^2) is an upper bound for (x+y)^2.
        if (x * x + y * y > 2f * (max_dist2 + h.largeRadius2)) return noColl;
        
        //Derotate and subtract by node1:local to get the G vector (node1:world -> target)
        float _x = cos_rot * x + sin_rot * y - node1.x;
        y = cos_rot * y - sin_rot * x - node1.y;
        x = _x;

        float radius2 = (radius + h.radius) * (radius + h.radius);
        float lradius2 = (radius + h.largeRadius) * (radius + h.largeRadius);

        //Dot product of A:(node1:world -> target) and B:(node1 -> node2)
        float dot = x * delta.x + y * delta.y;
        if (dot < 0) {
            //target is in the opposite direction 
            float d2 = x * x + y * y;
            return new CollisionResult(d2 < radius2, d2 < lradius2);
        } else if (dot > delta_mag2) { //ie. proj_B(A) > ||B||
            //target is beyond node2
            x -= delta.x;
            y -= delta.y;
            float d2 = x * x + y * y;
            return new CollisionResult(d2 < radius2, d2 < lradius2);
        } else {
            //proj_B(A) = (dot / delta_mag)
            //We have a right triangle A, proj_B(A), norm_B(A)
            float norm = x * x + y * y - dot * dot / delta_mag2;
            return new CollisionResult(norm < radius2, norm < lradius2);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnRotatedSegment(Vector2 cLoc, float cRad, Vector2 src, float radius, 
        Vector2 node1, Vector2 delta, float scale, float delta_mag2, float max_dist2, float cos_rot, float sin_rot) {
        src.x = (cLoc.x - src.x) / scale;
        src.y = (cLoc.y - src.y) / scale;
        if (src.x * src.x + src.y * src.y > 2f * (max_dist2 + cRad * cRad)) return false;
        
        float _gbg = cos_rot * src.x + sin_rot * src.y - node1.x;
        src.y = cos_rot * src.y - sin_rot * src.x - node1.y;
        src.x = _gbg;
        
        float radius2 = (radius + cRad) * (radius + cRad);

        float projection_unscaled = src.x * delta.x + src.y * delta.y;
        if (projection_unscaled < 0) {
            float d2 = src.x * src.x + src.y * src.y;
            return d2 < radius2;
        } else if (projection_unscaled > delta_mag2) {
            src.x -= delta.x;
            src.y -= delta.y;
            float d2 = src.x * src.x + src.y * src.y;
            return d2 < radius2;
        } else {
            float norm = src.x * src.x + src.y * src.y - projection_unscaled * projection_unscaled / delta_mag2;
            return norm < radius2;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [UsedImplicitly]
    public static bool PointInCircle(Vector2 pt, CCircle c) {
        pt.x -= c.x;
        pt.y -= c.y;
        return pt.x * pt.x + pt.y * pt.y < c.r * c.r;
    }
    public static readonly ExFunction pointInCircle = ExFunction.Wrap(t, "PointInCircle", new[] {ExUtils.tv2, ExUtils.tcc});
    
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleInRect(Vector2 pt, float radius, CRect rect) {
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
        return px + radius < rect.halfW && pt.y + radius < rect.halfH;
    }
    public static readonly ExFunction pointInRect = ExFunction.Wrap(t, "PointInRect", new[] {ExUtils.tv2, ExUtils.tcr});
   
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnAABB(AABB rect, Vector2 pt, float rad) {
        float dx = pt.x - rect.x;
        float dy = pt.y - rect.y;
        //Inlined absolutes are much faster
        if (dx < 0) dx *= -1;
        if (dy < 0) dy *= -1;
        dx -= rect.rx;
        dy -= rect.ry;
        return dx < rad && 
               dy < rad && 
               (dx < 0 || dy < 0 || dx * dx + dy * dy < rad * rad);
    }

    /// <summary>
    /// May report collisions when none exist, but is a good approximation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WeakCircleOnAABB(float minX, float minY, float maxX, float maxY, float dx, float dy, float r) =>
        dx > minX - r && dx < maxX + r && dy > minY - r && dy < maxY + r;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnRect(in Hitbox circ, in float rectX, in float rectY, in float rectHalfX, in float rectHalfY, in float diag2, in float scale, in float cos_rot, in float sin_rot) {
        var x = (circ.x - rectX) / scale;
        var y = (circ.y - rectY) / scale;
        //Early exit condition: ||src -> target||^2 > 2*(diag^2 + Lrad^2)
        //The extra 2 is because 2(x^2+y^2) is an upper bound for (x+y)^2.
        if (x * x + y * y > 2f * (diag2 + circ.largeRadius2)) return noColl;
        //First DErotate the delta vector and get its absolutes. Note we use -sin_rot
        //Store delta vector in Rect for efficiency
        float _x = cos_rot * x + sin_rot * y;
        y = cos_rot * y - sin_rot * x;
        x = _x;
        //Inlined absolutes are much faster
        if (x < 0) x *= -1;
        if (y < 0) y *= -1;
        //Then we are in one of three locations:
        if (y < rectHalfY) {
            //In "front" of the rectangle.
            return new CollisionResult(x - rectHalfX < circ.radius,
                x - rectHalfX < circ.largeRadius);
        }
        if (x < rectHalfX) {
            // On "top" of the rectangle
            return new CollisionResult(y - rectHalfY < circ.radius,
                y - rectHalfY < circ.largeRadius);
        }
        //In front and on top.
        x -= rectHalfX;
        y -= rectHalfY;
        float dsqr = x * x + y * y;
        return new CollisionResult(dsqr < circ.radius2, dsqr < circ.largeRadius2);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnRect(Vector2 cLoc, float cRad, Vector2 rLoc, float rectHalfX, float rectHalfY, 
        float diag2, float scale, float cos_rot, float sin_rot) {
        rLoc.x = (cLoc.x - rLoc.x) / scale;
        rLoc.y = (cLoc.y - rLoc.y) / scale;
        if (rLoc.x * rLoc.x + rLoc.y * rLoc.y > 2f * (diag2 + cRad * cRad)) return false;
        float gbg = cos_rot * rLoc.x + sin_rot * rLoc.y;
        rLoc.y = cos_rot * rLoc.y - sin_rot * rLoc.x;
        rLoc.x = gbg;
        if (rLoc.x < 0) rLoc.x *= -1;
        if (rLoc.y < 0) rLoc.y *= -1;
        if (rLoc.y < rectHalfY) {
            return rLoc.x - rectHalfX < cRad;
        }
        if (rLoc.x < rectHalfX) {
            return rLoc.y - rectHalfY < cRad;
        }
        rLoc.x -= rectHalfX;
        rLoc.y -= rectHalfY;
        return rLoc.x * rLoc.x + rLoc.y * rLoc.y < cRad * cRad;
    }
}
}
