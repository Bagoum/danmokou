using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BagoumLib;
using BagoumLib.Functional;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Reflection2;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Danmaku {

/// <summary>
/// A set of bound variables for danmaku objects (including bullets and summons).
/// These are modified in the process of object firing in <see cref="Danmaku.Patterns.SyncPattern"/>,
///  and will generally eventually be frozen into a <see cref="ParametricInfo"/>.
/// </summary>
public class GenCtx : IDisposable {
    public static readonly GenCtx Empty = new();
    public EnvFrame EnvFrame { get; private set; } = EnvFrame.Empty;
    public AutoVars? AutoVars { get; set; } = null!;
    public bool HasGCXVars => AutoVars is AutoVars.GenCtx;
    public AutoVars.GenCtx GCXVars => (AutoVars.GenCtx?)AutoVars ?? throw new Exception("GXR autovars not provided");
    
    public int _i = 0;
    public int _pi = 0;

    /// <summary>
    /// Loop iteration
    /// </summary>
    public int i {
        get => _i;
        set {
            _i = value;
            if (AutoVars is AutoVars.GenCtx g)
                EnvFrame.Value<float>(g.i) = value;
        }
    }
    
    /// <summary>
    /// Parent loop iteration
    /// </summary>
    public int pi {
        get => _pi;
        set {
            _pi = value;
            if (AutoVars is AutoVars.GenCtx g)
                EnvFrame.Value<float>(g.pi) = value;
        }
    }

    /// <summary>
    /// Firing index
    /// </summary>
    public int index = 0;
    /// <summary>
    /// Firing BEH (copied from DelegatedCreator)
    /// </summary>
    public BehaviorEntity exec = null!;
    /// <summary>
    /// Used in deeply nested player fires for keeping track of the parent.
    /// </summary>
    public PlayerController? playerController;
    //Note: this doesn't store any bound variables, just the references like PICustomData.playerController
    private PIData fctx = null!;
    public ref V2RV2 RV2 => ref EnvFrame.Value<V2RV2>(GCXVars.rv2);
    public ref V2RV2 BaseRV2 => ref EnvFrame.Value<V2RV2>(GCXVars.brv2);
    public ref float SummonTime => ref EnvFrame.Value<float>(GCXVars.st);
    public Vector2 Loc => exec.GlobalPosition();
    public uint? idOverride = null;
    /// <summary>
    /// Get a <see cref="ParametricInfo"/> with <see cref="idOverride"/> or a random ID
    ///  to use for <see cref="GCXF{T}"/> functions.
    /// </summary>
    [UsedImplicitly]
    public ParametricInfo AsBPI => new(fctx, Loc, index, idOverride ?? exec.rBPI.id, i);
    private static readonly Stack<GenCtx> cache = new();
    private bool _isInCache = false;

    /*
    private static int allocedCount = 0;
    private static int recachedCount = 0;
    private static int decachedCount = 0;
    private static int itrCounter = 0;
    private int _itr = 0;

    public static string DebugState() =>
        $"GCX cache: {cache.Count}; alloced: {allocedCount}; recached: {recachedCount}; decached: {decachedCount}";*/
    
    private GenCtx() { }

    public uint NextID() => RNG.GetUInt();

    private static GenCtx NewUnscoped(BehaviorEntity exec) {
        //Logs.Log($"Acquiring new GCX {itrCounter}", true, LogLevel.DEBUG1);
        /*if (cache.Count > 0)
            ++decachedCount;
        else
            ++allocedCount;*/
        var newgc = (cache.Count > 0) ? cache.Pop() : new GenCtx();
        newgc._isInCache = false;
        //newgc._itr = itrCounter++;
        newgc.exec = exec;
        newgc.fctx = PIData.NewUnscoped(newgc);
        return newgc;
    }
    public static GenCtx New(BehaviorEntity exec, EnvFrame? ef = null) {
        var newgc = NewUnscoped(exec);
        newgc.EnvFrame = ef ?? EnvFrame.Empty;
        newgc.AutoVars = newgc.EnvFrame.Scope.AutoVars;
        return newgc;
    }

    public void OverrideScope(BehaviorEntity nexec, int ind) {
        exec = nexec;
        index = ind;
    }

    public PIData DeriveFCTX() => PIData.New((EnvFrame.Scope, this));

    public void Dispose() {
        if (this == Empty) return;
        if (_isInCache)
            throw new Exception("GenCtx was disposed twice. Please report this.");
        //Logs.Log($"Disposing GCX {_itr}", true, LogLevel.DEBUG1);
        _isInCache = true;
        fctx.Dispose();
        exec = null!;
        playerController = null;
        idOverride = null;
        EnvFrame.Free();
        EnvFrame = EnvFrame.Empty;
        AutoVars = null!;
        i = 0;
        pi = 0;
        cache.Push(this);
        //++recachedCount;
    }

    public GenCtx Copy((LexicalScope scope, AutoVars autoVars)? newScope) {
        var cp = NewUnscoped(exec);
        cp.index = this.index;
        cp.idOverride = this.idOverride;
        cp.playerController = playerController;
        if (newScope.Try(out var ns)) {
            cp.EnvFrame = EnvFrame.Create(ns.scope, EnvFrame);
            cp.AutoVars = ns.autoVars;
            if (AutoVars is AutoVars.GenCtx) {
                cp.RV2 = RV2;
                cp.BaseRV2 = BaseRV2;
                cp.SummonTime = SummonTime;
            } else if (cp.AutoVars is AutoVars.GenCtx) {
                cp.RV2 = cp.BaseRV2 = V2RV2.Zero;
                cp.SummonTime = 0;
            }
        } else {
            cp.EnvFrame = EnvFrame.Clone();
            cp.AutoVars = AutoVars;
        }
        cp.i = this.i;
        cp.pi = this.pi;
        return cp;
    }

    public void FinishIteration(List<GCRule>? postloop, V2RV2? rv2Increment) {
        UpdateRules(postloop);
        if (rv2Increment is {} incr)
            RV2 += incr;
        ++i;
    }

    private bool TryGetType(ReferenceMember refr, out Type ext) {
        if (EnvFrame.Scope.FindDeclaration(refr.var) is { } decl) {
            ext = decl.FinalizedType!;
            return true;
        }
        ext = null!;
        return false;
    }

    private void UpdateRule(GCRule rule) {
        if (!TryGetType(rule.refr, out var variableType)) variableType = rule.exType.AsType();
        ref T Value<T>() => ref EnvFrame.Value<T>(rule.refr.var);
        if (rule is GCRule<float> rf) {
            float value = rf.Evaluate(this);
            if (rule.refr.var == "_") return;
            if      (variableType == typeof(float)) 
                rule.refr.Resolve(ref Value<float>(), value, rule.op);
            else if (variableType == typeof(Vector2))
                rule.refr.ResolveMembers(ref Value<Vector2>(), value, rule.op);
            else if (variableType == typeof(Vector3))
                rule.refr.ResolveMembers(ref Value<Vector3>(), value, rule.op);
            else if (variableType == typeof(V2RV2))
                rule.refr.ResolveMembers(ref Value<V2RV2>(), value, rule.op);
            else throw new Exception($"Can't assign float to {variableType}");
        } else if (rule is GCRule<Vector2> r2) {
            Vector2 value = r2.Evaluate(this);
            if (rule.refr.var == "_") return;
            if      (variableType == typeof(Vector2)) 
                rule.refr.ResolveMembers(ref Value<Vector2>(), value, rule.op);
            else if (variableType == typeof(Vector3))
                rule.refr.ResolveMembers(ref Value<Vector3>(), value, rule.op);
            else if (variableType == typeof(V2RV2))
                rule.refr.ResolveMembers(ref Value<V2RV2>(), value, rule.op);
            else throw new Exception($"Can't assign V2 to {variableType}");
        } else if (rule is GCRule<Vector3> r3) {
            Vector3 value = r3.Evaluate(this);
            if (rule.refr.var == "_") return;
            if (variableType == typeof(Vector3)) 
                rule.refr.ResolveMembers(ref Value<Vector3>(), value, rule.op);
            else throw new Exception($"Can't assign V2 to {variableType}");
        } else if (rule is GCRule<V2RV2> rrv) {
            V2RV2 value = rrv.Evaluate(this);
            if (rule.refr.var == "_") return;
            if (variableType == typeof(V2RV2))
                rule.refr.ResolveMembers(ref Value<V2RV2>(), value, rule.op);
            else throw new Exception($"Can't assign RV2 to {variableType}");
        }
    }

    public void UpdateRules(List<GCRule>? rules) {
        if (rules == null) return;
        for (int ii = 0; ii < rules.Count; ++ii) UpdateRule(rules[ii]);
    }
}

public enum GCOperator {
    /// <summary>
    /// =
    /// </summary>
    Assign,
    /// <summary>
    /// +=
    /// </summary>
    AddAssign,
    /// <summary>
    /// -=
    /// </summary>
    SubAssign,
    /// <summary>
    /// *=
    /// </summary>
    MulAssign,
    /// <summary>
    /// /=
    /// </summary>
    DivAssign,
    /// <summary>
    /// //=
    /// </summary>
    FDivAssign
}

public abstract class GCRule {
    public readonly ReferenceMember refr;
    public readonly Reflector.ExType exType;
    public readonly GCOperator op;

    protected GCRule(Reflector.ExType ext, ReferenceMember rf, GCOperator op) {
        refr = rf;
        exType = ext;
        this.op = op;
    }
}
public class GCRule<T> : GCRule {
    private readonly GCXF<T> func;

    public GCRule(Reflector.ExType ext, ReferenceMember rf, GCOperator op, GCXF<T> f) : base(ext, rf, op) {
        func = f;
    }

    public T Evaluate(GenCtx gcx) => func(gcx);
}
}