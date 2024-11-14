using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.Reflection;
using Danmokou.SM;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using Scriptor;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Expressions;
using Scriptor.Reflection;
using AST = Scriptor.Compile.AST;
using IAST = Scriptor.Compile.IAST;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou {

public class DMKLanguageServiceProvider : ILangCustomizer {
    public DMKLanguageServiceProvider() {
        GlobalScope.Singleton = new DMKScope();
        ServiceLocator.Register<ILangCustomizer>(this);
    }

    D ILangCustomizer.CompileDelegate<D>(Func<TExArgCtx, TEx> func, params IDelegateArg[] args) =>
        CompilerHelpers.PrepareDelegate<D>(func, args).Compile();

    AOTMode ILangCustomizer.AOTMode =>
#if EXBAKE_SAVE
        AOTMode.Save;
#elif EXBAKE_LOAD
        AOTMode.Load;
#else
        AOTMode.None;
#endif

    Func<ST.Import, Either<EnvFrame, ReflectionException>> ILangCustomizer.Import { get; set; } =
        imp => StateMachineManager.LoadImport(imp.File.Content);

    void ILangCustomizer.Declare(LexicalScope s, PositionRange p, int autoVarMethod) =>
        AutoVarHelper.AutoDeclare(s, p, (AutoVarMethod)autoVarMethod);

    void ILangCustomizer.Extend(LexicalScope s, PositionRange p, int autoVarExtend, string? key) =>
        AutoVarHelper.AutoExtend(s, p, (AutoVarExtend)autoVarExtend, key);

    void ILangCustomizer.AttachEFToLambdaLikeObject(AST ast, Unifier u) {
        if (ast.LocalScope is null) return;
        foreach (var prm in ast.Params)
            RecurseChild(prm);
        
        void RecurseChild(IAST child) {
            if (child.EnclosingScope != ast.LocalScope) return;
            if (child is AST.MethodCall { SelectedOverload: { } so } meth
                && so.method.Mi.GetAttribute<AssignsAttribute>() is null
                && so.simplified.Last.Resolve(u).TryL(out var typ)
                && DMKScope.useEfWrapperTypes.Contains(typ)) {
                if (typ == typeof(StateMachine))
                    meth.SameTypeCast = AttachEFSMConv.Singleton;
                else if (typ == typeof(AsyncPattern))
                    meth.SameTypeCast = AttachEFAPConv.Singleton;
                else if (typ == typeof(SyncPattern))
                    meth.SameTypeCast = AttachEFSPConv.Singleton;
                else throw new StaticException($"Wrong type for attaching envframe: {typ.RName()}");
                return;
            }
            foreach (var prm in child.Params)
                RecurseChild(prm);
        }
    }

    public static object? AttachScope(object? prm, LexicalScope scope) => prm switch {
        GenCtxProperties props => EnvFrameAttacher.AttachScopePropsAny(props, scope),
        GenCtxProperty[] props => EnvFrameAttacher.ExtendScopeProps(props, scope),
        StateMachine sm => EnvFrameAttacher.AttachScopeSM(sm, scope),
        _ => prm
    };

    Ex ILangCustomizer.AttachScopeToScopeAwareObject(TExArgCtx tac, Ex prm, Type typ, LexicalScope scope) {
#if !EXBAKE_SAVE && !EXBAKE_LOAD
        if (prm is ConstantExpression ce)
            return Ex.Constant(AttachScope(ce.Value, scope), ce.Type);
#endif
        if (typ == typeof(StateMachine))
            return EnvFrameAttacher.attachScopeSM.Of(prm, Ex.Constant(tac.Proxy(scope)));
        if (typ == typeof(GenCtxProperty[])) {
#if !EXBAKE_SAVE && !EXBAKE_LOAD
            if (prm is NewArrayExpression ne && prm.NodeType == ExpressionType.NewArrayInit) {
                return Ex.NewArrayInit(typeof(GenCtxProperty), ne.Expressions.Append(
                    Ex.Constant(GenCtxProperty._AssignLexicalScope(scope))
                ));
            } else
#endif
                return EnvFrameAttacher.extendScopeProps.Of(prm, Ex.Constant(tac.Proxy(scope)));
        }
        if (typ.IsGenericType && typ.GetGenericTypeDefinition() == typeof(GenCtxProperties<>)) {
            return EnvFrameAttacher.attachScopeProps.Specialize(prm.Type.GetGenericArguments())
                .InvokeEx(prm, Ex.Constant(tac.Proxy(scope)));
        }
        return prm;
    }

    LString? ILangCustomizer.TryFindLocalizedStringReference(string content) => LocalizedStrings.TryFindReference(content);

    DocumentSymbol? ILangCustomizer.CustomSymbolTree(IDebugAST ast) {
        if (ast is not AST.MethodCall mast) return null;
        if (mast.SelectedOverload?.method is not { } m) return null;
        try {
            if (m.Mi.IsCtor && m.Mi.ReturnType == typeof(PhaseSM) &&
                (Ex)mast.Params[1].Realize(new TExArgCtx()) is ConstantExpression { Value: PhaseProperties props }) {
                return new($"{props.phaseType?.ToString() ?? "Phase"}", props.cardTitle?.Value ?? "",
                    m.Mi.Member.Symbol(), mast.Position.ToRange(), 
                    mast.FlattenParams((p, i) => p.ToSymbolTree($"({m.Params[i].Name})")));
            }
        } catch {
            //pass
        }
        return null;
    }

    SemanticToken ILangCustomizer.FromMethod(IMethodSignature mi, PositionRange p, string? tokenType, Type? retType) =>
        SemanticTokenHelpers.FromMethod(mi, p, tokenType, retType);
}
}