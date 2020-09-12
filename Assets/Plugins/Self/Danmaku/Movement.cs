using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using Core;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static DMath.ExM;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTTP = System.Func<TEx<float>, DMath.TExPI, TEx<UnityEngine.Vector2>>;
//ExCoordF does not necessarily return a TEx<V2>, but this is used for consistency; it is compiled to void anyways.
using ExCoordF = System.Func<TEx<float>, TEx<float>, DMath.TExPI, DMath.RTExV2, System.Func<TEx<float>, TEx<float>, TEx<UnityEngine.Vector2>>, TEx<UnityEngine.Vector2>>;
using ExVTP = System.Func<Danmaku.ITExVelocity, TEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;
using ExLVTP = System.Func<Danmaku.ITExVelocity, RTEx<float>, RTEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTBPY = System.Func<TEx<float>, DMath.TExPI, TEx<float>>;
using static Danmaku.VTPConstructors;
using static DMath.ExMConversions;

namespace Danmaku {
public readonly struct LimitedTimeVelocity {
    public readonly VTP VTP2;
    public readonly float enabledFor;
    public readonly Action done;
    public readonly ICancellee cT;
    public readonly int firingIndex;
    [CanBeNull] public readonly Pred condition;
    public bool ThisCannotContinue(ParametricInfo bpi) => !(condition?.Invoke(bpi) ?? true);

    public LimitedTimeVelocity(VTP path, float enabledFor, Action done, ICancellee cT, int p, [CanBeNull] Pred condition=null) {
        this.VTP2 = path;
        this.enabledFor = enabledFor;
        this.done = done;
        this.cT = cT;
        this.firingIndex = p;
        this.condition = condition;
    }
}

/// <summary>
/// Repository for constructing path expressions by converting lesser computations into Cartesian coordinates
/// and applying appropriate rotation.
/// </summary>
public static class VTPConstructors {
    //Note for expressions: since this is parented by VTP2, which is not parented,
    //you can reassign values to BPI.t, but NOT to bpi.loc.
    public static ExCoordF CartesianRot(ExTP erv) => (c, s, bpi, nrv, fxy) => {
        var v2 = new TExV2();
        return Ex.Block(new ParameterExpression[] { v2 },
            Ex.Assign(v2, erv(bpi)),
            fxy(Ex.Subtract(Ex.Multiply(c, v2.x), Ex.Multiply(s, v2.y)),
                Ex.Add(Ex.Multiply(s, v2.x), Ex.Multiply(c, v2.y)))
        );
    };

    public static CoordF CartesianRot(TP rv) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        nrv = rv(bpi);
        bpi.t = c * nrv.x - s * nrv.y;
        nrv.y = s * nrv.x + c * nrv.y;
        nrv.x = bpi.t;
    };
    public static ExCoordF CartesianNRot(ExTP enrv) => (c, s, bpi, nrv, fxy) => Ex.Block(Ex.Assign(nrv, enrv(bpi)), fxy(nrv.x, nrv.y));

    public static CoordF CartesianNRot(TP tpnrv) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        nrv = tpnrv(bpi);
    };
   
    public static ExCoordF Cartesian(ExTP erv, ExTP enrv) {
        var v2 = new TExV2();
        return (c, s, bpi, nrv, fxy) => Ex.Block(
            new ParameterExpression[] { v2 },
            Ex.Assign(nrv, enrv(bpi)),
            Ex.Assign(v2, erv(bpi)),
            fxy(nrv.x.Add(Ex.Subtract(Ex.Multiply(c, v2.x), Ex.Multiply(s, v2.y))),
                nrv.y.Add(Ex.Add(Ex.Multiply(s, v2.x), Ex.Multiply(c, v2.y))))
        );
    }
