using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
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
using Scriptor;
using Scriptor.Expressions;
using Scriptor.Reflection;
using UnityEngine;
using static BagoumLib.Reflection.ReflectionUtils;
using ExBPY = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<float>>;
using ExPred = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<bool>>;
using ExTP = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<UnityEngine.Vector3>>;
using ExBPRV2 = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<BagoumLib.Mathematics.V2RV2>>;

namespace Danmokou.Reflection {
public static partial class Reflector {
    public record ParamFeatures(bool LookupMethod = false, bool NonExplicit = false, bool BDSL1ImplicitSMList = false) {
        public static implicit operator ParamFeatures(ParameterInfo pi) => 
            new(pi.GetCustomAttribute<LookupMethodAttribute>() != null,
                pi.GetCustomAttribute<NonExplicitParameterAttribute>() != null,
                pi.GetCustomAttribute<BDSL1ImplicitChildrenAttribute>() != null);
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

    private static void AllowFuncification<ExR>() {
        var exr = typeof(ExR);
        funcifiableTypes[typeof(Func<TExArgCtx, ExR>)] = ReflectionData.GetFuncedSignature<TExArgCtx, ExR>;
        FuncifySimplifications[typeof(Func<TExArgCtx, ExR>)] = exr;
        TypeLifter.FuncifiableReturnTypes.Add(exr);
        if (exr.IsGenericType)
            TypeLifter.FuncifiableReturnTypeGenerics.Add(exr.GetGenericTypeDefinition());
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
            return compiler.mi.Invoke(new[]{ExtInvokeMethod(compiler.source, member, prms)});
        } else if (t == typeof(StateMachine)) {
            return StateMachine.Create(member, prms).Evaluate();
        }
        if (ASTTryLookForMethod(t, member) is { } result)
            return result.Invoke(prms);
        //this also requires fallthrough support, which is handled through parent methods in the inner call.
        if (FallThroughOptions.TryGetValue(t, out var ftmi)) {
            if ((result = ASTTryLookForMethod(ftmi.mi.Params[0].Type, member)) != null)
                return ftmi.mi.Invoke(new[]{result.Invoke(prms)});
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