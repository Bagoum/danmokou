using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Expressions;
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
using Danmokou.Reflection.CustomData;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine.Profiling;
using Ex = System.Linq.Expressions.Expression;
using ExVTP = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV3, Danmokou.Expressions.TEx>;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
#pragma warning disable CS0162

namespace Danmokou.DMath {
/// <summary>
/// DMK v10 replacement for FiringCtx. This class is subclassed via runtime MSIL generation to
///  provide efficient lookup of arbitrary fields.
/// </summary>
public class PICustomData {
    public static readonly PICustomData Empty = new();
    //For dictionary variables, such as those created for state control in SS0 or onlyonce
    private static readonly Dictionary<(Type type, string name), int> dynamicKeyNames = new();
    public static int GetDynamicKey(Type t, string name) {
        return dynamicKeyNames[(t, name)] = PICustomDataBuilder.Builder.GetVariableKey(name, t);
    }
    public int typeIndex;

    //For culled bullets, sb.bpi.t points to a countdown from FADE_TIME to 0, and this points to the
    // lifetime of the bullet, which is used to calculate direction.
    public float culledBulletTime;
    
    //Late-bound variables, such as those created for state control in SS0 or onlyonce
    // In the DISABLE_TYPE_BUILDING case, this is used for all bound variables
    public readonly Dictionary<int, int> boundInts = new();
    public readonly Dictionary<int, float> boundFloats = new();
    public readonly Dictionary<int, Vector2> boundV2s = new();
    public readonly Dictionary<int, Vector3> boundV3s = new();
    public readonly Dictionary<int, V2RV2> boundRV2s = new();

    public BehaviorEntity? firer; //Note this may be repooled or otherwise destroyed during execution
    
    public PlayerController? playerController; //For player bullets
    [UsedImplicitly]
    public PlayerController PlayerController =>
        playerController != null ?
            playerController :
            throw new Exception("FiringCtx does not have a player controller. " +
                                "Please make sure that player bullets are fired in player scripts only.");

    [UsedImplicitly]
    public FireOption OptionFirer {
        get {
            if (firer is FireOption fo)
                return fo;
            throw new Exception("FiringCtx does not have an option firer");
        }
    }

    public CurvedTileRenderLaser? laserController;
    [UsedImplicitly]
    public CurvedTileRenderLaser LaserController => 
        laserController ?? throw new Exception("FiringCtx does not have a laser controller");
    public PlayerBullet? playerBullet;

    /// <summary>
    /// Copy this object's variables into another object of the same type.
    /// <br/>Not virtual, so only this class' variables are copied.
    /// </summary>
    public PICustomData CopyInto(PICustomData copyee) {
        ++Metadata.GetTypeDef(this).Copied;
        boundInts.CopyInto(copyee.boundInts);
        boundFloats.CopyInto(copyee.boundFloats);
        boundV2s.CopyInto(copyee.boundV2s);
        boundV3s.CopyInto(copyee.boundV3s);
        boundRV2s.CopyInto(copyee.boundRV2s);
        copyee.typeIndex = typeIndex;
        copyee.firer = firer;
        copyee.playerController = playerController;
        copyee.laserController = laserController;
        copyee.playerBullet = playerBullet;
        return copyee;
    }

    /// <summary>
    /// Copy this object's variables into another object of the same type.
    /// <br/>Virtual, so subclasses should implement this by casting the argument to their own type
    /// and then calling their own <see cref="CopyInto"/>.
    /// </summary>
    public virtual PICustomData CopyIntoVirtual(PICustomData copyee) => CopyInto(copyee);
    
    public virtual PICustomData Clone() => CopyInto(new PICustomData());