/*
    public static ExCoordF OffsetAsVelocity(ExCoordF vtp, float dT) {
        TExV2 v2 = TExV2.Variable();
        Ex delta = Ex.Constant(dT);
        return (c, s, bpi, nrv) => Ex.Block(new ParameterExpression[] {v2},
            vtp(c, s, bpi, nrv),
            Ex.Assign(v2, nrv),
            Ex.Assign(bpi.t, bpi.t.Sub(delta)),
            vtp(c, s, bpi, nrv),
            Ex.Assign(nrv.x, v2.x.Sub(nrv.x).Div(delta)),
            Ex.Assign(nrv.y, v2.y.Sub(nrv.y).Div(delta))
        );
    }*/
    
    public static CoordF Cartesian(TP rv, TP tpnrv) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        nrv = tpnrv(bpi);
        bpi.loc = rv(bpi);
        nrv.x += c * bpi.loc.x - s * bpi.loc.y;
        nrv.y += s * bpi.loc.x + c * bpi.loc.y;
    };
    public static ExCoordF Polar(ExBPY r, ExBPY theta) {
        var vr = ExUtils.VFloat();
        var lookup = new TExV2();
        return (c, s, bpi, nrv, fxy) => Ex.Block(new[] { vr, lookup },
            Ex.Assign(lookup, ExM.CosSinDeg(theta(bpi))),
            Ex.Assign(vr, r(bpi)),
            fxy(Ex.Subtract(Ex.Multiply(c, lookup.x), Ex.Multiply(s, lookup.y)).Mul(vr), 
                Ex.Add(Ex.Multiply(s, lookup.x), Ex.Multiply(c, lookup.y)).Mul(vr))
        );
    }
    public static ExCoordF Polar2(ExTP radThetaDeg) {
        var rt = new TExV2();
        var lookup = new TExV2();
        return (c, s, bpi, nrv, fxy) => Ex.Block(new ParameterExpression[] { rt, lookup },
            Ex.Assign(rt, radThetaDeg(bpi)),
            Ex.Assign(lookup, ExM.CosSinDeg(rt.y)),
            fxy(Ex.Subtract(Ex.Multiply(c, lookup.x), Ex.Multiply(s, lookup.y)).Mul(rt.x), 
                Ex.Add(Ex.Multiply(s, lookup.x), Ex.Multiply(c, lookup.y)).Mul(rt.x))
        );
    }
    
    public static CoordF Polar(BPY r, BPY theta) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        float baseRot = M.degRad * theta(bpi);
        bpi.t = r(bpi);
        nrv.x = bpi.t * Mathf.Cos(baseRot);
        nrv.y = bpi.t * Mathf.Sin(baseRot);
        bpi.t = c * nrv.x - s * nrv.y;
        nrv.y = s * nrv.x + c * nrv.y;
        nrv.x = bpi.t;
    };
    
}

public static class VTPControllers {
    private static T InLetCtx<T>(ITExVelocity vel, Func<T> exec) {
        using (new ReflectEx.LetDirect("root", vel.root)) {
            using (new ReflectEx.LetDirect("a", vel.angle)) {
                using (new ReflectEx.LetDirect("ac", vel.cos)) {
                    using (new ReflectEx.LetDirect("as", vel.sin)) {
                        return exec();
                    }
                }
            }
        }
    }

    public static ExVTP Velocity(ExCoordF cf) => (vel, dt, bpi, nrv) => InLetCtx(vel, () =>
        cf(vel.cos, vel.sin, bpi, nrv, (x, y) =>
            Ex.Block(
                nrv.x.Is(vel.flipX.Mul(x).Mul(dt)),
                nrv.y.Is(vel.flipY.Mul(y).Mul(dt))
            )));
    
    public static VTP Velocity(CoordF coordF) => delegate(in Velocity vel, in float dT, ParametricInfo bpi, out Vector2 nrv) {
        coordF(vel.cos_rot, vel.sin_rot, bpi, out nrv);
        nrv.x *= vel.flipX * dT;
        nrv.y *= vel.flipY * dT;
    };
    public static ExVTP Offset(ExCoordF cf) => (vel, dt, bpi, nrv) => InLetCtx(vel, () => 
        cf(vel.cos, vel.sin, bpi, nrv, (x, y) =>
            Ex.Block(
                nrv.x.Is(vel.flipX.Mul(x).Add(vel.rootX).Sub(bpi.locx)),
                nrv.y.Is(vel.flipY.Mul(y).Add(vel.rootY).Sub(bpi.locy))
            )));
    
    public static VTP Offset(CoordF coordF) => delegate(in Velocity vel, in float dT, ParametricInfo bpi, out Vector2 nrv) {
        coordF(vel.cos_rot, vel.sin_rot, bpi, out nrv);
        nrv.x = nrv.x * vel.flipX + vel.rootPos.x - bpi.loc.x;
        nrv.y = nrv.y * vel.flipY + vel.rootPos.y - bpi.loc.y;
    };
}

