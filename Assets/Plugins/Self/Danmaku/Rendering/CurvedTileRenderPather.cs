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
/// <summary>
/// A pather remembers the positions it has been in and draws a line through them. WARNING: This currently does not support parenting.
/// </summary>
public class CurvedTileRenderPather : CurvedTileRender {
    private const int FramePosCheck = 4;
    private int _cL;
    public float lineRadius;
    private float scaledLineRadius;
    public float headCutoffRatio = 0.05f;
    public float tailCutoffRatio = 0.05f;
    private BPY remember;
    private Velocity velocity;
    /// <summary>
    /// The last return value of Velocity.Update. Used for backstepping.
    /// </summary>
    private Vector2 lastDelta;
    private float accTime;
    //Why accTime in addition to frameCtr?
    //Reason: Consider that we are updating every 2 frames, and RUF comes in on frame 3 (RUF=3) and frame 4 (RUF=1).
    //The times array needs to assign the first block to 3 frames and the second block to 1 frame to avoid stretching.
    //However, when the frame 4 update occurs, frameCtr=2. There is no way to remember that the last
    //update had leftover frames except by using accTime.
    //You can see this stretching effect as a ripple in the texture when the update isn't perfect.
    //It even occurs with VSYNC.
    private float[] times;
    protected int read_from;
    private ParametricInfo bpi;
    public ref ParametricInfo BPI => ref bpi;
    
    //set in pathtracker.awake
    private Action onCameraCulled = null;
    private bool checkCameraCull = false;
    private int cullCtr;
    private const int checkCullEvery = 120;
    /// <summary>
    /// For pathers with a lot of bending effects, use "smoothpather" to force it to update every engine frame
    /// instead of only on camera-render frames. This gives it twice as much resolution for collision and mesh.
    /// </summary>
    private bool updateEveryFrame = false;
    private float updateRate;
    private int updateRateMul;

    private Pather exec;

    public void SetYScale(float scale) {
        PersistentYScale = scale;
        scaledLineRadius = lineRadius * scale;
    }
    public void Initialize(Pather locationer, Material material, bool isNew, Velocity vel, uint bpiId, int firingIndex, BPY rememberTime, float maxRememberTime, ref RealizedBehOptions options) {
        exec = locationer;
        updateEveryFrame = options.smooth;
        updateRate = updateEveryFrame ? ETime.ENGINEFPS : ETime.SCREENFPS;
        updateRateMul = updateEveryFrame ? 2 : 1;
        int newTexW = (int) Math.Ceiling(maxRememberTime * updateRate);
        base.Initialize(locationer, material, isNew, false, newTexW, options.hueShift);
        if (locationer.HasParent()) throw new NotImplementedException("Pather cannot be parented");
        accTime = 0f;
        velocity = vel;
        bpi = new ParametricInfo(vel.rootPos, firingIndex, bpiId);
        _ = velocity.UpdateZero(ref bpi, 0f);
        _cL = centers.Length;
        remember = rememberTime;
        intersectStatus = SelfIntersectionStatus.RAS;
        read_from = _cL;
        isnonzero = false;
        unsafe {
            for (int ii = 0; ii <= texRptWidth; ++ii) {
                int iivw = ii + texRptWidth + 1;
                vertsPtr[ii].loc.x = vertsPtr[ii].loc.y = vertsPtr[iivw].loc.x = 
                    vertsPtr[iivw].loc.y = vertsPtr[ii].uv.x = vertsPtr[iivw].uv.x = 0;
                centers[ii].x = centers[ii].y = 0;
                times[ii] = 0f;
            }
            centers[texRptWidth] = bpi.loc - velocity.rootPos;
        }
    }

    public Vector2 GlobalPosition => exec.RawGlobalPosition() + centers[texRptWidth];

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

    protected override void OnNewMesh() {
        base.OnNewMesh();
        times = new float[texRptWidth + 1];
    }

    public void UpdateMovement(ushort fd, float dT) {
        velocity.UpdateDeltaAssignAcc(ref bpi, out lastDelta, dT);
        accTime += dT;
        base.UpdateMovement(dT);
    }

    public override void UpdateRender() {
        exec.FaceInDirectionRaw(M.AtanD(lastDelta));
        base.UpdateRender();
    }

    [ContextMenu("Debug uvx")]
    public unsafe void DebugUVX() {
        for (int ii = 0; ii <= texRptWidth; ++ii) {
            Debug.Log(vertsPtr[ii].uv.x);
        }
    }
    public unsafe bool IsIllegal() {
        float maxX = 0f;
        for (int ii = 0; ii <= texRptWidth; ++ii) {
            float nextX = vertsPtr[ii].uv.x;
            if (nextX < maxX) return true;
            maxX = nextX;
        }
        return false;
    }

    private bool isnonzero;
    
