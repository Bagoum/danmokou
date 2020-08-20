using Danmaku;
using UnityEngine;
using ExVTP = System.Func<Danmaku.ITExVelocity, TEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;

namespace DMath {
//Note to future self: ref bpi is not a worthwhile optimization. However, out nrv was super successful.
public delegate void CoordF(float cos, float sin, ParametricInfo bpi, out Vector2 nrv);
public delegate void VTP(in Velocity vel, in float dT, ParametricInfo bpi, out Vector2 nrv);

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

public delegate Vector2 TP(ParametricInfo bpi);
public delegate Vector2 SBV2(ref BulletManager.SimpleBullet sb);

public delegate Vector3 TP3(ParametricInfo bpi);

public delegate Vector4 TP4(ParametricInfo bpi);

public delegate float BPY(ParametricInfo bpi);

public delegate float LBPY(ParametricInfo bpi, float lT);

public delegate V2RV2 BPRV2(ParametricInfo bpi);

public delegate float FXY(float t);

public delegate bool Pred(ParametricInfo bpi);
public delegate bool LPred(ParametricInfo bpi, float lT);

/// <summary>
/// A wrapper type used for functions that operate over a GCX.
/// The BPI variables are bound as follows:
/// <br/>`bpi.t = gcx.i`
/// <br/>`bpi.index = gcx.index`
/// <br/>`bpi.loc = gcx.beh.globalLoc`
/// </summary>
/// <param name="gcx"></param>
/// <typeparam name="T">Return object type (eg. float, v2, rv2)</typeparam>
public delegate T GCXF<T>(GenCtx gcx);

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
public delegate void SBCF(BulletManager.AbsSimpleBulletCollection sbc, int ii, ParametricInfo bpi);

public delegate void SPCF(string pool);

}