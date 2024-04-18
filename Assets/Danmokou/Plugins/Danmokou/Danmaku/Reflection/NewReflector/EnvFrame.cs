using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.Scenes;
using JetBrains.Annotations;
using UnityEngine.Profiling;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection2 {
/// <summary>
/// An environment frame, storing the local variables for an instantiated lexical scope,
/// in script code execution.
/// </summary>
public class EnvFrame {
    public static int Created = 0;
    public static int Cloned = 0;
    public static int Disposed = 0;
    private static readonly Stack<EnvFrame> cache = new();
    public static readonly EnvFrame Empty = new();
    private static uint counter;
    public uint Ctr { get; } = counter++;
    
    private int dependents = 0;
    private int owners = 0;
    /// <summary>
    /// The environment frame that contains this one.
    /// </summary>
    public EnvFrame? Parent { get; private set; }
    private void TakeParent(EnvFrame? parent) {
        Parent?.FreeDependent();
        if ((Parent = parent) != null)
            ++Parent.dependents;
    }

    /// <summary>
    /// The lexical scope for which this envframe is instantiated.
    /// </summary>
    public LexicalScope Scope { get; private set; } = DMKScope.Singleton;
    public FrameVars[] Variables = null!;

    public EnvFrame this[int parentage] {
        get {
            var ef = this;
            for (int ii = 0; ii < parentage; ++ii) {
                ef = ef!.Parent;
            }
            return ef!;
        }
    }

    /// <summary>
    /// Creates a new environment frame in the provided scope, as a child of the parent frame.
    /// <br/>If the provided scope disallows instantiating environment frames (<see cref="LexicalScope.UseEF"/> false),
    ///  or the provided scope is the same as the parent scope,
    ///  then transparently clones the parent frame instead.
    /// </summary>
    public static EnvFrame Create(LexicalScope scope, EnvFrame? parent) {
        //todo: handle crushed scopes
        if (scope is DynamicLexicalScope dyn) {
            scope = dyn.RealizeScope(parent?.Scope ??
                                     throw new Exception(
                                         "Dynamic lexical scopes must be instantiated with a parent envFrame"));
        }
        if (scope == parent?.Scope)
            return parent.Mirror();
        var np = scope.Parent;
        for (; np is { UseEF: false }; np = np.Parent) { }
        if ((parent?.Scope ?? DMKScope.Singleton) != np) {
            //Allow const scopes to have null parentage
            if (!(scope.IsConstScope && parent is null))
                throw new Exception("Incorrect envframe instantiation: parent scope is not the same as scope parent");
        }
        if (!scope.UseEF) 
            return (parent ?? throw new StaticException("Parent EF must be provided for non-EF scopes")).Mirror();
        var ef = cache.Count > 0 ? cache.Pop() : new();
        ef.TakeParent(parent);
        //Logs.Log($"CREATE {ef.Ctr} ({ef.Parent?.Ctr})", stackTrace: true);
        ++Created;
        ef.Scope = scope;
        ef.owners = 1;
        ef.Variables = EFArrayPool<FrameVars>.Rent(scope.VariableDecls.Length);
        for (int ii = 0; ii < scope.VariableDecls.Length; ++ii) {
            var (typ, decls) = scope.VariableDecls[ii];
            var v = FrameVars.Create(typ);
            v.AssertLength(decls.Length);
            ef.Variables[ii] = v;
        }
        return ef;
    }
    
    public static readonly ExFunction exCreate = ExFunction.WrapAny(typeof(EnvFrame), nameof(Create));
    
    /// <summary>
    /// Get a value stored in this envframe or a parent envframe.
    /// </summary>
    public Maybe<T> MaybeGetValue<T>(string varName) {
        for (var envFrame = this; envFrame != null; envFrame = envFrame.Parent) {
            if (envFrame.Scope.variableDecls.TryGetValue(varName, out var decl)) {
                if (decl.FinalizedType != typeof(T))
                    throw new Exception(
                        $"Types do not align for variable {varName}. Requested: {typeof(T).SimpRName()}; found: {decl.FinalizedType?.SimpRName()}");
                return ((FrameVars<T>)envFrame.Variables[decl.TypeIndex]).Values[decl.Index];
            }
        }
        return Maybe<T>.None;
    }
    
    /// <summary>
    /// Get a reference to a value stored in this envframe or a parent envframe.
    /// </summary>
    public ref T Value<T>(string varName) {
        for (var envFrame = this; envFrame != null; envFrame = envFrame.Parent) {
            if (envFrame.Scope.variableDecls.TryGetValue(varName, out var decl)) {
                if (decl.FinalizedType != typeof(T))
                    throw new Exception(
                        $"Types do not align for variable {varName}. Requested: {typeof(T).SimpRName()}; found: {decl.FinalizedType?.SimpRName()}");
                return ref ((FrameVars<T>)envFrame.Variables[decl.TypeIndex]).Values[decl.Index];
            }
        }
        throw new Exception($"Variable {varName} not found in environment frame");
    }
    
    /// <summary>
    /// Get a reference to a value stored in this envframe or a parent envframe.
    /// </summary>
    public ref T Value<T>(VarDecl decl) {
        if (decl == null) throw new Exception($"Declaration not provided to {nameof(Value)}");
        for (var envFrame = this; envFrame != null; envFrame = envFrame.Parent) {
            if (decl.DeclarationScope == envFrame.Scope || decl.DeclarationScope == envFrame.Scope.DynRealizeSource)
                return ref ((FrameVars<T>)envFrame.Variables[decl.TypeIndex]).Values[decl.Index];
        }
        throw new Exception($"Variable {decl.Name}<{decl.FinalizedType!.SimpRName()}> not found in environment frame");
    }

    /// <summary>
    /// Get a value stored in this envframe or a parent envframe.
    /// </summary>
    public T NonRefValue<T>(VarDecl decl) => Value<T>(decl);

    public static Ex FrameVarValues(LexicalScope scope, Ex envFrame, int parentage, Type typ) {
        while (true) {
            if (scope.UseEF) {
                if (parentage-- == 0) break;
                envFrame = envFrame.Field(nameof(Parent));
            }
            scope = scope.Parent!;
        }
        return FrameVarValues(envFrame, Ex.Constant(scope.TypeIndexMap[typ]), typ);
    }

    public static Ex FrameVarValues(Ex envFrame, Ex typeIdx, Type typ) =>
        //(ef.Variables[var.FinalizedType] as VariableStore<var.FinalizedType>).Values
        envFrame
            .Field(nameof(Variables)).Index(typeIdx)
            .As(FrameVars.GetVarStoreType(typ))
            .Field(nameof(FrameVars<float>.Values));
    
    public static Ex Value(Ex envFrame, Ex typeIdx, Ex valueIdx, Type typ) => 
        FrameVarValues(envFrame, typeIdx, typ).Index(valueIdx);

    public void Free() {
        if (this == Empty) return;
        //Logs.Log($"free {Ctr} ({owners - 1} rem)", stackTrace: true);
        if (--owners == 0 && dependents == 0)
            Dispose();
    }
    
    public static readonly ExFunction exFree = ExFunction.WrapAny(typeof(EnvFrame), nameof(Free));


    private void FreeDependent() {
        if (this == Empty) return;
        if (--dependents == 0 && owners == 0) {
            Dispose();
        }
    }

    private void Dispose() {
        if (this == Empty) return;
        //Logs.Log($"DISPOSED {Ctr} ({Parent?.Ctr})", stackTrace: true);
        ++Disposed;
        for (int ii = 0; ii < Scope.VariableDecls.Length; ++ii)
            Variables[ii].Cache();
        EFArrayPool<FrameVars>.Return(Variables);
        Variables = null!;
        cache.Push(this);
        TakeParent(null); //calls FreeDependent
    }

    /// <summary>
    /// Return this envframe, but mark an additional owner such that the envframe will only be disposed
    ///  when both owners are finished using it.
    /// </summary>
    public EnvFrame Mirror() {
        if (this == Empty) return Empty;
        ++owners;
        //Logs.Log($"dup {Ctr} ({owners} rem)", stackTrace: true);
        return this;
    }
    
    /// <summary>
    /// Copy all the values inside this envframe into a new envframe. The parent frame is shared, but the
    ///  local values are not.
    /// </summary>
    public EnvFrame Clone() {
        if (this == Empty) return Empty;
        var nxt = cache.Count > 0 ? cache.Pop() : new();
        nxt.TakeParent(Parent);
        nxt.Scope = Scope;
        nxt.owners = 1;
        //Logs.Log($"CLONE {nxt.Ctr} <- {Ctr} ({Parent?.Ctr})", stackTrace: true);
        ++Cloned;
        nxt.Variables = EFArrayPool<FrameVars>.Rent(Scope.VariableDecls.Length);
        for (int ii = 0; ii < Scope.VariableDecls.Length; ++ii)
            nxt.Variables[ii] = Variables[ii].Clone();
        return nxt;
    }
}

public abstract class FrameVars {
    private static readonly Dictionary<Type, IVariableStoreCreator> creators = new();
    //maps T to VariableStore<T>
    private static readonly Dictionary<Type, Type> varStoreTypes = new();
    public abstract Type Type { get; }

    public abstract void AssertLength(int numVars);
    public abstract void Cache();
    public abstract FrameVars Clone();

    public static Type GetVarStoreType(Type t) => varStoreTypes.TryGetValue(t, out var vst) ?
        vst :
        varStoreTypes[t] = typeof(FrameVars<>).MakeGenericType(t);
    public static FrameVars Create(Type t) {
        if (!creators.TryGetValue(t, out var c))
            creators[t] = c = Activator.CreateInstance(typeof(VariableStoreCreator<>).MakeGenericType(t)) 
                                  as IVariableStoreCreator ?? 
                              throw new Exception($"Failed to generate VariableStoreCreator for type {t.SimpRName()}");
        return c.Create();
    }
    
}

public class FrameVars<T> : FrameVars {
    private static readonly Stack<FrameVars<T>> cache = new();
    
    static FrameVars() => SceneIntermediary.SceneUnloaded.Subscribe(_ => cache.Clear());
    
    public override Type Type { get; } = typeof(T);
    public static FrameVars<T> Create() => cache.Count > 0 ? cache.Pop() : new();

    private int len;
    public T[] Values = null!;


    public override void AssertLength(int numVars) {
        Values = EFArrayPool<T>.Rent(len = numVars, clear: true);
    }

    public override void Cache() {
        EFArrayPool<T>.Return(Values);
        Values = null!;
        cache.Push(this);
    }

    public override FrameVars Clone() {
        var nxt = Create();
        nxt.Values = EFArrayPool<T>.Rent(nxt.len = len);
        Array.Copy(Values, nxt.Values, len);
        return nxt;
    }
}

//using this instead of referencing the VariableStore<T>.Create method makes EF instantiation faster
public interface IVariableStoreCreator {
    FrameVars Create();
}

public class VariableStoreCreator<T> : IVariableStoreCreator {
    FrameVars IVariableStoreCreator.Create() => FrameVars<T>.Create();
}

//Implementation of ArrayPool of small lengths that isn't thread-safe but has zero amortized allocation
public static class EFArrayPool<T> {
    //We have buckets as follows:
    //One bucket for each length up to 8.
    //From there, one bucket for each exponent [8+2^n, 8+2*2^n).
    private static readonly List<Queue<T[]>> buckets = new();

    static EFArrayPool() => SceneIntermediary.SceneUnloaded.Subscribe(_ => {
        foreach (var q in buckets)
            q.Clear();
        buckets.Clear();
    });

    private static void AssertBucket(int bucket) {
        while (buckets.Count <= bucket)
            buckets.Add(new());
    }

    static int BucketForGetLength(in int len) {
        if (len <= 8)
            return len - 1;
        var idx = 8;
        for (int diff = len - 8; diff > 1; diff /= 2)
            ++idx;
        return idx;
    }
    static int BucketForSetLength(in int len) {
        if (len <= 8)
            return len - 1;
        var idx = 8;
        for (int diff = len - 8; diff > 1; diff /= 2)
            ++idx;
        var exp = 1;
        for (int ii = 8; ii < idx; ++ii)
            exp *= 2;
        return (len == 8 + exp) ? idx: idx + 1;
    }

    public static void Return(T[] array) {
        if (array.Length == 0) return;
        var bucket = BucketForSetLength(array.Length);
        AssertBucket(bucket);
        buckets[bucket].Enqueue(array);
    }

    public static T[] Rent(int len, bool clear = false) {
        if (len == 0) return Array.Empty<T>();
        var bucket = BucketForGetLength(len);
        for (; bucket < buckets.Count; ++bucket)
            if (buckets[bucket].TryDequeue(out var arr)) {
                if (clear)
                    Array.Clear(arr, 0, len);
                return arr;
            }
        return new T[len];
    }

    public static void ClearAll() {
        buckets.Clear();
    }
    

}

}