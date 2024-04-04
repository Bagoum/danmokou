using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DataHoist;
using Danmokou.Expressions;
using UnityEngine;
using Danmokou.DMath.Functions;
using Danmokou.Graphics;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Reflection2;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using Mizuhashi;
using UnityEngine.Profiling;
using Ex = System.Linq.Expressions.Expression;
using ExVTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<Danmokou.Expressions.VTPExpr>>;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
#pragma warning disable CS0162

namespace Danmokou.DMath {
/// <summary>
/// A class storing state information for bullets and other entities.
/// <br/>Contains a link to the environment frame where the bullet was created, which contains most relevant data.
/// </summary>
public class PIData {
    public static readonly PIData Empty = new();
    //For dictionary variables, such as those created for state control in SS0 or onlyonce
    private static readonly Dictionary<(Type type, string name), int> dynamicKeyNames = new();
    private static readonly Dictionary<Type, int> lastVarID = new();
    public static int GetDynamicKey(Type t, string name) {
        if (dynamicKeyNames.TryGetValue((t, name), out var res)) return res;
        lastVarID.TryAdd(t, 0);
        return dynamicKeyNames[(t, name)] = lastVarID[t]++;
    }
    private static Stack<PIData> Cache { get; } = new();
    public static int Allocated { get; private set; } 
    public static int Popped { get; private set; } //Popped and recached should be about equal
    public static int Recached { get; private set; }
    public static int Copied { get; internal set; }
    public static int Cleared { get; private set; }

    //For culled bullets, sb.bpi.t points to a countdown from FADE_TIME to 0, and this points to the
    // lifetime of the bullet (including the lifetime of the original bullet), which is used to calculate direction.
    public float culledBulletTime;
    
    //Variables bound in environment frames. These variables are at a higher scope, and may be changed
    // by other actors.
    public EnvFrame envFrame = EnvFrame.Empty;
    
    //Late-bound variables, such as those created for state control in SS0 or onlyonce
    public readonly Dictionary<int, int> boundInts = new();
    public readonly Dictionary<int, float> boundFloats = new();
    public readonly Dictionary<int, Vector2> boundV2s = new();
    public readonly Dictionary<int, Vector3> boundV3s = new();
    public readonly Dictionary<int, V2RV2> boundRV2s = new();

    public BehaviorEntity? firer; //Note this may be repooled or otherwise destroyed during execution

    [UsedImplicitly]
    public BehaviorEntity Firer => 
        firer != null ? firer : 
            throw new Exception("PICustomData is not a bullet or a GenCtx proxy, " +
                                $"and therefore does not have a {nameof(Firer)}.");
    
    public PlayerController? playerController; //For player bullets
    [UsedImplicitly]
    public PlayerController PlayerController =>
        playerController != null ?
            playerController :
            throw new Exception("PICustomData does not have a player controller. " +
                                "Please make sure that player bullets are fired in player scripts only.");

    [UsedImplicitly]
    public FireOption OptionFirer => 
         firer as FireOption ?? throw new Exception("PICustomData is not a bullet fired by a player shot option.");

    public Bullet? bullet;
    /// <summary>
    /// If this data struct is being used for a Bullet GameObject (such as pathers or lasers), then this points to the bullet.
    /// </summary>
    public Bullet Bullet => bullet != null ?
        bullet :
        throw new Exception("PICustomData is not linked to a bullet, but a bullet-specific function was called.");
    
    public CurvedTileRenderLaser? laserController;
    /// <summary>
    /// If this data struct is being used for a Laser, then this points to the laser.
    /// </summary>
    public CurvedTileRenderLaser Laser => 
        laserController ?? throw new Exception("PICustomData is not a laser.");
    public PlayerBullet? playerBullet;

    /// <summary>
    /// Copy this object's variables into another object of the same type.
    /// <br/>Not virtual, so only this class' variables are copied.
    /// </summary>
    public PIData CopyInto(PIData copyee) {
        ++Copied;
        boundInts.CopyInto(copyee.boundInts);
        boundFloats.CopyInto(copyee.boundFloats);
        boundV2s.CopyInto(copyee.boundV2s);
        boundV3s.CopyInto(copyee.boundV3s);
        boundRV2s.CopyInto(copyee.boundRV2s);
        copyee.envFrame = envFrame.Clone();
        copyee.firer = firer;
        copyee.playerController = playerController;
        copyee.laserController = laserController;
        copyee.bullet = bullet;
        copyee.playerBullet = playerBullet;
        return copyee;
    }

