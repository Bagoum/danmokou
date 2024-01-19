using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reflection;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.SM;
using Mizuhashi;
using Ex = System.Linq.Expressions.Expression;
using ExVTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<Danmokou.Expressions.VTPExpr>>;


namespace Danmokou.Reflection2 {
/// <summary>
/// Enum describing the type and visibility of a lexical scope.
/// </summary>
public enum LexicalScopeType {
    /// <summary>
    /// A scope in the script code created by standard code flow controls that is always visible.
    /// </summary>
    Standard,
    
    /// <summary>
    /// A scope resulting from <see cref="CreatesInternalScopeAttribute"/>, which is only visible
    ///  to GCXF and other compiled expressions at *SM execution time* and not at *script execution time*.
    /// </summary>
    CompiledExpressionScope,
    
    /// <summary>
    /// A scope within a compiled expression tree, whose variables are realized as Ex.Block parameters.
    /// </summary>
    ExpressionInternal,
}

/// <summary>
/// A lexical scope in script code.
/// </summary>
public class LexicalScope {
    public static readonly Stack<LexicalScope> OpenLexicalScopes = new ();
    public class ParsingScope : IDisposable {
        public ParsingScope(LexicalScope Scope) {
            OpenLexicalScopes.Push(Scope);
        }

        public void Dispose() => OpenLexicalScopes.Pop();
    }

    public static LexicalScope NewTopLevelScope() => new(DMKScope.Singleton);
    public static LexicalScope NewTopLevelDynamicScope() => new DynamicLexicalScope(DMKScope.Singleton);
    
    public DMKScope GlobalRoot { get; protected init; }
    public LexicalScope ScriptRoot { get; }
    public LexicalScope? Parent { get; protected set; }
    public readonly Dictionary<string, VarDecl> varsAndFns = new();
    public IEnumerable<VarDecl> AllVarsInAllScopes => varsAndFns.Values.Concat(Children.SelectMany(c => c.AllVarsInAllScopes));
    
    /// <summary>
    /// A list of variables automatically declared in this scope.
    /// </summary>
    public AutoVars? AutoVars { get; private set; }
    private List<LexicalScope> Children { get; } = new();
    
    public IScopedTypeConverter? Converter { get; private set; }

    public LexicalScope UseConverter(IScopedTypeConverter? conv) {
        Converter = conv;
        if (conv is { Kind : ScopedConversionKind.ScopedExpression })
            Type = LexicalScopeType.ExpressionInternal;
        return this;
    }
    
    /// <inheritdoc cref="LexicalScopeType"/>
    public LexicalScopeType Type { get; private set; }

    /// <summary>
    /// True iff this scope is or is contained within a compiled expression.
    /// <br/>Any such scopes use Ex.Block instead of environment frames (see <see cref="UseEF"/>).
    /// </summary>
    public bool WithinExpressionInternalScope => Type is LexicalScopeType.ExpressionInternal ||
                                                 (Parent?.WithinExpressionInternalScope ?? false);

    /// <summary>
    /// True iff this scope is instantiated by an environment frame
    /// (false if it is instantiated by Expression.Block or has no variables).
    /// <br/>This is finalized in <see cref="FinalizeVariableTypes"/>.
    /// </summary>
    public bool UseEF { get; private set; } = true;

    /// <summary>
    /// The final set of variables declared in this scope. 
    /// <br/>This is only non-empty after <see cref="FinalizeVariableTypes"/> is called.
    /// </summary>
    public (Type type, VarDecl[] decls)[] VariableDecls { get; protected set; } = null!;
    
    /// <summary>
    /// The map of types to their index in <see cref="VariableDecls"/>.
    /// <br/>This is only non-empty after <see cref="FinalizeVariableTypes"/> is called.
    /// </summary>
    public Dictionary<Type, int> TypeIndexMap { get; } = new();
    