/// <summary>
/// Repository for movement functions.
/// </summary>
public static class VTPRepo {
        [DontReflect]
        public static bool IsNone(this VTP func) => func == NoVTP;
        public static readonly ExVTP ExNoVTP = VTPControllers.Velocity(CartesianNRot(Parametrics.Zero()));
    #if NO_EXPR
        public static readonly VTP NoVTP = VTPControllers.Velocity(CartesianNRot(_ => Vector2.zero));
    #else
        public static readonly VTP NoVTP = Compilers.VTP_Force(ExNoVTP);
    #endif
        /// <summary>
        /// No movement.
        /// </summary>
        public static ExVTP Null() => ExNoVTP;
        /// <summary>
        /// Movement with Cartesian rotational velocity only.
        /// </summary>
        /// <param name="rv">Rotational velocity parametric</param>
        /// <returns></returns>
        [Alias("tprot")]
        public static ExVTP RVelocity(ExTP rv) => VTPControllers.Velocity(CartesianRot(rv));
        /// <summary>
        /// Movement with Cartesian nonrotational velocity only.
        /// </summary>
        /// <param name="nrv">Nonrotational velocity parametric</param>
        /// <returns></returns>
        [Alias("tpnrot")]
        public static ExVTP NRVelocity(ExTP nrv) => VTPControllers.Velocity(CartesianNRot(nrv));
        /// <summary>
        /// Movement with Cartesian rotational velocity and nonrotational velocity.
        /// </summary>
        /// <param name="rv">Rotational velocity parametric</param>
        /// <param name="nrv">Nonrotational velocity parametric</param>
        /// <returns></returns>
        [Alias("tp")]
        public static ExVTP Velocity(ExTP rv, ExTP nrv) => VTPControllers.Velocity(Cartesian(rv, nrv));
        /// <summary>
        /// Movement with Cartesian rotational offset only.
        /// </summary>
        /// <param name="rp">Rotational offset parametric</param>
        /// <returns></returns>
        public static ExVTP ROffset(ExTP rp) => VTPControllers.Offset(CartesianRot(rp));
        /// <summary>
        /// Movement with Cartesian nonrotational offset only.
        /// </summary>
        /// <param name="nrp">Nonrotational offset parametric</param>
        /// <returns></returns>
        public static ExVTP NROffset(ExTP nrp) => VTPControllers.Offset(CartesianNRot(nrp));
        /// <summary>
        /// Movement with Cartesian rotational offset and nonrotational offset.
        /// </summary>
        /// <param name="rp">Rotational offset parametric</param>
        /// <param name="nrp">Nonrotational offset parametric</param>
        /// <returns></returns>
        public static ExVTP Offset(ExTP rp, ExTP nrp) => VTPControllers.Offset(Cartesian(rp, nrp));

        /// <summary>
        /// Offset function for dependent (empty-guided) fires.
        /// Reduces to `offset (* RADIUS (@ HOISTDIR p)) (@ HOISTLOC p)`
        /// </summary>
        /// <param name="hoistLoc">Location of empty guider</param>
        /// <param name="hoistDir">Direction of empty guider</param>
        /// <param name="indexer">Indexer function for public hoisting</param>
        /// <param name="radius">Radial offset of guided</param>
        /// <returns></returns>
        public static ExVTP DOffset(ReflectEx.Hoist<Vector2> hoistLoc, ReflectEx.Hoist<Vector2> hoistDir, 
            ExBPY indexer, ExBPY radius) => Offset(
            bpi => Mul(radius(bpi), RetrieveHoisted(hoistDir, indexer(bpi))),
            bpi => RetrieveHoisted(hoistLoc, indexer(bpi))
        );
        /// <summary>
        /// Offset function for dependent (empty-guided) fires.
        /// Reduces to `offset (rotatev (@ HOISTDIR p) OFFSET) (@ HOISTLOC p)`
        /// </summary>
        /// <param name="hoistLoc">Location of empty guider</param>
        /// <param name="hoistDir">Direction of empty guider</param>
        /// <param name="indexer">Indexer function for public hoisting</param>
        /// <param name="offset">Parametric offset of guided</param>
        /// <returns></returns>
        public static ExVTP DTPOffset(ReflectEx.Hoist<Vector2> hoistLoc, ReflectEx.Hoist<Vector2> hoistDir, 
            ExBPY indexer, ExTP offset) => Offset(
            bpi => RotateV(RetrieveHoisted(hoistDir, indexer(bpi)), offset(bpi)),
            bpi => RetrieveHoisted(hoistLoc, indexer(bpi))
        );
        
