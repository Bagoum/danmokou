using System;
using System.Collections.Generic;
using System.Threading;
using DMath;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Danmaku {
public delegate void LCF(CurvedTileRenderLaser ctr);
public delegate void LPCF(string pool);

[Serializable]
public class LaserRenderCfg : TiledRenderCfg {
    public float lineRadius;
    public bool alignEnd;
}

//These are always initialized manually, and may or may not be pooled.
public class CurvedTileRenderLaser : CurvedTileRender {
    private LaserVelocity path;
    private Vector3 simpleEulerRotation = new Vector3(0, 0, 0);
    private ParametricInfo bpi = new ParametricInfo();
    private readonly float lineRadius;
    private const float defaultUpdateStagger = 0.1f;
    private float updateStagger;
    private readonly bool alignEnd = false;
    private Laser laser;
    private float scaledLineRadius;
    private Laser.PointContainer endpt = new Laser.PointContainer(null);

    public CurvedTileRenderLaser(LaserRenderCfg cfg) : base(cfg) {
        alignEnd = cfg.alignEnd;
        lineRadius = cfg.lineRadius;
    }

    public void SetYScale(float scale) {
        PersistentYScale = scale;
        scaledLineRadius = lineRadius * scale;
    }

    //TileRenders are always Initialize-initialized.
    public void Initialize(Laser locationer, Material material, bool isNew, uint bpiId, int firingIndex, ref RealizedLaserOptions options) {
        laser = locationer;
        updateStagger = options.staggerMultiplier * defaultUpdateStagger;
        int newTexW = Mathf.CeilToInt(options.length / updateStagger);
        base.Initialize(locationer, material, isNew, options.isStatic, newTexW, options.hueShift); //doesn't do any mesh generation, safe to call first
        path = options.lpath;
        bpi = new ParametricInfo(locater.GlobalPosition(), firingIndex, bpiId);
    }
    
    //(this, material, isNew, bpi.id, firingIndex, ref RealizedLaserOptions);

    public void UpdateLaserStyle(string style) {
        thisStyleControls = LazyGetControls(style);
    }

    private const float ToNearestIndexCutoff = 0.07f;
    public V2RV2? Index(float time) {
        float idx = time / updateStagger;
        if (idx < 0 || idx > centers.Length - 1) return null;
        int idxL = (int) idx;
        int idxH = Math.Min(centers.Length - 1, idxL + 1);
        Vector2 locL = centers[idxL];
        Vector2 locH = centers[idxH];
        Vector2 loc = Vector2.Lerp(locL, locH, idx - idxL);
        float ang;
        if (idx - idxL < ToNearestIndexCutoff) {
            ang = M.Atan(locH - centers[Math.Max(0, idxL - 1)]);
        } else if (idxH - idx < ToNearestIndexCutoff) {
            ang = M.Atan(centers[Math.Min(centers.Length - 1, idxL - 1)] - locH);
        } else ang = Mathf.Lerp(M.Atan(loc - locL), M.Atan(locH - loc), idx - idxL);
        return V2RV2.NRotAngled(loc, ang * M.radDeg).RotateAll(simpleEulerRotation.z);
    }

    /// <summary>
    /// The last result from Velocity.Update while drawing a path.
    /// This is modified during flip (unlike Pather's version) and
    /// therefore can be accumulated.
    /// </summary>
    private Vector2 nextTrueDelta;
    /// <summary>
    /// The last return value of Velocity.Update. Used for flip adjustments and directionality.
    /// bpi.Flip will only flip the direction of this, making it not true.
    /// </summary>
    private Vector2 nextDirectionalDelta;

    private void UpdateCentersOnly() {
        int vw = texRptWidth + 1;
        path.Update(in lifetime, ref bpi, out nextDirectionalDelta, out nextTrueDelta, in updateStagger);
        for (int iw = 1; iw < vw; ++iw) {
            int styleCt = thisStyleControls.Count;
            for (int ii = 0; ii < styleCt; ++ii) thisStyleControls[ii].action(this);
            centers[iw].x = centers[iw - 1].x + nextTrueDelta.x;
            centers[iw].y = centers[iw - 1].y + nextTrueDelta.y;
            path.Update(in lifetime, ref bpi, out nextDirectionalDelta, out nextTrueDelta, in updateStagger);
        }
    }

    public override void UpdateMovement(float dT) {
        bpi.loc = locater.GlobalPosition();
        UpdateRotation();
        base.UpdateMovement(dT);
    }

    public override void UpdateRender() {
        ReassignTransform();
        SetEndpoint(centers[texRptWidth], nextTrueDelta);
        base.UpdateRender();
    }
    
    public void SetupEndpoint(Danmaku.Laser.PointContainer ep) {
        endpt = ep;
        if (endpt.exists) endpt.beh.TakeParent(laser);
    }

    /// <summary>
    /// </summary>
    /// <param name="localPos"></param>
    /// <param name="dir">(Unnormalized) direction to point in.</param>
    protected void SetEndpoint(Vector2 localPos, Vector2 dir) {
        if (endpt.exists) {
            endpt.beh.ExternalSetLocalPosition(localPos);
            endpt.beh.FaceInDirection(dir);
        }
    }

    protected override unsafe void UpdateVerts(bool renderRequired) {
        path.ResetFlip();
        int vw = texRptWidth + 1;
        path.rootPos = bpi.loc;
        bpi.t = 0;
        path.Update(in lifetime, ref bpi, out Vector2 accP, out var d1, 0f);
        centers[0] = d1;
        if (!renderRequired) {
            UpdateCentersOnly();
        } else {
            float ddf = spriteBounds.y / 2f;
            float distToSpriteWMult = 1f / spriteBounds.x;
            //The direction model is: each point is oriented perpendicular to the step *preceding it*.
            //While it's easier to make points perpendicular to the *proceeding* step, this model is intercompatible
            //with Pather, which is more important.
            //This does require special handling for index 0; we give it the same direction as index 1.
            vertsPtr[0].uv.x = vertsPtr[vw].uv.x = 0f; 
            //WARNING This will break if you give it zero velocity; avoid giving lasers parametrics that eval to zero!
            path.Update(in lifetime, ref bpi, out nextDirectionalDelta, out nextTrueDelta, in updateStagger);
            float denormedDirF = ddf / (float) Math.Sqrt(nextTrueDelta.x * nextTrueDelta.x + nextTrueDelta.y * nextTrueDelta.y);
            vertsPtr[0].loc.x = accP.x + denormedDirF * nextTrueDelta.y;
            vertsPtr[0].loc.y = accP.y + denormedDirF * -nextTrueDelta.x;
            vertsPtr[vw].loc.x = accP.x + denormedDirF * -nextTrueDelta.y;
            vertsPtr[vw].loc.y = accP.y + denormedDirF * nextTrueDelta.x;
            for (int iw = 1; iw < vw; ++iw) {
                int styleCt = thisStyleControls.Count;
                for (int ii = 0; ii < styleCt; ++ii) thisStyleControls[ii].action(this);
                accP.x += nextTrueDelta.x;
                accP.y += nextTrueDelta.y;
                centers[iw].x = accP.x;
                centers[iw].y = accP.y;
                float dist = (float) Math.Sqrt(nextTrueDelta.x * nextTrueDelta.x + nextTrueDelta.y * nextTrueDelta.y);
                vertsPtr[iw].uv.x = vertsPtr[iw + vw].uv.x = vertsPtr[iw - 1].uv.x + dist * distToSpriteWMult;
                denormedDirF = ddf / dist;
                vertsPtr[iw].loc.x = accP.x + denormedDirF * nextTrueDelta.y;
                vertsPtr[iw].loc.y = accP.y + denormedDirF * -nextTrueDelta.x;
                vertsPtr[iw + vw].loc.x = accP.x + denormedDirF * -nextTrueDelta.y;
                vertsPtr[iw + vw].loc.y = accP.y + denormedDirF * nextTrueDelta.x;
                if (intersectStatus != SelfIntersectionStatus.RAS) {
                    RecallSelfIntersection(nextDirectionalDelta, BACKSTEP, iw > 6 ? iw - 6 : 0, iw, ddf);
                }
                path.Update(in lifetime, ref bpi, out nextDirectionalDelta, out nextTrueDelta, in updateStagger);
            }
        }
        if (alignEnd) {
            for (int ii = 0; ii < vw; ++ii) {
                vertsPtr[ii].uv.x -= vertsPtr[vw - 1].uv.x;
                vertsPtr[ii + vw].uv.x = vertsPtr[ii].uv.x;
            }
        }
        
        //Vector2 bd_mid = new Vector3(centers[vw-1].x / 2,  centers[vw-1].y / 2, 0f);
        //bds = new Bounds(bd_mid, centers[vw - 1]);
    }

    private const float BACKSTEP = 2f;

    public CollisionResult CheckCollision(SOCircle target) {
        float rot = M.degRad * (parented ? tr.eulerAngles.z : simpleEulerRotation.z);
        // 10000 is a number that is big enough to usually ensure only one collision iteration for simple lasers.
        // If it's not big enough, then you'll have two collision iteration, which is fine.
        return DMath.Collision.GrazeCircleOnSegments(target, locater.GlobalPosition(), centers, 0, path.isSimple ? 10000 : 1, centers.Length, scaledLineRadius, Mathf.Cos(rot), Mathf.Sin(rot));
    }

    private bool requiresTrRotUpdate = false;
    //This function may be called during parallel update step, so we can't do the rotation reassignment here
    private void UpdateRotation() {
        bpi.t = lifetime; // Note that we use bpi.t to store path-time as well as life-time, depending on context.
        //The rotation function uses life-time and the path functions use path-time.
        float newRot = path.RotationDeg(bpi) % 360f;
        requiresTrRotUpdate = (Math.Abs(simpleEulerRotation.z - newRot) > float.Epsilon);
        simpleEulerRotation.z = newRot;
    }

    private void ReassignTransform() {
        if (requiresTrRotUpdate) {
            tr.localEulerAngles = simpleEulerRotation;
        }
    }

    public override void Activate() {
        base.Activate();
        UpdateRotation();
        requiresTrRotUpdate = true;
        ReassignTransform();
    }


    //TODO (iparent) does this work on parented stuff? check the phoenix spell
    public void SpawnSimple(string style) {
        int skip = Mathf.CeilToInt(0.5f / updateStagger);
        Vector2 basePos = locater.GlobalPosition();
        for (int ii = centers.Length - 1; ii > 0; ii -= skip) {
            BulletManager.RequestSimple(style, null, null,
                new Velocity(M.RotateVectorDeg(centers[ii], simpleEulerRotation.z) + basePos, 
                    M.RotateVectorDeg((centers[ii] - centers[ii-1]).normalized, simpleEulerRotation.z))
                , 0, 0, null);
        }
    }

    private void FlipBPIAndDeltaSimple(bool y, float around) {
        if (y) {
            float prevy = bpi.loc.y;
            bpi.FlipSimple(true, around);
            nextTrueDelta.y += bpi.loc.y - prevy;
            nextDirectionalDelta.y *= -1;
        } else {
            float prevx = bpi.loc.x;
            bpi.FlipSimple(false, around);
            nextTrueDelta.x += bpi.loc.x - prevx;
            nextDirectionalDelta.x *= -1;
        }
    }
    
#if UNITY_EDITOR
    public unsafe void Draw() {
        Handles.color = Color.cyan;
        GenericColliderInfo.DrawGizmosForSegments(centers, 0, 1, centers.Length, locater.GlobalPosition(), scaledLineRadius, 0);
        
        Handles.color = Color.red;
        Vector3 rp = locater.GlobalPosition();
        for (int ii = 0; ii < texRptWidth + 1; ++ii) {
            Handles.DrawWireDisc(vertsPtr[ii].loc + rp, Vector3.forward, 0.01f);
        }
        Handles.color = Color.yellow;
        for (int ii = 0; ii < texRptWidth + 1; ++ii) {
            Handles.DrawWireDisc(vertsPtr[ii + texRptWidth + 1].loc + rp, Vector3.forward, 0.01f);
        }
    }
#endif
    
    
    /// <summary>
    /// Complex bullet pool control descriptor.
    /// </summary>
    public readonly struct LaserControl {
        public readonly LCF action;
        public readonly Pred persist;

        public LaserControl(LCF act, Pred persistent) {
            action = act;
            persist = persistent;
        }
    }
    
    /// <summary>
    /// Pool controls for laser paths.
    /// Keys are added the first time a command is created or a bullet is spawned.
    /// All controls are persistent.
    /// </summary>
    private static readonly Dictionary<string, DMCompactingArray<LaserControl>> controls = new Dictionary<string, DMCompactingArray<LaserControl>>();
    //For quick iteration
    private static readonly List<DMCompactingArray<LaserControl>> initializedPools = new List<DMCompactingArray<LaserControl>>(16);
    private DMCompactingArray<LaserControl> thisStyleControls;
    
    private static DMCompactingArray<LaserControl> LazyGetControls(string style) {
        if (!controls.ContainsKey(style)) {
            controls[style] = new DMCompactingArray<LaserControl>();
            initializedPools.Add(controls[style]);
        }
        return controls[style];
    }
    
    
    //Warning: these commands MUST be destroyed in the scope in which they are created, otherwise you will get cT disposal errors.
    public static void ControlPoolSM(Pred persist, BulletManager.StyleSelector styles, SM.StateMachine sm, CancellationToken cT, LPred condFunc) {
        LaserControl lc = new LaserControl(b => {
            if (condFunc(b.bpi, b.lifetime)) {
                //TODO (iparent) rotate lastDelta by global euler angle
                _ = BEHPooler.INode(b.bpi.loc, V2RV2.Angle(b.laser.original_angle), b.nextTrueDelta, b.bpi.index, 
                    null, "f-pool-triggered").RunExternalSM(SMRunner.Cull(sm, cT));
            }
        }, persist);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            LazyGetControls(styles.Complex[ii]).Add(lc);
        }
    }
    
    /// <summary>
    /// Pool controls for use with the `bulletl-control` SM command. These deal with the draw-paths of lasers.
    /// </summary>
    public static class LaserControls {
        /// <summary>
        /// Flip the x-velocity and x-position of laser paths around a wall on the right.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static LCF FlipXGT(float wall, Pred cond) {
            return b => {
                if (b.bpi.loc.x > wall && cond(b.bpi)) {
                    b.FlipBPIAndDeltaSimple(false, wall);
                    b.path.FlipX();
                    b.intersectStatus = SelfIntersectionStatus.CHECK_THIS_AND_NEXT;
                }
            };
        }
        /// <summary>
        /// Flip the x-velocity and x-position of laser paths around a wall on the left.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static LCF FlipXLT(float wall, Pred cond) {
            return b => {
                if (b.bpi.loc.x < wall && cond(b.bpi)) {
                    b.FlipBPIAndDeltaSimple(false, wall);
                    b.path.FlipX();
                    b.intersectStatus = SelfIntersectionStatus.CHECK_THIS_AND_NEXT;
                }
            };
        }
        /// <summary>
        /// Flip the y-velocity and y-position of laser paths around a wall on the top.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static LCF FlipYGT(float wall, Pred cond) {
            return b => {
                if (b.bpi.loc.y > wall && cond(b.bpi)) {
                    b.FlipBPIAndDeltaSimple(true, wall);
                    b.path.FlipY();
                    b.intersectStatus = SelfIntersectionStatus.CHECK_THIS_AND_NEXT;
                }
            };
        }
        /// <summary>
        /// Flip the y-velocity and y-position of laser paths around a wall on the bottom.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static LCF FlipYLT(float wall, Pred cond) {
            return b => {
                if (b.bpi.loc.y < wall && cond(b.bpi)) {
                    b.FlipBPIAndDeltaSimple(true, wall);
                    b.path.FlipY();
                    b.intersectStatus = SelfIntersectionStatus.CHECK_THIS_AND_NEXT;
                }
            };
        }
    }
    //Laser controls are always persistent
    public static void ControlPool(Pred persist, BulletManager.StyleSelector styles, LCF control) {
        LaserControl lc = new LaserControl(control, persist);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            LazyGetControls(styles.Complex[ii]).Add(lc);
        }
    }
    
    
    public static class PoolControls {
        /// <summary>
        /// Clear the bullet controls on a pool.
        /// </summary>
        /// <returns></returns>
        public static LPCF Reset() {
            return pool => LazyGetControls(pool).Empty();
        }
    }
    
    public static void ControlPool(BulletManager.StyleSelector styles, LPCF control) {
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            control(styles.Complex[ii]);
        }
    }
    public static void PruneControls() {
        for (int ii = 0; ii < initializedPools.Count; ++ii) {
            var pcs = initializedPools[ii];
            for (int jj = 0; jj < pcs.Count; ++jj) {
                if (!pcs[jj].persist(GlobalBEH.Main.rBPI)) {
                    pcs.Delete(jj);
                } 
            }
            pcs.Compact();
        }
    }

    public static void ClearControls() {
        for (int ii = 0; ii < initializedPools.Count; ++ii) {
            initializedPools[ii].Empty();
        }
    }
}

}