    /// <summary>
    /// Clone this object. The data type is pooled, so this method has amortized O(0) allocations.
    /// </summary>
    public PIData Clone_NoAlloc() => 
        //Don't need logic for lexical scope, envFrame will be cloned in CopyInto
        CopyInto(New(null)); 

    /// <summary>
    /// ONLY CALL THIS FROM FUNCTIONS WITH <see cref="CreatesInternalScopeAttribute"/> WITH DYNAMIC=TRUE
    /// </summary>
    public GenCtx RevertToGCX(LexicalScope dynamicScope, BehaviorEntity exec) {
        if (dynamicScope is not DynamicLexicalScope)
            throw new Exception("RevertToGCX may only be called with a dynamic lexical scope");
        var gcx = GenCtx.New(exec, EnvFrame.Create(dynamicScope, envFrame));
        gcx.playerController = playerController;
        //Dynamic keys (such as those bound via StopSampling) are not copied
        return gcx;
    }

    public void Dispose() {
        if (this == Empty) return;
        envFrame.Free();
        boundInts.Clear();
        boundFloats.Clear();
        boundV2s.Clear();
        boundV3s.Clear();
        boundRV2s.Clear();
        firer = null;
        playerController = null;
        laserController = null;
        bullet = null;
        playerBullet = null;
        ++Recached;
        Cache.Push(this);
    }
    

    //Use for unscoped cases (bullet controls) only! Otherwise it's redundant
    // as the value will always be defined
    /// <summary>
    /// Create an expression that retrieves a field with name <see cref="name"/> and type <see cref="T"/>
    ///  if it exists. If it doesn't exist, returns <see cref="deflt"/> or throws an exception.
    /// </summary>
    public static Ex GetIfDefined<T>(TExArgCtx tac, string name, Ex? deflt) {
        return LexicalScope.VariableWithoutLexicalScope(tac, name, typeof(T), deflt);
    }


    /// <summary>
    /// Create an expression that sets the value of a field with name <see cref="name"/>.
    /// <br/>If the subclass of <see cref="PIData"/> is known, then does this by direct field access,
    /// otherwise uses the WriteT jumptable lookup.
    /// </summary>
    public static Ex SetValue(TExArgCtx tac, Type t, string name, Func<TExArgCtx, TEx> val) {
        var decl = tac.Ctx.Scope.FindVariable(name) ?? throw new Exception($"Couldn't locate variable {name}");
        return tac.Ctx.Scope.LocalOrParentVariable(tac, tac.EnvFrame, decl).Is(val(tac));
    }

    public static Ex SetValueDynamic(TExArgCtx tac, Type t, string name, Func<TExArgCtx, TEx> val) {
        return LexicalScope.VariableWithoutLexicalScope(tac, name, t, opOnValue: l => l.Is(val(tac)));
    }

    
    //Late-bound (dictionary-typed) variable handling

    private static TEx Hoisted(TExArgCtx tac, Type typ, string name, Func<Expression, Expression> constructor) {
        var key = Ex.Constant(GetDynamicKey(typ, name));
        var ex = constructor(key);
#if EXBAKE_SAVE
        //Don't duplicate hoisted references
        var key_name = "_hoisted" + name;
        var key_assign = FormattableString.Invariant(
            $"var {key_name} = PICustomData.GetDynamicKey(typeof({CSharpTypePrinter.Default.Print(typ)}), \"{name}\");");
        if (!tac.Ctx.HoistedVariables.Contains(key_assign)) {
            tac.Ctx.HoistedVariables.Add(key_assign);
            tac.Ctx.HoistedReplacements[key] = Expression.Variable(typeof(int), key_name);
        }
#endif
        return ex;
    }
    
    //Dynamic lookup methods, using dictionary instead of field references
    public static TEx ContainsDynamic(TExArgCtx tac, Type typ, string name) =>
        Hoisted(tac, typ, name, key => GetDict(tac.BPI.FiringCtx, typ).DictContains(key));