    public virtual bool HasFloat(int id) => 
        PICustomDataBuilder.DISABLE_TYPE_BUILDING && boundFloats.ContainsKey(id);
    public virtual bool HasInt(int id) => 
        PICustomDataBuilder.DISABLE_TYPE_BUILDING && boundInts.ContainsKey(id);
    public virtual bool HasVector2(int id) => 
        PICustomDataBuilder.DISABLE_TYPE_BUILDING && boundV2s.ContainsKey(id);
    public virtual bool HasVector3(int id) => 
        PICustomDataBuilder.DISABLE_TYPE_BUILDING && boundV3s.ContainsKey(id);
    public virtual bool HasV2RV2(int id) => 
        PICustomDataBuilder.DISABLE_TYPE_BUILDING && boundRV2s.ContainsKey(id);
    public virtual float ReadFloat(int id) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundFloats[id] : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(ReadFloat)}");
    public virtual int ReadInt(int id) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundInts[id] : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(ReadInt)}");
    public virtual Vector2 ReadVector2(int id) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundV2s[id] : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(ReadVector2)}");
    public virtual Vector3 ReadVector3(int id) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundV3s[id] : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(ReadVector3)}");
    public virtual V2RV2 ReadV2RV2(int id) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundRV2s[id] : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(ReadV2RV2)}");
    
    public virtual float WriteFloat(int id, float val) => 
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundFloats[id] = val : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(WriteFloat)}");
    public virtual int WriteInt(int id, int val) => 
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundInts[id] = val : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(WriteInt)}");
    public virtual Vector2 WriteVector2(int id, Vector2 val) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundV2s[id] = val : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(WriteVector2)}");
    public virtual Vector3 WriteVector3(int id, Vector3 val) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundV3s[id] = val : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(WriteVector3)}");
    public virtual V2RV2 WriteV2RV2(int id, V2RV2 val) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ? boundRV2s[id] = val : throw new Exception(
        $"The base {nameof(PICustomData)} class has no dynamic variables for {nameof(WriteV2RV2)}");

    public GenCtx RevertToGCX(BehaviorEntity exec) {
        var gcx = GenCtx.New(exec, V2RV2.Zero);
        gcx.playerController = playerController;
        foreach (var field in PICustomDataBuilder.Builder.GetTypeDef(this).Descriptor.Fields) {
            var ft = field.Descriptor.Type;
            if (ft == ExUtils.tfloat)
                gcx.fs[field.Descriptor.Name] = ReadFloat(field.ID);
            if (ft == ExUtils.tv2)
                gcx.v2s[field.Descriptor.Name] = ReadVector2(field.ID);
            if (ft == ExUtils.tv3)
                gcx.v3s[field.Descriptor.Name] = ReadVector3(field.ID);
            if (ft == ExUtils.tv2rv2)
                gcx.rv2s[field.Descriptor.Name] = ReadV2RV2(field.ID);
        }
        foreach (var sk in dynamicKeyNames) {
            if (boundFloats.ContainsKey(sk.Value))
                gcx.fs[sk.Key.name] = boundFloats[sk.Value];
            if (boundV2s.ContainsKey(sk.Value))
                gcx.v2s[sk.Key.name] = boundV2s[sk.Value];
            if (boundV3s.ContainsKey(sk.Value))
                gcx.v3s[sk.Key.name] = boundV3s[sk.Value];
            if (boundRV2s.ContainsKey(sk.Value))
                gcx.rv2s[sk.Key.name] = boundRV2s[sk.Value];
        }
        return gcx;
    }

    public void Dispose() {
        if (this == Empty) return;
        Metadata.GetTypeDef(this).Return(this);
    }

    public void UploadWrite(Type ext, string varName, GenCtx gcx, bool useDefaultValue = false) { 
        var id = PICustomDataBuilder.Builder.GetVariableKey(varName, ext);
        if (PICustomDataBuilder.DISABLE_TYPE_BUILDING) {
            if (ext == ExUtils.tfloat)
                boundFloats[id] = gcx.MaybeGetFloat(varName) ?? (useDefaultValue ? default : 
                    throw new Exception($"No float {varName} in bullet GCX"));
            else if (ext == ExUtils.tv2)
                boundV2s[id] = gcx.V2s.MaybeGet(varName) ?? (useDefaultValue ? default :
                                  throw new Exception($"No vector2 {varName} in bullet GCX"));
            else if (ext == ExUtils.tv3)
                boundV3s[id] = gcx.V3s.MaybeGet(varName) ?? (useDefaultValue ? default :
                                  throw new Exception($"No vector3 {varName} in bullet GCX"));
            else if (ext == ExUtils.tv2rv2)
                boundRV2s[id] = gcx.RV2s.MaybeGet(varName) ?? (useDefaultValue ? default :
                                   throw new Exception($"No V2RV2 {varName} in bullet GCX"));
            else throw new ArgumentOutOfRangeException($"{ext}");
        } else {
            if (ext == ExUtils.tfloat)
                WriteFloat(id, gcx.MaybeGetFloat(varName) ?? (useDefaultValue ?
                    default :
                    throw new Exception($"No float {varName} in bullet GCX")));
            else if (ext == ExUtils.tint)
                WriteInt(id, (useDefaultValue ? default : throw new Exception($"No int {varName} in bullet GCX")));
            else if (ext == ExUtils.tv2)
                WriteVector2(id, gcx.V2s.MaybeGet(varName) ?? (useDefaultValue ?
                    default :
                    throw new Exception($"No vector2 {varName} in bullet GCX")));
            else if (ext == ExUtils.tv3)
                WriteVector3(id, gcx.V3s.MaybeGet(varName) ?? (useDefaultValue ?
                    default :
                    throw new Exception($"No vector3 {varName} in bullet GCX")));
            else if (ext == ExUtils.tv2rv2)
                WriteV2RV2(id, gcx.RV2s.MaybeGet(varName) ?? (useDefaultValue ?
                    default :
                    throw new Exception($"No V2RV2 {varName} in bullet GCX")));
            else throw new ArgumentOutOfRangeException($"{ext}");
        }
    }
    
    /// <summary>
    /// Retrieve the variables defined in <see cref="boundVars"/> from <see cref="gcx"/>
    ///  and set them on this object.
    /// </summary>
    public void UploadAdd(IList<(Type, string)> boundVars, GenCtx gcx) {
        for (int ii = 0; ii < boundVars.Count; ++ii) {
            var (ext, varNameS) = boundVars[ii];
            UploadWrite(ext, varNameS, gcx);
        }
    }
    

    //Use for unscoped cases (bullet controls) only! Otherwise it's redundant
    // as the value will always be defined
    /// <summary>
    /// Create an expression that retrieves a field with name <see cref="name"/> and type <see cref="T"/>
    ///  if it exists, else returns <see cref="deflt"/>.
    /// </summary>
    public static Ex GetIfDefined<T>(TExArgCtx tac, string name, TEx<T> deflt) {
        if (PICustomDataBuilder.DISABLE_TYPE_BUILDING)
            return GetValueDynamic(tac, name, deflt);
        var t = typeof(T);
        var id = Ex.Constant(Metadata.GetVariableKey(name, t));
        var has = ExFunction.WrapAny<PICustomData>(Metadata.FieldCheckerMethodName(t));
        var get = ExFunction.WrapAny<PICustomData>(Metadata.FieldReaderMethodName(t));
        return Ex.Condition(
            has.InstanceOf(tac.BPI.FiringCtx, id),
            get.InstanceOf(tac.BPI.FiringCtx, id),
            deflt);
    }

    /// <summary>
    /// Create an expression that retrieves a field with name <see cref="name"/>.
    /// <br/>If the subclass of <see cref="PICustomData"/> is known, then does this by direct field access,
    /// otherwise uses the ReadT jumptable lookup.
    /// </summary>
    public static Ex GetValue(TExArgCtx tac, Type t, string name) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ?
            GetValueDynamic(tac, t, name) :
            tac.Ctx.CustomDataType is { } cdt ?
                //For scoped calls, use field access, eg. (bpi.ctx as CustomData1).m0_myFloat
                tac.BPI.FiringCtx.As(cdt)
                    .Field(Metadata.GetFieldName(name, t)) :
                //For unscoped calls, use the ReadT call
                ExFunction.WrapAny<PICustomData>(
                    Metadata.FieldReaderMethodName(t))
                    .InstanceOf(
                        tac.BPI.FiringCtx, 
                        Ex.Constant(Metadata.GetVariableKey(name, t)))
                ;

    /// <summary>
    /// <inheritdoc cref="GetValue"/>
    /// </summary>
    public static Ex GetValue<T>(TExArgCtx tac, string name) =>
        GetValue(tac, typeof(T), name);

    
    /// <summary>
    /// Create an expression that sets the value of a field with name <see cref="name"/>.
    /// <br/>If the subclass of <see cref="PICustomData"/> is known, then does this by direct field access,
    /// otherwise uses the WriteT jumptable lookup.
    /// </summary>
    public static Ex SetValue(TExArgCtx tac, Type t, string name, Ex val) =>
        PICustomDataBuilder.DISABLE_TYPE_BUILDING ?
            SetValueDynamic(tac, t, name, val) :
        tac.Ctx.CustomDataType is { } cdt ?
            tac.BPI.FiringCtx.As(cdt)
                .Field(Metadata.GetFieldName(name, t)).Is(val) :
            ExFunction.WrapAny<PICustomData>(
                    Metadata.FieldWriterMethodName(t))
                .InstanceOf(
                    tac.BPI.FiringCtx, 
                    Ex.Constant(Metadata.GetVariableKey(name, t)),
                    val);

    /// <summary>
    /// <inheritdoc cref="SetValue"/>
    /// </summary>
    public static Ex SetValue<T>(TExArgCtx tac, string name, Ex val) =>
        SetValue(tac, typeof(T), name, val);
    
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
        Hoisted(tac, typeof(T), name, key => GetDict(tac.BPI.FiringCtx, typeof(T)).DictContains(key));
    
    public static TEx GetValueDynamic(TExArgCtx tac, Type typ, string name) =>
        Hoisted(tac, typ, name, key => GetDict(tac.BPI.FiringCtx, typ).DictGet(key));
    public static Expression GetValueDynamic<T>(TExArgCtx tac, string name, TEx<T>? deflt = null) =>
        Hoisted(tac, typeof(T), name, key => deflt != null ?
            GetDict(tac.BPI.FiringCtx, typeof(T)).DictSafeGet(key, deflt) :
            GetDict(tac.BPI.FiringCtx, typeof(T)).DictGet(key));

    public static Expression SetValueDynamic(TExArgCtx tac, Type typ, string name, Expression val) =>
        Hoisted(tac, typ, name, key => GetDict(tac.BPI.FiringCtx, typ).DictSet(key, val));
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

    private static Type piDataType = typeof(PICustomData);
    private static PICustomDataBuilder Metadata => PICustomDataBuilder.Builder;

    /// <summary>
    /// Create a new instance of the base <see cref="PICustomData"/> class.
    /// Only use this if you don't need to store any bound variables.
    /// </summary>
    public static PICustomData New(GenCtx? gcx = null) =>
        Metadata.ConstructedBaseType.MakeNew(gcx);

}
/*
public class FiringCtx {
    public const string FLIPX = "flipX";
    public const int FLIPX_KEY = -1;
    public const string FLIPY = "flipY";
    public const int FLIPY_KEY = -2;
    private static readonly Dictionary<string, int> keyNames = new();
    //Negative values are reserved
    private static int lastKey = 0;

    static FiringCtx() {
        ClearNames();
    }

    private static void ReserveNames() {
        keyNames[FLIPX] = FLIPX_KEY;
        keyNames[FLIPY] = FLIPY_KEY;
    }
    public static void ClearNames() {
        keyNames.Clear();
        ReserveNames();
        lastKey = 0;
    }
    public static int GetKey(string name) {
        if (keyNames.TryGetValue(name, out var res)) return res;
        keyNames[name] = lastKey;
        return lastKey++;
    }
    
    public enum DataType {
        Int,
        Float,
        V2,
        V3,
        RV2
    }
    public readonly Dictionary<int, int> boundInts = new();
    public readonly Dictionary<int, float> boundFloats = new();
    public readonly Dictionary<int, Vector2> boundV2s = new();
    public readonly Dictionary<int, Vector3> boundV3s = new();
    public readonly Dictionary<int, V2RV2> boundRV2s = new();
    public BehaviorEntity? firer; //Note this may be repooled or otherwise destroyed during execution
    
    public PlayerController? playerController; //For player bullets
    [UsedImplicitly]
    public PlayerController PlayerController =>
        playerController != null ?
            playerController :
            throw new Exception("FiringCtx does not have a player controller. " +
                                "Please make sure that player bullets are fired in player scripts only.");

    [UsedImplicitly]
    public FireOption OptionFirer {
        get {
            if (firer is FireOption fo)
                return fo;
            throw new Exception("FiringCtx does not have an option firer");
        }
    }

    public CurvedTileRenderLaser? laserController;
    [UsedImplicitly]
    public CurvedTileRenderLaser LaserController => 
        laserController ?? throw new Exception("FiringCtx does not have a laser controller");
    public PlayerBullet? playerBullet;
    
    private static readonly Stack<FiringCtx> cache = new();
    public static int Allocated { get; private set; }
    public static int Popped { get; private set; }
    public static int Recached { get; private set; }
    public static int Copied { get; private set; }

    public static readonly FiringCtx Empty = new();

    private FiringCtx() { }
    public static FiringCtx New(GenCtx? gcx = null) {
        FiringCtx nCtx;
        if (cache.Count > 0) {
            nCtx = cache.Pop();
            ++Popped;
        } else {
            nCtx = new FiringCtx();
            ++Allocated;
        }
        nCtx.firer = gcx?.exec;
        nCtx.playerController = nCtx.firer switch {
            PlayerController pi => pi,
            FireOption fo => fo.Player,
            Bullet b => b.Player?.firer,
            _ => null
        };
        if (nCtx.playerController == null)
            nCtx.playerController = gcx?.playerController;
        return nCtx;
    }

    public void FlipX() => boundFloats[FLIPX_KEY] *= -1;
    public void FlipY() => boundFloats[FLIPY_KEY] *= -1;
    
    public static Expression ExFlipX(TExArgCtx tac) {
        var d = GetDict(tac.BPI.FiringCtx, DataType.Float);
        return d.DictSet(Expression.Constant(FLIPX_KEY), d.DictGet(Expression.Constant(FLIPX_KEY)).Mul(-1f));
    }
    public static Expression ExFlipY(TExArgCtx tac) {
        var d = GetDict(tac.BPI.FiringCtx, DataType.Float);
        return d.DictSet(Expression.Constant(FLIPY_KEY), d.DictGet(Expression.Constant(FLIPY_KEY)).Mul(-1f));
    }
    
    private float? DefaultFloatValue(string varName) => varName switch {
        "flipX" => 1,
        "flipY" => 1,
        _ => null
    };
    private void UploadAddOne(Reflector.ExType ext, string varName, GenCtx gcx) {
        var varId = GetKey(varName);
        if (ext == Reflector.ExType.Float)
            boundFloats[varId] = gcx.MaybeGetFloat(varName) ?? DefaultFloatValue(varName) ??
                throw new Exception($"No float {varName} in bullet GCX");
        else if (ext == Reflector.ExType.V2)
            boundV2s[varId] = gcx.V2s.MaybeGet(varName) ??
                              throw new Exception($"No vector2 {varName} in bullet GCX");
        else if (ext == Reflector.ExType.V3)
            boundV3s[varId] = gcx.V3s.MaybeGet(varName) ??
                              throw new Exception($"No vector3 {varName} in bullet GCX");
        else if (ext == Reflector.ExType.RV2)
            boundRV2s[varId] = gcx.RV2s.MaybeGet(varName) ??
                               throw new Exception($"No V2RV2 {varName} in bullet GCX");
        else throw new Exception($"Cannot hoist GCX data {varName}<{ext}>.");
    }
    public void UploadAdd(IList<(Reflector.ExType, string)> boundVars, GenCtx gcx) {
        for (int ii = 0; ii < boundVars.Count; ++ii) {
            var (ext, varNameS) = boundVars[ii];
            UploadAddOne(ext, varNameS, gcx);
        }
        //for (int ii = 0; ii < gcx.exposed.Count; ++ii) {
        //    var (ext, varNameS) = gcx.exposed[ii];
        //    UploadAddOne(ext, varNameS, gcx);
        //}
    }

    public GenCtx RevertToGCX(BehaviorEntity exec) {
        var gcx = GenCtx.New(exec, V2RV2.Zero);
        gcx.playerController = playerController;
        foreach (var sk in keyNames) {
            if (boundFloats.ContainsKey(sk.Value))
                gcx.fs[sk.Key] = boundFloats[sk.Value];
            if (boundV2s.ContainsKey(sk.Value))
                gcx.v2s[sk.Key] = boundV2s[sk.Value];
            if (boundV3s.ContainsKey(sk.Value))
                gcx.v3s[sk.Key] = boundV3s[sk.Value];
            if (boundRV2s.ContainsKey(sk.Value))
                gcx.rv2s[sk.Key] = boundRV2s[sk.Value];
        }
        return gcx;
    }

    public FiringCtx Copy() {
        ++Copied;
        var nCtx = New();
        boundInts.CopyInto(nCtx.boundInts);
        boundFloats.CopyInto(nCtx.boundFloats);
        boundV2s.CopyInto(nCtx.boundV2s);
        boundV3s.CopyInto(nCtx.boundV3s);
        boundRV2s.CopyInto(nCtx.boundRV2s);
        nCtx.firer = firer;
        nCtx.playerController = playerController;
        nCtx.laserController = laserController;
        nCtx.playerBullet = playerBullet;
        return nCtx;
    }

    public void Dispose() {
        if (this == Empty) return;
        boundInts.Clear();
        boundFloats.Clear();
        boundV2s.Clear();
        boundV3s.Clear();
        boundRV2s.Clear();
        firer = null;
        playerController = null;
        laserController = null;
        playerBullet = null;
        ++Recached;
        cache.Push(this);
    }

    //Expression methods

    public static DataType FromType<T>() {
        var t = typeof(T);
        if (t == typeof(Vector2))
            return DataType.V2;
        if (t == typeof(Vector3))
            return DataType.V3;
        if (t == typeof(V2RV2))
            return DataType.RV2;
        if (t == typeof(int))
            return DataType.Int;
        else
            return DataType.Float;
    }

    private static TEx Hoisted(TExArgCtx tac, DataType typ, string name, Func<Expression, Expression> constructor) {
        var ex = constructor(exGetKey(name));
#if EXBAKE_SAVE
        //Don't duplicate hoisted references
        var key_name = "_hoisted" + name;
        var key_assign = FormattableString.Invariant(
            $"var {key_name} = FiringCtx.GetKey(\"{name}\");");
        if (!tac.Ctx.HoistedVariables.Contains(key_assign)) {
            tac.Ctx.HoistedVariables.Add(key_assign);
            tac.Ctx.HoistedReplacements[exGetKey(name)] = Expression.Variable(typeof(int), key_name);
        }
#endif
        return ex;
    }
    
    public static TEx Contains(TExArgCtx tac, DataType typ, string name) =>
        Hoisted(tac, typ, name, key => GetDict(tac.BPI.FiringCtx, typ).DictContains(key));
    public static Expression Contains<T>(TExArgCtx tac, string name) =>
        Hoisted(tac, FromType<T>(), name, key => GetDict(tac.BPI.FiringCtx, FromType<T>()).DictContains(key));
    
    public static TEx GetValue(TExArgCtx tac, DataType typ, string name) =>
        Hoisted(tac, typ, name, key => GetDict(tac.BPI.FiringCtx, typ).DictGet(key));
    public static Expression GetValue<T>(TExArgCtx tac, string name, TEx<T>? deflt = null) =>
        Hoisted(tac, FromType<T>(), name, key => deflt != null ?
            GetDict(tac.BPI.FiringCtx, FromType<T>()).DictSafeGet(key, deflt) :
            GetDict(tac.BPI.FiringCtx, FromType<T>()).DictGet(key));

    public static Expression SetValue(TExArgCtx tac, DataType typ, string name, Expression val) =>
        Hoisted(tac, typ, name, key => GetDict(tac.BPI.FiringCtx, typ).DictSet(key, val));
    public static Expression SetValue<T>(TExArgCtx tac, string name, Expression val) =>
        Hoisted(tac, FromType<T>(), name, key => GetDict(tac.BPI.FiringCtx, FromType<T>()).DictSet(key, val));
    
    public static Expression GetDict(Expression fctx, DataType typ) => typ switch {
        DataType.RV2 => fctx.Field("boundRV2s"),
        DataType.V3 => fctx.Field("boundV3s"),
        DataType.V2 => fctx.Field("boundV2s"),
        DataType.Int => fctx.Field("boundInts"),
        _ => fctx.Field("boundFloats")
    };

    private static Expression exGetKey(string name) => Expression.Constant(GetKey(name));
    
}
*/

