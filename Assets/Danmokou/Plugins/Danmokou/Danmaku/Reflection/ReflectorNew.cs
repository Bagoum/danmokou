using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib;
using BagoumLib.Expressions;
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
    public static Type RemapExType(Type t) => exTypeRemap.TryGetValue(t, out var v) ? v : t;
    public static string SimpRName(this Type t) => SimplifiedExprPrinter.Default.Print(t);
    private class SimplifiedExprPrinter : CSharpTypePrinter {
        public new static readonly ITypePrinter Default = new SimplifiedExprPrinter();

        private static readonly Type[] BypassTypes = {
            typeof(GCXU<>), typeof(TEx<>), typeof(EEx<>)
        };
        public override string Print(Type t) {
            if (exTypeRemap.TryGetValue(t, out var v))
                return Print(v);
            if (t.IsConstructedGenericType && BypassTypes.Contains(t.GetGenericTypeDefinition()))
                return Print(t.GenericTypeArguments[0]);
            return base.Print(t);
        }
    }
    public record NamedParam(Type Type, string Name, bool LookupMethod, bool NonExplicit) {
        public static implicit operator NamedParam(ParameterInfo pi) => 
            new(pi.ParameterType, pi.Name, 
                pi.GetCustomAttribute<LookupMethodAttribute>() != null,
                pi.GetCustomAttribute<NonExplicitParameterAttribute>() != null);

        public string Description => $"{Name}<{CSharpTypePrinter.Default.Print(Type)}>";
        public string AsParameter => $"{Type.SimpRName()} {Name}";
        public string SimplifiedDescription => $"\"{Name}\" (type: {SimplifiedExprPrinter.Default.Print(Type)})";
    }

    /// <summary>
    /// A description of a method called in reflection.
    /// </summary>
    /// <param name="Mi"><see cref="MethodInfo"/> or <see cref="ConstructorInfo"/> for the method.</param>
    /// <param name="CalledAs">The name by which the user called the method (which may be an alias).</param>
    /// <param name="Params">Simplified description of the method parameters.</param>
    public record MethodSignature(MethodBase Mi, string? CalledAs, NamedParam[] Params) {
        public bool IsFallthrough { get; init; } = false;
        public bool IsDeprecated => Mi.GetCustomAttribute<ObsoleteAttribute>() != null;
        public string TypeName => Mi.DeclaringType!.SimpRName();
        public bool isCtor => Mi.Name == ".ctor";
        
        public string SimpleName {
            get {
                var prefix = isCtor ? TypeName : Mi.Name;
                return (CalledAs == null || CalledAs == Mi.Name.ToLower()) ?
                    prefix : $"{prefix}/{CalledAs}";
            }
        }
        public string Name => 
            isCtor ? 
                $"new {TypeName}" :
                (CalledAs == null || CalledAs == Mi.Name.ToLower()) ? 
                    Mi.Name : 
                    $"{Mi.Name}/{CalledAs}";
        public string TypeEnclosedName => 
            isCtor ?
                Name :
                $"{TypeName}.{Name}";

        public string AsSignature => AsSignatureWithParamMod((p, _) => p.AsParameter);
        public string AsSignatureWithParamMod(Func<NamedParam, int, string> paramMod) => 
            isCtor ? 
                $"new {TypeName}({string.Join(", ", Params.Select(paramMod))})" :
                $"{ReturnType.SimpRName()} {Name}({string.Join(", ", Params.Select(paramMod))})";
        
        public string TypeOnlySignature {
            get {
                if (Params.Length == 0) 
                    return isCtor ? "" : $"{ReturnType.SimpRName()}";
                var suffix = isCtor ? "" : $": {ReturnType.SimpRName()}";
                return $"({string.Join(", ", Params.Select(p => p.Type.SimpRName()))}){suffix}";
            }
        }

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


        public string FileLink =>
            Mi.DeclaringType!.GetCustomAttribute<ReflectAttribute>()?.FileLink(TypeEnclosedName) ??
            TypeEnclosedName;

        public virtual Type ReturnType => Mi switch {
            ConstructorInfo constructorInfo => constructorInfo.DeclaringType!,
            MethodInfo methodInfo => methodInfo.ReturnType,
            _ => throw new ArgumentOutOfRangeException(nameof(Mi))
        };

        public virtual object? InvokeMi(params object?[] prms) => Mi switch {
            ConstructorInfo cI => cI.Invoke(prms),
            _ => Mi.Invoke(null, prms)
        };

        public virtual IAST ToAST(PositionRange pos, PositionRange callPos, IAST[] arguments, bool parenthesized) =>
            new AST.MethodInvoke(pos, callPos, this, arguments) { Parenthesized = parenthesized };
        
        public static MethodSignature FromMethod(MethodBase mi, string? calledAs = null, ParameterInfo[]? srcPrms = null, bool isFallthrough = false) {
            srcPrms ??= mi.GetParameters();
            var nPrms = new NamedParam[srcPrms.Length];
            for (int ii = 0; ii < nPrms.Length; ++ii)
                nPrms[ii] = srcPrms[ii];
            return new(mi, calledAs, nPrms) {IsFallthrough = isFallthrough};
        }
    }

    /// <summary>
    /// A description of a funcified method called in reflection.
    /// <br/>A funcified method has a "source" signature (A, B, C)->R, but is internally
    /// converted to "funcified" signature (T->A, T->B, T->C)->(T->R). This is because
    /// some internal reflection functions are of type <see cref="TExArgCtx"/>->TEx,
    ///  but it is generally easier to write them as type TEx where possible.
    /// </summary>
    /// <param name="Mi">Method info for the method. This has the source signature (A, B, C)->R.</param>
    /// <param name="CalledAs">The name by which the user called the method (which may be an alias).</param>
    /// <param name="FuncedParams">The parameter list [T->A, T->B, T->C]. This is provided as <see cref="MethodSignature.Params"/>.</param>
    /// <param name="BaseParams">The parameter list [A, B, C].</param>
    public abstract record FuncedMethodSignature(MethodBase Mi, string? CalledAs, NamedParam[] FuncedParams, NamedParam[] BaseParams) : MethodSignature(Mi, CalledAs, FuncedParams) {
    }

    /// <summary>
    /// See <see cref="FuncedMethodSignature"/>. 
    /// </summary>
    public record FuncedMethodSignature<T, R>(MethodBase Mi, string? CalledAs, NamedParam[] FuncedParams,
        NamedParam[] BaseParams) : FuncedMethodSignature(Mi, CalledAs, FuncedParams, BaseParams) {
        public override Type ReturnType => typeof(Func<T, R>);

        public override IAST ToAST(PositionRange pos, PositionRange callPos, IAST[] arguments, bool parenthesized) =>
            new AST.FuncedMethodInvoke<T, R>(pos, callPos, this, arguments) { Parenthesized = parenthesized };

        public override object? InvokeMi(params object?[] fprms)
            => InvokeMiFunced(fprms);
        public Func<T,R> InvokeMiFunced(params object?[] fprms) => 
            //Note: this lambda capture generally prevents using ArrayCache
            bpi => {
                var baseParams = new object?[BaseParams.Length];
                for (int ii = 0; ii < baseParams.Length; ++ii)
                    //Convert from funced object to base object (eg. TExArgCtx->TEx<float> to TEx<float>)
                    baseParams[ii] = ReflectionData.Defuncify(BaseParams[ii].Type, FuncedParams[ii].Type, fprms[ii]!, bpi!);
                return (R)base.InvokeMi(baseParams)!;
            };
    }

    /// <summary>
    /// Within the context of a given return type, try to get the signature for a funced method.
    /// </summary>
    private delegate FuncedMethodSignature? GetSignature(string member);

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

    private static void AllowFuncification<ExR>() {
        funcifiableTypes[typeof(Func<TExArgCtx, ExR>)] = ReflectionData.GetFuncedSignature<TExArgCtx, ExR>;
        FuncifySimplifications[typeof(Func<TExArgCtx, ExR>)] = typeof(ExR);
        funcifiableReturnTypes.Add(typeof(ExR));
    }

    private static MethodSignature? ASTTryLookForMethod(Type rt, string member) {
        if (funcifiableTypes.TryGetValue(rt, out var fs) && fs(member) is { } funcedSig) {
            return funcedSig;
        }
        if (ReflectionData.HasMember(rt, member)) {
            return ReflectionData.GetArgTypes(rt, member);
        }
        return null;
    }

    private static readonly Dictionary<Type, Func<string, object>> letFuncs =
        new() {
            {typeof(ExBPY), ReflectEx.ReferenceLet<float>},
            {typeof(ExTP), ReflectEx.ReferenceLet<Vector2>},
            {typeof(ExTP3), ReflectEx.ReferenceLet<Vector3>},
            {typeof(ExBPRV2), ReflectEx.ReferenceLet<V2RV2>},
        };

    public static object? ExtInvokeMethod(Type t, string member, object[] prms) {
        member = Sanitize(member);
        if (TryCompileOption(t, out var compiler)) {
            return compiler.mi.InvokeMi(ExtInvokeMethod(compiler.source, member, prms));
        } else if (t == typeof(StateMachine)) {
            return StateMachine.Create(member, prms).Evaluate(new());
        }
        if (ASTTryLookForMethod(t, member) is { } result)
            return result.InvokeMi(prms);
        //this also requires fallthrough support, which is handled through parent methods in the inner call.
        if (FallThroughOptions.TryGetValue(t, out var ftmi)) {
            if ((result = ASTTryLookForMethod(ftmi.mi.Params[0].Type, member)) != null)
                return ftmi.mi.InvokeMi(result.InvokeMi(prms));
        }
        throw new Exception($"External method invocation failed for type {t.RName()}, method {member}. " +
                            "This is probably an error in static code.");
    }

    public static T ExtInvokeMethod<T>(string member, object[] prms) => 
        ExtInvokeMethod(typeof(T), member, prms) is T obj ?
            obj :
            throw new StaticException($"External method invoke for method {member} returned wrong type");



    private static readonly Dictionary<Type, (Type source, MethodSignature mi)> CompileOptions =
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
            var sig = MethodSignature.FromMethod(compiler, isFallthrough: true);
            CompileOptions[compiledType] = (sig.Params[0].Type, sig);
        }
    }
        
    
    public static bool TryCompileOption(Type compiledType, out (Type source, MethodSignature mi) compile) {
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
    
    public static readonly Dictionary<Type, (FallthroughAttribute fa, MethodSignature mi)> FallThroughOptions =
        new();

}
}