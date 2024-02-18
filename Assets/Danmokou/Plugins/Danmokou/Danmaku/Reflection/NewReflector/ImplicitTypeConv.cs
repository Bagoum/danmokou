using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
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
    BlockScopedExpression,
    
    /// <summary>
    /// A conversion method that compiles an expression and thus creates a local scope, and which
    ///  should use environment frames to instantiate scopes.
    /// <br/>Because this incurs high garbage/computational overhead in repeated invocations of compiled expressions,
    ///  this is only used for GCXF of StateMachine, AsyncPattern, and SyncPattern, where it is necessary.
    /// </summary>
    EFScopedExpression,
    
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

public interface ITypeConvWithInstance : IImplicitTypeConverter {
    TypeDesignation.Dummy SharedMethodType { get; }
    void UpdateNextInstance(IImplicitTypeConverterInstance next);
    public class Instance : IImplicitTypeConverterInstance {
        public ITypeConvWithInstance Converter { get; }
        public TypeDesignation.Dummy MethodType { get; }
        /// <inheritdoc/>
        public TypeDesignation.Variable[] Generic { get; }
        IImplicitTypeConverter IImplicitTypeConverterInstance.Converter => Converter;

        public Instance(ITypeConvWithInstance conv) {
            Converter = conv;
            MethodType = conv.SharedMethodType.RecreateVariablesD();
            Generic = MethodType.GetVariables().Distinct().ToArray();
        }

        public void MarkUsed() {
            if (Generic.Length > 0)
                Converter.UpdateNextInstance(new Instance(Converter));
        }

        public IRealizedImplicitCast Realize(Unifier u) => new RealizedImplicitCast(this, u);
    }
}

/// <summary>
/// Implicit type conversion for non-generic types.
/// </summary>
/// Note: even if the type is non-generic, usage of "implicitly generic" tex types, such as
///  Func&lt;TExArgCtx, TEx&gt;, can result in the method type having variables.
public abstract class FixedImplicitTypeConv : IScopedTypeConverter, ITypeConvWithInstance {
    /// <inheritdoc/>
    public abstract TypeDesignation.Dummy MethodType { get; }
    TypeDesignation.Dummy ITypeConvWithInstance.SharedMethodType => MethodType;
    public IDelegateArg[]? ScopeArgs { get; init; }
    public ScopedConversionKind Kind { get; init; } = ScopedConversionKind.Trivial;
    public IImplicitTypeConverter Converter => this;
    //todo: this must be constructed in inheriting type constructors so they have their instances ready
    public IImplicitTypeConverterInstance NextInstance { get; protected set; } = null!;
    public void UpdateNextInstance(IImplicitTypeConverterInstance next) => NextInstance = next;

    public abstract TEx Convert(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac);
}
/// <summary>
/// Implicit type conversion from type T to type R.
/// </summary>
public class FixedImplicitTypeConv<T, R> : FixedImplicitTypeConv {
    public override TypeDesignation.Dummy MethodType { get; } = 
        TypeDesignation.Dummy.Method(
            TypeDesignation.FromType(typeof(R)),
            TypeDesignation.FromType(typeof(T)));

    private record ConvMethod {
        public record DirectFunc(Expression<Func<T, R>> Conv) : ConvMethod {
            public Func<T, R> ConstConv { get; } = Conv.Compile();
        }

        public record TacGeneratedFunc(Func<Func<TExArgCtx, TEx>, Func<TExArgCtx, TEx<R>>> Conv) : ConvMethod;
    }
    private readonly ConvMethod convMethod;

    protected FixedImplicitTypeConv(Expression<Func<T, R>> converter) {
        this.convMethod = new ConvMethod.DirectFunc(converter);
        NextInstance = new ITypeConvWithInstance.Instance(this);
    }
    public FixedImplicitTypeConv(Func<Func<TExArgCtx, TEx>, Func<TExArgCtx, TEx<R>>> converter) {
        this.convMethod = new ConvMethod.TacGeneratedFunc(converter);
        NextInstance = new ITypeConvWithInstance.Instance(this);
    }

