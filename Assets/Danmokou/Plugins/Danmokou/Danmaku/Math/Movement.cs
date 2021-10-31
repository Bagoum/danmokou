using System;
using System.Runtime.CompilerServices;
using BagoumLib.Cancellation;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using JetBrains.Annotations;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.DMath {
/// <summary>
/// A struct containing configuration for moving a BehaviorEntity via the `move` StateMachine.
/// </summary>
public readonly struct LimitedTimeMovement {
    public readonly VTP vtp;
    public readonly float enabledFor;
    public readonly Action done;
    public readonly ICancellee cT;
    public readonly ParametricInfo pi;
    public readonly Pred? condition;
    public bool ThisCannotContinue(ParametricInfo bpi) => !(condition?.Invoke(bpi) ?? true);

    public LimitedTimeMovement(VTP path, float enabledFor, Action done, ICancellee cT, ParametricInfo pi, Pred? condition=null) {
        this.vtp = path;
        this.enabledFor = enabledFor;
        this.done = done;
        this.cT = cT;
        this.pi = pi;
        this.condition = condition;
    }
}

/// <summary>
/// A struct that can move objects along specific paths in space.
/// </summary>
public struct Movement {
    //36 byte struct. (36 unpacked)
        //Funcx1  = 8
        //V2      = 8
        //Floatx5 = 20
    private readonly VTP vtp;
    public Vector2 rootPos;
    //Used by TExVel
    public float angle;
    public float cos_rot;
    public float sin_rot;
    public float flipX;
    public float flipY;
    public Vector2 Direction => new Vector2(cos_rot, sin_rot);

    /// <summary>
    /// Create a velocity configuration.
    /// </summary>
    /// <param name="path">Movement descriptor</param>
    /// <param name="parentLoc">Global location of parent. Set to zero if using a transform parent</param>
    /// <param name="localLoc">Location of this relative to parent. Only distinguished from parent for applying modifiers.</param>
    public Movement(VTP path, Vector2 parentLoc, V2RV2 localLoc) {
        angle = localLoc.angle;
        cos_rot = M.CosDeg(localLoc.angle);
        sin_rot = M.SinDeg(localLoc.angle);
        vtp = path;
        flipX = 1;
        flipY = 1;
        this.rootPos = parentLoc + localLoc.TrueLocation;
    }

    public Movement(VTP vtp, Vector2 rootPos, float ang) : this(vtp, rootPos, M.CosDeg(ang), M.SinDeg(ang), 1, 1) { }
    private Movement(VTP vtp, Vector2 rootPos, float c, float s, float fx, float fy) {
        cos_rot = c;
        sin_rot = s;
        this.rootPos = rootPos;
        this.vtp = vtp;
        this.flipX = fx;
        this.flipY = fy;
        this.angle = M.Atan2D(s, c);
    }
    public Movement WithNoMovement() => new Movement(VTPRepo.NoVTP, rootPos, cos_rot, sin_rot, flipX, flipY);

    public Movement(VTP path): this(path, Vector2.zero, V2RV2.Zero) {}

    /// <summary>
    /// Create a shell velocity configuration with no movement.
    /// </summary>
    /// <param name="parentLoc">Global location of parent. Set to zero if using a transform parent</param>
    /// <param name="localPos">Location of this relative to parent</param>
    public Movement(Vector2 parentLoc, V2RV2 localPos) : this(VTPRepo.NoVTP, parentLoc, localPos) { }
    
    public static Movement None => new Movement(Vector2.zero, V2RV2.Zero);

    public Movement(Vector2 loc, Vector2 dir) {
        cos_rot = dir.x;
        sin_rot = dir.y;
        vtp = VTPRepo.NoVTP;
        flipX = flipY = 1;
        rootPos = loc;
        this.angle = M.AtanD(dir);
    }

    public Movement(Vector2 loc, float angleDeg) : this(loc, M.PolarToXY(angleDeg)) { }

    private Vector2 DefaultDirection() {
        return new Vector2(cos_rot * flipX, sin_rot * flipY);
    }

    /// <summary>
    /// Initialize a parametric info container.
    /// bpi.t should contain the desired starting time of the container. This function will set it to zero and
    ///  incrementally update it until it reaches its initial value.
    /// </summary>
    /// <param name="bpi">Parametric info</param>
    /// <returns>Direction to face</returns>
    public Vector2 UpdateZero(ref ParametricInfo bpi) {
        const float dT = ETime.FRAME_TIME;
        var nrv = Vector2.zero;
        float timeOffset = bpi.t;
        var zeroTime = timeOffset < float.Epsilon;
        //We have to run this regardless of whether timeOffset=0 so offset functions are correct on frame 1
        for (bpi.t = 0f; timeOffset >= 0; timeOffset -= dT) {
            float effdT = (timeOffset < dT) ? timeOffset : dT;
            bpi.t += effdT;
            vtp(ref this, in effdT, ref bpi, out var delta);
            bpi.loc.x += delta.x;
            bpi.loc.y += delta.y;
            nrv.x += delta.x;
            nrv.y += delta.y;
        }
        //If timeOffset=0, then simulate the next update for direction
        if (zeroTime) {
            bpi.t += dT;
            vtp(ref this, dT, ref bpi, out nrv);
            bpi.t = 0;
        } 
        float mag = nrv.x * nrv.x + nrv.y * nrv.y;
        if (mag > M.MAG_ERR) return nrv * 1f / (float) Math.Sqrt(mag);
        else return DefaultDirection();
    }

    /// <summary>
    /// Update a BPI according to the velocity description.
    /// Doesn't calculate normalized direction.
    /// Doesn't add to bpi.t.
    /// </summary>
    /// <param name="bpi">Parametric info</param>
    /// <param name="accDelta">The delta moved this update (updated in addition to BPI.loc)</param>
    /// <param name="ang_deg">Overrride angle rotation</param>
    /// <param name="cos_r">Override cosine rotation</param>
    /// <param name="sin_r">Override sine rotation</param>
    /// <param name="dT">Delta time</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateDeltaNoTime(ref ParametricInfo bpi, ref Vector2 accDelta, float ang_deg, float cos_r, float sin_r, in float dT) {
        angle = ang_deg;
        cos_rot = cos_r;
        sin_rot = sin_r;
        vtp(ref this, in dT, ref bpi, out Vector2 nrv);
        accDelta.x += nrv.x;
        accDelta.y += nrv.y;
        bpi.loc.x += nrv.x;
        bpi.loc.y += nrv.y;
    }

    [UsedImplicitly]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateDeltaNoTime(BulletManager.AbsSimpleBulletCollection sbc, int ii) {
        ref var sb = ref sbc[ii];
        rootPos = sb.bpi.loc;
        UpdateDeltaNoTime(ref sb.bpi, ref sb.accDelta, sb.movement.angle, sb.movement.cos_rot, sb.movement.sin_rot, sbc.NextDT);
    }

    private static readonly ExFunction updateDeltaNoTime = ExFunction.Wrap<Movement>("UpdateDeltaNoTime",
        new[] {typeof(BulletManager.AbsSimpleBulletCollection), typeof(int)});
    public Ex UpdateDeltaNoTime(Ex sbc, Ex ii) => updateDeltaNoTime.InstanceOf(Ex.Constant(this), sbc, ii);

    /// <summary>
    /// Update a BPI according to the velocity description.
    /// Doesn't calculate normalized direction.
    /// </summary>
    /// <param name="bpi">Parametric info</param>
    /// <param name="dT">Delta time</param>
    /// <param name="delta">The delta moved this update (updated in addition to BPI.loc)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateDeltaAssignAcc(ref ParametricInfo bpi, out Vector2 delta, in float dT) {
        bpi.t += dT;
        vtp(ref this, in dT, ref bpi, out delta);
        bpi.loc.x += delta.x;
        bpi.loc.y += delta.y;
    }

    public void FlipX() {
        this.flipX *= -1;
    }
    public void FlipY() {
        this.flipY *= -1;
    }
    public bool IsEmpty() => vtp.IsNone();
}

