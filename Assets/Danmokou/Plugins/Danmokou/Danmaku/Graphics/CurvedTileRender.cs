using System;
using Danmokou.DMath;
using UnityEngine;
using Danmokou.Core;

namespace Danmokou.Graphics {
public abstract class CurvedTileRender : TiledRender {
    protected Vector2[] centers = null!;

    public CurvedTileRender(GameObject obj) : base(obj) { }

    // Start is called before the first frame update
    public void Initialize(ITransformHandler locationer, TiledRenderCfg cfg, Material mat, bool isNew, bool is_static,
        bool isPlayer, int newTexW) {
        base.Initialize(locationer, cfg, mat, is_static, isPlayer);
        texRptHeight = 1;
        if (texRptWidth != newTexW || isNew) {
            texRptWidth = newTexW;
            PrepareNewMesh();
            centers = new Vector2[newTexW + 1];
        }
    }

    protected override unsafe void OnNewMesh() {
        base.OnNewMesh();
        for (int ii = 0; ii <= texRptWidth; ++ii) {
            vertsPtr[ii].uv.y = 0;
            vertsPtr[ii + texRptWidth + 1].uv.y = 1;
        }
    }


    protected enum SelfIntersectionStatus {
        RAS,
        CHECK_THIS_AND_NEXT, //After flip, next update needs to run intersect search
        //Separate NEXT and THIS because a flipped point may not incurse enough (eg. 4.51->5.01>>4.99),
        //so we check incursion up to twice
        CHECK_THIS,
        //Points will continue to be recalled to the intersection point until crossed
        RECALLING_LOW, //The lower verts row (uv.y=0)
        RECALLING_HIGH //The higher verts row (uv.y=1)
    }

    private const float INTERSECT_SEARCH = 10f;
    private const int DEFAULT_SELF_INTERSECT_BACK = 10;

    /// <summary>
    /// Given an inclusive range of vertices to search, check if a ray projected from a point intersects
    /// any of the line segments. If an intersection occurred, store the location of the intersection
    /// and the number of the first vertex after the intersection in vptr_to.
    /// </summary>
    /// <param name="vptr_from"></param>
    /// <param name="vptr_to"></param>
    /// <param name="target_x"></param>
    /// <param name="target_y"></param>
    /// <param name="target_project_x"></param>
    /// <param name="target_project_y"></param>
    /// <param name="inters_x"></param>
    /// <param name="inters_y"></param>
    /// <param name="raycast">Distance to project the ray.</param>
    /// <returns></returns>
    protected unsafe bool FindIntersection(int vptr_from, ref int vptr_to, float target_x, float target_y,
        float target_project_x, float target_project_y, out float inters_x, out float inters_y,
        float raycast = INTERSECT_SEARCH) {
        float t2x = target_x + raycast * target_project_x;
        float t2y = target_y + raycast * target_project_y;
        for (int ii = vptr_to; ii > vptr_from; --ii) {
            if (M.SegmentIntersection(
                vertsPtr[ii - 1].loc.x, vertsPtr[ii - 1].loc.y,
                vertsPtr[ii].loc.x, vertsPtr[ii].loc.y,
                target_x, target_y, t2x, t2y,
                out inters_x, out inters_y)) {
                vptr_to = ii;
                return true;
            }
        }
        inters_x = inters_y = 0;
        return false;
    }

    protected SelfIntersectionStatus intersectStatus = SelfIntersectionStatus.RAS;
    private float intersX;
    private float intersY;

    protected unsafe void RecallSelfIntersection(Vector2 lastDelta, float backstep, int read_from, int target,
        float ddf) {
        int vw = texRptWidth + 1;
        if (intersectStatus == SelfIntersectionStatus.CHECK_THIS ||
            intersectStatus == SelfIntersectionStatus.CHECK_THIS_AND_NEXT) {
            float last_mag = (float) Math.Sqrt(lastDelta.x * lastDelta.x + lastDelta.y * lastDelta.y);
            float ldsx = lastDelta.x / last_mag;
            float ldsy = lastDelta.y / last_mag;
            float eff_low_x = centers[target].x + ddf * ldsy - lastDelta.x * backstep;
            float eff_low_y = centers[target].y + ddf * -ldsx - lastDelta.y * backstep;
            int read_to = target - 1;
            if (FindIntersection(read_from, ref read_to, eff_low_x, eff_low_y,
                ldsx, ldsy, out intersX, out intersY)) {
                for (; read_to < vw; ++read_to) {
                    //Includes most recent point
                    vertsPtr[read_to].loc.x = intersX;
                    vertsPtr[read_to].loc.y = intersY;
                }
                intersectStatus = SelfIntersectionStatus.RECALLING_LOW;
            } else {
                float eff_high_x = centers[target].x + ddf * -ldsy - lastDelta.x * backstep;
                float eff_high_y = centers[target].y + ddf * ldsx - lastDelta.y * backstep;
                read_to = target - 1 + vw;
                if (FindIntersection(read_from + vw, ref read_to, eff_high_x,
                    eff_high_y,
                    ldsx, ldsy, out intersX, out intersY)) {
                    for (; read_to < 2 * vw; ++read_to) {
                        //Includes most recent point
                        vertsPtr[read_to].loc.x = intersX;
                        vertsPtr[read_to].loc.y = intersY;
                    }
                    intersectStatus = SelfIntersectionStatus.RECALLING_HIGH;
                } else {
                    intersectStatus = (intersectStatus == SelfIntersectionStatus.CHECK_THIS) ?
                        SelfIntersectionStatus.RAS :
                        SelfIntersectionStatus.CHECK_THIS;
                }
            }
        } else if (intersectStatus == SelfIntersectionStatus.RECALLING_LOW) {
            if (M.IsCounterClockwise(intersX, intersY,
                vertsPtr[target].loc.x, vertsPtr[target].loc.y,
                vertsPtr[target + vw].loc.x, vertsPtr[target + vw].loc.y)) {
                intersectStatus = SelfIntersectionStatus.RAS;
            } else {
                vertsPtr[target].loc.x = intersX;
                vertsPtr[target].loc.y = intersY;
            }
        } else if (intersectStatus == SelfIntersectionStatus.RECALLING_HIGH) {
            if (M.IsCounterClockwise(intersX, intersY,
                vertsPtr[target].loc.x, vertsPtr[target].loc.y,
                vertsPtr[target + vw].loc.x, vertsPtr[target + vw].loc.y)) {
                intersectStatus = SelfIntersectionStatus.RAS;
            } else {
                vertsPtr[target + vw].loc.x = intersX;
                vertsPtr[target + vw].loc.y = intersY;
            }
        }

    }

}
}