    public static Expression ContainsDynamic<T>(TExArgCtx tac, string name) =>
        ContainsDynamic(tac, typeof(T), name);
    public static Expression GetValueDynamic<T>(TExArgCtx tac, string name, TEx<T>? deflt = null) =>
        Hoisted(tac, typeof(T), name, key => deflt != null ?
            GetDict(tac.BPI.FiringCtx, typeof(T)).DictSafeGet(key, deflt) :
            GetDict(tac.BPI.FiringCtx, typeof(T)).DictGet(key));
    public static Expression SetValueDynamic<T>(TExArgCtx tac, string name, Expression val) =>
        Hoisted(tac, typeof(T), name, key => GetDict(tac.BPI.FiringCtx, typeof(T)).DictSet(key, val));
    
    public static Expression GetDict(Expression fctx, Type typ) {
        if (typ == ExUtils.tfloat)
            return fctx.Field("boundFloats");
        if (typ == ExUtils.tint)
            return fctx.Field("boundInts");
        if (typ == ExUtils.tv2)
            return fctx.Field("boundV2s");
        if (typ == ExUtils.tv3)
            return fctx.Field("boundV3s");
        if (typ == ExUtils.tv2rv2)
            return fctx.Field("boundRV2s");
        throw new ArgumentOutOfRangeException(typ.Name);
    }

    public static PIData New((LexicalScope scope, GenCtx gcx)? parent = null) {
        PIData data;
        if (Cache.Count > 0) {
            data = Cache.Pop();
            ++Popped;
        } else {
            data = new();
            ++Allocated;
        }
        if (parent.Try(out var p) && p.scope is not DMKScope) {
            data.envFrame = EnvFrame.Create(p.scope, p.gcx.EnvFrame);
        } else
            data.envFrame = EnvFrame.Empty;
        data.UpdateFirer(parent?.gcx);
        return data;
    }

    public void UpdateFirer(GenCtx? gcx) {
        firer = gcx?.exec;
        playerController = firer switch {
            PlayerController pi => pi,
            FireOption fo => fo.Player,
            Bullet b => b.Player?.firer,
            _ => null
        };
        if (playerController == null)
            playerController = gcx?.playerController;
    }
    
    /// <summary>
    /// Create a new instance of the base <see cref="PIData"/> class.
    /// Only use this if you don't need to store any bound variables.
    /// </summary>
    public static PIData NewUnscoped(GenCtx? gcx = null) =>
        New(gcx == null ? null : (DMKScope.Singleton, gcx));

}

/// <summary>
/// A struct containing the input required for a parametric equation.
/// </summary>
public struct ParametricInfo {
    public static ParametricInfo Zero = new(PIData.Empty, Vector2.zero, 0, 0, 0);
    /// <summary>Random ID</summary>
    public readonly uint id;
    /// <summary>Firing index</summary>
    public readonly int index;
    /// <summary>Global position</summary>
    public Vector3 loc;
    /// <summary>Life-time (with minor adjustment)</summary>
    public float t;
    /// <summary>Context containing additional bound variables</summary>
    public PIData ctx;

    /// <summary>
    /// Global location as a Vector2 (ignores Z-coordinate)
    /// </summary>
    [UsedImplicitly]
    public Vector2 LocV2 => loc;

    public ParametricInfo(in Movement mov, int findex = 0, uint? id = null, float t = 0, GenCtx? firer = null) : 
        this(mov.rootPos, findex, id, t, firer) { }
    public ParametricInfo(Vector3 position, int findex = 0, uint? id = null, float t = 0, GenCtx? firer = null) {
        loc = position;
        index = findex;
        this.id = id ?? RNG.GetUInt();
        this.t = t;
        this.ctx = PIData.NewUnscoped(firer);
    }
    public ParametricInfo(PIData ctx, in Movement mov, int findex = 0, uint? id = null, float t = 0) : 
        this(ctx, mov.rootPos, findex, id, t) { }
    public ParametricInfo(PIData ctx, Vector3 position, int findex = 0, uint? id = null, float t = 0) {
        loc = position;
        index = findex;
        this.id = id ?? RNG.GetUInt();
        this.t = t;
        this.ctx = ctx;
    }