    /// <summary>
    /// The dynamic scope that instantiated this one for a particular caller, if such a dynamic scope exists.
    /// </summary>
    public DynamicLexicalScope? DynRealizeSource { get; }

    public LexicalScope(DynamicLexicalScope copyFrom, LexicalScope newParent) {
        this.Parent = newParent;
        this.GlobalRoot = Parent.GlobalRoot;
        if (Parent is not DMKScope) {
            Parent.Children.Add(this);
            ScriptRoot = Parent.ScriptRoot;
        } else {
            ScriptRoot = this;
        }
        varsAndFns = copyFrom.varsAndFns;
        AutoVars = copyFrom.AutoVars;
        //don't copy children
        UseEF = copyFrom.UseEF;
        Converter = copyFrom.Converter;
        Type = copyFrom.Type;
        VariableDecls = copyFrom.VariableDecls;
        TypeIndexMap = copyFrom.TypeIndexMap;
        DynRealizeSource = copyFrom;
    }
    
    //Constructor for DMKScope
    protected LexicalScope() {
        GlobalRoot = null!;
        ScriptRoot = null!;
    }
    
    protected LexicalScope(LexicalScope parent) {
        this.Parent = parent;
        this.GlobalRoot = parent.GlobalRoot;
        if (parent is not DMKScope) {
            parent.Children.Add(this);
            ScriptRoot = parent.ScriptRoot;
        } else {
            ScriptRoot = this;
        }
    }

    /// <summary>
    /// Create a lexical scope that is a child of the provided parent scope.
    /// </summary>
    public static LexicalScope Derive(LexicalScope parent, IScopedTypeConverter? converter = null) {
        var sc = parent is DynamicLexicalScope ? 
            new DynamicLexicalScope(parent).UseConverter(converter): 
            new LexicalScope(parent).UseConverter(converter);
        if (converter != null)
            sc.DeclareImplicitArgs(converter.ScopeArgs ?? Array.Empty<IDelegateArg>());
        return sc;
    }

    public void UpdateParent(LexicalScope newParent) {
        Parent?.Children.Remove(this);
        Parent = newParent;
        if (Parent is not DMKScope)
            Parent.Children.Add(this);
    }

    public virtual void DeclareImplicitArgs(params IDelegateArg[] args) {
        foreach (var a in args)
            if (!string.IsNullOrWhiteSpace(a.Name)) {
                var decl = a.MakeImplicitArgDecl();
                decl.DeclarationScope = this;
                varsAndFns[a.Name] = decl;
            }
    }

    /// <summary>
    /// Declare a variable or function in this lexical scope.
    /// <br/>This variable/function can shadow variables/functions from parent scopes, but cannot redeclare a name already used in this scope.
    /// </summary>
    /// <returns>Left if declaration succeeded; Right if a variable or function already exists with the same name local to this scope.</returns>
    public Either<Unit, VarDecl> DeclareVariable(VarDecl decl) {
        if (decl.Hoisted && Parent is { } p and not DMKScope) {
            return p._DeclareVariable(decl);
        } else
            return _DeclareVariable(decl);
    }

    protected virtual Either<Unit, VarDecl> _DeclareVariable(VarDecl decl) {
        if (varsAndFns.TryGetValue(decl.Name, out var prev))
            return prev;
        varsAndFns[decl.Name] = decl;
        decl.DeclarationScope = this;
        return Unit.Default;
    }

    /// <summary>
    /// Declare a variable or function in this lexical scope.
    /// <br/>This variable/function can shadow variables/functions from parent scopes, but will throw if
    ///  it is already declared in this scope.
    /// </summary>
    /// <returns>The provided declaration.</returns>
    public VarDecl DeclareOrThrow(VarDecl decl) {
        if (DeclareVariable(decl).IsLeft)
            return decl;
        throw new Exception($"Failed to declare variable {decl.Name}<{decl.KnownType?.ExRName()}>");
    }

