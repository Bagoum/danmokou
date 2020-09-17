
using System;
using System.Collections.Generic;
using JetBrains.Annotations;

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

public class ReflWrap<T> : ReflWrap where T : class{
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
}