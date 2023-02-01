using System;
using System.Collections.Generic;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using JetBrains.Annotations;
using UnityEngine.Profiling;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection2 {
/// <summary>
/// An environment frame, storing the local variables for an instantiated lexical scope,
/// in script code execution.
/// </summary>
public class EnvFrame {
    private static readonly Stack<EnvFrame> cache = new();
    public static readonly EnvFrame Empty = new();
    /// <summary>
    /// The environment frame that contains this one.
    /// </summary>
    public EnvFrame? Parent { get; private set; }

    /// <summary>
    /// The lexical scope for which this envframe is instantiated.
    /// </summary>
    public LexicalScope Scope { get; private set; } = null!;
    public List<FrameVars> Variables { get; } = new();

    public FrameVars this[int typeIndex] => Variables[typeIndex];

    public static EnvFrame Create(LexicalScope scope, EnvFrame? parent) {
        //todo: handle crushed scopes
        var np = scope.Parent;
        for (; np is { UseEF: false }; np = np.Parent) { }
        if ((parent?.Scope ?? DMKScope.Singleton) != np)
            throw new Exception("Incorrect envframe instantiation: parent scope is not the same as scope parent");
        var ef = cache.Count > 0 ? cache.Pop() : new();
        ef.Parent = parent;
        ef.Scope = scope;
        for (int ii = 0; ii < scope.VariableDecls.Length; ++ii) {
            var (typ, decls) = scope.VariableDecls[ii];
            var v = FrameVars.Create(typ);
            v.AssertLength(decls.Length);
            ef.Variables.Add(v);
        }
        return ef;
    }
    
    public static readonly ExFunction exCreate = ExFunction.WrapAny(typeof(EnvFrame), nameof(Create));

    public void Dispose() {
        if (this == Empty) return;
        for (int ii = 0; ii < Variables.Count; ++ii)
            Variables[ii].Cache();
        Variables.Clear();
        cache.Push(this);
    }
}

public abstract class FrameVars {
    public abstract Type Type { get; }
    private static readonly Dictionary<Type, IVariableStoreCreator> creators = new();
    //maps T to VariableStore<T>
    private static readonly Dictionary<Type, Type> varStoreTypes = new();

    public abstract void AssertLength(int numVars);
    public abstract void Cache();

    public static Type GetVarStoreType(Type t) => varStoreTypes.TryGetValue(t, out var vst) ?
        vst :
        varStoreTypes[t] = typeof(FrameVars<>).MakeGenericType(t);
    public static FrameVars Create(Type t) {
        if (!creators.TryGetValue(t, out var c))
            creators[t] = c = Activator.CreateInstance(typeof(VariableStoreCreator<>).MakeGenericType(t)) 
                                  as IVariableStoreCreator ?? 
                              throw new Exception($"Failed to generate VariableStoreCreator for type {t.RName()}");
        return c.Create();
    }
    
}

public class FrameVars<T> : FrameVars {
    private static readonly Stack<FrameVars<T>> cache = new();
    public override Type Type { get; } = typeof(T);

    [UsedImplicitly]
    public List<T> Values { get; } = new();
    
    [UsedImplicitly]
    public FrameVars<T> Create() => cache.Count > 0 ? cache.Pop() : new();

    public override void AssertLength(int numVars) {
        for (int ii = 0; ii < numVars; ++ii)
            Values.Add(default!);
    }

    public override void Cache() {
        Values.Clear();
        cache.Push(this);
    }
}

//using this instead of referencing the VariableStore<T>.Create method makes EF instantiation faster
public interface IVariableStoreCreator {
    FrameVars Create();
}

public class VariableStoreCreator<T> : IVariableStoreCreator {
    public FrameVars<T> Create() => new();
    FrameVars IVariableStoreCreator.Create() => Create();
}

}