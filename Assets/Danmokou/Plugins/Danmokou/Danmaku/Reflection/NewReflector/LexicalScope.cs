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
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.SM;
using Mizuhashi;
using Ex = System.Linq.Expressions.Expression;
using ExVTP = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV3, Danmokou.Expressions.TEx>;


namespace Danmokou.Reflection2 {

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
    
    public DMKScope Root { get; protected set; }
    public LexicalScope? Parent { get; protected set; }
    public readonly Dictionary<string, VarDecl> varsAndFns = new();
    
    /// <summary>
    /// A list of variables automatically declared in this scope.
    /// </summary>
    public AutoVars? AutoVars { get; private set; }
    private List<LexicalScope> Children { get; } = new();

    /// <summary>
    /// True iff this scope is instantiated by an environment frame
    /// (false if it is instantiated by Expression.Block or has no variables).
    /// <br/>This is finalized in <see cref="FinalizeVariableTypes"/>.
    /// </summary>
    public bool UseEF { get; private set; } = true;
    
    public IScopedTypeConverter? Converter { get; init; }

    /// <summary>
    /// True iff this scope is or is contained within a <see cref="Converter"/>-provided scope with a kind
    ///  that disabled environment-frames.
    /// <br/>This is finalized in <see cref="FinalizeVariableTypes"/>.
    /// </summary>
    public bool WithinDisabledEFScope { get; private set; } = false;

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
        this.Root = Parent.Root;
        if (Parent is not DMKScope)
            Parent.Children.Add(this);
        varsAndFns = copyFrom.varsAndFns;
        AutoVars = copyFrom.AutoVars;
        //don't copy children
        UseEF = copyFrom.UseEF;
        Converter = copyFrom.Converter;
        WithinDisabledEFScope = copyFrom.WithinDisabledEFScope;
        VariableDecls = copyFrom.VariableDecls;
        TypeIndexMap = copyFrom.TypeIndexMap;
        DynRealizeSource = copyFrom;
    }
    
    //Constructor for DMKScope
    protected LexicalScope() {
        Root = null!;
    }
    
    protected LexicalScope(LexicalScope parent) {
        this.Parent = parent;
        this.Root = parent.Root;
        if (parent is not DMKScope)
            parent.Children.Add(this);
    }

    /// <summary>
    /// Create a lexical scope that is a child of the provided parent scope.
    /// </summary>
    public static LexicalScope Derive(LexicalScope parent, IScopedTypeConverter? converter = null) {
        var sc = parent is DynamicLexicalScope ? 
            new DynamicLexicalScope(parent) { Converter = converter } : 
            new LexicalScope(parent) { Converter = converter };
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
    public virtual Either<Unit, VarDecl> DeclareVariable(VarDecl decl) {
        if (Converter != null)
            throw new StaticException("Do not declare variables in conversion scopes");
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
        VarDecl Declare<T>(string name) => DeclareOrThrow(new(p, typeof(T), name));
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
        VarDecl Declare<T>(string name) => DeclareOrThrow(new(p, typeof(T), name));
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
        if (Converter is { Kind: not ScopedConversionKind.Trivial })
            WithinDisabledEFScope = true;
        else
            WithinDisabledEFScope = Parent?.WithinDisabledEFScope ?? false;
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
                UseEF = !WithinDisabledEFScope && (VariableDecls.Length > 0 || Parent is DMKScope);
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

    private static readonly Type[] useEfWrapperTypes =
        { typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern) };
    [RestrictTypes(0, typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern))]
    private static Func<TExArgCtx, TEx<(EnvFrame, T)>> ConstToEFExpr<T>(T val) => 
        tac => ExUtils.Tuple<EnvFrame, T>(tac.EnvFrame, Ex.Constant(val));
    private static T[] SingleToArray<T>(T single) => new[] { single };

    private static GenericMethodSignature GetCompilerMethod(string name) =>
        MethodSignature.Get(
            typeof(Compilers).GetMethod(name, BindingFlags.Public | BindingFlags.Static) ??
            throw new StaticException($"Compiler method `{name}` not found")) 
            as GenericMethodSignature ?? 
        throw new StaticException($"Compiler method `{name}` is not generic");

    public static TypeResolver BaseResolver { get; } = new(new IImplicitTypeConverter[] {
            //T -> Func<TExArgCtx, TEx<(EnvFrame, T)>>
            new GenericMethodConv1((MethodSignature.Get(
                    typeof(DMKScope).GetMethod(nameof(ConstToEFExpr), BindingFlags.Static | BindingFlags.NonPublic)!) as
                GenericMethodSignature)!),
            
            //T -> Func<TExArgCtx, TEx<T>>
            new ConstantToExprConv(useEfWrapperTypes),
            
            //scopeargs removed for now
            //Func<TExArgCtx, TEx<T>> -> GCXF<T>
            new GenericMethodConv1(GetCompilerMethod(nameof(Compilers.GCXF))) 
                { Kind = ScopedConversionKind.GCXFFunction },
            new GenericMethodConv1(GetCompilerMethod(nameof(Compilers.ErasedGCXF))) 
                { Kind = ScopedConversionKind.GCXFFunction },
            new GenericMethodConv1(GetCompilerMethod(nameof(Compilers.ErasedParametric))) 
                { Kind = ScopedConversionKind.SimpleFunction },
            
            //T -> T[]
            new GenericMethodConv1((MethodSignature.Get(
                    typeof(DMKScope).GetMethod(nameof(SingleToArray), BindingFlags.Static | BindingFlags.NonPublic)!) as
                GenericMethodSignature)!),
        
            new FixedImplicitTypeConv<string, LString>(x => x),
            //these are from Reflector.autoConstructorTypes
            new FixedImplicitTypeConv<GenCtxProperty[],GenCtxProperties<SyncPattern>>(props => new(props)),
            new FixedImplicitTypeConv<GenCtxProperty[],GenCtxProperties<AsyncPattern>>(props => new(props)),
            new FixedImplicitTypeConv<GenCtxProperty[],GenCtxProperties<StateMachine>>(props => new(props)),
            new FixedImplicitTypeConv<SBOption[],SBOptions>(props => new(props)),
            new FixedImplicitTypeConv<LaserOption[],LaserOptions>(props => new(props)),
            new FixedImplicitTypeConv<BehOption[],BehOptions>(props => new(props)),
            new FixedImplicitTypeConv<PowerAuraOption[],PowerAuraOptions>(props => new(props)),
            new FixedImplicitTypeConv<PhaseProperty[],PhaseProperties>(props => new(props)),
            new FixedImplicitTypeConv<PatternProperty[],PatternProperties>(props => new(props)),
            new FixedImplicitTypeConv<string,BulletManager.StyleSelector>(sel => new(sel)),
            new FixedImplicitTypeConv<string[][],BulletManager.StyleSelector>(sel => new(sel)),
            //Value-typed auto constructors
            new FixedImplicitTypeConv<BulletManager.exBulletControl,BulletManager.cBulletControl>(c => new(c)),
        }.Concat(
            //Simple expression compilers such as ExVTP -> VTP
            typeof(Compilers).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<ExpressionBoundaryAttribute>() != null && 
                            m.GetCustomAttribute<FallthroughAttribute>() != null && !m.IsGenericMethodDefinition)
                .Select(m => MethodSignature.AsImplicitTypeConvUntyped(
                                MethodSignature.Get(m), ScopedConversionKind.SimpleFunction))
        ).ToArray()
    );
    public TypeResolver Resolver => BaseResolver;

    private DMKScope() : base() {
        Root = this;
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

    public override Either<Unit, VarDecl> DeclareVariable(VarDecl decl) =>
        throw new Exception("Do not declare variables in DMKScope");

    public override Either<Unit, TypeUnifyErr> FinalizeVariableTypes(Unifier u) =>
        throw new Exception("Do not call FinalizeVariableTypes in DMKScope");
}


}