/// <summary>
/// A struct containing the input required for a parametric equation.
/// </summary>
public struct ParametricInfo {
    public static ParametricInfo Zero = new(Vector2.zero, 0, 0, 0);
    /// <summary>Random ID</summary>
    public readonly uint id;
    /// <summary>Firing index</summary>
    public readonly int index;
    /// <summary>Global position</summary>
    public Vector3 loc;
    /// <summary>Life-time (with minor adjustment)</summary>
    public float t;
    /// <summary>Context containing additional bound variables</summary>
    public PICustomData ctx;

    /// <summary>
    /// Global location as a Vector2 (ignores Z-coordinate)
    /// </summary>
    [UsedImplicitly]
    public Vector2 LocV2 => loc;

    public static ParametricInfo WithRandomId(Vector3 position, int findex, float t) => new(position, findex, RNG.GetUInt(), t);
    public static ParametricInfo WithRandomId(Vector3 position, int findex) => WithRandomId(position, findex, 0f);

    public ParametricInfo(in Movement mov, int findex = 0, uint? id = null, float t = 0) : 
        this(mov.rootPos, findex, id, t) { }
    public ParametricInfo(Vector3 position, int findex = 0, uint? id = null, float t = 0) {
        loc = position;
        index = findex;
        this.id = id ?? RNG.GetUInt();
        this.t = t;
        this.ctx = PICustomData.New();
    }
    public ParametricInfo(PICustomData ctx, in Movement mov, int findex = 0, uint? id = null, float t = 0) : 
        this(ctx, mov.rootPos, findex, id, t) { }
    public ParametricInfo(PICustomData ctx, Vector3 position, int findex = 0, uint? id = null, float t = 0) {
        loc = position;
        index = findex;
        this.id = id ?? RNG.GetUInt();
        this.t = t;
        this.ctx = ctx;
    }