        /// <summary>
        /// Movement with polar rotational offset.
        /// </summary>
        /// <param name="radius">Radius function</param>
        /// <param name="theta">Theta function (degrees)</param>
        /// <returns></returns>
        public static ExVTP Polar(ExBPY radius, ExBPY theta) => VTPControllers.Offset(VTPConstructors.Polar(radius, theta));
        /// <summary>
        /// Movement with polar rotational offset. Uses a vector2 instead of two floats. (This is slower.)
        /// </summary>
        /// <param name="rt">Radius function (X), Theta function (Y) (degrees)</param>
        /// <returns></returns>
        public static ExVTP Polar2(ExTP rt) => VTPControllers.Offset(VTPConstructors.Polar2(rt));
        /// <summary>
        /// Movement with polar rotational velocity.
        /// <br/>Note: I'm pretty sure this doesn't work at all.
        /// </summary>
        /// <param name="radius">Radius derivative function</param>
        /// <param name="theta">Theta derivative function (degrees)</param>
        /// <returns></returns>
        public static ExVTP VPolar(ExBPY radius, ExBPY theta) => VTPControllers.Velocity(VTPConstructors.Polar(radius, theta));

        private static ExVTP WrapLet<T>((string, Func<TExPI, TEx<T>>)[] aliases, ExVTP inner) => 
            new ExVTP((v,t,bpi,nrv) => ReflectEx.Let(aliases, () => inner(v,t,bpi,nrv), bpi));
        [Alias("::")]
        public static ExVTP LetFloats((string, ExBPY)[] aliases, ExVTP inner) => WrapLet(aliases, inner);
        [Alias("::v2")]
        public static ExVTP LetV2s((string, ExTP)[] aliases, ExVTP inner) => WrapLet(aliases, inner);

        public static ExVTP LetDecl(ReflectEx.Alias<TExPI>[] aliases, ExVTP inner) => (c, s, bpi, nrv) =>
            ReflectEx.Let2(aliases, () => inner(c, s, bpi, nrv), bpi);

}

public struct Velocity {
    //32 byte struct. (30 unpacked)
        //Funcx1  = 8
        //V2      = 8
        //Floatx3 = 12
        //Misc    = 2
    private readonly VTP vtp;
    public Vector2 rootPos;
    public float angle;
    public float cos_rot;
    public float sin_rot;
    public sbyte flipX;
    public sbyte flipY;
    public Vector2 Direction => new Vector2(cos_rot, sin_rot);

    /// <summary>
    /// Create a velocity configuration.
    /// </summary>
    /// <param name="path">Movement descriptor</param>
    /// <param name="parentLoc">Global location of parent. Set to zero if using a transform parent</param>
    /// <param name="localLoc">Location of this relative to parent. Only distinguished from parent for applying modifiers.</param>
    public Velocity(VTP path, Vector2 parentLoc, V2RV2 localLoc) {
        angle = localLoc.angle;
        cos_rot = M.CosDeg(localLoc.angle);
        sin_rot = M.SinDeg(localLoc.angle);
        vtp = path;
        flipX = 1;
        flipY = 1;
        this.rootPos = parentLoc + localLoc.TrueLocation;
    }

    public Velocity(VTP vtp, Vector2 rootPos, float ang) : this(vtp, rootPos, M.CosDeg(ang), M.SinDeg(ang), 1, 1) { }
    private Velocity(VTP vtp, Vector2 rootPos, float c, float s, sbyte fx, sbyte fy) {
        cos_rot = c;
        sin_rot = s;
        this.rootPos = rootPos;
        this.vtp = vtp;
        this.flipX = fx;
        this.flipY = fy;
        this.angle = M.Atan2D(s, c);
    }
    public Velocity WithNoMovement() => new Velocity(VTPRepo.NoVTP, rootPos, cos_rot, sin_rot, flipX, flipY);