    public AutoVars AutoDeclareVariables(PositionRange p, AutoVarMethod method) {
        if (AutoVars != null)
            throw new StaticException("AutoVars already declared in this scope");
        VarDecl Declare<T>(string name) => DeclareOrThrow(new(p, false, typeof(T), name));
        return AutoVars = method switch {
            AutoVarMethod.GenCtx => new AutoVars.GenCtx(
                Declare<float>("i"), Declare<float>("pi"), Declare<V2RV2>("rv2"), 
                Declare<V2RV2>("brv2"), Declare<float>("st"), Declare<float>("times")),
            _ => new AutoVars.None()
        };
    }
    public void AutoDeclareExtendedVariables(PositionRange p, AutoVarExtend method, string? key = null) {
        if (AutoVars == null)
            throw new StaticException("AutoVars not yet declared in this scope");
        VarDecl Declare<T>(string name) => DeclareOrThrow(new(p, false, typeof(T), name));
        var gcxAV = (AutoVars.GenCtx)AutoVars;
        switch (method) {
            case AutoVarExtend.BindAngle:
                gcxAV.bindAngle = Declare<float>("angle");
                break;
            case AutoVarExtend.BindItr:
                gcxAV.bindItr = Declare<float>(key ?? throw new StaticException("Target not provided for bindItr"));
                break;
            case AutoVarExtend.BindLR:
                gcxAV.bindLR = (Declare<float>("lr"), Declare<float>("rl"));
                break;
            case AutoVarExtend.BindUD:
                gcxAV.bindUD = (Declare<float>("ud"), Declare<float>("du"));
                break;
            case AutoVarExtend.BindArrow:
                gcxAV.bindArrow = (Declare<float>("axd"), Declare<float>("ayd"), Declare<float>("aixd"), Declare<float>("aiyd"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(method), method, null);
        }
        
    }

    /// <summary>
    /// Find a declared variable in this scope or any parent scope.
    /// </summary>
    public VarDecl? FindDeclaration(string name) => FindScopedDeclaration(name)?.Item2;

    /// <summary>
    /// Find a declared variable in this scope or any parent scope.
    /// </summary>
    public virtual (LexicalScope, VarDecl)? FindScopedDeclaration(string name) {
        if (varsAndFns.TryGetValue(name, out var d))
            return (this, d);
        return Parent?.FindScopedDeclaration(name);
    }

    public virtual List<MethodSignature>? FindStaticMethodDeclaration(string name) {
        return Parent?.FindStaticMethodDeclaration(name);
    }

    public virtual Either<Unit, TypeUnifyErr> FinalizeVariableTypes(Unifier u) {
        return varsAndFns.Values
            .SequenceL(v => v.FinalizeType(u))
            .BindL(_ => Children
                .SequenceL(c => c.FinalizeVariableTypes(u)))
            .FMapL(_ => {
                //note that Identifier is not necessarily ordered
                // since dict.Values as well as GroupBy may not be stable.
                VariableDecls = varsAndFns.Values
                    .GroupBy(v => v.FinalizedType!)
                    .Select((g, ti) => {
                        TypeIndexMap[g.Key] = ti;
                        var decls = g.OrderBy(d => d.Position.Start.Index).ToArray();
                        for (int ii = 0; ii < decls.Length; ++ii) {
                            decls[ii].TypeIndex = ti;
                            decls[ii].Index = ii;
                        }
                        return (g.Key, decls);
                    })
                    .ToArray();
                //TODO: (optimization) if all variables are unchanging AND
                // none are referenced by function declarations or compiled functions (hard part), use Ex.Block
                UseEF = !WithinExpressionInternalScope && (varsAndFns.Count > 0 || Parent is DMKScope);
                return Unit.Default;
            });
    }


    /// <summary>
    /// Get an expression representing a readable/writeable variable
    ///  stored locally in an environment frame for this scope,
    ///  or stored locally in any parent environment frame.
    /// <br/>The returned expression is in the form
    ///  `(envFrame.Parent.Parent....FrameVars[i] as FrameVars{T}).Values[j]`.
    /// <br/>Note that MSIL will optimize out the common frameVars access if many variables are referenced.
    /// </summary>
    public Ex LocalOrParent(TExArgCtx tac, Ex envFrame, VarDecl variable, out int parentage) {
        if (varsAndFns.TryGetValue(variable.Name, out var v) && v == variable) {
            parentage = 0;
            return variable.Value(envFrame, tac);
        }
        if (Parent is null || this is DynamicLexicalScope)
            throw new Exception($"Could not find a scope containing variable {variable}");
        //todo skip-parent handling
        var ret = Parent.LocalOrParent(tac, UseEF ? envFrame.Field(nameof(EnvFrame.Parent)) : envFrame, variable, out parentage);
        if (UseEF)
            ++parentage;
        return ret;
    }

    /// <inheritdoc cref="LocalOrParent(Danmokou.Expressions.TExArgCtx,Ex,Danmokou.Reflection2.VarDecl,out int)"/>
    public Ex LocalOrParent(TExArgCtx tac, Type t, string varName, out VarDecl decl, out int parentage) {
        decl = FindDeclaration(varName) ??
               throw new Exception($"There is no variable {varName}<{t.ExRName()}> accessible in this lexical scope.");
        if (decl.FinalizedType != t)
            throw new Exception($"Variable {varName} actually has type {decl.FinalizedType?.ExRName()}, but was" +
                                $"accessed as {t.ExRName()}");
        return LocalOrParent(tac, tac.EnvFrame, decl, out parentage);
    }

    /// <inheritdoc cref="LocalOrParent(Danmokou.Expressions.TExArgCtx,Ex,Danmokou.Reflection2.VarDecl,out int)"/>
    public Ex? TryGetLocalOrParent(TExArgCtx tac, Type t, string varName, out VarDecl? decl, out int parentage) {
        decl = FindDeclaration(varName);
        if (decl == null || decl.FinalizedType != t) {
            parentage = 0;
            return null;
        }
        return LocalOrParent(tac, tac.EnvFrame, decl, out parentage);
    }

    
    /// <summary>
    /// Get an expression representing a readable/writeable variable
    ///  stored locally in an environment frame for X's scope,
    ///  or stored locally in any parent environment frame of X,
    /// for *any* envframe X that might be passed.
    /// </summary>
    public static Ex VariableWithoutLexicalScope(TExArgCtx tac, string varName, Type typ, Ex? deflt = null, Func<Ex, Ex>? opOnValue = null) {
        Ex ef = tac.EnvFrame;
        if (tac.Ctx.UnscopedEnvframeAcess.TryGetValue((varName, typ), out var keys)) {
            var (p, t, v) = keys;
            return (opOnValue ?? noOpOnValue)(EnvFrame.Value(ef.DictGet(p), t, v, typ));
        } else {
            var parentage = ExUtils.V<int>("parentage");
            var typeIdx = ExUtils.V<int>("typeIdx");
            var valueIdx = ExUtils.V<int>("valueIdx");
            //We cache the out parameters of the instructions call, but only within the scope of the `opOnValue` call.
            // This allows cases like `updatef { var1 (&var1 + 1) }`/`&var1 = &var1 + 1` to be handled efficiently.
            tac.Ctx.UnscopedEnvframeAcess[(varName, typ)] = (parentage, typeIdx, valueIdx);
            var ret = Ex.Block(new[] { parentage, typeIdx, valueIdx },
                Ex.Condition(getInstructions.InstanceOf(Ex.PropertyOrField(ef, nameof(EnvFrame.Scope)), 
                        Ex.Constant((varName, typ)), parentage, typeIdx, valueIdx),
                    (opOnValue ?? noOpOnValue)(EnvFrame.Value(ef.DictGet(parentage), typeIdx, valueIdx, typ)),
                    deflt ??
                    Ex.Throw(Ex.Constant(new Exception(
                        $"The variable {varName}<{typ.ExRName()}> was referenced in an unscoped context " +
                        $"(eg. a bullet control), but some bullets do not have this variable defined")), typ))
            );
            tac.Ctx.UnscopedEnvframeAcess.Remove((varName, typ));
            return ret;
        }
    }

    private static readonly Func<Ex, Ex> noOpOnValue = x => x;

    private readonly Dictionary<(string, Type), (bool, int, int, int)> instructionsCache = new();
    private static readonly List<LexicalScope> unwinder = new();
    private static readonly ExFunction getInstructions = 
        ExFunction.WrapAny(typeof(LexicalScope), nameof(GetLocalOrParentInstructions));
    
    /// <summary>
    /// Get instructions on where the given variable is stored in an envframe with this scope.
    /// </summary>
    public bool GetLocalOrParentInstructions((string name, Type t) var, out int parentage, out int typeIdx, out int valueIdx) {
        bool ret = false;
        if (instructionsCache.TryGetValue(var, out var inst)) {
            (ret, parentage, typeIdx, valueIdx) = inst;
            return ret;
        }
        unwinder.Clear();
        parentage = 0;
        typeIdx = 0;
        valueIdx = 0;
        for (var scope = this; scope != null; scope = scope.Parent) {
            unwinder.Add(scope);
            if (scope.instructionsCache.TryGetValue(var, out inst)) {
                (ret, parentage, typeIdx, valueIdx) = inst;
                break;
            }
            if (scope.varsAndFns.TryGetValue(var.name, out var v)) {
                if (v.FinalizedType == var.t) {
                    ret = true;
                    typeIdx = v.TypeIndex;
                    valueIdx = v.Index;
                } else
                    ret = false;
                break;
            }
        }
        for (int ii = unwinder.Count - 1; ii >= 0; --ii) {
            unwinder[ii].instructionsCache[var] = (ret, parentage, typeIdx, valueIdx);
            if (ii > 0 && unwinder[ii - 1].UseEF)
                ++parentage;
        }
        unwinder.Clear();
        return ret;
    }

}


/// <summary>
/// A lexical scope that can be realized with a parent envFrame from *any* scope
///  (by default, the parent envFrame must be from this scope's parent).
/// </summary>
public class DynamicLexicalScope : LexicalScope {
    private readonly Dictionary<LexicalScope, LexicalScope> parentScopeToRealizedScope = new();
    
