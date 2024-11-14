using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reflection;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Functional;
using BagoumLib.Unification;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.SM;
using Scriptor;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Expressions;
using Scriptor.Reflection;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection {
/// <summary>
/// The static scope that contains all DMK reflection methods. Variables cannot be declared in this scope.
/// </summary>
public class DMKScope : GlobalScope {
    private static readonly Dictionary<Type, IImplicitTypeConverter> compilerConverters;
    private static readonly GenericMethodConv1 gcxfConverter = 
        new(GetGenericCompilerMethod(nameof(Compilers.GCXF)));
    private static readonly GenericMethodConv1 efScopeGcxfConverter =
        //don't use `gcxfConverter with {...}` as that will break NextInstance handling
        //EFScopedExpression is required so EF can be attached to the SM/ASync/Sync "lambda"
        new(GetGenericCompilerMethod(nameof(Compilers.GCXF))) { Kind = ScopedConversionKind.EFScopedExpression };
    private static readonly Dictionary<Type, IImplicitTypeConverter> lowPriConverters;
    
    static DMKScope() {
        compilerConverters = new() {
            [typeof(ErasedGCXF)] = new MethodConv1(GetCompilerMethod(nameof(Compilers.ErasedGCXF))),
            [typeof(ErasedParametric)] = new MethodConv1(GetCompilerMethod(nameof(Compilers.ErasedParametric)))
        };
        //Simple expression compilers such as ExVTP -> VTP
        foreach (var m in typeof(Compilers).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.GetCustomAttribute<ExpressionBoundaryAttribute>() != null &&
                                 m.GetCustomAttribute<FallthroughAttribute>() != null &&
                                 !m.IsGenericMethodDefinition)) {
            compilerConverters[m.ReturnType] = new MethodConv1(MethodSignature.Get(m));
        }
        lowPriConverters = new() {
            [typeof(TP3)] = new MethodConv1(GetCompilerMethod(nameof(Compilers.TP3FromVec2)))
        };
    }

    public override IImplicitTypeConverter? GetConverterForCompiledExpressionType(Type compiledType) {
        if (compilerConverters.TryGetValue(compiledType, out var conv))
            return conv;
        else if (compiledType.IsGenericType && compiledType.GetGenericTypeDefinition() == typeof(GCXF<>))
            return useEfWrapperTypes.Contains(compiledType.GetGenericArguments()[0]) 
                ? efScopeGcxfConverter : gcxfConverter;
        return null;
    }

    public override IImplicitTypeConverter? TryFindLowPriorityConversion(TypeDesignation to, TypeDesignation from) {
        var invoke = TypeDesignation.Dummy.Method(to, from);
        if (to.Resolve().LeftOrNull is { } toT && lowPriConverters.TryGetValue(toT, out var conv) &&
            conv.NextInstance.MethodType.Unify(invoke, Unifier.Empty).IsLeft)
            return conv;
        return null;
    }

    public static readonly Type[] useEfWrapperTypes =
        { typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern) };
    [RestrictTypes(0, typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern))]
    private static Func<TExArgCtx, TEx<(EnvFrame, T)>> ConstToEFExpr<T>(T val) => 
        tac => ExUtils.Tuple<EnvFrame, T>(tac.EnvFrame, Ex.Constant(val));

    private static MethodSignature GetCompilerMethod(string name) =>
        MethodSignature.Get(
            typeof(Compilers).GetMethod(name, BindingFlags.Public | BindingFlags.Static) ??
            throw new StaticException($"Compiler method `{name}` not found"));
    
    private static GenericMethodSignature GetGenericCompilerMethod(string name) =>
        GetCompilerMethod(name) as GenericMethodSignature ?? 
        throw new StaticException($"Compiler method `{name}` is not generic");

    public override TypeResolver Resolver { get; } = new(
        //T -> T[]
        new SingletonToArrayConv(),

        FixedImplicitTypeConv<string, LString>.FromFn(x => x),

        //hoist constructor (can't directly use generic class constructor)
        new GenericMethodConv1((GenericMethodSignature)MethodSignature.Get(typeof(ReflectConstructors)
            .GetMethod(nameof(ReflectConstructors.H), BindingFlags.Public | BindingFlags.Static)!)),
        //uncompiledCode helper
        new GenericMethodConv1((GenericMethodSignature)MethodSignature.Get(typeof(Compilers)
            .GetMethod(nameof(Compilers.Code), BindingFlags.Public | BindingFlags.Static)!)),
        
        //language doesn't support double implicit conversion float->GCXF->Synchronizer
        //so we make this custom converter
        //note that float->GCXF is always constant but GCXF->Synchronizer is only constant in non-AOT
        new FixedImplicitTypeConv<float, Synchronizer>(
            exf => tac => {
                var gcxf = Ex.Constant(Compilers.GCXF(TExLambdaTyper.Convert<float>(exf)));
                if (ServiceLocator.Find<ILangCustomizer>().AOTMode is AOTMode.None)
                    return Synchronization.timeMeth.InvokeExIfNotConstant(gcxf);
                else
                    return Synchronization.timeMeth.InvokeEx(gcxf);
            }) { Kind = ScopedConversionKind.BlockScopedExpression },

        FixedImplicitTypeConv<int, float>.FromFn(x => x, allowConst: true),
        FixedImplicitTypeConv<Vector2, Vector3>.FromFn(v2 => v2, allowConst: true),
        
        //these are from Reflector.autoConstructorTypes
        FixedImplicitTypeConv<GenCtxProperty[],GenCtxProperties<SyncPattern>>.FromFn(props => new(props)),
        FixedImplicitTypeConv<GenCtxProperty[],GenCtxProperties<AsyncPattern>>.FromFn(props => new(props)),
        FixedImplicitTypeConv<GenCtxProperty[],GenCtxProperties<StateMachine>>.FromFn(props => new(props)),
        FixedImplicitTypeConv<SBOption[],SBOptions>.FromFn(props => new(props)),
        FixedImplicitTypeConv<LaserOption[],LaserOptions>.FromFn(props => new(props)),
        FixedImplicitTypeConv<BehOption[],BehOptions>.FromFn(props => new(props)),
        FixedImplicitTypeConv<PowerAuraOption[],PowerAuraOptions>.FromFn(props => new(props)),
        FixedImplicitTypeConv<PhaseProperty[],PhaseProperties>.FromFn(props => new(props)),
        FixedImplicitTypeConv<PatternProperty[],PatternProperties>.FromFn(props => new(props)),
        FixedImplicitTypeConv<string,StyleSelector>.FromFn(sel => new(sel, false)),
        FixedImplicitTypeConv<string[],StyleSelector>.FromFn(sel => new(new[]{sel}, false)),
        FixedImplicitTypeConv<string[][],StyleSelector>.FromFn(sel => new(sel, false)),
        //Value-typed auto constructors
        FixedImplicitTypeConv<BulletManager.exBulletControl, BulletManager.cBulletControl>.FromFn(c => new(c), 
            ScopedConversionKind.BlockScopedExpression)
    );

    internal DMKScope() : base(DefaultGlobalScope.ExtensionTypes) { }

    private readonly Dictionary<string, List<MethodSignature>> smInitMultiDecls = new();
    public override List<MethodSignature>? StaticMethodDeclaration(string name) {
        if (smInitMultiDecls.TryGetValue(name, out var results))
            return results;
        MethodSignature? smSig = null;
        if (StateMachine.SMInitMap.TryGetValue(name, out var typ)) {
            //SM constructors are constable
            smSig = Reflector.GetConstructorSignature(typ);
            smSig.Flags |= MethodFlags.ConstableNonAOT;
        }
        if (Reflector.ReflectionData.AllBDSL2Methods.TryGetValue(name, out results)) {
            if (smSig != null)
                return smInitMultiDecls[name] = results.Append(smSig).ToList();
            return results;
        }
        return smSig != null ? 
            smInitMultiDecls[name] = new List<MethodSignature> { smSig } : 
            null;
    }
}

}