    public ParametricInfo Rehash() => new(ctx, loc, index, RNG.Rehash(id), t);
    public ParametricInfo CopyWithT(float newT) => new(ctx, loc, index, id, newT);

    public ParametricInfo CopyCtx(uint newId) => new(ctx.Clone(), loc, index, newId, t);
    
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
        ctx = PICustomData.Empty;
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
/// A wrapper type used for functions that operate over a GCX.
/// </summary>
/// <typeparam name="T">Return object type (eg. float, v2, rv2)</typeparam>
public delegate T GCXF<T>(GenCtx gcx);


//public delegate Fn GCXUFn<Fn>(GenCtx gcx, out FiringCtx fctx);

/// <summary>
/// A wrapper type used to upload values from a GCX to private data hoisting before providing a delegate to a new object.
/// <br/>It is recommended to call <see cref="CompileDelegate"/> or <see cref="ShareTypeAndCompile"/> immediately after construction, as this avoids compiling expressions or types during gameplay, and is also required for AOT support.
/// </summary>
public abstract record GCXU(List<(Type, string)> BoundAliases) {
    public ConstructedType? CustomDataType { get; private set; }

    public abstract void CompileDelegate();
    
    /// <summary>
    /// Compile the custom data type for this GCXU if it is not already set.
    /// </summary>
    public ConstructedType CompileCustomDataType() {
        if (CustomDataType == null)
            ShareType(this);
        return CustomDataType!;
    }

