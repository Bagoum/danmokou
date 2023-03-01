using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using JetBrains.Annotations;
using Danmokou.SM;
using Danmokou.SM.Parsing;
using Mizuhashi;
using UnityEngine;
using static BagoumLib.Reflection.ReflectionUtils;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector3>>;
using ExTP4 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector4>>;
using ExBPRV2 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>>;

namespace Danmokou.Reflection {
public static partial class Reflector {
    private static readonly Dictionary<Type, Type> exTypeRemap = new() {
        { typeof(ExTP), typeof(TP) },
        { typeof(ExTP3), typeof(TP3) },
        { typeof(ExTP4), typeof(TP4) },
        { typeof(ExBPY), typeof(BPY) },
        { typeof(ExBPRV2), typeof(BPRV2) },
        { typeof(Func<TExArgCtx, TEx<bool>>), typeof(Pred) },
        { typeof(Func<ITexMovement, TEx<float>, TExArgCtx, TExV3, TEx>), typeof(VTP) },
        { typeof(Func<ITexMovement, TEx<float>, TEx<float>, TExArgCtx, TExV2, TEx>), typeof(LVTP) },
        { typeof(Func<TExSBC, TEx<int>, TEx<BagoumLib.Cancellation.ICancellee>, TExArgCtx, TEx>), typeof(SBCF) }
    };
    private static readonly Type[] BypassTypes = {
        typeof(GCXU<>), typeof(TEx<>), //typeof(EEx<>)
    };
    public static Type RemapExType(Type t) => exTypeRemap.TryGetValue(t, out var v) ? v : t;
    public static string SimpRName(this Type t) => SimplifiedExprPrinter.Default.Print(t);

    public static string SimpRName(this TypeDesignation t) {
        if (t.IsResolved)
            return t.Resolve(Unifier.Empty).LeftOrThrow.SimpRName();
        if (t is TypeDesignation.Known kt) {
            if (kt.Typ == typeof(Func<,>) && (kt.Arguments[0] as TypeDesignation.Known)?.Typ == typeof(TExArgCtx)) {
                return SimpRName(kt.Arguments[1]);
            } else if (BypassTypes.Contains(kt.Typ))
                return SimpRName(kt.Arguments[0]);
            return kt.IsArrayTypeConstructor ?
                SimpRName(kt.Arguments[0]) + "[]" :
                kt.Arguments.Length == 0 ?
                    kt.Typ.RName() :
                    kt.Typ.RName() + $"<{string.Join(",", kt.Arguments.Select(SimpRName))}>";
        } else if (t is TypeDesignation.Dummy d) {
            return $"({string.Join(",", d.Arguments[..^1].Select(SimpRName))})->{SimpRName(d.Last)}";
        } else
            return t.ToString();
    }
    private class SimplifiedExprPrinter : CSharpTypePrinter {
        public new static readonly ITypePrinter Default = new SimplifiedExprPrinter();
        public override string Print(Type t) {
            if (exTypeRemap.TryGetValue(t, out var v))
                return Print(v);
            if (t.IsConstructedGenericType) {
                if (t.GetGenericTypeDefinition() == typeof(Func<,>) && t.GenericTypeArguments[0] == typeof(TExArgCtx)) {
                    return Print(t.GenericTypeArguments[1]);
                }
                if (BypassTypes.Contains(t.GetGenericTypeDefinition()))
                    return Print(t.GenericTypeArguments[0]);
            }
            return base.Print(t);
        }
    }
    /// <summary>
    /// A simplified description of a method parameter.
    /// </summary>
    /// <param name="Type">Parameter type</param>
    /// <param name="Name">Parameter name</param>
    /// <param name="LookupMethod">Whether this parameter has LookupMethodAttribute</param>
    /// <param name="NonExplicit">Whether this parameter has NonExplicitAttribute</param>
    public record NamedParam(Type Type, string Name, bool LookupMethod, bool NonExplicit) {
        public static implicit operator NamedParam(ParameterInfo pi) => 
            new(pi.ParameterType, pi.Name, 
                pi.GetCustomAttribute<LookupMethodAttribute>() != null,
                pi.GetCustomAttribute<NonExplicitParameterAttribute>() != null);

