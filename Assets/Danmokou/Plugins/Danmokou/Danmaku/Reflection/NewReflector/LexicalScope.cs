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
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Reflection;
using Danmokou.Services;
using Danmokou.SM;
using JetBrains.Annotations;
using Mizuhashi;
using UnityEngine;
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
    /// A scope resulting from <see cref="CreatesInternalScopeAttribute"/>; ie. a scope on a method call.
    /// </summary>
    MethodScope,
    
    /// <summary>
    /// A scope within a compiled expression tree, whose variables are realized as Ex.Block parameters.
    /// </summary>
    ExpressionBlock,
    
    /// <summary>
    /// A scope within a compiled expression tree that uses EnvFrames to realize parameters.
    /// </summary>
    ExpressionEF,
}

public record UntypedReturn(ReturnStatementConfig Return) : TypeUnifyErr;

public class ReturnStatementConfig {
    public LexicalScope Scope { get; }
    public TypeDesignation Type { get; }
    public ScriptFnDecl Function { get; set; } = null!;
    public Type? FinalizedType { get; private set; }
    private LabelTarget? label;
    public LabelTarget Label =>
        label ?? throw new Exception("Cannot retrieve return label before type finalization");

    public ReturnStatementConfig(LexicalScope scope, Type? t) {
        Scope = scope;
        Type = t != null ? TypeDesignation.FromType(t) : new TypeDesignation.Variable();
    }

    public TypeUnifyErr? FinalizeType(Unifier u) {
        var t = Type.Resolve(u);
        if (t.IsRight)
            return new UntypedReturn(this);
        label = Ex.Label(FinalizedType = t.Left);
        return null;
    }
}

/// <summary>
/// A lexical scope in script code.
/// </summary>
public class LexicalScope {
    private static readonly Stack<LexicalScope> OpenLexicalScopes = new ();
    public static LexicalScope CurrentOpenParsingScope =>
        OpenLexicalScopes.Count > 0 ? OpenLexicalScopes.Peek() : DMKScope.Singleton;
    /// <summary>
    /// Proxy to allow TExArgCtx to access scoped variables (mainly for compatibility with BDSL1 logic).
    /// </summary>
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
    public int Depth => Parent is null ? 0 : 1 + Parent.Depth;
    public LexicalScope? Parent { get; protected set; }
    public readonly Dictionary<string, VarDecl> variableDecls = new();
    public readonly Dictionary<string, ScriptFnDecl> functionDecls = new();
    private readonly Dictionary<string, ScriptImport> _importDecls = new();
    private readonly Dictionary<string, MacroDecl> _macroDecls = new();
    public Dictionary<string, ScriptImport> ImportDecls => ScriptRoot._importDecls;
    public Dictionary<string, MacroDecl> MacroDecls => ScriptRoot._macroDecls;
    public IEnumerable<VarDecl> AllVarsInDescendantScopes => 
        variableDecls.Values.Concat(Children.SelectMany(c => c.AllVarsInDescendantScopes));
    public IEnumerable<ScriptFnDecl> AllFnsInDescendantScopes => 
        functionDecls.Values.Concat(Children.SelectMany(c => c.AllFnsInDescendantScopes));

    [PublicAPI]
    public IEnumerable<VarDecl> AllVisibleVars =>
        variableDecls.Values.Concat(Parent?.AllVisibleVars ?? Array.Empty<VarDecl>());
    [PublicAPI]
    public IEnumerable<ScriptFnDecl> AllVisibleScriptFns =>
        functionDecls.Values.Concat(Parent?.AllVisibleScriptFns ?? Array.Empty<ScriptFnDecl>());
    
