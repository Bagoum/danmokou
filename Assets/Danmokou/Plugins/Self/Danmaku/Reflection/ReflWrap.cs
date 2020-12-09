
using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace DMK.Reflection {
public abstract class ReflWrap {
    private static readonly List<ReflWrap> wrappers = new List<ReflWrap>();

    protected ReflWrap() {
        wrappers.Add(this);
    }

    public static void ClearWrappers() {
        foreach (var x in wrappers) x.Reset();
    }

    public abstract void Reset();
}

public class ReflWrap<T> : ReflWrap where T : class {
    private readonly Func<T> constructor;
    [CanBeNull] private T value;
    public T Value => value = value ?? constructor();

    public ReflWrap(Func<T> constructor) {
        this.constructor = constructor;

    }

    public override void Reset() {
        value = null;
    }

    public static implicit operator T(ReflWrap<T> wrap) => wrap.Value;
    public static implicit operator ReflWrap<T>(Func<T> constructor) => new ReflWrap<T>(constructor);

    private static readonly Dictionary<string, ReflWrap<T>> autoWrapped = new Dictionary<string, ReflWrap<T>>();

    /// <summary>
    /// Use for small objects that may be repeatedly recreated, such as FireOption offsets/etc.
    /// TODO: consider enclosing StateMachineManager under this.
    /// </summary>
    public static T Wrap(string s) {
        if (!autoWrapped.TryGetValue(s, out var rw)) {
            autoWrapped[s] = rw = new ReflWrap<T>(s.Into<T>);
        }
        return rw.Value;
    }

    public static void Load([CanBeNull] string s) {
        if (string.IsNullOrWhiteSpace(s)) return;
        Wrap(s);
    }
}
}