    /// <summary>
    /// Set the custom data type, throwing an exception if it is already set.
    /// </summary>
    /// <param name="type"></param>
    protected void SetNewCustomDataType(ConstructedType type) {
        if (CustomDataType == null)
            CustomDataType = type;
        else if (CustomDataType != type)
            throw new Exception("GCXU's custom data type cannot be changed once set");
    }
    
    /// <summary>
    /// Create a single shared type for multiple GCXUs.
    /// </summary>
    public static ConstructedType ShareType(params GCXU?[] gcxus) {
        ConstructedType type;
        for (int ii = 0; ii < gcxus.Length; ++ii) {
            if (gcxus[ii]?.BoundAliases.Count > 0)
                goto multiple;
        }
        type = PICustomDataBuilder.Builder.ConstructedBaseType;
        goto assign;
        
        multiple:
        var aliases = new HashSet<(Type, string)>();
        for (int ii = 0; ii < gcxus.Length; ++ii)
        for (int vi = 0; vi < gcxus[ii]?.BoundAliases.Count; ++vi)
            aliases.Add(gcxus[ii]!.BoundAliases[vi]);
        type = PICustomDataBuilder.Builder.GetCustomDataType(aliases.OrderBy(x => (x.Item1.Name, x.Item2)).ToArray());
        
        assign:
        for (int ii = 0; ii < gcxus.Length; ++ii)
            gcxus[ii]?.SetNewCustomDataType(type);
        return type;
    }

