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
    public DMKScope Root { get; protected set; }
    public LexicalScope? Parent { get; private set; }
    public readonly Dictionary<string, VarDecl> varsAndFns = new();
    private readonly Dictionary<string, VarDeclPromise> varPromises = new();
    private List<LexicalScope> Children { get; } = new();

    /// <summary>
    /// True iff this scope is instantiated by an environment frame
    /// (false iff it is instantiated by Expression.Block).
    /// <br/>This is finalized in <see cref="FinalizeVariableTypes"/>.
    /// </summary>
    public bool UseEF { get; private set; } = true;

    /// <summary>
    /// True iff this scope is or is contained within a <see cref="ScopedConversionScope"/> with a scope kind
    ///  that disabled environment-frames.
    /// <br/>This is finalized in <see cref="FinalizeVariableTypes"/>.
    /// </summary>
    public bool WithinDisabledEFScope { get; private set; } = false;

    /// <summary>
    /// The final set of variables declared in this scope. 
    /// <br/>This is only non-empty after <see cref="FinalizeVariableTypes"/> is called.
    /// </summary>
    public (Type type, VarDecl[] decls)[] VariableDecls { get; private set; } = null!;

    //Constructor for DMKScope
    protected LexicalScope() {
        Root = null!;
    }
    
    public LexicalScope(LexicalScope parent) {
        this.Parent = parent;
        this.Root = parent.Root;
        if (parent is not DMKScope)
            parent.Children.Add(this);
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
                varsAndFns[a.Name!] = decl;
            }
    }

    /// <summary>
    /// Declare a variable or function in this lexical scope.
    /// <br/>This variable/function can shadow variables/functions from parent scopes, but cannot redeclare a name already used in this scope.
    /// </summary>
    /// <returns>Left if declaration succeeded; Right if a variable or function already exists with the same name local to this scope.</returns>
    public virtual Either<Unit, VarDecl> DeclareVariable(VarDecl decl) {
        if (varsAndFns.TryGetValue(decl.Name, out var prev))
            return prev;
        varsAndFns[decl.Name] = decl;
        decl.DeclarationScope = this;
        return Unit.Default;
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

    /// <summary>
    /// Request that an undeclared variable eventually be declared in this scope or any parent scope.
    /// </summary>
    public virtual VarDeclPromise RequestDeclaration(PositionRange usedAt, string name) {
        //NB: we don't query parent because it's possible that an unbound declaration `k` eventually
        // gets different declarations in different usage scopes.
        if (varPromises.TryGetValue(name, out var promise))
            return promise;
        return varPromises[name] = new VarDeclPromise(usedAt, name);
    }

    public virtual List<Reflector.IMethodSignature>? FindStaticMethodDeclaration(string name) {
        return Parent?.FindStaticMethodDeclaration(name);
    }

    public virtual Either<Unit, TypeUnifyErr> FinalizeVariableTypes(Unifier u) {
        if (this is ScopedConversionScope { Converter: { Kind: not ScopedConversionKind.Trivial } })
            WithinDisabledEFScope = true;
        else
            WithinDisabledEFScope = Parent?.WithinDisabledEFScope ?? false;
        return varsAndFns.Values
            .Select(v => v.FinalizeType(u))
            .SequenceL()
            .BindL(_ => varPromises.Values
                .Select( p => p.FinalizeType(u))
                .SequenceL())
            .BindL(_ => Children
                .Select(c => c.FinalizeVariableTypes(u))
                .SequenceL())
            .FMapL(_ => {
                //note that Identifier is not necessarily ordered
                // since dict.Values as well as GroupBy may not be stable.
                VariableDecls = varsAndFns.Values.OfType<VarDecl>()
                    .GroupBy(v => v.FinalizedType!)
                    .Select((g, ti) => {
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
                UseEF = !WithinDisabledEFScope && VariableDecls.Length > 0;
                return Unit.Default;
            });
    }


    /// <summary>
    /// Get an expression representing a readable/writeable variable
    ///  stored locally in an environment frame for this scope,
    ///  or stored locally in any parent environment frame.
    /// </summary>
    public Ex GetLocalOrParent(TExArgCtx tac, Ex envFrame, VarDecl variable) {
        //todo skip-parent handling
        if (varsAndFns.TryGetValue(variable.Name, out var v) && v == variable) {
            return variable.Value(envFrame, tac);
        }
        if (Parent is null)
            throw new Exception($"Could not find a scope containing variable {variable}");
        return Parent.GetLocalOrParent(tac, UseEF ? envFrame.Field(nameof(EnvFrame.Parent)) : envFrame, variable);
    }

}

public class ScopedConversionScope : LexicalScope {
    public IScopedTypeConverter Converter { get; }

    public ScopedConversionScope(IScopedTypeConverter conv, LexicalScope parent) : base(parent) {
        this.Converter = conv;
    }

    //declareArgs ok
    public override Either<Unit, VarDecl> DeclareVariable(VarDecl decl) =>
        throw new Exception("Do not declare variables in ImplicitArgScope");

    public override VarDeclPromise RequestDeclaration(PositionRange usedAt, string name)  =>
        throw new Exception("Do not request declarations in ImplicitArgScope");
}

/// <summary>
/// The scope that contains all DMK reflection methods. Variables cannot be declared in this scope.
/// </summary>
public class DMKScope : LexicalScope {
    private static DMKScope? _singleton;
    public static DMKScope Singleton => _singleton ??= new DMKScope();

    private static T[] SingleToArray<T>(T single) => new[] { single };

    private static Reflector.GenericMethodSignature GetCompilerMethod(string name) =>
        Reflector.MethodSignature.Get(
            typeof(Compilers).GetMethod(name, BindingFlags.Public | BindingFlags.Static) ??
            throw new StaticException($"Compiler method `{name}` not found")) 
            as Reflector.GenericMethodSignature ?? 
        throw new StaticException($"Compiler method `{name}` is not generic");

    public static TypeResolver BaseResolver { get; } = new(new IImplicitTypeConverter[] {
            new ConstantToExprConv(),
            new GenericMethodConv1(GetCompilerMethod("GCXF"))
                { ScopeArgs = Compilers.GCXFArgs, Kind = ScopedConversionKind.GCXFFunction },
            new GenericMethodConv1(GetCompilerMethod("ErasedGCXF"))
                { ScopeArgs = Compilers.GCXFArgs, Kind = ScopedConversionKind.GCXFFunction },
            new FixedImplicitTypeConv<ExVTP, GCXU<VTP>>(Compilers.GCXU) 
                { ScopeArgs = Compilers.VTPArgs, Kind = ScopedConversionKind.GCXUFunction },
            new FixedImplicitTypeConv<ExVTP, VTP>(Compilers.VTP) 
                { ScopeArgs = Compilers.VTPArgs, Kind = ScopedConversionKind.SimpleFunction },
            
            new GenericMethodConv1((Reflector.MethodSignature.Get(
                    typeof(DMKScope).GetMethod(nameof(SingleToArray), BindingFlags.Static | BindingFlags.NonPublic)!) as
                Reflector.GenericMethodSignature)!),
        
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
        }
    );
    public TypeResolver Resolver => BaseResolver;

    private DMKScope() : base() {
        Root = this;
    }
    public override (LexicalScope, VarDecl)? FindScopedDeclaration(string name) {
        return null;
    }

    private readonly Dictionary<string, List<Reflector.IMethodSignature>> smInitMultiDecls = new();
    public override List<Reflector.IMethodSignature>? FindStaticMethodDeclaration(string name) {
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

    public override VarDeclPromise RequestDeclaration(PositionRange usedAt, string name)  =>
        throw new Exception("Do not request declarations in DMKScope");

    public override Either<Unit, TypeUnifyErr> FinalizeVariableTypes(Unifier u) =>
        throw new Exception("Do not call FinalizeVariableTypes in DMKScope");
}

public interface IUsedVariable {
    string Name { get; }
    TypeDesignation TypeDesignation { get; }
    bool IsBound => true;
    VarDecl Bound { get; }
}

public record UntypedVariable(VarDecl Declaration) : TypeUnifyErr;

public record UnboundPromise(VarDeclPromise Promise) : TypeUnifyErr;

public record PromiseBindingFailure(VarDeclPromise Promise, VarDecl Bound, TypeUnifyErr Err) : TypeUnifyErr;

/// <summary>
/// A declaration of a variable.
/// </summary>
public class VarDecl : IUsedVariable {
    public PositionRange Position { get; }
    
    /// <summary>
    /// The type of this variable as provided in the declaration. May be empty, in which case it will be inferred.
    /// </summary>
    public Type? KnownType { get; }
    public string Name { get; }
    public TypeDesignation TypeDesignation { get; }
    public VarDecl Bound => this;

    /// <summary>
    /// The scope in which this variable is declared. Assigned by <see cref="LexicalScope.DeclareVariable"/>
    /// </summary>
    public LexicalScope DeclarationScope { get; set; } = null!;

    /// <summary>
    /// The expression pointing to this variable (null if the scope is an Ex.Block scope).
    /// </summary>
    private ParameterExpression? _parameter;
    
    /// <summary>
    /// The final non-ambiguous type of this variable. Only present after <see cref="FinalizeType"/> is called.
    /// </summary>
    public TypeDesignation? FinalizedTypeDesignation { get; protected set; }
    
    /// <summary>
    /// The final non-ambiguous type of this variable. Only present after <see cref="FinalizeType"/> is called.
    /// </summary>
    public Type? FinalizedType { get; protected set; }
    
    /// <summary>
    /// The index of this declaration's type among the types of all declarations in the same <see cref="LexicalScope"/>.
    /// <br/>The ordering of indexes is arbitrary and may not correspond to the order of declarations.
    /// <br/>Assigned by <see cref="LexicalScope.FinalizeVariableTypes"/>.
    /// </summary>
    public int TypeIndex { get; set; }
    
    /// <summary>
    /// The index of this declaration among all declarations
    ///  with the same <see cref="FinalizedType"/> declared in the same <see cref="LexicalScope"/>.
    /// <br/>The ordering of indexes is arbitrary and may not correspond to the order of declarations.
    /// <br/>Assigned by <see cref="LexicalScope.FinalizeVariableTypes"/>.
    /// </summary>
    public int Index { get; set; }
    
    /// <summary>
    /// The number of times this declaration receives an assignment.
    /// <br/>Set during the AST <see cref="IAST.Verify"/> step.
    /// <br/>This should always be at least 1.
    /// </summary>
    public int Assignments { get; set; }
    
    /// <summary>
    /// A declaration of a variable.
    /// </summary>
    public VarDecl(PositionRange Position, Type? knownType, string Name) {
        this.Position = Position;
        this.KnownType = knownType;
        this.Name = Name;
        TypeDesignation = knownType == null ?
            new TypeDesignation.Variable() : TypeDesignation.FromType(knownType);
    }

    /// <summary>
    /// If this is a local variable for an Ex.Block (rather than an implicit variable),
    /// return the parameter definition.
    /// </summary>
    public virtual ParameterExpression? DeclaredParameter(TExArgCtx tac) =>
        DeclarationScope.UseEF ? null : _parameter ??= Ex.Variable(
                FinalizedType ?? 
                throw new ReflectionException(Position, $"Variable declaration {Name} not finalized"), Name);

    /// <summary>
    /// Get the expression of this variable, where `envFrame` is the frame in which
    ///  this variable is declared.
    /// </summary>
    public T Value<T>(EnvFrame frame) => (frame[TypeIndex] as FrameVars<T>)!.Values[Index];
    
    /// <summary>
    /// Get the expression representing this variable, where `envFrame` is the frame in which
    ///  this variable was declared (and tac.EnvFrame is the frame of usage).
    /// </summary>
    public virtual Expression Value(Ex envFrame, TExArgCtx tac) =>
        DeclarationScope.UseEF ?
            //(ef.Variables[var.FinalizedType] as VariableStore<var.FinalizedType>).Values[var.Index]
            envFrame
                //.Field(nameof(EnvFrame.Variables))
                .DictGet(Ex.Constant(TypeIndex))
                .As(FrameVars.GetVarStoreType(FinalizedType!))
                .Field("Values")
                //dict[index] also works for list[index]
                .DictGet(Ex.Constant(Index)) :
            _parameter ?? throw new StaticException($"{nameof(Value)} called before {nameof(DeclaredParameter)}");


    public virtual Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        FinalizedTypeDesignation = TypeDesignation.Simplify(u);
        var td = FinalizedTypeDesignation.Resolve(u);
        if (td.IsRight)
            return new UntypedVariable(this);
        FinalizedType = td.Left;
        return Unit.Default;
    }
        
    public IEnumerable<PrintToken> DebugPrint() {
        if (KnownType == null)
            yield return $"var &{Name}";
        else
            yield return $"var &{Name}::{KnownType.RName()}";
    }
}

public class ImplicitArgDecl : VarDecl {
    public ImplicitArgDecl(PositionRange Position, Type? knownType, string Name) : base(Position, knownType, Name) { }
}

/// <summary>
/// A declaration implicitly provided through TExArgCtx.
/// </summary>
public class ImplicitArgDecl<T> : ImplicitArgDecl {
    public override ParameterExpression? DeclaredParameter(TExArgCtx tac) => null;
    public override Expression Value(Ex envFrame, TExArgCtx tac) =>
            tac.GetByName<T>(Name);
    
    public ImplicitArgDecl(PositionRange Position, string Name) : base(Position, typeof(T), Name) {
        FinalizedTypeDesignation = TypeDesignation;
        FinalizedType = typeof(T);
    }
    public override Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        return Unit.Default;
    }

    public override string ToString() => $"{Name}<{typeof(T).RName()}>";
}

/// <summary>
/// A variable declaration that has not yet been made. 
/// </summary>
public record VarDeclPromise(PositionRange UsedAt, string Name) : IUsedVariable {
    public TypeDesignation TypeDesignation { get; } = new TypeDesignation.Variable();
    public TypeDesignation? FinalizedType { get; private set; }
    private VarDecl? _binding;
    public bool IsBound => _binding != null;
    public VarDecl Bound => _binding ?? throw new Exception($"The variable {Name} is unbound.");
    private readonly HashSet<AST.Reference> requirers = new();

    public void IsRequiredBy(AST.Reference r) {
        requirers.Add(r);
    }

    public void IsNotRequiredBy(AST.Reference r) {
        requirers.Remove(r);
    }

    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        if (!IsBound) {
            FinalizedType = TypeDesignation.Simplify(u);
            if (requirers.Count == 0)
                return Unit.Default;
            return new UnboundPromise(this);
        } else {
            FinalizedType = _binding!.FinalizedTypeDesignation;
            return Unit.Default;
        }
    }

    public Either<Unifier, TypeUnifyErr> MaybeBind(LexicalScope scope, in Unifier u) {
        if (scope.FindDeclaration(Name) is { } decl) {
            _binding = decl;
            var uerr = _binding.TypeDesignation.Unify(TypeDesignation, u);
            if (uerr.IsLeft)
                return uerr.Left;
            else
                return new PromiseBindingFailure(this, decl, uerr.Right);
        } else return u;
    }

    public override string ToString() => _binding?.ToString() ?? $"Unbound variable {Name}";
}


}