        public string Description => $"{Name}<{CSharpTypePrinter.Default.Print(Type)}>";
        public string AsParameter => $"{Type.SimpRName()} {Name}";
        public string SimplifiedDescription => $"\"{Name}\" (type: {SimplifiedExprPrinter.Default.Print(Type)})";
    }

    //this doesn't implement IMethodDesignation because we don't want it to report TypeDesignation at this level,
    // as that would result in all invocations of the same method sharing the same type variables
    //instead, TypeDesignation is copied at the InvokedMethod level, preventing cross-contamination
    public interface IMethodSignature {
        /// <summary>
        /// Get a representation of this method's type. This should not directly be used for unification, as
        ///  its variable types should not be shared between all invocations.
        ///  Call <see cref="TypeDesignation.RecreateVariables"/> before using for unification.
        /// <br/>Note that lifted methods return a lifted type here.
        /// <br/>Note that instance methods should prepend the instance type at the beginning of the argument array.
        /// </summary>
        TypeDesignation.Dummy SharedType { get; }
        
        /// <summary>
        /// The parameters of the method signature.
        /// </summary>
        NamedParam[] Params { get; }

        /// <summary>
        /// True if this is a fallthrough method (BDSL1 only).
        /// </summary>
        bool IsFallthrough => false;

        /// <summary>
        /// True if this method is a constructor.
        /// </summary>
        bool IsCtor => false;
        
        /// <summary>
        /// True if this is a static method.
        /// </summary>
        bool IsStatic { get; }
        
        /// <summary>
        /// The return type of this method.
        /// <br/>Lifted methods return a lifted return type.
        /// </summary>
        Type ReturnType { get; }
        
        /// <summary>
        /// The type declaring this method.
        /// </summary>
        Type? DeclaringType { get; }
        
        /// <summary>
        /// Show the signature of this method.
        /// </summary>
        string AsSignature { get; }
        
        /// <summary>
        /// Show the signature of this method, only including types and not names.
        /// </summary>
        string TypeOnlySignature { get; }

        InvokedMethod Call(string? calledAs);

        /// <summary>
        /// Get an attribute defined on the method.
        /// </summary>
        T? GetAttribute<T>() where T : Attribute;

        /// <summary>
        /// Invoke this method.
        /// </summary>
        object? InvokeStatic(Reflection2.AST.MethodCall? ast, params object?[] args) => Invoke(ast, null, args);

        /// <summary>
        /// Invoke this method.
        /// </summary>
        object? Invoke(Reflection2.AST.MethodCall? ast, object? instance, params object?[] args);

        /// <summary>
        /// If this method is defined in a file, make a link to the file.
        /// </summary>
        string? MakeFileLink(string typName);

        /// <summary>
        /// (Informational) The name of the type declaring this method.
        /// </summary>
        string TypeName { get; }
        
        /// <summary>
        /// (Informational) The name of this method.
        /// </summary>
        /// <returns></returns>
        string Name { get; }
    }
    /// <summary>
    /// An annotated method signature.
    /// </summary>
    public record MethodSignature : IMethodSignature {
        private static readonly Dictionary<MethodBase, MethodSignature> globals = new();
        /// <summary><see cref="MethodInfo"/> or <see cref="ConstructorInfo"/> for the method.</summary>
        public MethodBase Mi { get; init; }
        /// <summary>Simplified description of the method parameters.</summary>
        public NamedParam[] Params { get; init; }
        /// <inheritdoc/>
        public TypeDesignation.Dummy SharedType { get; }
        /// <inheritdoc cref="IGenericMethodSignature.SharedGenericTypes"/>
        public TypeDesignation.Variable[] SharedGenericTypes { get; }

        protected MethodSignature(MethodBase Mi, NamedParam[] Params) {
            this.Mi = Mi;
            this.Params = Params;
            SharedType = TypeDesignation.FromMethod(ReturnType, 
                Params.Select(p => p.Type).And(p => Mi.IsStatic ? p : p.Prepend(Mi.DeclaringType)) , out var map);
            SharedGenericTypes = 
                Mi.IsGenericMethodDefinition ? 
                    Mi.GetGenericArguments()
                    //it's possible for t to not be in map if it's "useless", eg. int Method<T>(int x).
                        .Select(t => map.TryGetValue(t, out var vt) ? vt : new TypeDesignation.Variable())
                        .ToArray()
                    : Array.Empty<TypeDesignation.Variable>();
        }
        public bool IsFallthrough { get; init; } = false;
        public string TypeName => Mi.DeclaringType!.SimpRName();
        public string Name => Mi.Name;
        public bool IsCtor => Mi.Name == ".ctor";
        public bool IsStatic => Mi.IsStatic;

