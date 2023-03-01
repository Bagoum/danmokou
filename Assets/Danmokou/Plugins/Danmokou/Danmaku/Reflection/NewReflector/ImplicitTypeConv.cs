using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection2 {

/// <summary>
/// The kind of scoped conversion created by an <see cref="IScopedTypeConverter"/>.
/// </summary>
public enum ScopedConversionKind {
    /// <summary>
    /// A conversion method that converts eg. ExVTP to GCXU{VTP}.
    /// </summary>
    GCXUFunction,
    /// <summary>
    /// A conversion method that converts eg. ExTP to GCXF{Vector2}.
    /// </summary>
    GCXFFunction,
    /// <summary>
    /// A conversion method that converts ag. ExTP to TP.
    /// </summary>
    SimpleFunction,
    
    Trivial
}

/// <summary>
/// An implicit type converter that may introduce a new scope, such as for an expression compiler.
/// </summary>
public interface IScopedTypeConverter : IImplicitTypeConverter {
    /// <summary>
    /// Arguments implicit to the scope. If this is null, then a new scope will not be created.
    /// </summary>
    IDelegateArg[]? ScopeArgs { get; }
    
    /// <inheritdoc cref="ScopedConversionKind"/>
    ScopedConversionKind Kind { get; }
}

/// <summary>
/// Implicit type conversion for non-generic types.
/// </summary>
public abstract class FixedImplicitTypeConv : IScopedTypeConverter, IImplicitTypeConverterInstance {
    /// <inheritdoc/>
    public abstract TypeDesignation.Dummy MethodType { get; }
    //since we are not using generics, we don't need to handle instances
    public TypeDesignation.Variable[] Generic { get; } = Array.Empty<TypeDesignation.Variable>();
    public IDelegateArg[]? ScopeArgs { get; init; }
    public ScopedConversionKind Kind { get; init; } = ScopedConversionKind.Trivial;
    public IImplicitTypeConverter Converter => this;
    public IImplicitTypeConverterInstance NextInstance => this;

    public IRealizedImplicitCast Realize(Unifier u) => new RealizedImplicitCast(this, u);


    public static FixedImplicitTypeConv<T, R> From<T, R>(Func<T, R> converter) => new(converter);

    public abstract object? Convert(object? castee);
    public void MarkUsed() { }
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

public abstract record GenericTypeConv1 : IScopedTypeConverter {
    public IImplicitTypeConverterInstance NextInstance { get; private set; }
    private TypeDesignation.Dummy SharedMethodType { get; }
    public IDelegateArg[]? ScopeArgs { get; init; }
    public ScopedConversionKind Kind { get; init; } = ScopedConversionKind.Trivial;
    private static readonly Dictionary<Type, MethodInfo> converters = new();
    private static readonly MethodInfo mi = typeof(GenericTypeConv1).GetMethod(nameof(Convert))!;
    protected GenericTypeConv1(TypeDesignation.Dummy SharedMethodType) {
        this.SharedMethodType = SharedMethodType;
        this.NextInstance = new Instance(this);
    }

    public abstract object? Convert<T>(object? castee);

    public virtual object? ConvertForType(Type t, object? castee) {
        var conv = converters.TryGetValue(t, out var c) ? c : converters[t] = mi.MakeGenericMethod(t);
        return conv.Invoke(this, new[] { castee });
    }

    private class Instance : IImplicitTypeConverterInstance {
        public GenericTypeConv1 Converter { get; }
        public TypeDesignation.Dummy MethodType { get; }
        /// <inheritdoc/>
        public TypeDesignation.Variable[] Generic { get; }
        IImplicitTypeConverter IImplicitTypeConverterInstance.Converter => Converter;

        public Instance(GenericTypeConv1 conv) {
            Converter = conv;
            MethodType = conv.SharedMethodType.RecreateVariablesD();
            Generic = MethodType.GetVariables().Distinct().ToArray();
        }

        public void MarkUsed() => Converter.NextInstance = new Instance(Converter);

        public IRealizedImplicitCast Realize(Unifier u) => new RealizedImplicitCast(this, u);
    }
}

public record GenericMethodConv1(Reflector.IGenericMethodSignature GMi) : GenericTypeConv1(GMi.SharedType) {
    public override object? Convert<T>(object? castee) => throw new NotImplementedException();

    public override object? ConvertForType(Type t, object? castee) =>
        //gmi.specialize is cached
        GMi.Specialize(t).Invoke(null, null, castee);
}

public record ConstantToExprConv() : GenericTypeConv1(new TypeDesignation.Variable().And(v =>
    TypeDesignation.Dummy.Method(v.MakeTExFunc(), v))) {
    public override object? Convert<T>(object? castee) =>
        (Func<TExArgCtx, TEx<T>>)(_ => Ex.Constant(castee, typeof(T)));
}
}