    /// <summary>
    /// Create a single shared type for multiple GCXUs, and compile them all with that type.
    /// </summary>
    public static ConstructedType ShareTypeAndCompile(params GCXU?[] gcxus) {
        var type = ShareType(gcxus);
        for (int ii = 0; ii < gcxus.Length; ++ii)
            gcxus[ii]?.CompileDelegate();
        return type;
    }
    
}

/// <inheritdoc/>
/// <typeparam name="Fn">Delegate type (eg. TP, BPY, Pred)</typeparam>
public record GCXU<Fn>(List<(Type, string)> BoundAliases, Func<ConstructedType, Fn> LazyDelegate) : GCXU(BoundAliases) {
    private Func<ConstructedType, Fn> LazyDelegate { get; set; } = LazyDelegate;
    private Fn? _delegate;

    /// <summary>
    /// Compile the delegate for this GCXU, using the custom data type that is already set or automatically determining it
    /// via <see cref="GCXU.ShareType"/>.
    /// <br/>After this is called, the custom data type cannot be changed.
    /// </summary>
    public override void CompileDelegate() {
        if (_delegate is null) {
            Profiler.BeginSample("GCXU compilation");
            _delegate = LazyDelegate(CompileCustomDataType());
            LazyDelegate = null!;
            Profiler.EndSample();
        }
    }
    