        public Type? DeclaringType => Mi.DeclaringType;

        public virtual Type ReturnType => Mi switch {
            ConstructorInfo constructorInfo => constructorInfo.DeclaringType!,
            MethodInfo methodInfo => methodInfo.ReturnType,
            _ => throw new ArgumentOutOfRangeException(nameof(Mi))
        };

        public string TypeOnlySignature {
            get {
                if (Params.Length == 0)
                    return IsCtor ? "" : $"{ReturnType.SimpRName()}";
                var suffix = IsCtor ? "" : $": {ReturnType.SimpRName()}";
                return $"({string.Join(", ", Params.Select(p => p.Type.SimpRName()))}){suffix}";
            }
        }

        public string AsSignature => AsSignatureWithParamMod((p, _) => p.AsParameter);

        public string AsSignatureWithParamMod(Func<NamedParam, int, string> paramMod) =>
            IsCtor ?
                $"new {TypeName}({string.Join(", ", Params.Select(paramMod))})" :
                $"{ReturnType.SimpRName()} {Name}({string.Join(", ", Params.Select(paramMod))})";

        /// <summary>
        /// Number of parameters that must be parsed by reflection.
        /// </summary>
        public int ExplicitParameterCount(int startingFromArg = 0) {
            var ct = 0;
            for (int ii = startingFromArg; ii < Params.Length; ++ii)
                if (!Params[ii].NonExplicit)
                    ++ct;
            return ct;
        }

        public T? GetAttribute<T>() where T : Attribute => Mi.GetCustomAttribute<T>();

        public virtual object? Invoke(Reflection2.AST.MethodCall? ast, object? instance, params object?[] args) => 
            InvokeMi(ast, instance, Mi, args);
        
        protected static object? InvokeMi(Reflection2.AST.MethodCall? ast, object? instance, MethodBase mi, object?[] args) 
            => mi switch {
                ConstructorInfo cI => cI.Invoke(args),
                _ => mi.Invoke(instance, args)
            };

        public string? MakeFileLink(string typName) =>
            Mi.DeclaringType!.GetCustomAttribute<ReflectAttribute>(false)?.FileLink(typName);

        public virtual InvokedMethod Call(string? calledAs) => new(this, calledAs);

        /// <summary>
        /// Returns a <see cref="MethodSignature"/> or <see cref="GenericMethodSignature"/> for this method.
        /// </summary>
        public static MethodSignature Get(MethodBase mi) {
            if (globals.TryGetValue(mi, out var sig))
                return sig;
            var prms = mi.GetParameters();
            var nPrms = new NamedParam[prms.Length];
            for (int ii = 0; ii < nPrms.Length; ++ii)
                nPrms[ii] = prms[ii];
            var fallthrough = mi.GetCustomAttribute<FallthroughAttribute>() != null;
            if (mi.IsGenericMethodDefinition && mi is MethodInfo inf)
                return globals[mi] = new GenericMethodSignature(inf, nPrms) { IsFallthrough = fallthrough };
            return globals[mi] = new(mi, nPrms) { IsFallthrough = fallthrough };
        }

        public virtual LiftedMethodSignature<T> Lift<T>() => LiftedMethodSignature<T>.Lift(this);
    }

    public interface IGenericMethodSignature : IMethodSignature {
        /// <summary>
        /// Make a concrete method out of a generic one using the provided type parameter.
        /// </summary>
        MethodSignature Specialize(params Type[] t);
        
        /// <summary>
        /// Get the type designations for each of the generic types of this method.
        /// Note this should not be used for unification, as it is shared between all invocations.
        /// </summary>
        TypeDesignation.Variable[] SharedGenericTypes { get; }
    }

    /// <inheritdoc cref="MethodSignature"/>
    public record GenericMethodSignature(MethodInfo Minf, NamedParam[] Params) : MethodSignature(Minf, Params), IGenericMethodSignature {
        public static readonly Dictionary<(FreezableArray<Type>, MethodSignature), MethodSignature> specializeCache = new();
        
