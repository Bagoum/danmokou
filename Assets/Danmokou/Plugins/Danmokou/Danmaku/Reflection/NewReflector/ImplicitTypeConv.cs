using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection2 {

/// <summary>
/// An implicit type converter that may introduce a new scope, such as for an expression compiler.
/// </summary>
public interface IScopedTypeConverter : IImplicitTypeConverter {
    /// <summary>
    /// Arguments implicit to the scope. If this is null, then a new scope will not be created.
    /// </summary>
    public IDelegateArg[]? ScopeArgs { get; }
}

/// <summary>
/// Implicit type conversion for non-generic types.
/// </summary>
public abstract class FixedImplicitTypeConv : IScopedTypeConverter {
    public abstract TypeDesignation.Dummy MethodType { get; }
    public TypeDesignation.Variable[] Generic { get; } = Array.Empty<TypeDesignation.Variable>();
    public IDelegateArg[]? ScopeArgs { get; init; }
    
    public IRealizedImplicitCast Realize(Unifier u) => new RealizedImplicitCast(this, u);

    public static FixedImplicitTypeConv<T, R> From<T, R>(Func<T, R> converter) => new(converter);

    public abstract object? Convert(object? castee);
}
/// <summary>
/// Implicit type conversion from type T to type R.
/// </summary>
public class FixedImplicitTypeConv<T, R> : FixedImplicitTypeConv {
    public override TypeDesignation.Dummy MethodType { get; }
    private readonly Func<T, R> converter;

    public FixedImplicitTypeConv(Func<T, R> converter) {
        this.converter = converter;
        MethodType = TypeDesignation.Dummy.Method(
            TypeDesignation.FromType(typeof(R)),
            TypeDesignation.FromType(typeof(T)));
    }

    public override object? Convert(object? castee) => converter((T)castee);
}

public abstract record GenericTypeConv1(TypeDesignation.Dummy MethodType) : IImplicitTypeConverter {
    public TypeDesignation.Variable[] Generic { get; } = MethodType.GetVariables().Distinct().ToArray();
    public IDelegateArg[]? ScopeArgs { get; init; }
    private static readonly Dictionary<Type, MethodInfo> converters = new();
    private static readonly MethodInfo mi = typeof(GenericTypeConv1).GetMethod(nameof(Convert))!;
    
    public IRealizedImplicitCast Realize(Unifier u) => new RealizedImplicitCast(this, u);

    public abstract object? Convert<T>(object? castee);

    public object? ConvertForType(Type t, object? castee) {
        var conv = converters.TryGetValue(t, out var c) ? c : converters[t] = mi.MakeGenericMethod(t);
        return conv.Invoke(this, new[] { castee });
    }
}

public record ConstantToExprConv() : GenericTypeConv1(new TypeDesignation.Variable().And(v =>
    TypeDesignation.Dummy.Method(v.MakeTExFunc(), v))) {
    public override object? Convert<T>(object? castee) =>
        (Func<TExArgCtx, TEx<T>>)(_ => Ex.Constant(castee, typeof(T)));
}


//todo: for generic type conversions, I'd prefer to actually call the methods like GCXF<T> by specifying them
}