    /// <summary>
    /// A list of variables automatically declared in this scope.
    /// </summary>
    public AutoVars? AutoVars { get; private set; }
    public AutoVars? NearestAutoVars => AutoVars ?? Parent?.AutoVars;
    private List<LexicalScope> Children { get; } = new();
    public ReturnStatementConfig? Return { get; set; }
    public (LabelTarget c, LabelTarget b, LexicalScope loopScope)? ContinueBreak { get; set; }
    public ReturnStatementConfig? NearestReturn =>
        Return ??
        //Return statements can't "escape" out of an expression or method scope, only out of standard block scope
        ((Type == LexicalScopeType.Standard) ? Parent?.NearestReturn : null);
    public (LabelTarget c, LabelTarget b, LexicalScope loopScope)? NearestContinueBreak => 
        ContinueBreak ??
        ((Type == LexicalScopeType.Standard) ? Parent?.NearestContinueBreak : null);
    
    public IScopedTypeConverter? Converter { get; private set; }

    public LexicalScope UseConverter(IScopedTypeConverter? conv) {
        Converter = conv;
        Type = conv?.Kind switch {
            ScopedConversionKind.BlockScopedExpression => LexicalScopeType.ExpressionBlock,
            ScopedConversionKind.EFScopedExpression => LexicalScopeType.ExpressionEF,
            _ => Type
        };
        return this;
    }

    /// <inheritdoc cref="LexicalScopeType"/>
    public LexicalScopeType Type { get; set; } = LexicalScopeType.Standard;
    
    /// <summary>
    /// If true, then content within this scope can only see constant declarations outside this scope.
    /// </summary>
    public bool IsConstScope { get; set; }

    /// <summary>
    /// True iff this scope is or is contained within a compiled expression that uses Ex.Block in place of
    ///  environment frames.
    /// </summary>
    public bool WithinExpressionBlockScope => Type is LexicalScopeType.ExpressionBlock ||
                                                 Type == LexicalScopeType.Standard &&
                                                 Parent?.WithinExpressionBlockScope is true;
    
    /// <summary>
    /// True iff this scope is or is contained within any compiled expression scope.
    /// <br/>If this is false (ie. there are only standard and method scopes until the root),
    ///  then references in this scope cannot see declarations made in <see cref="LexicalScopeType.MethodScope"/>.
    /// </summary>
    public bool WithinAnyExpressionScope => Type is LexicalScopeType.ExpressionBlock or LexicalScopeType.ExpressionEF ||
                                            Parent?.WithinAnyExpressionScope is true;

    /// <summary>
    /// True iff this scope is instantiated by an environment frame
    /// (false if it is instantiated by Expression.Block or has no variables).
    /// <br/>This is finalized in <see cref="FinalizeVariableTypes"/>.
    /// </summary>
    public bool UseEF { get; private set; } = true;

