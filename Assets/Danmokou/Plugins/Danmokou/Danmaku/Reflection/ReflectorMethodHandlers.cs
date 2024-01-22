using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
using Danmokou.Reflection2;
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
using MethodCall = Danmokou.Reflection2.AST.MethodCall;

namespace Danmokou.Reflection {
public static partial class Reflector {
    private static readonly Dictionary<Type, (string descr, Type compiled)> exTypeRemap = new() {
        { typeof(ExTP), ("TP/GCXF<Vector2>", typeof(TP)) },
        { typeof(ExTP3), ("TP3/GCXF<Vector3>", typeof(TP3)) },
        { typeof(ExTP4), ("TP4/GCXF<Vector4>", typeof(TP4)) },
        { typeof(ExBPY), ("BPY/GCXF<float>", typeof(BPY)) },
        { typeof(ExBPRV2), ("BPRV2/GCXF<V2RV2>", typeof(BPRV2)) },
        { typeof(Func<TExArgCtx, TEx<bool>>), ("Pred/GCXF<bool>", typeof(Pred)) },
        { typeof(Func<TExArgCtx, TEx<VTPExpr>>), ("VTP", typeof(VTP)) },
        { typeof(Func<TExArgCtx, TEx<LVTPExpr>>), ("LVTP", typeof(LVTP)) },
        { typeof(Func<TExSBC, TEx<int>, TEx<BagoumLib.Cancellation.ICancellee>, TExArgCtx, TEx>), ("ExSBCF/SBCF", typeof(SBCF)) }
    };
    private static readonly Type[] BypassTypes = {
        typeof(TEx<>),
    };
    public static Type RemapExType(Type t) => exTypeRemap.TryGetValue(t, out var v) ? v.compiled : t;

    public static string ExRName(this Type t) {
        return exTypeRemap.TryGetValue(t, out var v) ? v.descr : t.RName();
    }
    public static string ExRName(this TypeDesignation t) {
        if (t.IsResolved)
            return t.Resolve(Unifier.Empty).LeftOrThrow.ExRName();
        if (t is TypeDesignation.Known kt) {
            if (kt.IsArrayTypeConstructor)
                return ExRName(kt.Arguments[0]) + "[]";
            if (kt.Arguments.Length == 0)
                return kt.Typ.ExRName();
            if (ReflectionUtils.TupleTypesByArity.Contains(kt.Typ))
                return $"({string.Join(", ", kt.Arguments.Select(ExRName))})";
            return kt.Typ.ExRName() + $"<{string.Join(",", kt.Arguments.Select(ExRName))}>";
        } else if (t is TypeDesignation.Dummy d) {
            return $"({string.Join(",", d.Arguments.Take(d.Arguments.Length - 1).Select(ExRName))})->{ExRName(d.Last)}";
        } else if (t is TypeDesignation.Variable { RestrictedTypes: { } rt })
            return $"{string.Join(" or ", rt.Select(ExRName).Distinct())}";
        else
            return t.ToString();
    }

    public static string SimpRName(this Type t) => SimplifiedExprPrinter.Default.Print(t);
    