        public override object? Invoke(Reflection2.AST.MethodCall? ast, object? instance, params object?[] prms) {
            throw new Exception("A generic method signature cannot be invoked");
        }

        public MethodSignature Specialize(params Type[] t) {
            var typDef = new FreezableArray<Type>(t);
            return specializeCache.TryGetValue((typDef, this), out var m) ?
                m :
                specializeCache[(typDef, this)] = MethodSignature.Get(Minf.MakeGenericMethod(t));
        }

        public override LiftedMethodSignature<T> Lift<T>() => LiftGeneric<T>();

        public GenericLiftedMethodSignature<T> LiftGeneric<T>() => 
            LiftedMethodSignature<T>.Lift(this) as GenericLiftedMethodSignature<T> ??
            throw new StaticException("Incorrect lifting behavior on generic method signature");
    }

    /// <summary>
    /// A description of a funcified method called in reflection.
    /// <br/>A funcified method has a "source" signature (A, B, C)->R, but is internally
    /// converted to "funcified" signature (T->A, T->B, T->C)->(T->R);
    ///  ie. it is lifted over the reader functor. This is because
    /// some internal reflection functions are of type <see cref="TExArgCtx"/>->TEx,
    ///  but it is generally easier to write them as type TEx where possible.
    /// </summary>
    /// <param name="Mi">Method info for the method. This has the source signature (A, B, C)->R.</param>
    /// <param name="FuncedParams">The parameter list [T->A, T->B, T->C]. This is provided as <see cref="MethodSignature.Params"/>.</param>
    /// <param name="BaseParams">The parameter list [A, B, C].</param>
    public abstract record LiftedMethodSignature
        (MethodBase Mi, NamedParam[] FuncedParams, NamedParam[] BaseParams) : MethodSignature(Mi, FuncedParams) {
        protected static readonly Dictionary<(Type, Type), (Type lmsTR, ConstructorInfo constr)> typeSpecCache = new();
        protected static readonly Type[] consTypes = { typeof(MethodBase), typeof(NamedParam[]), typeof(NamedParam[]) };

        public override InvokedMethod Call(string? calledAs) => new LiftedInvokedMethod(this, calledAs);
        
        public override object? Invoke(Reflection2.AST.MethodCall? ast, object? instance, params object?[] prms) {
            throw new Exception(
                "This lifted method signature does not have a specified return type and therefore cannot be invoked");
        }

        /// <summary>
        /// Lift a set of parameters over the reader functor T->.
        /// </summary>
        public static NamedParam[] LiftParams<T>(MethodSignature method) => LiftParams(typeof(T), method);
        
        public static NamedParam[] LiftParams(Type t, MethodSignature method) {
            var baseTypes = method.Params;
            NamedParam[] fTypes = new NamedParam[baseTypes.Length];
            for (int ii = 0; ii < baseTypes.Length; ++ii) {
                var bt = baseTypes[ii].Type;
                fTypes[ii] = baseTypes[ii] with {
                    Type = ReflectionData.LiftType(t, bt, out var result) ? result : bt
                };
            }
            return fTypes;
        }
        
    }

    /// <inheritdoc cref="LiftedMethodSignature"/>
    public abstract record LiftedMethodSignature<T>(MethodBase Mi, NamedParam[] FuncedParams, NamedParam[] BaseParams) :
        LiftedMethodSignature(Mi, FuncedParams, BaseParams) {
        private static readonly Dictionary<MethodBase, LiftedMethodSignature<T>> liftCache = new();
        
        /// <summary>
        /// Lift a method over the reader functor T->.
        /// <br/>If R is known statically, use <see cref="LiftedMethodSignature{T,R}"/>'s Lift instead.
        /// </summary>
        public static LiftedMethodSignature<T> Lift(MethodSignature method) {
            if (liftCache.TryGetValue(method.Mi, out var sig))
                return sig;
            if (method is LiftedMethodSignature)
                throw new Exception("Tried to lift a method twice");
            
            //easy case
            if (method is GenericMethodSignature gm)
                return liftCache[method.Mi] = new GenericLiftedMethodSignature<T>(gm.Minf, LiftParams<T>(gm), gm.Params);

            //not-easy case
            var t = typeof(T);
            var r = method.ReturnType;
            if (!typeSpecCache.TryGetValue((t, r), out var info)) {
                var type = typeof(LiftedMethodSignature<,>).MakeGenericType(t, r);
                var cons = type.GetConstructor(consTypes);
                typeSpecCache[(t, r)] = info = (type, cons);
            }
            return liftCache[method.Mi] = info.constr!.Invoke(new object[] { method.Mi, LiftParams(t, method), method.Params })
                as LiftedMethodSignature<T> ?? throw new StaticException(
                $"Dynamic instantiation of LiftedMethodSignature<{t.RName()},{r.RName()}> failed");
        }
    }
    
