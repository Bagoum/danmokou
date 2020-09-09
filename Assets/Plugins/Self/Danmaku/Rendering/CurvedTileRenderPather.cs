using System;
using System.Reflection;
using DMath;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

//Warning to future self: Pathers function really strangely in that their transform is located at the starting point, and the mesh
//simply moves farther away from that source. The transform does not move. The reason for this is that it's difficult to maintain
//a list of points you travelled through relative to your current point, but it's easy to maintain a list of points that someone else
//has travelled through relative to your static point, which is essentially what we do. This is why pathers do not support parenting.

namespace Danmaku {
[Serializable]
public class PatherRenderCfg : TiledRenderCfg {
    public float lineRadius;
    public float headCutoffRatio = 0.05f;
    public float tailCutoffRatio = 0.05f;
    public Transform trail;
    public TrailRenderer trailR;
}
/// <summary>
/// A pather remembers the positions it has been in and draws a line through them. WARNING: This currently does not support parenting.
/// </summary>
public class CurvedTileRenderPather : CurvedTileRender {
    /// = 0.033s
    private const int FramePosCheck = 4;
    private int cL;
    private readonly float lineRadius;
    private float scaledLineRadius;
    private readonly float headCutoffRatio;
    private readonly float tailCutoffRatio;
    private BPY remember;
    private Velocity velocity;
    /// <summary>
    /// The last return value of Velocity.Update. Used for backstepping.
    /// </summary>
    private Vector2 lastDelta;
    private int read_from;
    private ParametricInfo bpi;
    public ref ParametricInfo BPI => ref bpi;
    
    //set in pathtracker.awake
    private Action onCameraCulled = null;
    private bool checkCameraCull = false;
    private int cullCtr;
    private const int checkCullEvery = 120;
    /// <summary>
    /// For pathers with a lot of bending effects, use "smooth" to force it to update every engine frame
    /// instead of only on camera-render frames. This gives it twice as much resolution for collision and mesh.
    /// </summary>
    //Note: this must be true for pathers to be correct w.r.t replays.
    private bool updateEveryFrame = true;
    private float updateRate;

    private Pather exec;

    //Note: trailRenderer requires reversing the sprite.
    protected override bool UseMR => false;
    private readonly Transform trail;
    public readonly TrailRenderer trailR;

    private SOCircle target;

    public CurvedTileRenderPather(PatherRenderCfg cfg, GameObject obj) : base(cfg, obj) {
        lineRadius = cfg.lineRadius;
        trail = cfg.trail;
        trailR = cfg.trailR;
        tailCutoffRatio = cfg.tailCutoffRatio;
        headCutoffRatio = cfg.headCutoffRatio;
    }
    public void SetYScale(float scale) {
        PersistentYScale = scale;
        scaledLineRadius = lineRadius * scale;
    }
    public void Initialize(Pather locationer, Material material, bool isNew, Velocity vel, uint bpiId, int firingIndex, BPY rememberTime, float maxRememberTime, SOCircle collisionTarget, ref RealizedBehOptions options) {
        exec = locationer;
        updateEveryFrame = true;//options.smooth;
                                //Now that we are using TrailRender, we should always update centers for accuracy.
                                //Consider removing code related to smooth later.
                                //Note: this must be true for pathers to be correct w.r.t replays.
        updateRate = updateEveryFrame ? ETime.ENGINEFPS : ETime.SCREENFPS;
        int newTexW = (int) Math.Ceiling(maxRememberTime * updateRate) + 1; 
        base.Initialize(locationer, material, isNew, false, newTexW, options.hueShift);
        if (locationer.HasParent()) throw new NotImplementedException("Pather cannot be parented");
        velocity = vel;
        bpi = new ParametricInfo(vel.rootPos, firingIndex, bpiId);
        _ = velocity.UpdateZero(ref bpi, 0f);
        lastDataIndex = cL = centers.Length;
        remember = rememberTime;
        intersectStatus = SelfIntersectionStatus.RAS;
        read_from = cL;
        //isnonzero = false;
        unsafe {
            for (int ii = 0; ii < cL; ++ii) {
                int iivw = ii + cL;
                //vertsPtr[ii].loc.x = vertsPtr[ii].loc.y = vertsPtr[iivw].loc.x = 
                //    vertsPtr[iivw].loc.y = vertsPtr[ii].uv.x = vertsPtr[iivw].uv.x = 0;
                centers[ii].x = centers[ii].y = 0;
            }
            centers[cL - 1] = bpi.loc - velocity.rootPos;
        }
        prevRemember = trailR.time = 0f;
        trailR.sharedMaterial = material;
        trailR.sortingLayerID = mr.sortingLayerID;
        trailR.sortingOrder = mr.sortingOrder;
        skipNextCollisionCheck = false;
        target = collisionTarget;
    }