    public static string SimpRName(this TypeDesignation t) {
        if (t.IsResolved)
            return t.Resolve(Unifier.Empty).LeftOrThrow.SimpRName();
        if (t is TypeDesignation.Known kt) {
            if (kt.Typ == typeof(Func<,>) && (kt.Arguments[0] as TypeDesignation.Known)?.Typ == typeof(TExArgCtx)) {
                return SimpRName(kt.Arguments[1]);
            } else if (BypassTypes.Contains(kt.Typ))
                return SimpRName(kt.Arguments[0]);
            if (kt.IsArrayTypeConstructor)
                return SimpRName(kt.Arguments[0]) + "[]";
            if (kt.Arguments.Length == 0)
                return kt.Typ.ExRName();
            if (ReflectionUtils.TupleTypesByArity.Contains(kt.Typ))
                return $"({string.Join(", ", kt.Arguments.Select(SimpRName))})";
            return kt.Typ.ExRName() + $"<{string.Join(",", kt.Arguments.Select(SimpRName))}>";
        } else if (t is TypeDesignation.Dummy d) {
            return $"({string.Join(",", d.Arguments[..^1].Select(SimpRName))})->{SimpRName(d.Last)}";
        } else if (t is TypeDesignation.Variable { RestrictedTypes: { } rt })
            return $"{string.Join(" or ", rt.Select(SimpRName).Distinct())}";
        else
            return t.ToString();
    }
    private class SimplifiedExprPrinter : CSharpTypePrinter {
        public new static readonly ITypePrinter Default = new SimplifiedExprPrinter()
            { PrintTypeNamespace = _ => false };
        public override string Print(Type t) {
            if (t.IsConstructedGenericType) {
                if (t.GetGenericTypeDefinition() == typeof(Func<,>) && t.GenericTypeArguments[0] == typeof(TExArgCtx)) {
                    return Print(t.GenericTypeArguments[1]);
                }
                if (BypassTypes.Contains(t.GetGenericTypeDefinition()))
                    return Print(t.GenericTypeArguments[0]);
            }
            if (exTypeRemap.TryGetValue(t, out var v))
                return Print(v.compiled);
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
    /// <param name="BDSL1ImplicitSMList">Whether this parameter has <see cref="BDSL1ImplicitChildrenAttribute"/></param>
    public record NamedParam(Type Type, string Name, bool LookupMethod = false, bool NonExplicit = false, bool BDSL1ImplicitSMList = false) {
        public static implicit operator NamedParam(ParameterInfo pi) => 
            new(pi.ParameterType, pi.Name, 
                pi.GetCustomAttribute<LookupMethodAttribute>() != null,
                pi.GetCustomAttribute<NonExplicitParameterAttribute>() != null,
                pi.GetCustomAttribute<BDSL1ImplicitChildrenAttribute>() != null);

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
        /// <br/>Note that lifted methods do NOT return a lifted type here. The types here are *unlifted*
        ///  over the TExArgCtx->TEx&lt;&gt; functor.
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
        string AsSignatureWithParamMod(Func<NamedParam, int, string> paramMod);
        
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
        /// Invoke this method. If this is an instance method, the instance should be the first argument of `args`.
        /// </summary>
        object? Invoke(MethodCall? ast, object?[] args);
        
        /// <summary>
        /// Invoke this method. If this is an instance method, the instance should be the first argument of `args`.
        /// </summary>
        Expression InvokeEx(MethodCall? ast, params Expression[] args);
        
        /// <summary>
        /// Return the invocation of this method as an expression node,
        /// but if all arguments are constant, then instead call the method and wrap it in Ex.Constant.
        /// </summary>
        public Expression InvokeExIfNotConstant(MethodCall? ast, params Expression[] args) {
            for (int ii = 0; ii < args.Length; ++ii)
                if (args[ii] is not ConstantExpression)
                    return InvokeEx(ast, args);
            return Expression.Constant(Invoke(ast, args.Select(a => ((ConstantExpression)a).Value).ToArray()));
        }

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

        /// <summary>
        /// If the return type of the called method is a subtype of <see cref="StateMachine"/>, then "hide" it
        ///  by replacing it with StateMachine.
        /// </summary>
        public InvokedMethod HideSMReturn() {
            if (Mi is MethodSignature { SharedType: { Last: TypeDesignation.Known k } } msig &&
                k.Typ.IsSubclassOf(typeof(StateMachine))) {
                var typs = msig.SharedType.Arguments.ToArray();
                typs[^1] = new TypeDesignation.Known(typeof(StateMachine));
                return new(msig with {
                    SharedType = new TypeDesignation.Dummy(TypeDesignation.Dummy.METHOD_KEY, typs)
                }, CalledAs);
            } else
                return this;
        }
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
            return compiler.mi.Invoke(null, new[]{ExtInvokeMethod(compiler.source, member, prms)});
        } else if (t == typeof(StateMachine)) {
            return StateMachine.Create(member, prms).Evaluate(new());
        }
        if (ASTTryLookForMethod(t, member) is { } result)
            return result.Invoke(null, prms);
        //this also requires fallthrough support, which is handled through parent methods in the inner call.
        if (FallThroughOptions.TryGetValue(t, out var ftmi)) {
            if ((result = ASTTryLookForMethod(ftmi.mi.Params[0].Type, member)) != null)
                return ftmi.mi.Invoke(null, new[]{result.Invoke(null, prms)});
        }
        throw new Exception($"External method invocation failed for type {t.ExRName()}, method {member}. " +
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