    public ParametricInfo Rehash() => new(ctx, loc, index, RNG.Rehash(id), t);
    public ParametricInfo CopyWithT(float newT) => new(ctx, loc, index, id, newT);

    public ParametricInfo CopyCtx(uint newId) => new(ctx.Clone_NoAlloc(), loc, index, newId, t);
    
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

    public void Dispose() {
        ctx.Dispose();
        //Prevents double dispose
        ctx = PIData.Empty;
    }
}

//Note: ref mov/ in dT/ ref bpi/ ref delta are significant optimizations.
// (I don't know why in float is so significant. Probably because in the SimpleBullet case
// it's read from the same memory location for all bullets within a pool. That would be good cache performance.)
//ref bpi is used over in bpi because there are methods on bpi (copyWithP, copyWithT, etc) that
// would trigger defensive struct copies. (Methods and properties both trigger defensive copies.)
//ref mov is used for the same reason, though no such methods/properties currently exist.
//ref delta is used instead of out delta because 2D equations do not assign to the Z-component for efficiency.

/// <summary>
/// A function that converts ParametricInfo into a possibly-rotated Cartesian coordinate.
/// </summary>
public delegate void CoordF(float cos, float sin, ParametricInfo bpi, ref Vector3 vec);
/// <summary>
/// A function that converts ParametricInfo into a possibly-rotated Cartesian coordinate
/// representing the next position that the Velocity struct should take with a timestep of dT.
/// </summary>
public delegate void VTP(ref Movement vel, in float dT, ref ParametricInfo bpi, ref Vector3 delta);
/// <summary>
/// A function that converts ParametricInfo into a possibly-rotated Cartesian coordinate
/// representing the next position that the <see cref="LaserMovement"/> struct should take with a timestep of dT
/// and a laser lifetime of lT.
/// </summary>
public delegate void LVTP(ref LaserMovement vel, in float dT, in float lT, ref ParametricInfo bpi, ref Vector3 delta);


public readonly struct RootedVTP {
    public readonly GCXF<Vector2> root;
    public readonly VTP path;

    public RootedVTP(GCXF<Vector2> root, VTP path) {
        this.root = root;
        this.path = path;
    }

    public RootedVTP(GCXF<Vector2> root, ExVTP path) : this(root, Compilers.VTP(path)) { }

    public RootedVTP(ExBPY x, ExBPY y, ExVTP path) : this(Parametrics.PXY(x, y), path) { }
    public RootedVTP(ExTP root, ExVTP path) : this(Compilers.GCXF(root), Compilers.VTP(path)) { }
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
/// A function that converts ParametricInfo into a V2RV2.
/// </summary>
public delegate V2RV2 BPRV2(ParametricInfo bpi);

/// <summary>
/// A function that converts ParametricInfo into a boolean.
/// </summary>
public delegate bool Pred(ParametricInfo bpi);
/// <summary>
/// A function that converts ParametricInfo and a laser lifetime into a Vector2.
/// </summary>
public delegate bool LPred(ParametricInfo bpi, float lT);

/// <summary>
/// A wrapper type used for functions that operate over a <see cref="GenCtx"/>.
/// </summary>
/// <typeparam name="T">Return object type (eg. float, v2, rv2)</typeparam>
public delegate T GCXF<T>(GenCtx gcx);

/// <summary>
/// A wrapper around <see cref="GCXF{T}"/> whose return value is discarded.
/// </summary>
public delegate void ErasedGCXF(GenCtx gcx);

/// <summary>
/// A wrapper around a function of ParametricInfo whose return value is discarded.
/// </summary>
public delegate void ErasedParametric(ParametricInfo bpi);

/// <summary>
/// A bullet control function performing some operation on a SimpleBullet.
/// <br/>The cancellation token is stored in the BulletControl struct. It may be used by the control
/// to bound nested summons (eg. via the SM control).
/// </summary>
public delegate void SBCF(in BulletManager.SimpleBulletCollection.VelocityUpdateState state, in ParametricInfo bpi, in ICancellee cT);

/// <summary>
/// A pool control function performing some operation on a simple bullet pool.
/// <br/>The returned disposable can be used to cancel the effect.
/// </summary>
public delegate IDisposable SPCF(string pool, ICancellee cT);

}