    /// <summary>
    /// Compile the delegate for this GCXU, using the custom data type provided as an argument.
    /// </summary>
    private Fn CompileDelegate(ConstructedType withType) {
        if (_delegate is null) {
            SetNewCustomDataType(withType);
            CompileDelegate();
        } else if (withType != CustomDataType) {
            throw new Exception("GCXU's custom data type cannot be set after delegate compilation");
        }
        return _delegate!;
    }


    /// <summary>
    /// When a FiringCtx already exists, write bound values to it
    ///  and then return the delegate.
    /// </summary>
    public Fn Execute(GenCtx gcx, PICustomData fctx) {
        fctx.UploadAdd(BoundAliases, gcx);
        CompileDelegate();
        return _delegate!;
    }
    
    /// <summary>
    /// Create a new FiringCtx, write bound values to it,
    ///  and then return the delegate.
    /// </summary>
    public Fn Execute(GenCtx gcx, out PICustomData fctx) {
        fctx = CompileCustomDataType().MakeNew(gcx);
        return Execute(gcx, fctx);
    }    
    
    /// <summary>
    /// When a FiringCtx already exists, write bound values to it
    ///  and then return the delegate.
    /// Also asserts that the custom data can be provided by the given
    ///  <see cref="ConstructedType"/>.
    /// </summary>
    public Fn ExecuteWithType(GenCtx gcx, PICustomData fctx, ConstructedType ct) {
        fctx.UploadAdd(BoundAliases, gcx);
        return CompileDelegate(ct);
    }
    /// <summary>
    /// Create a new FiringCtx with the given <see cref="ConstructedType"/>,
    ///  write bound values to it, and then return the delegate.
    /// Also asserts that the custom data can be provided by the given
    ///  <see cref="ConstructedType"/>.
    /// </summary>
    public Fn ExecuteWithType(GenCtx gcx, out PICustomData fctx, ConstructedType ct) {
        fctx = ct.MakeNew(gcx);
        return ExecuteWithType(gcx, fctx, ct);
    }

}

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