using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BagoumLib.Expressions;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.Expressions;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;

namespace Danmokou.DMath {

/// <summary>
/// The combination of many <see cref="CollisionResult"/>s.
/// </summary>
public readonly struct CollisionsAccumulation {
    public readonly int damage;
    public readonly int graze;

    public CollisionsAccumulation(int dmg, int graze) {
        this.damage = dmg;
        this.graze = graze;
    }

    public static CollisionsAccumulation operator +(CollisionsAccumulation a, CollisionsAccumulation b) =>
        new(Math.Max(a.damage, b.damage), a.graze + b.graze);

    public CollisionsAccumulation WithDamage(int otherDamage) => new(Math.Max(damage, otherDamage), graze);
    public CollisionsAccumulation WithGraze(int otherGraze) => new(damage, graze + otherGraze);
}
/// <summary>
/// The result of a collision test against hard collision and graze collision.
/// </summary>
public readonly struct CollisionResult {
    public readonly bool collide;
    public readonly bool graze;

    public CollisionResult(bool collide, bool graze) {
        this.collide = collide;
        this.graze = graze;
    }

    public CollisionResult NoGraze() => new(collide, false);
}

public static class CollisionMath {
    public static readonly CollisionResult NoCollision = new(false, false);
    private static readonly Type t = typeof(CollisionMath);
/*
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnCircle(Vector2 c1t, Vector2 c2t, float c1rc2r) {
        c2t.x -= c1t.x;
        c2t.y -= c1t.y;
        return c2t.x * c2t.x + c2t.y * c2t.y < c1rc2r * c1rc2r;
    }*/

/*
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnCircle(in Hitbox c1, Vector2 c2t, float c2r) {
        c2t.x -= c1.x;
        c2t.y -= c1.y;
        c2r += c1.radius;
        return c2t.x * c2t.x + c2t.y * c2t.y < c2r * c2r;
    }*/

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnPoint(in Vector2 c1, in float r, in Vector2 c2) {
        var dx = c2.x - c1.x;
        var dy = c2.y - c1.y;
        return dx * dx + dy * dy < r * r;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnPoint(in Vector2 c1, in float r, in float c2x, in float c2y) {
        var dx = c2x - c1.x;
        var dy = c2y - c1.y;
        return dx * dx + dy * dy < r * r;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnCircle(in float c1x, in float c1y, in float c1r, in float c2x, in float c2y, in float c2r) {
        var dx = c2x - c1x;
        var dy = c2y - c1y;
        return dx * dx + dy * dy < (c1r + c2r) * (c1r + c2r);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnCircle(in Vector2 c1Loc, in float c1r, in float c2x, in float c2y, in float c2r) {
        var dx = c2x - c1Loc.x;
        var dy = c2y - c1Loc.y;
        return dx * dx + dy * dy < (c1r + c2r) * (c1r + c2r);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnCircle(in Hurtbox h, in float x, in float y, in float r) {
        var dx = x - h.x;
        var dy = y - h.y;
        var lrSum = r + h.grazeRadius;
        var rSum = r + h.radius;
        var d2 = dx * dx + dy * dy;
        return new(d2 < rSum * rSum,  d2 < lrSum * lrSum);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnCircle(in Hurtbox h, in float x, in float y, in float r, in float scale) {
        var dx = x - h.x;
        var dy = y - h.y;
        var lrSum = (r * scale) + h.grazeRadius;
        var rSum = (r * scale) + h.radius;
        var d2 = dx * dx + dy * dy;
        return new(d2 < rSum * rSum,  d2 < lrSum * lrSum);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float PointToRectSquareDistance(in float px, in float py, in float rx, in float ry, in float rHalfW, in float rHalfH) {
        var dx = px - rx;
        var dy = py - ry;
        if (dx < 0)
            dx = -dx - rHalfW;
        else
            dx = dx - rHalfW;
        if (dy < 0)
            dy = -dy - rHalfH;
        else
            dy = dy - rHalfH;
        //Then we are in one of three locations:
        if (dy < 0) {
            //In "front" of the rectangle.
            if (dx < 0) return 0;
            return dx * dx;
        }
        if (dx < 0) {
            // On "top" of the rectangle.
            return dy * dy;
        }
        //In front and on top.
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// For a line segment (sx1,sy1)->(sx2,sy2), find the closest squared distance of the normal-matched corners of
    ///  the provided rectangle to the ray defined by the line segment.
    /// <br/>Returns inf if the closest point is not on the line segment, and 0 if the line segment crosses
    ///  into the rect.
    /// <br/>The "normal-matched corners" are the two diagonally opposite corners through which a line has
    ///  the same positive/negative slope direction as the normal of the line segment.
    /// <br/>For example, if the line segment is (-2,-1)->(4,6) (positive slope), then the normal-matched
    ///  corners are the top-left and bottom-right corners, through which a line would have a negative slope.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SegmentProjectToRectSquareDistance(in float sx1, in float sy1, in float sx2, in float sy2, in float rx, in float ry,
        in float rHalfW, in float rHalfH) {
        var sdx = sx2 - sx1;
        var sdy = sy2 - sy1;
        var sdmag2 = sdx * sdx + sdy * sdy;
        var sign = sdx * sdy;
        var gx = rx - sx1;
        var gy = ry - sy1;
        float minDist2 = float.PositiveInfinity;
        if (sign >= 0) {
            //Positive slope
            //Top left corner
            var tlx = gx - rHalfW;
            var tly = gy + rHalfH;
            var tldot = tlx * sdx + tly * sdy;
            var tlValid = tldot >= 0 && tldot < sdmag2;
            if (tlValid) {
                var norm2 = (tlx * tlx + tly * tly) - tldot * tldot / sdmag2;
                if (norm2 < minDist2)
                    minDist2 = norm2;
            }
            //Bottom right corner
            var brx = gx + rHalfW;
            var bry = gy - rHalfH;
            var brdot = brx * sdx + bry * sdy;
            var brValid = brdot >= 0 && brdot < sdmag2;
            if (brValid) {
                var norm2 = (brx * brx + bry * bry) - brdot * brdot / sdmag2;
                if (norm2 < minDist2)
                    minDist2 = norm2;
            }
            if ((tlValid && (ry + rHalfH - sy2) * tly <= 0 ||
                brValid && (ry - rHalfH - sy2) * bry <= 0) &&
                M.IsCounterClockwise(in sdx, in sdy, in tlx, in tly) != M.IsCounterClockwise(in sdx, in sdy, in brx, in bry))
                return 0;
            
        }
        if (sign <= 0) {
            //Negative slope
            //Top right corner
            var trx = gx + rHalfW;
            var trY = gy + rHalfH;
            var trdot = trx * sdx + trY * sdy;
            var trValid = trdot >= 0 && trdot < sdmag2;
            if (trValid) {
                var norm2 = (trx * trx + trY * trY) - trdot * trdot / sdmag2;
                if (norm2 < minDist2)
                    minDist2 = norm2;
            }
            //Bottom left corner
            var blx = gx - rHalfW;
            var bly = gy - rHalfH;
            var bldot = blx * sdx + bly * sdy;
            var blValid = bldot >= 0 && bldot < sdmag2;
            if (blValid) {
                var norm2 = (blx * blx + bly * bly) - bldot * bldot / sdmag2;
                if (norm2 < minDist2)
                    minDist2 = norm2;
            }
            if ((trValid && (ry + rHalfH - sy2) * trY <= 0 ||
                 blValid && (ry - rHalfH - sy2) * bly <= 0) &&
                M.IsCounterClockwise(in sdx, in sdy, in trx, in trY) != M.IsCounterClockwise(in sdx, in sdy, in blx, in bly))
                return 0;
        }
        return minDist2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RectOnSegments(in Vector2 rLoc, in Vector2 rHalfDim, in Vector2 rRot, in Vector2 src, in Vector2[] points,
        in int start, in int skip, in int end, in float radius, in float cos_rot, in float sin_rot, out int segment) {
        segment = start;
        if (start >= end) return false;
        Profiler.BeginSample("RectOnSegments");
        //Combine rect and laser rotations
        float rRotX = rRot.x * cos_rot + rRot.y * sin_rot;
        float rRotY = rRot.y * cos_rot - rRot.x * sin_rot;
        //Get the position of the rect relative to the segment start
        var rx = rLoc.x - src.x;
        var ry = rLoc.y - src.y;
        float _tmp = rRotX * rx + rRotY * ry;
        ry = rRotX * ry - rRotY * rx;
        rx = _tmp;

        float radius2 = radius * radius;
        float prevx = points[start].x, prevy = points[start].y;
        _tmp = rRotX * prevx + rRotY * prevy;
        prevy = rRotX * prevy - rRotY * prevx;
        prevx = _tmp;
        int ii = start + skip;
        if (PointToRectSquareDistance(in prevx, in prevy, in rx, in ry, in rHalfDim.x, in rHalfDim.y) < radius2) {
            Profiler.EndSample();
            return true;
        }
        float nxtx, nxty;
        for (; ii < end - 1; ii += skip) {
            nxtx = rRotX * points[ii].x + rRotY * points[ii].y;
            nxty = rRotX * points[ii].y - rRotY * points[ii].x;
            if (PointToRectSquareDistance(in nxtx, in nxty, in rx, in ry, in rHalfDim.x, in rHalfDim.y) < radius2 ||
                SegmentProjectToRectSquareDistance(in prevx, in prevy, in nxtx, in nxty, in rx, in ry, in rHalfDim.x,
                    in rHalfDim.y) < radius2) {
                Profiler.EndSample();
                segment = ii;
                return true;
            }
            prevx = nxtx;
            prevy = nxty;
        }
        //Last segment check
        segment = end - 1;
        nxtx = rRotX * points[segment].x + rRotY * points[segment].y;
        nxty = rRotX * points[segment].y - rRotY * points[segment].x;
        var result = PointToRectSquareDistance(in nxtx, in nxty, in rx, in ry, in rHalfDim.x, in rHalfDim.y) < radius2 ||
               SegmentProjectToRectSquareDistance(in prevx, in prevy, in nxtx, in nxty, in rx, in ry, in rHalfDim.x,
                   in rHalfDim.y) < radius2;
        Profiler.EndSample();
        return result;
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnSegments(in Vector2 target, in float targetRad, in Vector2 src, Vector2[] points, in int start, in int skip, in int end, in float radius, in float cos_rot, in float sin_rot, out int segment) {
        segment = 0;
        if (start >= end) return false;
        // px/py = delta vector to target, derotated.
        var px = target.x - src.x;
        var py = target.y - src.y;
        float _tmp = cos_rot * px + sin_rot * py;
        py = cos_rot * py - sin_rot * px;
        px = _tmp;

        float radius2 = (radius + targetRad) * (radius + targetRad);
        float prevx = points[start].x, prevy = points[start].y;
        float dx, dy, gx, gy;
        float dot, d2;
        int ii = start + skip;
        for (; ii < end - 1; ii += skip) {
            gx = px - prevx;
            gy = py - prevy;
            d2 = gx * gx + gy * gy;
            //Check circle collision at every point for accurate out segment
            if (d2 < radius2) {
                segment = ii - skip;
                return true;
            }
            dx = -prevx + (prevx = points[ii].x);
            dy = -prevy + (prevy = points[ii].y);
            dot = gx * dx + gy * dy;
            if (dot > 0) {
                float dmag2 = dx * dx + dy * dy;
                if (dot < dmag2) {
                    float norm2 = d2 - dot * dot / dmag2;
                    if (norm2 < radius2) {
                        segment = ii;
                        return true;
                    }
                }
            }
        }
        //Last segment check
        gx = src.x - prevx;
        gy = src.y - prevy;
        d2 = gx * gx + gy * gy;
        if (d2 < radius2) {
            segment = ii - skip;
            return true;
        }
        segment = end - 1;
        dx = points[segment].x - prevx;
        dy = points[segment].y - prevy;
        dot = gx * dx + gy * dy;
        d2 = gx * gx + gy * gy;
        if (dot > 0) {
            float dmag2 = dx * dx + dy * dy;
            if (dot < dmag2) {
                float norm2 = d2 - dot * dot / dmag2;
                if (norm2 < radius2) return true;
            }
        }
        //Last point circle collision
        gx = px - points[segment].x;
        gy = py - points[segment].y;
        return gx * gx + gy * gy < radius2;
    }

    
    /// <summary>
    /// Check collision between a circle hurtbox and a sequence of segments with circular radii (ie. a pather or laser).
    /// </summary>
    /// <param name="c1">Circular hitbox</param>
    /// <param name="src">Base location of segments</param>
    /// <param name="points">Offset from src of each segment</param>
    /// <param name="start">First segment to consider</param>
    /// <param name="skip">Delta of segment indexes to test collision against (eg. if this is 2, then check collision on the interpolated sequence of start, start+2, start+4... end)</param>
    /// <param name="end">Last segment to consider, exclusive</param>
    /// <param name="radius">Radius of each segment point</param>
    /// <param name="cos_rot">Cosine rotation of sequence of segments</param>
    /// <param name="sin_rot">Sine rotation of sequence of segments</param>
    /// <param name="segment">Segment at which collision occurred</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnSegments(in Hurtbox c1, in Vector2 src, in Vector2[] points, in int start, in int skip, in int end, in float radius, in float cos_rot, in float sin_rot, out int segment) {
        segment = 0;
        if (start >= end) return NoCollision;
        bool grazed = false;
        // px/py = delta vector to target, derotated.
        var px = c1.x - src.x;
        var py = c1.y - src.y;
        float _tmp = cos_rot * px + sin_rot * py;
        py = cos_rot * py - sin_rot * px;
        px = _tmp;

        float lradius2 = (radius + c1.grazeRadius) * (radius + c1.grazeRadius);
        float radius2 = (radius + c1.radius) * (radius + c1.radius);
        float prevx = points[start].x, prevy = points[start].y;
        float dx, dy, gx, gy;
        float dot, d2;
        int ii = start + skip;
        for (; ii < end - 1; ii += skip) {
            gx = px - prevx;
            gy = py - prevy;
            d2 = gx * gx + gy * gy;
            grazed |= d2 < lradius2;
            if (d2 < radius2) {
                segment = ii - skip;
                return new CollisionResult(true, grazed);
            }
            dx = -prevx + (prevx = points[ii].x);
            dy = -prevy + (prevy = points[ii].y);
            dot = gx * dx + gy * dy;
            if (dot > 0) {
                float dmag2 = dx * dx + dy * dy;
                if (dot < dmag2) {
                    float norm2 = d2 - dot * dot / dmag2;
                    grazed |= norm2 < lradius2;
                    if (norm2 < radius2) {
                        segment = ii;
                        return new CollisionResult(true, grazed);
                    }
                }
            }
        }
        //Last segment check
        gx = px - prevx;
        gy = py - prevy;
        d2 = gx * gx + gy * gy;
        grazed |= d2 < lradius2;
        if (d2 < radius2) {
            segment = ii - skip;
            return new CollisionResult(true, grazed);
        }
        segment = end - 1;
        dx = points[segment].x - prevx;
        dy = points[segment].y - prevy;
        dot = gx * dx + gy * dy;
        if (dot > 0) {
            float dmag2 = dx * dx + dy * dy;
            if (dot < dmag2) {
                float norm2 = d2 - dot * dot / dmag2;
                grazed |= norm2 < lradius2;
                if (norm2 < radius2) {
                    return new CollisionResult(true, grazed);
                }
            }
        }
        //Last point circle collision
        gx = px - points[segment].x;
        gy = py - points[segment].y;
        d2 = gx * gx + gy * gy;
        return new CollisionResult(d2 < radius2, grazed || d2 < lradius2);
    }


    //We use this for pill-like bullets
    //NOTE: the calling mechanism is different. You pass node1 and delta=node2-node1. The reason for this is because 
    //delta can be precomputed.
    //NOTE: it's also more efficient to compute scale stuff in here.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnRotatedSegment(in Hurtbox h, in float x, in float y, in float radius, in Vector2 node1, in Vector2 delta, in float scale, in float delta_mag2, in float max_dist2, in Vector2 direction) {
        //First, we get src -> target and descale it, so we don't need any other scaling
        var dx = (h.x - x) / scale;
        var dy = (h.y - y) / scale;
        
        //Early exit condition: ||src -> target||^2 > 2(max_dist^2 + Lrad^2) > (max_dist + Lrad)^2
        if (dx * dx + dy * dy > 2f * (max_dist2 + h.grazeRadius2)) return NoCollision;
        
        //Derotate and subtract by node1:local to get the G vector (node1:world -> target)
        float _dx = direction.x * dx + direction.y * dy - node1.x;
        dy = direction.x * dy - direction.y * dx - node1.y;
        dx = _dx;

        float radius2 = (radius + h.radius) * (radius + h.radius);
        float lradius2 = (radius + h.grazeRadius) * (radius + h.grazeRadius);

        //Dot product of A:(node1:world -> target) and B:(node1 -> node2)
        float dot = dx * delta.x + dy * delta.y;
        if (dot < 0) {
            //target is in the opposite direction 
            float d2 = dx * dx + dy * dy;
            return new CollisionResult(d2 < radius2, d2 < lradius2);
        } else if (dot > delta_mag2) { //ie. proj_B(A) > ||B||
            //target is beyond node2
            dx -= delta.x;
            dy -= delta.y;
            float d2 = dx * dx + dy * dy;
            return new CollisionResult(d2 < radius2, d2 < lradius2);
        } else {
            //proj_B(A) = (dot / delta_mag)
            //We have a right triangle A, proj_B(A), norm_B(A)
            float norm = dx * dx + dy * dy - dot * dot / delta_mag2;
            return new CollisionResult(norm < radius2, norm < lradius2);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnRotatedSegment(in float cx, in float cy, in float cRad, in float x, in float y, in float radius, 
        in Vector2 node1, in Vector2 delta, in float scale, in float delta_mag2, in float max_dist2, in float cos_rot, in float sin_rot) {
        //First, we get src -> target and descale it, so we don't need any other scaling
        var dx = (cx - x) / scale;
        var dy = (cy - y) / scale;
        
        //Early exit condition: ||src -> target||^2 > 2(max_dist^2 + Lrad^2)
        //The extra 2 is because 2(x^2+y^2) is an upper bound for (x+y)^2.
        if (dx * dx + dy * dy > 2f * (max_dist2 + cRad * cRad)) return false;
        
        //Derotate and subtract by node1:local to get the G vector (node1:world -> target)
        float _dx = cos_rot * dx + sin_rot * dy - node1.x;
        dy = cos_rot * dy - sin_rot * dx - node1.y;
        dx = _dx;

        float radius2 = (radius + cRad) * (radius + cRad);

        //Dot product of A:(node1:world -> target) and B:(node1 -> node2)
        float dot = dx * delta.x + dy * delta.y;
        if (dot < 0) {
            //target is in the opposite direction 
            return dx * dx + dy * dy < radius2;
        } else if (dot > delta_mag2) { //ie. proj_B(A) > ||B||
            //target is beyond node2
            dx -= delta.x;
            dy -= delta.y;
            return dx * dx + dy * dy < radius2;
        } else {
            //proj_B(A) = (dot / delta_mag)
            //We have a right triangle A, proj_B(A), norm_B(A)
            return dx * dx + dy * dy - dot * dot / delta_mag2 < radius2;
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
        if (px < 0) px *= -1;
        if (pt.y < 0) pt.y *= -1;
        return px < rect.halfW && pt.y < rect.halfH;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ProjectPointOntoRect(Vector2 pt, CRect rect) {
        pt.x -= rect.x;
        pt.y -= rect.y;
        float d = (float)Math.Sqrt(pt.x * pt.x + pt.y * pt.y);
        float ascent = (float)Math.Atan2(rect.halfH, rect.halfW);
        float ang = (float)Math.Atan2(pt.y, pt.x);
        float angq = BMath.Mod(BMath.PI, ang - rect.angle);
        if (angq > BMath.PI)
            angq -= BMath.PI;
        if (angq > BMath.HPI)
            angq = BMath.PI - angq;
        float projectMult;
        if (angq < ascent)
            projectMult = rect.halfW / (d*(float)Math.Cos(angq));
        else
            projectMult = rect.halfH / (d*(float)Math.Sin(angq));
        return new Vector2(rect.x + projectMult * pt.x, rect.y + projectMult * pt.y);

    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleFullyInRect(float x, float y, float radius, CRect rect) {
        x -= rect.x;
        y -= rect.y;
        float px = rect.cos_rot * x + rect.sin_rot * y;
        y = rect.cos_rot * y - rect.sin_rot * x;
        if (px < 0) px *= -1;
        if (y < 0) y *= -1;
        return px + radius < rect.halfW && y + radius < rect.halfH;
    }
    public static readonly ExFunction pointInRect = ExFunction.Wrap(t, "PointInRect", ExUtils.tv2, ExUtils.tcr);
   
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnAABB(in AABB aabb, in float x, in float y, in float rad) {
        float dx = x - aabb.x;
        float dy = y - aabb.y;
        //Inlined absolutes are much faster
        if (dx < 0) dx *= -1;
        if (dy < 0) dy *= -1;
        dx -= aabb.halfW;
        dy -= aabb.halfH;
        return dx < rad && 
               dy < rad && 
               (dx < 0 || dy < 0 || dx * dx + dy * dy < rad * rad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AABBOnAABB(in AABB rect1, in AABB rect2) {
        var dx = rect1.x - rect2.x;
        var dy = rect1.y - rect2.y;
        if (dx < 0) dx *= -1;
        if (dy < 0) dy *= -1;
        return dx < rect1.halfW + rect2.halfW && dy < rect1.halfH + rect2.halfH;
    }

    public static bool RectOnAABB(in AABB aabb, in Vector2 rLoc, in Vector2 rHalfDim, in Vector2 rRot) {
        var dx = rLoc.x - aabb.x;
        var dy = rLoc.y - aabb.y;
        float rotx, roty;
        if (dx < 0) {
            dx = -dx - aabb.halfW;
            rotx = -rRot.x;
        } else {
            dx = dx - aabb.halfW;
            rotx = rRot.x;
        }
        if (dy < 0) {
            dy = -dy - aabb.halfH;
            roty = -rRot.y;
        } else {
            dy = dy - aabb.halfH;
            roty = rRot.y;
        }
        //Restrict the rect rotation to (-90,+90)
        if (rotx < 0) {
            rotx = -rotx;
            roty = -roty;
        }
        //The rect has been reflected to be above+to the right of the AABB.
        // dx,dy are the distance of the rect center from the top-right corner of the AABB.
        
        if (dx < 0 && dy < 0)
            //Rect's center is inside the AABB.
            return true;
        
        float rcx = rotx * rHalfDim.x, rsy = roty * rHalfDim.y, rcy = rotx * rHalfDim.y, rsx = roty * rHalfDim.x;
        //Bottom left corner: (dx,dy) + rotate(-rw, -rh)
        var bx = dx - rcx + rsy;
        var by = dy - rcy - rsx;
        if (bx < 0 && by < 0)
            return true;
        
        if (roty > 0) {
            //Rect has a positive rotation, so its left wall is the closest wall to the AABB.
            //Top left corner: (dx,dy) + rotate(-rw, rh)
            var tx = dx - rcx - rsy;
            var ty = dy + rcy - rsx;
            if (tx < 0 && ty < 0)
                return true;
            return SegmentProjectToRectSquareDistance(in bx, in by, in tx, in ty, -aabb.halfW, -aabb.halfH, aabb.halfW,
                aabb.halfH) <= 0;
        } else {
            //Rect has a negative rotation, so its bottom wall is the closest wall to the AABB.
            //Bottom right corner: (dx,dy) + rotate(rw, -rh)
            var tx = dx + rcx + rsy;
            var ty = dy - rcy + rsx;
            if (tx < 0 && ty < 0)
                return true;
            return SegmentProjectToRectSquareDistance(in bx, in by, in tx, in ty, -aabb.halfW, -aabb.halfH, aabb.halfW,
                aabb.halfH) <= 0;
        }
    }

    /// <summary>
    /// May report collisions when none exist, but is a good approximation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WeakCircleOnAABB(float minX, float minY, float maxX, float maxY, float dx, float dy, float r) =>
        dx > minX - r && dx < maxX + r && dy > minY - r && dy < maxY + r;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollisionResult GrazeCircleOnRect(in Hurtbox h, in float x, in float y, in Vector2 halfDim, in float diag2, in float scale, in Vector2 direction) {
        var dx = (h.x - x) / scale;
        var dy = (h.y - y) / scale;
        //Early exit condition: ||src -> target||^2 > 2(diag^2 + Lrad^2) > (diag + Lrad)^2
        if (dx * dx + dy * dy > 2f * (diag2 + h.grazeRadius2)) return NoCollision;
        //First DErotate the delta vector and get its absolutes. Note we use -sin_rot
        //Store delta vector in Rect for efficiency
        float _dx = direction.x * dx + direction.y * dy;
        dy = direction.x * dy - direction.y * dx;
        dx = _dx;
        //Inlined absolutes are much faster
        if (dx < 0) dx *= -1;
        if (dy < 0) dy *= -1;
        //Then we are in one of three locations:
        if (dy < halfDim.y) {
            //In "front" of the rectangle.
            return new CollisionResult(dx - halfDim.x < h.radius,
                dx - halfDim.x < h.grazeRadius);
        }
        if (dx < halfDim.x) {
            // On "top" of the rectangle
            return new CollisionResult(dy - halfDim.y < h.radius,
                dy - halfDim.y < h.grazeRadius);
        }
        //In front and on top.
        dx -= halfDim.x;
        dy -= halfDim.y;
        float dsqr = dx * dx + dy * dy;
        return new CollisionResult(dsqr < h.radius2, dsqr < h.grazeRadius2);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CircleOnRect(in float cx, in float cy, in float cRad, in float rectX, in float rectY, in float rectHalfWidth, in float rectHalfHeight, 
        in float diag2, in float scale, in float cos_rot, in float sin_rot) {
        var dx = (cx - rectX) / scale;
        var dy = (cy - rectY) / scale;
        //Early exit condition: ||src -> target||^2 > 2*(diag^2 + Lrad^2)
        //The extra 2 is because 2(x^2+y^2) is an upper bound for (x+y)^2.
        if (dx * dx + dy * dy > 2f * (diag2 + cRad * cRad)) return false;
        //First DErotate the delta vector and get its absolutes. Note we use -sin_rot
        float _dx = cos_rot * dx + sin_rot * dy;
        dy = cos_rot * dy - sin_rot * dx;
        dx = _dx;
        //Inlined absolutes are much faster
        if (dx < 0) dx *= -1;
        if (dy < 0) dy *= -1;
        
        //Then we are in one of three locations:
        if (dy < rectHalfHeight) {
            //In "front" of the rectangle.
            return dx - rectHalfWidth < cRad;
        }
        if (dx < rectHalfWidth) {
            // On "top" of the rectangle
            return dy - rectHalfHeight < cRad;
        }
        //In front and on top.
        dx -= rectHalfWidth;
        dy -= rectHalfHeight;
        return dx * dx + dy * dy < cRad * cRad;
    }
    
    public const float skinWidth = 0.001f;
    public const float distIntegrator = 0.002f;
    public const float slidingFriction = 0.05f;
    private static readonly List<Collider2D> doNotCollide = new(2);
    
    public static (Vector2 movement, Vector2 carried) CollideAndSlide(Vector2 delta, Vector2 pos, float radius, int collisionMask,
        out bool collided, out bool squashed, int bounces = 7) {
        var movement = Vector2.zero;
        var carried = Vector2.zero;
        collided = false;
        squashed = false;
        RaycastHit2D DoCast(Vector2 offset, Vector2 dir, float dist) => 
            Physics2D.CircleCast(pos + movement + carried + offset, radius - skinWidth, dir, dist, collisionMask);
        doNotCollide.Clear();
        for (var leftover = delta; bounces > 0; --bounces) {
            var cast = DoCast(Vector2.zero, leftover.normalized, leftover.magnitude + skinWidth);
            if (cast.collider == null)
                return (movement + leftover, carried);
            collided = true;
            if (cast.distance <= 0) {
                //Push out and try again
                if (Vector2.Dot(cast.normal, leftover) > 0 && DoCast(leftover, cast.normal, 0).collider == null)
                    return (movement + leftover, carried);
                
                for (var pushOut = distIntegrator;; pushOut += distIntegrator) {
                    var castPushOut = DoCast(cast.normal * pushOut, cast.normal, 0);
                    if (castPushOut.collider == cast.collider) continue;
                    if (castPushOut.collider != null && castPushOut.collider != cast.collider) {
                        //If the second collider has already received an internal collision, then we fail.
                        //Otherwise, we allow an extra bounce to try to handle the second collider.
                        foreach (var dnc in doNotCollide)
                            if (dnc == castPushOut.collider) {
                                squashed = true;
                                return (Vector2.zero, Vector2.zero);
                            }
                        ++bounces;
                    }
                    var snap = cast.normal * pushOut;
                    if (Vector2.Dot(snap, leftover) > 0) {
                        var snapProject = M.ProjectVector(snap, leftover);
                        if (snapProject.sqrMagnitude > leftover.sqrMagnitude) {
                            //Effect of pushout is greater than remaining leftovers, so end here
                            // (go to next round with zero movement to ensure no second collider collision)
                            leftover = Vector2.zero;
                        } else {
                            leftover -= snapProject;
                        }
                        movement += snapProject;
                        carried += snap - snapProject;
                    } else
                        carried += snap;
                    doNotCollide.Add(cast.collider);
                    break;
                }
            } else {
                var realDist = cast.distance - skinWidth;
                for (; realDist > 0f; realDist -= distIntegrator) 
                    if (DoCast(Vector2.zero, leftover.normalized, realDist).collider == null) 
                        break;
                var snap = leftover.normalized * Math.Max(0f, realDist);
                leftover -= snap;
                leftover = (leftover - M.ProjectVectorUnit(leftover, cast.normal)) * (1-slidingFriction);
                movement += snap;
            }
        }
        return (movement, carried);
    }

    
}
}
