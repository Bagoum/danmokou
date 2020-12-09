using DMK.Danmaku;
using DMK.Expressions;
using UnityEngine;
using DMK.DMath.Functions;
using DMK.Graphics;
using DMK.Reflection;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExTP = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExVTP = System.Func<DMK.Expressions.ITExVelocity, DMK.Expressions.TEx<float>, DMK.Expressions.TExPI, DMK.Expressions.RTExV2, DMK.Expressions.TEx<UnityEngine.Vector2>>;

namespace DMK.DMath {
/// <summary>
/// A struct containing the input required for a parametric equation.
/// </summary>
public struct ParametricInfo {
    public static ParametricInfo Zero = new ParametricInfo(Vector2.zero, 0, 0, 0);
    /// <summary>Random ID</summary>
    public readonly uint id;
    /// <summary>Firing index</summary>
    public readonly int index;
    /// <summary>Global position</summary>
    public Vector2 loc;
    /// <summary>Life-time (with minor adjustment)</summary>
    public float t;

    public static ParametricInfo WithRandomId(Vector2 position, int findex, float t) => new ParametricInfo(position, findex, RNG.GetUInt(), t);
    public static ParametricInfo WithRandomId(Vector2 position, int findex) => WithRandomId(position, findex, 0f);
    public ParametricInfo(Vector2 position, int findex, uint id, float t = 0) {
        loc = position;
        index = findex;
        this.id = id;
        this.t = t;
    }
    public static readonly ExFunction withRandomId = ExUtils.Wrap<ParametricInfo>("WithRandomId", new[] {typeof(Vector2), typeof(int), typeof(float)});

    public ParametricInfo Rehash() => new ParametricInfo(loc, index, RNG.Rehash(id), t);
    
    public ParametricInfo CopyWithP(int newP) => new ParametricInfo(loc, newP, id, t);
    public ParametricInfo CopyWithT(float newT) => new ParametricInfo(loc, index, id, newT);

    /// <summary>
    /// Flips the position around an X or Y axis.
    /// </summary>
    /// <param name="y">Iff true, flips Y values around an X axis. Else, flips X values around a Y axis.</param>
    /// <param name="around">Location of the axis.</param>
    public void FlipSimple(bool y, float around) {
        if (y) {
            loc.y = 2 * around - loc.y;
        } else {
            loc.x = 2 * around - loc.x;
        }
    }
}

//Note to future self: ref bpi is not a worthwhile optimization. However, out nrv was super successful.
/// <summary>
/// A function that converts ParametricInfo into a possibly-rotated Cartesian coordinate.
/// </summary>
public delegate void CoordF(float cos, float sin, ParametricInfo bpi, out Vector2 nrv);
/// <summary>
/// A function that converts ParametricInfo into a possibly-rotated Cartesian coordinate
/// representing the next position that the Velocity struct should take with a timestep of dT.
/// </summary>
public delegate void VTP(in Movement vel, in float dT, ParametricInfo bpi, out Vector2 nrv);
/// <summary>
/// A function that converts ParametricInfo into a possibly-rotated Cartesian coordinate
/// representing the next position that the LaserVelocity struct should take with a timestep of dT
/// and a laser lifetime of lT.
/// </summary>
public delegate void LVTP(in LaserMovement vel, in float dT, in float lT, ParametricInfo bpi, out Vector2 nrv);


public readonly struct RootedVTP {
    public readonly GCXF<Vector2> root;
    public readonly GCXU<VTP> path;

    public RootedVTP(GCXF<Vector2> root, GCXU<VTP> path) {
        this.root = root;
        this.path = path;
    }

    public RootedVTP(GCXF<Vector2> root, ExVTP path) : this(root, Compilers.GCXU(path)) { }

    public RootedVTP(ExBPY x, ExBPY y, ExVTP path) : this(Parametrics.PXY(x, y), path) { }
    public RootedVTP(ExTP root, ExVTP path) : this(Compilers.GCXF(root), Compilers.GCXU(path)) { }
    public RootedVTP(float x, float y, ExVTP path) : this(_ => new Vector2(x, y), path) { }
}

/// <summary>
/// A function that converts ParametricInfo into a Vector2.
/// </summary>
public delegate Vector2 TP(ParametricInfo bpi);
/// <summary>
/// A function that converts a SimpleBullet into a Vector2.
/// </summary>
public delegate Vector2 SBV2(ref BulletManager.SimpleBullet sb);

/// <summary>
/// A function that converts ParametricInfo into a Vector3.
/// </summary>
public delegate Vector3 TP3(ParametricInfo bpi);

/// <summary>
/// A function that converts ParametricInfo into a Vector4.
/// </summary>
public delegate Vector4 TP4(ParametricInfo bpi);

/// <summary>
/// A function that converts ParametricInfo into a float.
/// </summary>
public delegate float BPY(ParametricInfo bpi);
/// <summary>
/// A function that converts a SimpleBullet into a float.
/// </summary>
public delegate float SBF(ref BulletManager.SimpleBullet sb);

/// <summary>
/// A function that converts ParametricInfo and a laser lifetime into a float.
/// </summary>
public delegate float LBPY(ParametricInfo bpi, float lT);

/// <summary>
/// A function that converts ParametricInfo into a V2RV2.
/// </summary>
public delegate V2RV2 BPRV2(ParametricInfo bpi);

/// <summary>
/// A function that converts a float into a float.
/// </summary>
public delegate float FXY(float t);

/// <summary>
/// A function that converts ParametricInfo into a boolean.
/// </summary>
public delegate bool Pred(ParametricInfo bpi);
/// <summary>
/// A function that converts ParametricInfo and a laser lifetime into a Vector2.
/// </summary>
public delegate bool LPred(ParametricInfo bpi, float lT);

/// <summary>
/// A function that converts a laser into a vector4.
/// </summary>
public delegate Vector4 FnLaserV4(ParametricInfo bpi, CurvedTileRenderLaser l);

/// <summary>
/// A wrapper type used for functions that operate over a GCX.
/// </summary>
/// <typeparam name="T">Return object type (eg. float, v2, rv2)</typeparam>
public delegate T GCXF<T>(GenCtx gcx);

/// <summary>
/// A wrapper type used to upload values from a GCX to private data hoisting before providing a delegate to a new object.
/// </summary>
/// <typeparam name="T">Delegate type (eg. TP, BPY, Pred)</typeparam>
public readonly struct GCXU<T> {
    public delegate T GCXUNew(GenCtx gcx, ref uint id);
    public delegate T GCXUAdd(GenCtx gcx, uint id);
    public readonly GCXUNew New;
    public readonly GCXUAdd Add;

    public GCXU(GCXUNew uNew, GCXUAdd uAdd) {
        New = uNew;
        Add = uAdd;
    }

}

//Note: we don't use ref SB because some operations, like deletion and time modification,
//require access to sbc, ii.
/// <summary>
/// A bullet control function performing some operation on a SimpleBullet.
/// </summary>
public delegate void SBCF(BulletManager.AbsSimpleBulletCollection sbc, int ii, ParametricInfo bpi);

/// <summary>
/// A pool control function performing some operation on a simple bullet pool.
/// </summary>
public delegate void SPCF(string pool);

}