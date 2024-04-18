
using System;
using System.Collections.Generic;
using BagoumLib.Reflection;
using Danmokou.Expressions;
using Danmokou.Scenes;
using JetBrains.Annotations;

namespace Danmokou.Reflection {
public abstract class ReflWrap {
    private static readonly List<ReflWrap> wrappers = new();

    static ReflWrap() => SceneIntermediary.SceneUnloaded.Subscribe(_ => ClearWrappers());
    
    /// <summary>
    /// True iff the value should not be reset on scene change.
    /// </summary>
    public bool Persistent { get; init; } = false;

    protected ReflWrap() {
        wrappers.Add(this);
    }

    public static void ClearWrappers() {
        foreach (var x in wrappers) 
            if (!x.Persistent)
                x.Reset();
    }

    public static void InvokeAllWrappers() {
        foreach (var x in wrappers) x.Invoke();
    }

    public abstract void Reset();
    public abstract void Invoke();

    public static ReflWrap<T> FromFunc<T>(string uniqueKey, Func<T> constructor) where T : class =>
        ReflWrap<T>.FromFunc(uniqueKey, constructor);
    
    public static ReflWrap<T> FromString<T>(string text, Func<string, T> constructor) where T : class =>
        ReflWrap<T>.FromFunc(text + typeof(T).SimpRName(), () => constructor(text));

}

public class ReflWrap<T> : ReflWrap where T : class {
    private readonly Func<T> constructor;
    private T? value;
    public T Value => value ??= constructor();

    private ReflWrap(Func<T> constructor) : base() {
        this.constructor = constructor;
    }

    public ReflWrap(string intosrc) : this(intosrc.Into<T>) {
        Persistent = true;
    }
    
    public static ReflWrap<T> FromFunc(string uniqueKey, Func<T> constructor) => 
        new(() => {
            using var _ = BakeCodeGenerator.OpenContext(CookingContext.KeyType.MANUAL, uniqueKey);
            return constructor();
        });

    public override void Reset() {
        value = null;
    }

    public override void Invoke() {
        constructor();
    }

    public static implicit operator T(ReflWrap<T> wrap) => wrap.Value;

    private static readonly Dictionary<string, ReflWrap<T>> autoWrapped = new();

    /// <summary>
    /// Use for small objects that may be repeatedly recreated, such as FireOption offsets/etc.
    /// TODO: consider enclosing StateMachineManager under this.
    /// </summary>
    public static T Wrap(string s, bool persistent = true) {
        if (!autoWrapped.TryGetValue(s, out var rw)) {
            autoWrapped[s] = rw = new ReflWrap<T>(s) { Persistent = persistent };
        }
        return rw.Value;
    }

    public static T? MaybeWrap(string? s, bool persistent = true) => 
        string.IsNullOrWhiteSpace(s) ? null : Wrap(s!, persistent);

    public static void Load(string? s, bool persistent = true) {
        MaybeWrap(s, persistent);
    }
}
}