    /// <summary>
    /// The final set of variables declared in this scope (not including function parameters).
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
        IsConstScope = copyFrom.IsConstScope;
        variableDecls = copyFrom.variableDecls;
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
        IsConstScope = parent.IsConstScope;
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
                variableDecls[a.Name] = decl;
            }
    }

    /// <summary>
    /// Declare a variable or function in this lexical scope.
    /// <br/>This variable/function can shadow variables/functions from parent scopes, but cannot redeclare a name already used in this scope.
    /// </summary>
    /// <returns>Left if declaration succeeded; Right if a variable or function already exists with the same name local to this scope.</returns>
    public Either<Unit, IDeclaration> Declare(IDeclaration decl) {
        if (decl.Hoisted) {
            return HoistedScope._Declare(decl);
        } else
            return _Declare(decl);
    }

    /// <summary>
    /// The scope used for hoisted declarations. Generally just the parent scope, unless this
    ///  is already a top-level scope, in which case it is this scope.
    /// </summary>
    public LexicalScope HoistedScope => Parent is { } p and not DMKScope ? p : this;

    protected virtual Either<Unit, IDeclaration> _Declare(IDeclaration decl) {
        if (variableDecls.TryGetValue(decl.Name, out var prev))
            return prev;
        if (functionDecls.TryGetValue(decl.Name, out var prevf))
            return prevf;
        if (ImportDecls.TryGetValue(decl.Name, out var previmp))
            return previmp;
        if (MacroDecls.TryGetValue(decl.Name, out var prevmac))
            return prevmac;
        if (decl is VarDecl v) {
            variableDecls[v.Name] = v;
        } else if (decl is ScriptFnDecl fn) {
            functionDecls[fn.Name] = fn;
        } else if (decl is ScriptImport si) {
            ImportDecls[si.Name] = si;
        } else if (decl is MacroDecl md) {
            MacroDecls[md.Name] = md;
        } else
            throw new StaticException($"Unhandled declaration type: {decl.GetType()}");
        decl.DeclarationScope = this;
        return Unit.Default;
    }

    /// <summary>
    /// Declare a variable or function in this lexical scope.
    /// <br/>This variable/function can shadow variables/functions from parent scopes, but will throw if
    ///  it is already declared in this scope.
    /// </summary>
    /// <returns>The provided declaration.</returns>
    public T DeclareOrThrow<T>(T decl) where T : IDeclaration {
        if (Declare(decl).IsLeft)
            return decl;
        throw new Exception($"Failed to declare variable {decl.Name}");
    }

    public AutoVars AutoDeclareVariables(PositionRange p, AutoVarMethod method) {
        if (AutoVars != null)
            throw new StaticException("AutoVars already declared in this scope");
        VarDecl Declare<T>(string name, string comment) => DeclareOrThrow(new VarDecl(p, false, typeof(T), name) {
            DocComment = comment
        });
        return AutoVars = method switch {
            AutoVarMethod.GenCtx => new AutoVars.GenCtx(
                Declare<float>("i", "Iteration index (starting at 0) of this repeater"), 
                Declare<float>("pi", "Iteration index (starting at 0) of the parent repeater"), 
                Declare<V2RV2>("rv2", "Rotational coordinates"), 
                Declare<V2RV2>("brv2", "Rotational coordinates provided by parent function"), 
                Declare<float>("st", "Summon Time - the time in all repeaters that has passed since the last TimeReset GCXProp"), 
                Declare<float>("times", "Number of times this repeater will execute for"),
                Declare<float>("ir", "Iteration ratio = i / (times - 1)")),
            _ => new AutoVars.None()
        };
    }
    public void AutoDeclareExtendedVariables(PositionRange p, AutoVarExtend method, string? key = null) {
        if (AutoVars == null)
            throw new StaticException("AutoVars not yet declared in this scope");
        VarDecl Declare<T>(string name, string comment) => DeclareOrThrow(new VarDecl(p, false, typeof(T), name) {
            DocComment = comment
        });
        var gcxAV = (AutoVars.GenCtx)AutoVars;
        switch (method) {
            case AutoVarExtend.BindAngle:
                gcxAV.bindAngle = Declare<float>("angle", "The value copied from rv2.a");
                break;
            case AutoVarExtend.BindItr:
                gcxAV.bindItr = Declare<float>(key ?? throw new StaticException("Target not provided for bindItr"), "The value copied from i (iteration index)");
                break;
            case AutoVarExtend.BindLR:
                gcxAV.bindLR = (Declare<float>("lr", "1 if the iteration index is even, -1 if it is odd"), 
                    Declare<float>("rl", "-1 if the iteration index is even, 1 if it is odd"));
                break;
            case AutoVarExtend.BindUD:
                gcxAV.bindUD = (Declare<float>("ud", "1 if the iteration index is even, -1 if it is odd"), 
                    Declare<float>("du", "-1 if the iteration index is even, 1 if it is odd"));
                break;
            case AutoVarExtend.BindArrow:
                gcxAV.bindArrow = (Declare<float>("axd", "BindArrow axd"), Declare<float>("ayd", "BindArrow ayd"), 
                    Declare<float>("aixd", "BindArrow aixd"), Declare<float>("aiyd", "BindArrow aiyd"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(method), method, null);
        }
        
    }

    /// <summary>
    /// Get a list of expressions representing freeing environment frames
    ///  for each scope, going upwards, until (and not including) the scope that does not satisfy the condition.
    /// </summary>
    public List<Ex> FreeEfs(Ex ef, Func<LexicalScope, bool> cond) {
        var frees = new List<Ex>();
        for (var scope = this; cond(scope!); scope = scope.Parent)
            if (scope!.UseEF) {
                frees.Add(EnvFrame.exFree.InstanceOf(ef));
                ef = Ex.PropertyOrField(ef, nameof(EnvFrame.Parent));
            }
        //Need to free top-down because .Parent is not visible after freeing
        frees.Reverse();
        return frees;
    }

    public bool IsIssueOf(LexicalScope ancestor) {
        return this == ancestor || (Parent != null && Parent.IsIssueOf(ancestor));
    }

    /// <summary>
    /// Find a declared variable in this scope or any parent scope.
    /// </summary>
    public VarDecl? FindVariable(string name, DeclarationLookup flags = DeclarationLookup.Standard) => 
        FindScopedVariable(name, flags)?.Item2;

    /// <summary>
    /// Find a declared variable in this scope or any parent scope.
    /// </summary>
    //Note that we don't use WithinAnyExpressionScope here to filter; this is because
    // scope types aren't finalized until after ResolveUnifiers, while this code is called at AST creation time.
    public (LexicalScope, VarDecl)? FindScopedVariable(string name, DeclarationLookup flags) {
        if (variableDecls.TryGetValue(name, out var d)) {
            if (d.Constant && flags.HasFlag(DeclarationLookup.CONSTANT) || 
                !d.Constant && flags.HasFlag(DeclarationLookup.LEXICAL_SCOPE))
                return (this, d);
        }
        if (!flags.HasFlag(DeclarationLookup.DYNAMIC_SCOPE) && this is DynamicLexicalScope && Parent is not DynamicLexicalScope)
            return Parent?.FindScopedVariable(name, flags & DeclarationLookup.ConstOnly);
        if (IsConstScope && Parent?.IsConstScope is false)
            return Parent.FindScopedVariable(name, flags & DeclarationLookup.ConstOnly);
        return Parent?.FindScopedVariable(name, flags);
    }

    /// <summary>
    /// Find a declared script function in this scope or any parent scope.
    /// </summary>
    public ScriptFnDecl? FindScopedFunction(string name, DeclarationLookup flags) {
        if (functionDecls.TryGetValue(name, out var fn)) {
            if (fn.IsConstant && flags.HasFlag(DeclarationLookup.CONSTANT) || 
                !fn.IsConstant && flags.HasFlag(DeclarationLookup.LEXICAL_SCOPE))
                return fn;
        }
        if (!flags.HasFlag(DeclarationLookup.DYNAMIC_SCOPE) && this is DynamicLexicalScope && Parent is not DynamicLexicalScope)
            return Parent?.FindScopedFunction(name, flags & DeclarationLookup.ConstOnly);
        if (IsConstScope && Parent?.IsConstScope is false)
            return Parent.FindScopedFunction(name, flags & DeclarationLookup.ConstOnly);
        return Parent?.FindScopedFunction(name, flags);
    }

    public List<MethodSignature>? FindStaticMethodDeclaration(string name) => GlobalRoot.StaticMethodDeclaration(name);

    public Either<Unit, TypeUnifyErr> FinalizeVariableTypes(Unifier u) {
        return variableDecls.Values
            .SequenceL(v => v.FinalizeType(u))
            .FMapL(_ => {
                //note that Identifier is not necessarily ordered
                // since dict.Values as well as GroupBy may not be stable.
                VariableDecls = variableDecls.Values
                    .Where(x => x is not ImplicitArgDecl)
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
                UseEF = !WithinExpressionBlockScope && (variableDecls.Count > 0 || Parent is DMKScope);
                return Unit.Default;
            })
            .BindL(_ => functionDecls.Values.SequenceL(f => f.FinalizeType(u)))
            .BindL(_ => Children
                .SequenceL(c => c.FinalizeVariableTypes(u)))
            .BindL(_ => Return?.FinalizeType(u) is { } err ? new Either<Unit,TypeUnifyErr>(err) : Unit.Default);
    }

    /// <summary>
    /// Get an expression representing a readable/writeable variable
    ///  stored locally in an environment frame for this scope,
    ///  or stored locally in any parent environment frame.
    /// <br/>The returned expression is in the form
    ///  `(envFrame.Parent.Parent....FrameVars[i] as FrameVars{T}).Values[j]`.
    /// <br/>Note that MSIL will optimize out the common frameVars access if many variables are referenced.
    /// </summary>
    public Ex LocalOrParentVariable(TExArgCtx tac, Ex? envFrame, VarDecl variable) {
        if (variable.Constant && variable.ConstantValue.Try(out var val))
            return val;
        return _LocalOrParentVariable(tac, envFrame, variable, WithinAnyExpressionScope);
    }

    private Ex _LocalOrParentVariable(TExArgCtx tac, Ex? envFrame, VarDecl variable, bool methodScopeVisible) {
        var visible = (methodScopeVisible || Type != LexicalScopeType.MethodScope);
        if (visible && variableDecls.TryGetValue(variable.Name, out var v) && v == variable) {
            return variable.Value(envFrame, tac);
        }
        if (this is DynamicLexicalScope && Parent is not DynamicLexicalScope) {
            throw new CompileException(
                $"The variable `{variable.AsParam}` (declared at {variable.Position}) is not actually visible here." +
                $" This is because the usage is within a dynamic scope, and the variable is outside that scope.");
        }
        if (Parent is DMKScope) {
            throw new CompileException(
                $"The variable `{variable.AsParam}` (declared at {variable.Position}) is not actually visible here." +
                $" This is probably because the variable was declared in a method scope (such as GCR or GSR)" +
                $" and was used outside an expression function." +
                $"\nIf you are using this variable to construct a SyncPattern, AsyncPattern, or StateMachine, then you" +
                $" can wrap that SyncPattern/AsyncPattern/StateMachine using `Wrap` to make it an expression function.");
        }
        //todo skip-parent handling
        return Parent!._LocalOrParentVariable(tac, UseEF && visible ? envFrame?.Field(nameof(EnvFrame.Parent)) : envFrame, variable, methodScopeVisible);
    }

    /// <inheritdoc cref="LocalOrParentVariable"/>
    public Ex? TryGetLocalOrParentVariable(TExArgCtx tac, Type t, string varName, out VarDecl? decl) {
        decl = FindVariable(varName);
        if (decl == null || decl.FinalizedType != t) {
            return null;
        }
        return LocalOrParentVariable(tac, tac.EnvFrame, decl);
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
                        $"The variable {varName}<{typ.SimpRName()}> was referenced in a dynamic context " +
                        $"(eg. a bullet control), but some callers do not have this variable defined")), typ))
            );
            tac.Ctx.UnscopedEnvframeAcess.Remove((varName, typ));
            return ret;
        }
    }

    /// <summary>
    /// Raise the provided envframe to the correct parent level for calling the provided script function.
    /// </summary>
    public Ex LocalOrParentFunctionEf(Ex envFrame, ScriptFnDecl sfn) =>
        _LocalOrParentFunctionEf(envFrame, sfn, WithinAnyExpressionScope);
    private Ex _LocalOrParentFunctionEf(Ex envFrame, ScriptFnDecl sfn, bool methodScopeVisible) {
        var visible = (methodScopeVisible || Type != LexicalScopeType.MethodScope);
        if (visible && functionDecls.TryGetValue(sfn.Name, out var f) && f == sfn) 
            return envFrame;
        if (Parent is DMKScope || this is DynamicLexicalScope && Parent is not DynamicLexicalScope)
            throw new Exception($"Could not find a scope containing function {sfn.Name}");
        return Parent!._LocalOrParentFunctionEf(UseEF && visible ? envFrame.Field(nameof(EnvFrame.Parent)) : envFrame, 
            sfn, methodScopeVisible);
    }

    /// <summary>
    /// Get an expression representing calling a script function declared locally in this scope,
    ///  or declared in any parent scope.
    /// </summary>
    public Ex LocalOrParentFunction(Ex envFrame, ScriptFnDecl sfn, IEnumerable<Ex> arguments, bool isPartial = false)
        => _LocalOrParentFunction(envFrame, sfn, arguments, WithinAnyExpressionScope, isPartial);
    private Ex _LocalOrParentFunction(Ex envFrame, ScriptFnDecl sfn, IEnumerable<Ex> arguments, bool methodScopeVisible, bool isPartial) {
        var visible = (methodScopeVisible || Type != LexicalScopeType.MethodScope);
        if (visible && functionDecls.TryGetValue(sfn.Name, out var f) && f == sfn) {
            return isPartial ?
                PartialFn.PartiallyApply(sfn.Compile(), arguments.Prepend(envFrame)) :
                PartialFn.Execute(sfn.Compile(), arguments.Prepend(envFrame));
        }
        if (Parent is DMKScope || this is DynamicLexicalScope && Parent is not DynamicLexicalScope)
            throw new Exception($"Could not find a scope containing function {sfn.Name}");
        return Parent!._LocalOrParentFunction(UseEF && visible ? envFrame.Field(nameof(EnvFrame.Parent)) : envFrame, sfn, arguments, methodScopeVisible, isPartial);
    }

    public static Ex FunctionWithoutLexicalScope(TExArgCtx tac, ScriptFnDecl sfn, IEnumerable<Ex> arguments) {
        return PartialFn.Execute(sfn.Compile(), arguments.Prepend(
            ((Ex)tac.EnvFrame).DictGet(
                getFnParentage.InstanceOf(
                    Ex.PropertyOrField(tac.EnvFrame, nameof(EnvFrame.Scope)),
                    Ex.Constant(sfn)))));
    }

    private static readonly Func<Ex, Ex> noOpOnValue = x => x;

    private readonly Dictionary<(string, Type), (bool, int, int, int)> instructionsCache = new();
    private static readonly List<LexicalScope> unwinder = new();
    private static readonly ExFunction getInstructions = 
        ExFunction.WrapAny(typeof(LexicalScope), nameof(GetLocalOrParentInstructions));
    private static readonly ExFunction getFnParentage = 
        ExFunction.WrapAny(typeof(LexicalScope), nameof(GetParentageToScriptFn));
    
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
            if (scope.variableDecls.TryGetValue(var.name, out var v)) {
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

    private readonly Dictionary<ScriptFnDecl, int> fnParentageCache = new();
    public int GetParentageToScriptFn(ScriptFnDecl defn) {
        if (fnParentageCache.TryGetValue(defn, out var parentage))
            return parentage;
        unwinder.Clear();
        for (var scope = this;; scope = scope.Parent) {
            unwinder.Add(scope);
            if (scope.fnParentageCache.TryGetValue(defn, out parentage))
                break;
            if (scope.functionDecls.TryGetValue(defn.Name, out var fn) && fn == defn) {
                parentage = 0;
                break;
            }
            if (scope.Parent == null)
                throw new Exception(
                    $"The script function {defn.Name} was referenced is an unscoped context (eg. a bullet control), but is not accessible in all callers' lexical scopes.");
        }
        for (int ii = unwinder.Count - 1; ii >= 0; --ii) {
            unwinder[ii].fnParentageCache[defn] = parentage;
            if (ii > 0 && unwinder[ii - 1].UseEF)
                ++parentage;
        }
        unwinder.Clear();
        return parentage;
    }
    
    public void SetDocComments(LexerMetadata metadata) {
        foreach (var decl in AllVarsInDescendantScopes.Concat<IDeclaration>(AllFnsInDescendantScopes).Concat(ImportDecls.Values))
            decl.TrySetDocComment(metadata);
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

    public static IImplicitTypeConverter? GetConverterForCompiledExpressionType(Type compiledType) {
        if (compilerConverters.TryGetValue(compiledType, out var conv))
            return conv;
        else if (compiledType.IsGenericType && compiledType.GetGenericTypeDefinition() == typeof(GCXF<>))
            return useEfWrapperTypes.Contains(compiledType.GetGenericArguments()[0]) 
                ? efScopeGcxfConverter : gcxfConverter;
        return null;
    }

    public static IImplicitTypeConverter? TryFindConversion(TypeDesignation to, TypeDesignation from) {
        var invoke = TypeDesignation.Dummy.Method(to, from);
        if (to.Resolve().LeftOrNull is { } toT && GetConverterForCompiledExpressionType(toT) is { } exCompiler)
            return exCompiler.NextInstance.MethodType.Unify(invoke, Unifier.Empty).IsLeft ? exCompiler : null;
        if (BaseResolver.GetImplicitSources(to, out var convs))
            foreach (var c in convs)
                if (c.NextInstance.MethodType.Unify(invoke, Unifier.Empty).IsLeft)
                    return c;
        
        if (BaseResolver.GetImplicitCasts(from, out convs))
            foreach (var c in convs)
                if (c.NextInstance.MethodType.Unify(invoke, Unifier.Empty).IsLeft)
                    return c;
        return null;
    }

    public static IImplicitTypeConverter? TryFindLowPriorityConversion(TypeDesignation to, TypeDesignation from) {
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

    public static readonly AttachEFSMConv AttachEFtoSMImplicit = new();
    public static readonly AttachEFAPConv AttachEFtoAPImplicit = new();
    public static readonly AttachEFSPConv AttachEFtoSPImplicit = new();
    public static TypeResolver BaseResolver { get; } = new(
            //T -> T[]
            new SingletonToArrayConv(),
        
            FixedImplicitTypeConv<string, LString>.FromFn(x => x),
            
            //hoist constructor (can't directly use generic class constructor)
            new GenericMethodConv1((GenericMethodSignature)MethodSignature.Get(typeof(ReflectConstructors)
                .GetMethod(nameof(ReflectConstructors.H), BindingFlags.Public | BindingFlags.Static)!)),
            //uncompiledCode helper
            new GenericMethodConv1((GenericMethodSignature)MethodSignature.Get(typeof(Compilers)
                .GetMethod(nameof(Compilers.Code), BindingFlags.Public | BindingFlags.Static)!)),
            
            FixedImplicitTypeConv<int,float>.FromFn(x => x),
            FixedImplicitTypeConv<Vector2,Vector3>.FromFn(v2 => v2),
            
            new FixedImplicitTypeConv<float, Synchronizer>(
                exf => tac => Ex.Constant(Synchronization.Time(
                    Compilers.GCXF<float>(Helpers.TExLambdaTyper.Convert<float>(exf))))) 
                { Kind = ScopedConversionKind.BlockScopedExpression },
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
            FixedImplicitTypeConv<string[],BulletManager.StyleSelector>.FromFn(sel => new(new[]{sel})),
            FixedImplicitTypeConv<string[][],BulletManager.StyleSelector>.FromFn(sel => new(sel)),
            //Value-typed auto constructors
            FixedImplicitTypeConv<BulletManager.exBulletControl,BulletManager.cBulletControl>.FromFn(c => new(c), 
                ScopedConversionKind.BlockScopedExpression)
    );
    public TypeResolver Resolver => BaseResolver;

    private DMKScope() : base() {
        GlobalRoot = this;
    }

    private readonly Dictionary<string, List<MethodSignature>> smInitMultiDecls = new();
    public List<MethodSignature>? StaticMethodDeclaration(string name) {
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

    protected override Either<Unit, IDeclaration> _Declare(IDeclaration decl) =>
        throw new Exception("Do not declare variables in DMKScope");
}


}