    public DynamicLexicalScope(LexicalScope parent) : base(parent) { }

    /// <summary>
    /// Create a non-dynamic lexical scope that has the same content as this one.
    /// </summary>
    public LexicalScope RealizeScope(LexicalScope parentScope) =>
        parentScopeToRealizedScope.TryGetValue(parentScope, out var s) ?
            s :
            parentScopeToRealizedScope[parentScope] = new(this, parentScope);
}

/// <summary>
/// The static scope that contains all DMK reflection methods. Variables cannot be declared in this scope.
/// </summary>
public class DMKScope : LexicalScope {
    public static DMKScope Singleton { get; } = new() {
        VariableDecls = Array.Empty<(Type, VarDecl[])>()
    };

    private static readonly Dictionary<Type, IImplicitTypeConverter> compilerConverters;
    private static readonly IImplicitTypeConverter gcxfConverter = 
        new GenericMethodConv1(GetCompilerMethod(nameof(Compilers.GCXF)));
    
    static DMKScope() {
        compilerConverters = new() {
            [typeof(ErasedGCXF)] = new GenericMethodConv1(GetCompilerMethod(nameof(Compilers.ErasedGCXF))),
            [typeof(ErasedParametric)] = new GenericMethodConv1(GetCompilerMethod(nameof(Compilers.ErasedParametric)))
        };
        //Simple expression compilers such as ExVTP -> VTP
        foreach (var m in typeof(Compilers).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.GetCustomAttribute<ExpressionBoundaryAttribute>() != null &&
                                 m.GetCustomAttribute<FallthroughAttribute>() != null &&
                                 !m.IsGenericMethodDefinition)) {
            compilerConverters[m.ReturnType] = new MethodConv1(MethodSignature.Get(m));
        }
    }