    protected override unsafe void UpdateVerts(bool renderRequired) {
        int vw = texRptWidth + 1; 
        //repeat for range RegUpdFrames
        if (!updateEveryFrame && !renderRequired) {
            if (intersectStatus == SelfIntersectionStatus.CHECK_THIS_AND_NEXT) {
                intersectStatus = SelfIntersectionStatus.CHECK_THIS;
            }
            return;
        }
        
        //Pull back previous positions
        for (int ii = 0; ii < texRptWidth; ++ii) {
            vertsPtr[ii].loc.x = vertsPtr[ii + 1].loc.x;
            vertsPtr[ii].loc.y = vertsPtr[ii + 1].loc.y;
            vertsPtr[ii + vw].loc.x = vertsPtr[ii + vw + 1].loc.x;
            vertsPtr[ii + vw].loc.y = vertsPtr[ii + vw + 1].loc.y;
            centers[ii].x = centers[ii + 1].x;
            centers[ii].y = centers[ii + 1].y;
            times[ii] = times[ii + 1];
        }
        //Add next position
        times[texRptWidth] = accTime;
        accTime = 0;
        centers[texRptWidth].x = bpi.loc.x - velocity.rootPos.x;
        centers[texRptWidth].y = bpi.loc.y - velocity.rootPos.y;
        Vector2 accDelta = new Vector2(centers[texRptWidth].x - centers[texRptWidth-1].x, centers[texRptWidth].y - centers[texRptWidth-1].y);
        float mag = (float) Math.Sqrt(accDelta.x * accDelta.x + accDelta.y * accDelta.y);
        if (mag > M.MAG_ERR) {
            isnonzero = true;
            float ddf = spriteBounds.y * 0.5f / mag;
            vertsPtr[texRptWidth].loc.x = centers[texRptWidth].x + ddf * accDelta.y;
            vertsPtr[texRptWidth].loc.y = centers[texRptWidth].y + ddf * -accDelta.x;
            vertsPtr[texRptWidth + vw].loc.x = centers[texRptWidth].x + ddf * -accDelta.y;
            vertsPtr[texRptWidth + vw].loc.y = centers[texRptWidth].y + ddf * accDelta.x;
        } else if (!isnonzero) {
            //If the pather is nonzero, then the previous value (the un-overwritten vertsPtr[trw] value)
            //is valid, so we can just use the previous value with no edits.
            //Otherwise, the previous value might be zero, which we don't want to copy,
            //so we make this node invisible.
            accTime = times[texRptWidth];
            times[texRptWidth] = 0;
        }

        int remembered = (int) Math.Ceiling(remember(bpi) * updateRate);
        remembered = (remembered > 2) ? ((remembered > vw) ? vw : remembered) : 2;
        //There must be at least one frame for a nonzero mesh, and we always ignore the frame before read_from.
        //This gives us at least two frames between which to draw a mesh.
        int new_zero = vw - remembered;
        for (int uvi = read_from; uvi <= new_zero; ++uvi) {
            vertsPtr[uvi].uv.x = vertsPtr[uvi + vw].uv.x = 0;
        }
        float total_time = 0f;
        //The first frame with a nonzero time is set as the zero frame. Ideally we would use the last frame with
        //a zero time, but since that frame doesn't have direction information, it would artifact.
        bool found_zero = times[new_zero] > 0;
        for (int uvi = new_zero + 1; uvi < vw; ++uvi) {
            if (found_zero) total_time += times[uvi];
            else {
                if (times[uvi] > 0) {
                    new_zero = uvi;
                    found_zero = true;
                }
                vertsPtr[uvi].uv.x = vertsPtr[uvi + vw].uv.x = 0;
            }
        }
        if (!found_zero) { 
            new_zero = texRptWidth;
            isnonzero = false;
        }
        float acc_time = 0f;
        for (int uvi = new_zero + 1; uvi < vw; ++uvi) {
            acc_time += times[uvi];
            vertsPtr[uvi].uv.x = vertsPtr[uvi + vw].uv.x = acc_time / total_time;
        }
        read_from = new_zero;
        
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

    public CollisionResult CheckCollision(SOCircle target) {
        cullCtr = (cullCtr + 1) % checkCullEvery; 
        //TODO move this culling somewhere else...
        if (cullCtr == 0 && checkCameraCull && CullCheck()) return CollisionResult.noColl;
        int cut1 = (int) Math.Ceiling((_cL - read_from + 1) * tailCutoffRatio);
        int cut2 = (int) Math.Ceiling((_cL - read_from + 1) * headCutoffRatio);
        return DMath.Collision.GrazeCircleOnSegments(target, exec.RawGlobalPosition(), centers, read_from + cut1,
            FramePosCheck, _cL - cut2, scaledLineRadius, 1, 0);
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
        for (int ii = texRptWidth; ii > read_from; ii -= FramePosCheck * 3) {
            BulletManager.RequestSimple(style, null, null,
                new Velocity(centers[ii] + basePos, (centers[ii] - centers[ii-1]).normalized)
                , 0, 0, null);
        }
    }
    
    
#if UNITY_EDITOR
    public unsafe void OnDrawGizmosSelected() {
        Handles.color = Color.cyan;
        int cut1 = (int) Math.Ceiling((_cL - read_from + 1) * tailCutoffRatio);
        int cut2 = Mathf.CeilToInt((_cL - read_from + 1) * headCutoffRatio);
        GenericColliderInfo.DrawGizmosForSegments(centers, read_from + cut1, 1, _cL - cut2, exec.RawGlobalPosition(), scaledLineRadius, 0);
        
        Handles.color = Color.magenta;
        for (int ii = 0; ii < texRptWidth + 1; ++ii) {
            Handles.DrawWireDisc(vertsPtr[ii].loc + (Vector3) velocity.rootPos, Vector3.forward, 0.005f + 0.005f * ii / texRptWidth);
        }
        Handles.color = Color.blue;
        for (int ii = 0; ii < texRptWidth + 1; ++ii) {
            Handles.DrawWireDisc(vertsPtr[ii + texRptWidth + 1].loc + (Vector3) velocity.rootPos, Vector3.forward, 0.005f + 0.005f *ii / texRptWidth);
        }
    }

    [ContextMenu("Debug info")]
    public void DebugPath() {
        //read_from = start + 1
        Log.Unity($"Start {read_from} Skip {FramePosCheck} End {centers.Length}", level: Log.Level.INFO);
    }
#endif
}
}