    /// <inheritdoc cref="LiftedMethodSignature"/>
    public record GenericLiftedMethodSignature<T>(MethodInfo Minf, NamedParam[] FuncedParams, NamedParam[] BaseParams) : LiftedMethodSignature<T>(Minf, FuncedParams, BaseParams), IGenericMethodSignature  {
        public override Type ReturnType => Func2Type(typeof(T), base.ReturnType);

        public override object? Invoke(Reflection2.AST.MethodCall? ast, object? instance, params object?[] prms)
            => throw new Exception("A generic lifted method cannot be invoked");
        
        public LiftedMethodSignature<T> Specialize(Type[] t) {
            var typDef = new FreezableArray<Type>(t);
            if (GenericMethodSignature.specializeCache.TryGetValue((typDef, this), out var m))
                return m as LiftedMethodSignature<T> ??
                       throw new StaticException("Cached specialization of lifted generic method failed");
            var method =  MethodSignature.Get(Minf.MakeGenericMethod(t)).Lift<T>();
            GenericMethodSignature.specializeCache[(typDef, this)] = method;
            return method;
        }

        MethodSignature IGenericMethodSignature.Specialize(Type[] t) => Specialize(t);
    }
    
    //Note that we must eventually specify the R in LiftedMethodSignature in order to ensure that
    // InvokeMiFunced creates a correctly-typed Func<T,R>.
    /// <inheritdoc cref="LiftedMethodSignature"/>
    public record LiftedMethodSignature<T, R>
        (MethodBase Mi, NamedParam[] FuncedParams, NamedParam[] BaseParams) : LiftedMethodSignature<T>(Mi, FuncedParams, BaseParams) {
        private static readonly Dictionary<MethodBase, LiftedMethodSignature<T, R>> liftCache = new();
        private Type? LiftedInstanceType => Mi.IsStatic ? null :
            ReflectionData.LiftType(typeof(T), Mi.DeclaringType!, out var result) ? result : Mi.DeclaringType!;
        public override Type ReturnType => typeof(Func<T, R>);

        public override object? Invoke(Reflection2.AST.MethodCall? ast, object? instance, params object?[] prms)
            => InvokeMiFunced(ast, instance, prms);
        public Func<T,R> InvokeMiFunced(Reflection2.AST.MethodCall? ast, object? instance, params object?[] fprms) => 
            //Note: this lambda capture generally prevents using ArrayCache
            bpi => {
                if (IsStatic && instance != null)
                    throw new Exception($"Static method {Name} provided with an instance argument");
                if (!IsStatic && instance == null)
                    throw new Exception($"Instance method {Name} provided with no instance argument");
                var baseArgs = new object?[BaseParams.Length];
                for (int ii = 0; ii < baseArgs.Length; ++ii)
                    //Convert from funced object to base object (eg. TExArgCtx->TEx<float> to TEx<float>)
                    baseArgs[ii] = ReflectionData.Defuncify(BaseParams[ii].Type, FuncedParams[ii].Type, fprms[ii]!, bpi!);
                foreach (var writeable in Mi.GetCustomAttribute<AssignsAttribute>()?.Indices ?? Array.Empty<int>()) {
                    if (Reflection2.Helpers.AssertWriteable(writeable, baseArgs[writeable]!) is { } exc)
                        throw ast?.Raise(exc) as Exception ?? exc;
                }
                if (instance != null)
                    instance = ReflectionData.Defuncify(DeclaringType!, LiftedInstanceType!, instance, bpi!);
                return (R)InvokeMi(ast, instance, Mi, baseArgs)!;
            };

        public override InvokedMethod Call(string? calledAs) => new LiftedInvokedMethod<T,R>(this, calledAs);

        /// <summary>
        /// Lift a method over the reader functor T->.
        /// <br/>If R is not known statically, use <see cref="LiftedMethodSignature{T}"/>'s Lift instead.
        /// </summary>
        public new static LiftedMethodSignature<T, R> Lift(MethodSignature method) {
            if (liftCache.TryGetValue(method.Mi, out var sig))
                return sig;
            //funced methods are not fallthrough
            return liftCache[method.Mi] = new(method.Mi, LiftedMethodSignature.LiftParams<T>(method), method.Params);
        }
    }

