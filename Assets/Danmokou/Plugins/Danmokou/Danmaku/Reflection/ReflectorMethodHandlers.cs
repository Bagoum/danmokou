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
    public static string SimpRName(this Type t) => SimplifiedExprPrinter.Default.Print(t);
    
    public static string SimpRName(this TypeDesignation t) {
        if (t is TypeDesignation.Known kt) {
            if (kt.Typ == typeof(Func<,>) && (kt.Arguments[0] as TypeDesignation.Known)?.Typ == typeof(TExArgCtx)) {
                return SimpRName(kt.Arguments[1]);
            } else if (kt.Typ == typeof(TEx<>))
                return SimpRName(kt.Arguments[0]);
            if (kt.IsArrayTypeConstructor)
                return SimpRName(kt.Arguments[0]) + "[]";
            if (kt.Arguments.Length == 0)
                return kt.Typ.SimpRName();
            if (ReflectionUtils.TupleTypesByArity.Contains(kt.Typ))
                return $"({string.Join(", ", kt.Arguments.Select(SimpRName))})";
            return kt.Typ.SimpRName() + $"<{string.Join(",", kt.Arguments.Select(SimpRName))}>";
        } else if (t is TypeDesignation.Dummy d) {
            return $"({string.Join(",", d.Arguments.Take(d.Arguments.Length - 1).Select(SimpRName))})->{SimpRName(d.Last)}";
        } else if (t is TypeDesignation.Variable { RestrictedTypes: { } rt })
            return $"{string.Join(" or ", rt.Select(SimpRName).Distinct())}";
        else
            return t.ToString();
    }
    private class SimplifiedExprPrinter : CSharpTypePrinter {
        public new static readonly ITypePrinter Default = new SimplifiedExprPrinter()
            { PrintTypeNamespace = _ => false };
        public override string Print(Type t) {
            if (t.IsTExOrTExFuncType(out var inner))
                t = inner;
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
                return (CalledAs == null || CalledAs.ToLower() == Mi.Name.ToLower()) ?
                    prefix : $"{prefix}/{CalledAs}";
            }
        }
        public string Name => 
            Mi.IsCtor ? 
                $"new {Mi.TypeName}" :
                (CalledAs == null || CalledAs.ToLower() == Mi.Name.ToLower()) ? 
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
            return StateMachine.Create(member, prms).Evaluate();
        }
        if (ASTTryLookForMethod(t, member) is { } result)
            return result.Invoke(null, prms);
        //this also requires fallthrough support, which is handled through parent methods in the inner call.
        if (FallThroughOptions.TryGetValue(t, out var ftmi)) {
            if ((result = ASTTryLookForMethod(ftmi.mi.Params[0].Type, member)) != null)
                return ftmi.mi.Invoke(null, new[]{result.Invoke(null, prms)});
        }
        throw new Exception($"External method invocation failed for type {t.SimpRName()}, method {member}. " +
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