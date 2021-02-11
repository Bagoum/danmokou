using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku;
using DMK.Danmaku.Descriptors;
using DMK.Danmaku.Options;
using DMK.DataHoist;
using DMK.DMath;
using DMK.Pooling;
using DMK.Scriptables;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace DMK.Graphics {
/// <summary>
/// A bullet control function performing some operation on a laser.
/// </summary>
public delegate void LCF(CurvedTileRenderLaser ctr);
/// <summary>
/// A pool control function performing some operation on a laser style.
/// </summary>
public delegate void LPCF(string pool);

[Serializable]
public class LaserRenderCfg : TiledRenderCfg {
    public float lineRadius;
    public bool alignEnd;
    public bool stretch;
}

//These are always initialized manually, and may or may not be pooled.
public class CurvedTileRenderLaser : CurvedTileRender {
    private LaserMovement path;
    private Vector3 simpleEulerRotation = new Vector3(0, 0, 0);
    private ParametricInfo bpi = new ParametricInfo();
    private readonly float lineRadius;
    private const float defaultUpdateStagger = 0.1f;
    private float updateStagger;
    private BPY? variableLength;
    private BPY? variableStart;
    private Pred? deactivator;
    private BPY? hueShift;
    private (TP4 black, TP4 white)? recolor;
    private TP4? tinter;
    private readonly bool alignEnd = false;
    private readonly bool stretch = false;
    private Laser laser = null!;
    private float scaledLineRadius;
    private Laser.PointContainer endpt = new Laser.PointContainer(null);
    private PlayerBulletCfg? playerBullet;
    [UsedImplicitly]
    public bool playerBulletIsColliding;
    //Player bullets only
    private int nonpiercingLength;
    private bool nonpiercing;
    public float LastActiveTime { get; private set; }

    public CurvedTileRenderLaser(LaserRenderCfg cfg, GameObject obj) : base(obj) {
        alignEnd = cfg.alignEnd;
        stretch = cfg.stretch;
        lineRadius = cfg.lineRadius;
    }

    public void SetYScale(float scale) {
        PersistentYScale = scale;
        scaledLineRadius = lineRadius * scale;
    }

    //TileRenders are always Initialize-initialized.
    public void Initialize(Laser locationer, TiledRenderCfg cfg, Material material, bool isNew, ParametricInfo pi, ref RealizedLaserOptions options) {
        laser = locationer;
        updateStagger = options.staggerMultiplier * defaultUpdateStagger;
        variableLength = options.varLength;
        variableStart = options.start;
        deactivator = options.deactivate;
        playerBullet = options.playerBullet;
        int newTexW = Mathf.CeilToInt(options.maxLength / updateStagger);
        base.Initialize(locationer, cfg, material, isNew, options.isStatic, playerBullet != null, newTexW); //doesn't do any mesh generation, safe to call first
        path = options.lpath;
        bpi = pi;
        bpi.loc = locater.GlobalPosition();
        playerBulletIsColliding = false;
        nonpiercingLength = centers.Length;
        nonpiercing = options.nonpiercing;
        hueShift = options.hueShift;
        recolor = options.recolor;
        tinter = options.tint;
        LastActiveTime = M.IntFloatMax;
        //This needs to be reset to zero here to ensure that the value isn't dirty, since hue-shift is always active
        pb.SetFloat(PropConsts.HueShift, hueShift?.Invoke(bpi) ?? 0f);
        //Likewise
        pb.SetColor(PropConsts.tint, tinter?.Invoke(bpi) ?? Color.white);
        if (hueShift != null || recolor != null || tinter != null) DontUpdateTimeAfter = M.IntFloatMax;
    }
    
    //(this, material, isNew, bpi.id, firingIndex, ref RealizedLaserOptions);

    public void UpdateLaserStyle(string? style) {
        myStyle = string.IsNullOrEmpty(style) ? defaultMeta : LazyGetControls(style!);
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
            ang = M.Atan(centers[Math.Min(centers.Length - 1, idxH + 1)] - locL);
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


    public override void UpdateMovement(float dT) {
        bpi.loc = locater.GlobalPosition();
        UpdateRotation();
        base.UpdateMovement(dT);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateGraphics() {
        bpi.t = lifetime;
        if (hueShift != null) {
            pb.SetFloat(PropConsts.HueShift, M.degRad * hueShift(bpi));
        }
        if (recolor.Try(out var rc)) {
            pb.SetVector(PropConsts.RecolorB, rc.black(bpi));
            pb.SetVector(PropConsts.RecolorW, rc.white(bpi));
        }
        if (tinter != null) {
            pb.SetVector(PropConsts.tint, tinter(bpi));
        }
    }
    public override void UpdateRender() {
        ReassignTransform();
        SetEndpoint(centers[texRptWidth], nextTrueDelta);
        if (ETime.LastUpdateForScreen) {
            UpdateGraphics();
        }
        base.UpdateRender();
    }
    
    public void SetupEndpoint(Laser.PointContainer ep) {
        endpt = ep;
        if (endpt.exists) endpt.beh!.TakeParent(laser);
    }

    /// <summary>
    /// </summary>
    /// <param name="localPos"></param>
    /// <param name="dir">(Unnormalized) direction to point in.</param>
    protected void SetEndpoint(Vector2 localPos, Vector2 dir) {
        if (endpt.exists) {
            endpt.beh!.ExternalSetLocalPosition(localPos);
            endpt.beh.SetDirection(dir);
        }
    }

    private void UpdateCentersOnly(int startP, int endP) {
        int vw = texRptWidth + 1;
        path.Update(in lifetime, ref bpi, out nextDirectionalDelta, out nextTrueDelta, in updateStagger);
        for (int iw = 1; iw < endP; ++iw) {
            myStyle.IterateControls(this);
            centers[iw].x = centers[iw - 1].x + nextTrueDelta.x;
            centers[iw].y = centers[iw - 1].y + nextTrueDelta.y;
            path.Update(in lifetime, ref bpi, out nextDirectionalDelta, out nextTrueDelta, in updateStagger);
        }
        for (int iw = 0; iw < startP; ++iw) {
            centers[iw] = centers[startP];
        }
        for (int iw = endP; iw < vw; ++iw) {
            centers[iw] = centers[endP - 1];
        }
    }
    protected override unsafe void UpdateVerts(bool renderRequired) {
        path.ResetFlip();
        int vw = texRptWidth + 1;
        path.rootPos = bpi.loc;
        bpi.t = lifetime;
        if (deactivator?.Invoke(bpi) == true) {
            deactivator = null;
            LastActiveTime = bpi.t;
        }
        int startP = (variableStart == null) ? 0 : M.Clamp(0, vw - 1, Mathf.RoundToInt(variableStart(bpi) / updateStagger));
        int endP = (variableLength == null) ? vw : M.Clamp(1 + startP, vw, Mathf.RoundToInt(variableLength(bpi) / updateStagger));
        bpi.t = 0;
        path.Update(in lifetime, ref bpi, out Vector2 accP, out var d1, 0f);
        centers[0] = d1;
        if (!renderRequired) {
            UpdateCentersOnly(startP, endP);
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
            //You still need to update starting from zero, not startP, so that the displayed points will be in the correct position for eg. velocity equations.
            for (int iw = 1; iw < endP; ++iw) {
                myStyle.IterateControls(this);
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
            for (int iw = 0; iw < startP; ++iw) {
                centers[iw] = centers[startP];
                vertsPtr[iw] = vertsPtr[startP];
                vertsPtr[iw + vw] = vertsPtr[startP + vw];
            }
            for (int iw = endP; iw < vw; ++iw) {
                centers[iw] = centers[endP - 1];
                vertsPtr[iw] = vertsPtr[endP - 1];
                vertsPtr[iw + vw] = vertsPtr[endP - 1 + vw];
            }
        }
        if (alignEnd) {
            for (int ii = 0; ii < vw; ++ii) {
                vertsPtr[ii].uv.x -= vertsPtr[vw - 1].uv.x;
                vertsPtr[ii + vw].uv.x = vertsPtr[ii].uv.x;
            }
        }
        if (stretch) {
            float w = vertsPtr[vw - 1].uv.x;
            for (int ii = 0; ii < vw; ++ii) {
                vertsPtr[ii].uv.x /= w;
                vertsPtr[ii + vw].uv.x = vertsPtr[ii].uv.x;
            }
        }
        
        //Vector2 bd_mid = new Vector3(centers[vw-1].x / 2,  centers[vw-1].y / 2, 0f);
        //bds = new Bounds(bd_mid, centers[vw - 1]);
    }

    private const float BACKSTEP = 2f;

    public CollisionResult CheckCollision(SOPlayerHitbox target) {
        playerBulletIsColliding = false;
        float rot = M.degRad * (parented ? tr.eulerAngles.z : simpleEulerRotation.z);
        if (playerBullet.Try(out var plb)) {
            var fe = Enemy.FrozenEnemies;
            var loc = locater.GlobalPosition();
            if (nonpiercing) {
                void CheckUntilNonpiercingLength() {
                    int nextCollSegment = nonpiercingLength;
                    for (int ii = 0; ii < fe.Count; ++ii) {
                        if (fe[ii].Active && CollisionMath.CircleOnSegments(fe[ii].pos, fe[ii].radius, loc, 
                            centers, 0, 1, nonpiercingLength, scaledLineRadius, 
                            (float)Math.Cos(rot), (float)Math.Sin(rot), out int segment)) {
                            playerBulletIsColliding = true;
                            //Don't modify playerBulletCollSegment in order that the collision check is the same for all enemies
                            //segment+1 since segment is inclusive, but collLength is exclusive
                            nextCollSegment = Math.Min(nextCollSegment, segment + 1);
                            if (fe[ii].enemy.TryHitIndestructible(bpi.id, plb.cdFrames)) {
                                fe[ii].enemy.QueuePlayerDamage(plb.bossDmg, plb.stageDmg, target.location);
                                fe[ii].enemy.ProcOnHit(plb.effect, loc + centers[segment]);
                            }
                        }
                    }
                    nonpiercingLength = nextCollSegment;
                }
                CheckUntilNonpiercingLength();
                
                if (!playerBulletIsColliding && nonpiercingLength < centers.Length) {
                    //Extend the nonpiercing laser and try again
                    nonpiercingLength = Mathf.RoundToInt(Mathf.Clamp(
                        Mathf.Lerp(nonpiercingLength, centers.Length, 0.05f), 
                        nonpiercingLength + 1, centers.Length));
                    CheckUntilNonpiercingLength();
                }
                
                //Pull the laser draw back
                unsafe {
                    for (int iw = nonpiercingLength; iw < centers.Length; ++iw) {
                        //centers[iw] = centers[endP - 1];
                        vertsPtr[iw] = vertsPtr[nonpiercingLength - 1];
                        vertsPtr[iw + centers.Length] = vertsPtr[nonpiercingLength - 1 + centers.Length];
                    }
                }

            } else {
                for (int ii = 0; ii < fe.Count; ++ii) {
                    if (fe[ii].Active && CollisionMath.CircleOnSegments(fe[ii].pos, fe[ii].radius, loc, 
                        centers, 0, 1, centers.Length, scaledLineRadius, 
                        (float)Math.Cos(rot), (float)Math.Sin(rot), out int segment)) {
                        playerBulletIsColliding = true;
                        if (fe[ii].enemy.TryHitIndestructible(bpi.id, plb.cdFrames)) {
                            fe[ii].enemy.QueuePlayerDamage(plb.bossDmg, plb.stageDmg, target.location);
                            fe[ii].enemy.ProcOnHit(plb.effect, loc + centers[segment]);
                        }
                    }
                }
            }
            
            return CollisionResult.noColl;
        }
        
        if (!target.Active) return CollisionResult.noColl;

        // 10000 is a number that is big enough to usually ensure only one collision iteration for simple lasers.
        // If it's not big enough, then you'll have two collision iteration, which is fine.
        return CollisionMath.GrazeCircleOnSegments(target.Hitbox, locater.GlobalPosition(), centers, 0, 
            path.isSimple ? 10000 : 1, centers.Length, scaledLineRadius, (float)Math.Cos(rot), (float)Math.Sin(rot));
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
        UpdateGraphics();
        base.Activate();
        UpdateRotation();
        requiresTrRotUpdate = true;
        ReassignTransform();
    }
    
    public void SpawnSimple(string style) {
        int skip = Mathf.CeilToInt(0.5f / updateStagger);
        Vector2 basePos = locater.GlobalPosition();
        for (int ii = centers.Length - 1; ii > 0; ii -= skip) {
            BulletManager.RequestNullSimple(style, M.RotateVectorDeg(centers[ii], simpleEulerRotation.z) + basePos, 
                    M.RotateVectorDeg((centers[ii] - centers[ii-1]).normalized, simpleEulerRotation.z));
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
        public readonly ICancellee cT;

        public LaserControl(LCF act, Pred persistent, ICancellee cT) {
            action = act;
            persist = persistent;
            this.cT = cT;
        }
    }

    private class LaserMetadata {
        public readonly BehaviorEntity.BEHStyleMetadata? metadata;

        public LaserMetadata(BehaviorEntity.BEHStyleMetadata? bsm) {
            this.metadata = bsm;
        }
        public void ResetPoolMetadata() { }

        public void Reset() => ResetPoolMetadata();
        
        public void AddPoolControl(LaserControl pc) => controls.Add(pc);
        
        public void PruneControls() {
            for (int ii = 0; ii < controls.Count; ++ii) {
                if (controls[ii].cT.Cancelled || !controls[ii].persist(ParametricInfo.Zero)) {
                    controls.Delete(ii);
                }
            }
            controls.Compact();
        }
        public void ClearControls() => controls.Empty();
        
        private readonly DMCompactingArray<LaserControl> controls = new DMCompactingArray<LaserControl>(4);
        
        public void IterateControls(CurvedTileRenderLaser laser) {
            int ct = controls.Count;
            for (int ii = 0; ii < ct; ++ii) {
                controls[ii].action(laser);
            }
        }
    }
    private static readonly LaserMetadata defaultMeta = new LaserMetadata(null);
    
    /// <summary>
    /// Pool controls for laser paths.
    /// Keys are added the first time a command is created or a bullet is spawned and reset on scene.
    /// They are not constructed on init because they store no metadata.
    /// </summary>
    private static readonly Dictionary<string, LaserMetadata> activePools = new Dictionary<string, LaserMetadata>();
    //For quick iteration
    private static readonly List<LaserMetadata> activePoolsList = new List<LaserMetadata>(16);
    private LaserMetadata myStyle = null!;
    
    public static void DeInitializePools() {
        foreach (var x in activePoolsList) {
            x.Reset();
        }
        activePools.Clear();
        activePoolsList.Clear();
    }
    private static LaserMetadata LazyGetControls(string style) {
        if (!activePools.ContainsKey(style)) {
            activePools[style] = new LaserMetadata(BehaviorEntity.GetPool(style));
            activePoolsList.Add(activePools[style]);
        }
        return activePools[style];
    }
    
    
    //Warning: these commands MUST be destroyed in the scope in which they are created, otherwise you will get cT disposal errors.
    public static void ControlPoolSM(Pred persist, BulletManager.StyleSelector styles, SM.StateMachine sm, ICancellee cT, LPred condFunc) {
        LaserControl lc = new LaserControl(b => {
            if (condFunc(b.bpi, b.lifetime)) {
                var mov = new Movement(b.bpi.loc, V2RV2.Angle(b.laser.original_angle));
                _ = BEHPooler.INode(mov, new ParametricInfo(in mov, b.bpi.index), b.nextTrueDelta, "l-pool-triggered")
                    .RunExternalSM(SMRunner.Cull(sm, cT));
            }
        }, persist, cT);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            LazyGetControls(styles.Complex[ii]).AddPoolControl(lc);
        }
    }
    
    /// <summary>
    /// Repository for functions that can be applied to lasers via the `bulletl-control` command.
    /// Note lasers can also be affected by `beh-control`, but these functions deal specifically with laser draw-paths.
    /// </summary>
    [Reflect]
    public static class LaserControls {
        /// <summary>
        /// Flip the x-velocity and x-position of laser paths around a wall on the right.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [Alias("flipx>")]
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
        [Alias("flipx<")]
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
        [Alias("flipy>")]
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
        [Alias("flipy<")]
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
    public static void ControlPool(Pred persist, BulletManager.StyleSelector styles, LCF control, ICancellee cT) {
        LaserControl lc = new LaserControl(control, persist, cT);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            LazyGetControls(styles.Complex[ii]).AddPoolControl(lc);
        }
    }
    
    /// <summary>
    /// Repository for functions that can be applied to lasers via the `pooll-control` command.
    /// These functions are applied to the metadata for each laser style, rather than the objects themselves.
    /// </summary>
    [Reflect]
    public static class PoolControls {
        /// <summary>
        /// Clear the bullet controls on a pool.
        /// </summary>
        /// <returns></returns>
        public static LPCF Reset() {
            return pool => LazyGetControls(pool).ClearControls();
        }
    }
    
    public static void ControlPool(BulletManager.StyleSelector styles, LPCF control) {
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            control(styles.Complex[ii]);
        }
    }
    public static void PrunePoolControls() {
        for (int ii = 0; ii < activePools.Count; ++ii) {
            activePoolsList[ii].PruneControls();
        }
    }

    public static void ClearPoolControls(bool clearPlayer) {
        for (int ii = 0; ii < activePools.Count; ++ii) {
            if (clearPlayer || (activePoolsList[ii].metadata?.IsPlayer == false)) activePoolsList[ii].ClearControls();
        }
    }
}

}