    public Vector2 GlobalPosition => bpi.loc;

    public void SetCameraCullable(Action onCull) {
        onCameraCulled = onCull;
    }

    public void SetCameraCullable(bool onoff) {
        checkCameraCull = onoff;
    }
    private const float CULL_RAD = 4;
    private const float FIRST_CULLCHECK_TIME = 4;
    //This is run during Update
    private bool CullCheck() {
        if (bpi.t > FIRST_CULLCHECK_TIME && LocationService.OffPlayableScreenBy(CULL_RAD, exec.RawGlobalPosition() + centers[read_from])) {
            onCameraCulled();
            return true;
        }
        return false;
    }

    public override void UpdateMovement(float dT) {
        velocity.UpdateDeltaAssignAcc(ref bpi, out lastDelta, dT);
        base.UpdateMovement(dT);
    }

    public override void UpdateRender() {
        exec.FaceInDirectionRaw(M.AtanD(lastDelta));
        base.UpdateRender();

        if (ETime.LastUpdateForScreen) {
            trail.localPosition = centers[cL - 1];
            //trailR.AddPosition(bpi.loc);
            if (prevRemember != nextRemember) {
                trailR.time = prevRemember = nextRemember;
            }
            if (lifetime < DontUpdateTimeAfter) trailR.SetPropertyBlock(pb);
        }
    }

    private float prevRemember = 0f;
    private float nextRemember = 0f;

    private bool skipNextCollisionCheck;

    //private bool isnonzero;
    /// <summary>
    /// The oldest index containing direction information.
    /// </summary>
    private int lastDataIndex;
    protected override unsafe void UpdateVerts(bool renderRequired) {
        var last = cL - 1;
        //repeat for range RegUpdFrames
        if (!updateEveryFrame && !renderRequired) {
            if (intersectStatus == SelfIntersectionStatus.CHECK_THIS_AND_NEXT) {
                intersectStatus = SelfIntersectionStatus.CHECK_THIS;
            }
            return;
        }
        Vector2 min = new Vector2(999,999);
        Vector2 max = new Vector2(-999,-999);
        float wx, wy;
        for (int ii = 0; ii < texRptWidth; ++ii) {
            /*vertsPtr[ii].loc.x = vertsPtr[ii + 1].loc.x;
            vertsPtr[ii].loc.y = vertsPtr[ii + 1].loc.y;
            vertsPtr[ii + cL].loc.x = vertsPtr[ii + cL + 1].loc.x;
            vertsPtr[ii + cL].loc.y = vertsPtr[ii + cL + 1].loc.y;*/
            centers[ii].x = wx = centers[ii + 1].x;
            centers[ii].y = wy = centers[ii + 1].y;
            if (wx < min.x) min.x = wx;
            if (wx > max.x) max.x = wx;
            if (wy < min.y) min.y = wy;
            if (wy > max.y) max.y = wy;
        }
        skipNextCollisionCheck = !DMath.Collision.CircleOnAABB(
            velocity.rootPos.x + 0.5f * (min.x + max.x) - target.location.x,
            velocity.rootPos.y + 0.5f * (min.y + max.y) - target.location.y,
            0.5f * (max.x - min.x) + lineRadius,
            0.5f * (max.y - min.y) + lineRadius,
            target.largeRadius, target.lradius2); 
        
        centers[last].x = bpi.loc.x - velocity.rootPos.x;
        centers[last].y = bpi.loc.y - velocity.rootPos.y;
        /*Vector2 accDelta = new Vector2(centers[last].x - centers[last-1].x, centers[last].y - centers[last-1].y);
        float mag = (float) Math.Sqrt(accDelta.x * accDelta.x + accDelta.y * accDelta.y);
        if (mag > M.MAG_ERR) {
            isnonzero = true;
            float ddf = spriteBounds.y * 0.5f / mag;
            vertsPtr[last].loc.x = centers[last].x + ddf * accDelta.y;
            vertsPtr[last].loc.y = centers[last].y + ddf * -accDelta.x;
            vertsPtr[last + cL].loc.x = centers[last].x + ddf * -accDelta.y;
            vertsPtr[last + cL].loc.y = centers[last].y + ddf * accDelta.x;
        } else if (!isnonzero) return; //If no nodes have been set, then we may get artifacting if we proceed.

        if (isnonzero) */
        lastDataIndex = (lastDataIndex > 0) ? lastDataIndex - 1 : 0;
        if (lastDataIndex == cL - 1) return; //Need at least two frames to draw

        int remembered = (int) Math.Ceiling((nextRemember = remember(bpi)) * updateRate);
        if (remembered < 2) remembered = 2;
        if (remembered > cL - lastDataIndex) remembered = cL - lastDataIndex;
        
        var new_read_from = cL - remembered;
        /*
        for (int uvi = read_from; uvi < new_read_from; ++uvi) {
            vertsPtr[uvi].uv.x = vertsPtr[uvi + cL].uv.x = 0;
        }*/
        read_from = new_read_from;
        /*
        float ratio = 1f / (remembered - 1);
        for (int uvi = read_from; uvi < cL; ++uvi) {
            vertsPtr[uvi].uv.x = vertsPtr[uvi + cL].uv.x = (uvi - read_from) * ratio;
        }*/

        /*
        if (intersectStatus != SelfIntersectionStatus.RAS) {
            RecallSelfIntersection(lastDelta, BACKSTEP, Math.Max(texRptWidth - 20 * updateRateMul, read_from), texRptWidth, spriteBounds.y / 2f);
        }*/
        /* else if (updateEveryFrame) {
            intersectStatus = SelfIntersectionStatus.CHECK_THIS;
            RecallSelfIntersection(lastDelta, BACKSTEP, Math.Max(texRptWidth - 5 * updateRateMul, read_from), texRptWidth, spriteBounds.y / 2f);
        }*/
        //Vector3 bd_mid = new Vector3(centers[vw-1].x / 2, centers[vw-1].y / 2, 0f);
        //bds = new Bounds(bd_mid, centers[vw-1]);
    }