    public static IImplicitTypeConverter? GetConverterForCompiledExpressionType(Type compiledType) {
        if (compilerConverters.TryGetValue(compiledType, out var conv))
            return conv;
        else if (compiledType.IsGenericType && compiledType.GetGenericTypeDefinition() == typeof(GCXF<>))
            return gcxfConverter;
        return null;
    }

    public static readonly Type[] useEfWrapperTypes =
        { typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern) };
    [RestrictTypes(0, typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern))]
    private static Func<TExArgCtx, TEx<(EnvFrame, T)>> ConstToEFExpr<T>(T val) => 
        tac => ExUtils.Tuple<EnvFrame, T>(tac.EnvFrame, Ex.Constant(val));

    private static GenericMethodSignature GetCompilerMethod(string name) =>
        MethodSignature.Get(
            typeof(Compilers).GetMethod(name, BindingFlags.Public | BindingFlags.Static) ??
            throw new StaticException($"Compiler method `{name}` not found")) 
            as GenericMethodSignature ?? 
        throw new StaticException($"Compiler method `{name}` is not generic");

    public static readonly AttachEFSMConv AttachEFtoSMImplicit = new();
    public static readonly AttachEFAPConv AttachEFtoAPImplicit = new();
    public static readonly AttachEFSPConv AttachEFtoSPImplicit = new();
    public static TypeResolver BaseResolver { get; } = new(
            //T -> T[]
            new SingletonToArrayConv(),
        
            FixedImplicitTypeConv<string, LString>.FromFn(x => x),
            
            //hoist constructor (can't directly use generic class constructor)
            new GenericMethodConv1((GenericMethodSignature)MethodSignature.Get(typeof(ReflectEx).GetMethod(nameof(ExM.H))!)),
            
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
            FixedImplicitTypeConv<string,BulletManager.StyleSelector>.FromFn(sel => new(sel)),
            FixedImplicitTypeConv<string[][],BulletManager.StyleSelector>.FromFn(sel => new(sel)),
            //Value-typed auto constructors
            FixedImplicitTypeConv<BulletManager.exBulletControl,BulletManager.cBulletControl>.FromFn(c => new(c))
    );
    public TypeResolver Resolver => BaseResolver;

    private DMKScope() : base() {
        GlobalRoot = this;
    }
    public override (LexicalScope, VarDecl)? FindScopedDeclaration(string name) {
        return null;
    }

    private readonly Dictionary<string, List<MethodSignature>> smInitMultiDecls = new();
    public override List<MethodSignature>? FindStaticMethodDeclaration(string name) {
        if (smInitMultiDecls.TryGetValue(name, out var results))
            return results;
        if (Reflector.ReflectionData.AllBDSL2Methods.TryGetValue(name, out results)) {
            if (StateMachine.SMInitMethodMap.TryGetValue(name, out var typ2)) {
                return smInitMultiDecls[name] = results.Concat(typ2).ToList();
            }
            return results;
        }
        if (StateMachine.SMInitMethodMap.TryGetValue(name, out var typ)) {
            return typ;
        }
        return null;
    }

    public override void DeclareImplicitArgs(params IDelegateArg[] args) =>
        throw new Exception("Do not declare arguments in DMKScope");

    protected override Either<Unit, VarDecl> _DeclareVariable(VarDecl decl) =>
        throw new Exception("Do not declare variables in DMKScope");

    public override Either<Unit, TypeUnifyErr> FinalizeVariableTypes(Unifier u) =>
        throw new Exception("Do not call FinalizeVariableTypes in DMKScope");
}


}