    public static FixedImplicitTypeConv<T,R> FromFn(Expression<Func<T, R>> converter, 
        ScopedConversionKind kind = ScopedConversionKind.Trivial) =>
        new(converter) { Kind = kind };
    public override TEx Convert(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) {
        if (convMethod is ConvMethod.DirectFunc dc) {
            var content = castee(tac);
            if ((Ex)content is ConstantExpression { Value: T obj })
                return (TEx<R>)Ex.Constant(dc.ConstConv(obj), typeof(R));
            else
                return (TEx<R>)new ReplaceParameterVisitor(dc.Conv.Parameters[0], castee(tac)).Visit(dc.Conv.Body);
        } else if (convMethod is ConvMethod.TacGeneratedFunc tgf) {
            return tgf.Conv(castee)(tac);
        } else
            throw new ArgumentOutOfRangeException(convMethod.GetType().RName());
    }
}

public abstract record GenericTypeConv1 : IScopedTypeConverter, ITypeConvWithInstance {
    public TypeDesignation.Dummy SharedMethodType { get; }
    public IDelegateArg[]? ScopeArgs { get; init; }
    public ScopedConversionKind Kind { get; init; } = ScopedConversionKind.Trivial;
    private static readonly Dictionary<Type, MethodInfo> converters = new();
    private static readonly MethodInfo mi = typeof(GenericTypeConv1).GetMethod(nameof(Convert))!;
    public IImplicitTypeConverterInstance NextInstance { get; private set; }
    public void UpdateNextInstance(IImplicitTypeConverterInstance next) => NextInstance = next;
    protected GenericTypeConv1(TypeDesignation.Dummy SharedMethodType) {
        this.SharedMethodType = SharedMethodType;
        NextInstance = new ITypeConvWithInstance.Instance(this);
    }

    public abstract TEx<T> Convert<T>(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac);

    public virtual TEx ConvertForType(Type t, IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) {
        var conv = converters.TryGetValue(t, out var c) ? c : converters[t] = mi.MakeGenericMethod(t);
        return (TEx)conv.Invoke(this, new object[] { ast, castee, tac });
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

    public override TEx<T> Convert<T>(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) {
        var content = castee(tac);
        if ((Ex)content is ConstantExpression { Value: T obj })
            return Ex.Constant(new[] { obj });
        else
            return Ex.NewArrayInit(typeof(T), castee(tac));
    }
}

/// <summary>
/// Implicit converter that uses a method to convert an input into an output.
/// </summary>
public class MethodConv1 : FixedImplicitTypeConv {
    public override TypeDesignation.Dummy MethodType => Mi.SharedType;
    public MethodSignature Mi { get; }
    public MethodConv1(MethodSignature Mi) {
        this.Mi = Mi;
        this.Kind = Mi.ImplicitTypeConvKind;
        NextInstance = new ITypeConvWithInstance.Instance(this);
    }

    public override TEx Convert(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) =>
        AST.MethodCall.RealizeMethod(ast, Mi, tac, (_, tac) => castee(tac), true);
}

/// <summary>
/// Implicit converter that uses a generic method to convert an input into an output.
/// </summary>
public record GenericMethodConv1 : GenericTypeConv1 {
    public GenericMethodSignature GMi { get; }
    public GenericMethodConv1(GenericMethodSignature GMi) : base(GMi.SharedType) {
        this.GMi = GMi;
        this.Kind = GMi.ImplicitTypeConvKind;
    }
    public override TEx<T> Convert<T>(IAST ast, Func<TExArgCtx, TEx> castee, TExArgCtx tac) => throw new NotImplementedException();

    public override TEx ConvertForType(Type t, IAST ast,  Func<TExArgCtx, TEx> castee, TExArgCtx tac) =>
        AST.MethodCall.RealizeMethod(ast, GMi.Specialize(t), tac, (_, tac) => castee(tac), true);
}



public class AttachEFSMConv : FixedImplicitTypeConv<StateMachine, StateMachine> {
    public AttachEFSMConv() : base(sm => tac => EnvFrameAttacher.attachSM.Of(sm(tac), tac.EnvFrame)) { }
}

public class AttachEFAPConv : FixedImplicitTypeConv<AsyncPattern, AsyncPattern> {
    public AttachEFAPConv() : base(ap => tac => EnvFrameAttacher.attachAP.Of(ap(tac), tac.EnvFrame)) { }
}
public class AttachEFSPConv : FixedImplicitTypeConv<SyncPattern, SyncPattern> {
    public AttachEFSPConv() : base(sp => tac => EnvFrameAttacher.attachSP.Of(sp(tac), tac.EnvFrame)) { }
}

}