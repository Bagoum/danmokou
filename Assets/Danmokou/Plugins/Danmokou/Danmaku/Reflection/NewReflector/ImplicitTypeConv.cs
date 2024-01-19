using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Danmaku.Patterns;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.SM;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection2 {

/// <summary>
/// The kind of scoped conversion created by an <see cref="IScopedTypeConverter"/>.
/// </summary>
public enum ScopedConversionKind {
    /// <summary>
    /// A conversion method that compiles an expression and thus creates a local scope, and which
    ///  should treat any local declarations (var) as within Ex.Block scope.
    /// </summary>
    ScopedExpression,
    /// <summary>
    /// A conversion method not interfacing with expressions, such as GenCtxProperty[] to GenCtxProperties{X}.
    /// </summary>
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

    public abstract Func<TExArgCtx, TEx> Convert(Func<TExArgCtx, TEx> castee);
    public void MarkUsed() { }
}
/// <summary>
/// Implicit type conversion from type T to type R.
/// </summary>
public class FixedImplicitTypeConv<T, R> : FixedImplicitTypeConv {
    public override TypeDesignation.Dummy MethodType { get; } = 
        TypeDesignation.Dummy.Method(
            TypeDesignation.FromType(typeof(R)),
            TypeDesignation.FromType(typeof(T)));
    
    private readonly Either<Expression<Func<T, R>>, Func<Func<TExArgCtx, TEx>, Func<TExArgCtx, TEx<R>>>> converter;

    protected FixedImplicitTypeConv(Expression<Func<T, R>> converter) {
        this.converter = converter;
    }
    protected FixedImplicitTypeConv(Func<Func<TExArgCtx, TEx>, Func<TExArgCtx, TEx<R>>> converter) {
        this.converter = converter;
    }

    public static FixedImplicitTypeConv<T,R> FromFn(Expression<Func<T, R>> converter) =>
        new(converter);
    
    
    public static FixedImplicitTypeConv<T,R> FromEx(Func<Func<TExArgCtx, TEx>, Func<TExArgCtx, TEx<R>>> converter) =>
        new(converter);

    public override Func<TExArgCtx, TEx> Convert(Func<TExArgCtx, TEx> castee) {
        if (converter.TryL(out var cl))
            return tac => (TEx<R>)
                new ReplaceParameterVisitor(cl.Parameters[0], castee(tac)).Visit(cl.Body);
        else
            return converter.Right(castee);
    }
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

    public abstract Func<TExArgCtx, TEx<T>> Convert<T>(Func<TExArgCtx, TEx> castee);

    public virtual Func<TExArgCtx, TEx> ConvertForType(Type t, Func<TExArgCtx, TEx> castee) {
        var conv = converters.TryGetValue(t, out var c) ? c : converters[t] = mi.MakeGenericMethod(t);
        return (Func<TExArgCtx, TEx>)conv.Invoke(this, new object[] { castee });
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

/// <summary>
/// Implicit converter that converts a singleton into an array.
/// </summary>
public record SingletonToArrayConv() : GenericTypeConv1(SharedType) {
    private static TypeDesignation.Dummy MakeSharedTypeSingleton() {
        var v = new TypeDesignation.Variable();
        return TypeDesignation.Dummy.Method(v.MakeArrayType(), v);
    }
    private static readonly TypeDesignation.Dummy SharedType = MakeSharedTypeSingleton();

    public override Func<TExArgCtx, TEx<T>> Convert<T>(Func<TExArgCtx, TEx> castee) => tac =>
        Ex.NewArrayInit(typeof(T), castee(tac));
}

public interface IMethodConv1 : IScopedTypeConverter {
    bool inputTex { get; }
    bool outputTex { get; }
    
    public static TypeDesignation.Dummy UnwrapMethodConv(MethodSignature mi, out bool inputIsTEx, out bool outputIsTEx) {
        inputIsTEx = mi.Params[0].Type.IsTExFuncType(out var inp);
        outputIsTEx = mi.ReturnType.IsTExFuncType(out var outp);
        if (!inputIsTEx && outputIsTEx)
            throw new Exception(
                "Cannot register an implicit converter method that transforms a non-expression into an expression");
        return TypeDesignation.FromMethod(outp, new[] { inp }, mi.GenericTypeMap);
    }

    public Func<TExArgCtx, TEx> ConvertForType(Reflector.IMethodSignature meth, Type r, Func<TExArgCtx, TEx> castee) {
        //obj => obj
        if (!inputTex)
            return tac => meth.InvokeExIfNotConstant(null, castee(tac));
        var outputRaw = meth.Invoke(null, new object[]{castee});
        //tex => tex
        if (outputTex)
            return (Func<TExArgCtx, TEx>?)outputRaw ??
                   throw new StaticException($"Method converter {meth.Name} did not return a TEx func");
        else
            //tex => obj (eg. function compiler)
            return r.MakeTypedLambda(tac => Ex.Constant(outputRaw));
    }
}

/// <summary>
/// Implicit converter that uses a method to convert an input into an output.
/// </summary>
public class MethodConv1 : FixedImplicitTypeConv, IMethodConv1 {
    public override TypeDesignation.Dummy MethodType { get; }
    private Type outputTypeUnwrapped;
    public MethodSignature Mi { get; }
    public bool inputTex { get; }
    public bool outputTex { get; }
    public MethodConv1(MethodSignature Mi) {
        this.Mi = Mi;
        this.Kind = Mi.ImplicitTypeConvKind;
        var m = MethodType = IMethodConv1.UnwrapMethodConv(Mi, out bool inputIsTEx, out bool outputIsTEx);
        outputTypeUnwrapped = m.Last.Resolve().LeftOrThrow;
        this.inputTex = inputIsTEx;
        this.outputTex = outputIsTEx;
    }

    public override Func<TExArgCtx, TEx> Convert(Func<TExArgCtx, TEx> castee) =>
        (this as IMethodConv1).ConvertForType(Mi, outputTypeUnwrapped, castee);
}

/// <summary>
/// Implicit converter that uses a generic method to convert an input into an output.
/// </summary>
public record GenericMethodConv1 : GenericTypeConv1, IMethodConv1 {
    public GenericMethodSignature GMi { get; }
    public bool inputTex { get; }
    public bool outputTex { get; }
    public GenericMethodConv1(GenericMethodSignature GMi) : base(IMethodConv1.UnwrapMethodConv(GMi, out bool inputIsTEx, out bool outputIsTEx)) {
        this.GMi = GMi;
        this.Kind = GMi.ImplicitTypeConvKind;
        this.inputTex = inputIsTEx;
        this.outputTex = outputIsTEx;
    }
    public override Func<TExArgCtx, TEx<T>> Convert<T>(Func<TExArgCtx, TEx> castee) => throw new NotImplementedException();

    public override Func<TExArgCtx, TEx> ConvertForType(Type t, Func<TExArgCtx, TEx> castee) =>
        (this as IMethodConv1).ConvertForType(GMi.Specialize(t), t, castee);
}


// TODO envframe when removing constanttoexprconv, this should continue, but not use ex.constant
public class AttachEFSMConv : FixedImplicitTypeConv<StateMachine, StateMachine> {
    public AttachEFSMConv() : base(sm => tac => EnvFrameAttacher.attachSM.Of(sm(tac), tac.EnvFrame)) { }
}

public class AttachEFAPConv : FixedImplicitTypeConv<AsyncPattern, Func<TExArgCtx, TEx<AsyncPattern>>> {
    public AttachEFAPConv() : base(ap => tac => EnvFrameAttacher.attachAP.Of(ap(tac), tac.EnvFrame)) { }
}
public class AttachEFSPConv : FixedImplicitTypeConv<SyncPattern, Func<TExArgCtx, TEx<SyncPattern>>> {
    public AttachEFSPConv() : base(sp => tac => EnvFrameAttacher.attachSP.Of(sp(tac), tac.EnvFrame)) { }
}

}