    public static MethodSignature Signature(this MethodBase mi) => MethodSignature.Get(mi);
    
    /// <summary>
    /// A description of a method called in reflection.
    /// </summary>
    public record InvokedMethod(IMethodSignature Mi, string? CalledAs) : IMethodDesignation {
        /// <summary>
        /// Method details.
        /// </summary>
        public IMethodSignature Mi { get; } = Mi;
        //important to ensure that multiple invocations don't share the same variable bindings!
        public TypeDesignation.Dummy Method { get; } = 
            (Mi.SharedType.RecreateVariables() as TypeDesignation.Dummy)!;
        
        /// <summary>
        /// The name by which the user called the method (which may be an alias).
        /// </summary>
        public string? CalledAs { get; } = CalledAs;
        public NamedParam[] Params => Mi.Params;
        public string SimpleName {
            get {
                var prefix = Mi.IsCtor ? Mi.TypeName : Mi.Name;
                return (CalledAs == null || CalledAs == Mi.Name.ToLower()) ?
                    prefix : $"{prefix}/{CalledAs}";
            }
        }
        public string Name => 
            Mi.IsCtor ? 
                $"new {Mi.TypeName}" :
                (CalledAs == null || CalledAs == Mi.Name.ToLower()) ? 
                    Mi.Name : 
                    $"{Mi.Name}/{CalledAs}";
        public string TypeEnclosedName => 
            Mi.IsCtor ?
                Name :
                $"{Mi.TypeName}.{Name}";

        public string FileLink =>
            Mi.MakeFileLink(TypeEnclosedName) ?? TypeEnclosedName;

        public virtual IAST ToAST(PositionRange pos, PositionRange callPos, IAST[] arguments, bool parenthesized) =>
            new AST.MethodInvoke(pos, callPos, this, arguments) { Parenthesized = parenthesized };

        public override string ToString() => Mi.AsSignature;
    }

    /// <summary>
    /// See <see cref="LiftedMethodSignature"/>
    /// </summary>
    public record LiftedInvokedMethod(LiftedMethodSignature FMi, string? CalledAs) : InvokedMethod(FMi, CalledAs) {
        public override string ToString() => Mi.AsSignature;
    }

    /// <summary>
    /// See <see cref="LiftedMethodSignature{T,R}"/>
    /// </summary>
    public record LiftedInvokedMethod<T, R>(LiftedMethodSignature<T, R> TypedFMi, string? CalledAs) : LiftedInvokedMethod(TypedFMi, CalledAs) {
        public override IAST ToAST(PositionRange pos, PositionRange callPos, IAST[] arguments, bool parenthesized) =>
            new AST.FuncedMethodInvoke<T, R>(pos, callPos, this, arguments) { Parenthesized = parenthesized };
        
        public override string ToString() => Mi.AsSignature;
    }

    /// <summary>
    /// Within the context of a given return type, try to get the signature for a funced method.
    /// </summary>
    private delegate LiftedMethodSignature? GetSignature(string member);

    /// <summary>
    /// A dictionary containing reflection information for types that can be "funcified", where
    /// funcification transforms a function (A,B,C...)->R into a function (A',B',C'...)->(T->R), where
    /// A', B', C' may relate to the introduced type T.
    /// <br/>The keys are of the form type(T->R).
    /// </summary>
    private static readonly Dictionary<Type, GetSignature> funcifiableTypes =
        new();
    /// <summary>
    /// A dictionary mapping funcifable reflection types (eg. TExArgCtx->tfloat) to simplified types
    /// (eg. tfloat). Used by language server.
    /// </summary>
    [PublicAPI]
    public static readonly Dictionary<Type, Type> FuncifySimplifications = new();
    private static readonly HashSet<Type> funcifiableReturnTypes = new();
    private static readonly HashSet<Type> funcifiableReturnTypeGenerics = new();