/// <summary>
/// An "extension" of the Movement struct to describe the nested movement function of a laser.
/// </summary>
public struct LaserMovement {
    private readonly LVTP? lvtp;
    [UsedImplicitly] private readonly float angle;
    [UsedImplicitly]
    public readonly float cos_rot;
    [UsedImplicitly]
    public readonly float sin_rot;
    [UsedImplicitly]
    public Vector2 rootPos;
    private readonly Vector2 simpleDir;
    private readonly BPY? rotation;
    public float flipX;
    public float flipY;
    private float tflipX;
    private float tflipY;
    public readonly bool isSimple;
    
    public LaserMovement(LVTP path, Vector2 parentLoc, V2RV2 localLoc) {
        angle = localLoc.angle;
        cos_rot = M.CosDeg(localLoc.angle);
        sin_rot = M.SinDeg(localLoc.angle);
        lvtp = path;
        flipX = 1;
        flipY = 1;
        tflipX = tflipY = 1;
        this.rootPos = parentLoc + localLoc.TrueLocation;
        rotation = null;
        isSimple = false;
        simpleDir = Vector2.zero;
    }

    /// <summary>
    /// Simple laser variant: fires in a straight line.
    /// </summary>
    /// <param name="base_rot_deg"></param>
    /// <param name="frame_rot"></param>
    public LaserMovement(float base_rot_deg, BPY? frame_rot) {
        angle = base_rot_deg;
        cos_rot = M.CosDeg(base_rot_deg);
        sin_rot = M.SinDeg(base_rot_deg);
        flipX = 1;
        flipY = 1;
        tflipX = tflipY = 1;
        simpleDir = new Vector2(cos_rot, sin_rot);
        rotation = frame_rot;
        isSimple = true;
        lvtp = null;
        rootPos = Vector2.zero; //Irrelevant for straight lasers
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(in float lt, ref ParametricInfo bpi, out Vector2 d1, out Vector2 d2, in float dT) {
        bpi.t += dT;
        if (isSimple) {
            d1 = new Vector2(simpleDir.x * dT * flipX, simpleDir.y * dT * flipY);
        } else {
            lvtp!(ref this, in dT, in lt, ref bpi, out d1);
        }
        bpi.loc.x += d1.x;
        bpi.loc.y += d1.y;
        d2 = d1;
    }
    
    public float RotationDeg(ParametricInfo bpi) {
        return rotation?.Invoke(bpi) ?? 0;
    }
    public void FlipX() {
        flipX *= -1;
        tflipX *= -1;
    }

    public void FlipY() {
        flipY *= -1;
        tflipY *= -1;
    }
    public void ResetFlip() {
        flipX *= tflipX;
        flipY *= tflipY;
        tflipX = 1;
        tflipY = 1;
    }
}

}