    private const float BACKSTEP = 2f;

    public CollisionResult CheckCollision() {
        cullCtr = (cullCtr + 1) % checkCullEvery; 
        if ((cullCtr == 0 && checkCameraCull && CullCheck()) || skipNextCollisionCheck) return CollisionResult.noColl;
        
        int cut1 = (int) Math.Ceiling((cL - read_from + 1) * tailCutoffRatio);
        int cut2 = (int) Math.Ceiling((cL - read_from + 1) * headCutoffRatio);
        return DMath.Collision.GrazeCircleOnSegments(target, exec.RawGlobalPosition(), centers, read_from + cut1,
            FramePosCheck, cL - cut2, scaledLineRadius, 1, 0);
    }
    public Vector2 GetGlobalDirection() {
        return lastDelta.normalized;
    }
    public void FlipVelX() {
        velocity.FlipX();
        intersectStatus = SelfIntersectionStatus.CHECK_THIS_AND_NEXT;
    }
    public void FlipVelY() {
        velocity.FlipY();
        intersectStatus = SelfIntersectionStatus.CHECK_THIS_AND_NEXT;
    }

    public void SpawnSimple(string style) {
        Vector2 basePos = exec.RawGlobalPosition();
        for (int ii = texRptWidth; ii > read_from; ii -= FramePosCheck * 2) {
            BulletManager.RequestSimple(style, null, null,
                new Velocity(centers[ii] + basePos, (centers[ii] - centers[ii-1]).normalized)
                , 0, 0, null);
        }
    }
    
    
    public override void SetSprite(Sprite s, float yscale) {
        base.SetSprite(s, yscale);
        trailR.widthMultiplier = spriteBounds.y;
        trailR.SetPropertyBlock(pb);
    }

    public override void Deactivate() {
        base.Deactivate();
        trailR.emitting = false;
        trailR.Clear();
    }

    public override void Activate() {
        base.Activate();
        trail.localPosition = centers[cL - 1];
        trailR.SetPropertyBlock(pb);
        trailR.Clear();
        trailR.emitting = true;
    }


#if UNITY_EDITOR
    public unsafe void Draw() {
        Handles.color = Color.cyan;
        int cut1 = (int) Math.Ceiling((cL - read_from + 1) * tailCutoffRatio);
        int cut2 = Mathf.CeilToInt((cL - read_from + 1) * headCutoffRatio);
        GenericColliderInfo.DrawGizmosForSegments(centers, (read_from + cut1), 1, cL - cut2, exec.RawGlobalPosition(), scaledLineRadius, 0);
        /*
        Handles.color = Color.magenta;
        for (int ii = 0; ii < cL; ++ii) {
            Handles.DrawWireDisc(vertsPtr[ii].loc + (Vector3) velocity.rootPos, Vector3.forward, 0.005f + 0.005f * ii / cL);
        }
        Handles.color = Color.blue;
        for (int ii = 0; ii < cL; ++ii) {
            Handles.DrawWireDisc(vertsPtr[ii + cL].loc + (Vector3) velocity.rootPos, Vector3.forward, 0.005f + 0.005f *ii / cL);
        }*/
    }

    [ContextMenu("Debug info")]
    public void DebugPath() {
        //read_from = start + 1
        Log.Unity($"Start {read_from} Skip {FramePosCheck} End {centers.Length}", level: Log.Level.INFO);
    }
#endif
}
}