    private static void AllowFuncification<ExR>() {
        var exr = typeof(ExR);
        funcifiableTypes[typeof(Func<TExArgCtx, ExR>)] = ReflectionData.GetFuncedSignature<TExArgCtx, ExR>;
        FuncifySimplifications[typeof(Func<TExArgCtx, ExR>)] = exr;
        funcifiableReturnTypes.Add(exr);
        if (exr.IsGenericType)
            funcifiableReturnTypeGenerics.Add(exr.GetGenericTypeDefinition());
    }

    private static IMethodSignature? ASTTryLookForMethod(Type rt, string member) {
        if (funcifiableTypes.TryGetValue(rt, out var fs) && fs(member) is { } funcedSig) {
            return funcedSig;
        }
        return ReflectionData.TryGetMember(rt, member);
    }

    private static readonly Dictionary<Type, Func<string, object>> referenceVarFuncs =
        new() {
            {typeof(ExBPY), ReflectEx.ReferenceExpr<float>},
            {typeof(ExTP), ReflectEx.ReferenceExpr<Vector2>},
            {typeof(ExTP3), ReflectEx.ReferenceExpr<Vector3>},
            {typeof(ExBPRV2), ReflectEx.ReferenceExpr<V2RV2>},
        };

    public static object? ExtInvokeMethod(Type t, string member, object[] prms) {
        if (TryCompileOption(t, out var compiler)) {
            return compiler.mi.InvokeStatic(null, ExtInvokeMethod(compiler.source, member, prms));
        } else if (t == typeof(StateMachine)) {
            return StateMachine.Create(member, prms).Evaluate(new());
        }
        if (ASTTryLookForMethod(t, member) is { } result)
            return result.InvokeStatic(null, prms);
        //this also requires fallthrough support, which is handled through parent methods in the inner call.
        if (FallThroughOptions.TryGetValue(t, out var ftmi)) {
            if ((result = ASTTryLookForMethod(ftmi.mi.Params[0].Type, member)) != null)
                return ftmi.mi.InvokeStatic(null, result.InvokeStatic(null, prms));
        }
        throw new Exception($"External method invocation failed for type {t.RName()}, method {member}. " +
                            "This is probably an error in static code.");
    }

    public static T ExtInvokeMethod<T>(string member, object[] prms) => 
        ExtInvokeMethod(typeof(T), member, prms) is T obj ?
            obj :
            throw new StaticException($"External method invoke for method {member} returned wrong type");



    private static readonly Dictionary<Type, (Type source, IMethodSignature mi)> CompileOptions =
        new();
    private static readonly HashSet<Type> checkedCompileOptions = new();
    private static readonly List<MethodInfo> genericCompileOptions = new();

    private static void AddCompileOption(MethodInfo compiler) {
        if (compiler.IsGenericMethodDefinition)
            genericCompileOptions.Add(compiler);
        else {
            var compiledType = compiler.ReturnType;
            if (CompileOptions.ContainsKey(compiledType))
                throw new StaticException(
                    $"Cannot have multiple expression compilers for the same return type {compiledType}.");
            var sig = MethodSignature.Get(compiler);
            CompileOptions[compiledType] = (sig.Params[0].Type, sig);
        }
    }
        
    
    public static bool TryCompileOption(Type compiledType, out (Type source, IMethodSignature mi) compile) {
        if (CompileOptions.TryGetValue(compiledType, out compile)) return true;
        if (!checkedCompileOptions.Contains(compiledType)) {
            for (int ii = 0; ii < genericCompileOptions.Count; ++ii) {
                var mi = genericCompileOptions[ii];
                if (ConstructedGenericTypeMatch(compiledType, mi.ReturnType, out var typeMap)) {
                    AddCompileOption(mi.MakeGeneric(typeMap));
                    compile = CompileOptions[compiledType];
                    return true;
                }
            }
            checkedCompileOptions.Add(compiledType);
        }
        return false;
    }
    
    public static readonly Dictionary<Type, (FallthroughAttribute fa, IMethodSignature mi)> FallThroughOptions =
        new();

}
}