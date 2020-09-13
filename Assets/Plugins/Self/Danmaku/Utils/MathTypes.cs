using Danmaku;
using UnityEngine;
using ExVTP = System.Func<Danmaku.ITExVelocity, TEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;

namespace DMath {
//Note to future self: ref bpi is not a worthwhile optimization. However, out nrv was super successful.
/// <summary>
/// A function that converts ParametricInfo into a possibly-rotated Cartesian coordinate.
/// </summary>
public delegate void CoordF(float cos, float sin, ParametricInfo bpi, out Vector2 nrv);
/// <summary>
/// A function that converts ParametricInfo into a possibly-rotated Cartesian coordinate
/// representing the next position that the Velocity struct should take with a timestep of dT.
/// </summary>
public delegate void VTP(in Velocity vel, in float dT, ParametricInfo bpi, out Vector2 nrv);
/// <summary>
/// A function that converts ParametricInfo into a possibly-rotated Cartesian coordinate
/// representing the next position that the LaserVelocity struct should take with a timestep of dT
/// and a laser lifetime of lT.
/// </summary>
public delegate void LVTP(in LaserVelocity vel, in float dT, in float lT, ParametricInfo bpi, out Vector2 nrv);


public readonly struct RootedVTP {
    public readonly GCXF<Vector2> root;
    public readonly GCXU<VTP> path;

    public RootedVTP(GCXF<Vector2> root, GCXU<VTP> path) {
        this.root = root;
        this.path = path;
    }

    public RootedVTP(GCXF<Vector2> root, ExVTP path) : this(root, Compilers.GCXU(path)) { }
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