    public Velocity(VTP path): this(path, Vector2.zero, V2RV2.Zero) {}

    /// <summary>
    /// Create a shell velocity configuration with no movement.
    /// </summary>
    /// <param name="parentLoc">Global location of parent. Set to zero if using a transform parent</param>
    /// <param name="localPos">Location of this relative to parent</param>
    public Velocity(Vector2 parentLoc, V2RV2 localPos) : this(VTPRepo.NoVTP, parentLoc, localPos) { }
    
    public static Velocity None => new Velocity(Vector2.zero, V2RV2.Zero);

    public Velocity(Vector2 loc, Vector2 dir) {
        cos_rot = dir.x;
        sin_rot = dir.y;
        vtp = VTPRepo.NoVTP;
        flipX = flipY = 1;
        rootPos = loc;
        this.angle = M.AtanD(dir);
    }

    public Velocity(Vector2 loc, float angleDeg) : this(loc, M.PolarToXY(angleDeg)) { }

    private Vector2 DefaultDirection() {
        return new Vector2(cos_rot * flipX, sin_rot * flipY);
    }

    /// <summary>
    /// Initialize a parametric info container.
    /// BPI time should initially be set to zero, and will be updated to timeOffset + FRAMEOFFSET.
    /// </summary>
    /// <param name="bpi">Parametric info</param>
    /// <param name="timeOffset">Desired initial time for BPI (without offset)</param>
    /// <returns>Direction to face</returns>
    public Vector2 UpdateZero(ref ParametricInfo bpi, float timeOffset) {
        const float dT = ETime.FRAME_TIME;
        var nrv = Vector2.zero;
        var zeroTime = timeOffset < float.Epsilon;
        //We have to run this regardless of whether timeOffset=0 so offset functions are correct on frame 1
        for (bpi.t = 0f; timeOffset >= 0; timeOffset -= dT) {
            float effdT = (timeOffset < dT) ? timeOffset : dT;
            bpi.t += effdT;
            vtp(in this, in effdT, bpi, out var delta);
            nrv.x += delta.x;
            nrv.y += delta.y;
        }
        bpi.loc.x += nrv.x;
        bpi.loc.y += nrv.y;
        //If timeOffset=0, then simulate the next update for direction
        if (zeroTime) {
            bpi.t += dT;
            vtp(in this, dT, bpi, out nrv);
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
        vtp(in this, in dT, bpi, out Vector2 nrv);
        accDelta.x += nrv.x;
        accDelta.y += nrv.y;
        bpi.loc.x += nrv.x;
        bpi.loc.y += nrv.y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateDeltaNoTime(BulletManager.AbsSimpleBulletCollection sbc, int ii) {
        ref var sb = ref sbc[ii];
        rootPos = sb.bpi.loc;
        UpdateDeltaNoTime(ref sb.bpi, ref sb.accDelta, sb.velocity.angle, sb.velocity.cos_rot, sb.velocity.sin_rot, sbc.NextDT);
    }

    private static readonly ExFunction updateDeltaNoTime = ExUtils.Wrap<Velocity>("UpdateDeltaNoTime",
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
        vtp(in this, in dT, bpi, out delta);
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
/// An "extension" of the Velocity struct to describe the nested movement function of a laser.
/// </summary>
public struct LaserVelocity {
    [CanBeNull] private readonly LVTP lvtp;
    [UsedImplicitly] private readonly float angle;
    [UsedImplicitly]
    private readonly float cos_rot;
    [UsedImplicitly]
    private readonly float sin_rot;
    [UsedImplicitly]
    public Vector2 rootPos;
    private readonly Vector2 simpleDir;
    [CanBeNull] private readonly BPY rotation;
    private sbyte flipX;
    private sbyte flipY;
    private sbyte tflipX;
    private sbyte tflipY;
    public readonly bool isSimple;
    
    public LaserVelocity(LVTP path, Vector2 parentLoc, V2RV2 localLoc) {
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
    public LaserVelocity(float base_rot_deg, [CanBeNull] BPY frame_rot) {
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
            lvtp(in this, in dT, in lt, bpi, out d1);
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