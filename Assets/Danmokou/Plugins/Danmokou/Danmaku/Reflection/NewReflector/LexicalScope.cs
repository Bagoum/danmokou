using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Mizuhashi;
using Ex = System.Linq.Expressions.Expression;
using ExVTP = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV3, Danmokou.Expressions.TEx>;


namespace Danmokou.Reflection2 {
/// <summary>
/// A class defining a lexical scope in script code.
/// </summary>
public class LexicalScope {
    public LexicalScope? Parent { get; set; }
    private readonly Dictionary<string, VarDecl> varsAndFns = new();
    private readonly Dictionary<string, VarDeclPromise> varPromises = new();
    private List<LexicalScope> Children { get; } = new();
    private readonly bool? isExpression;
    /// <summary>
    /// True iff this scope is defined within an expression.
    /// </summary>
    public bool IsExpression => isExpression ?? Parent?.IsExpression ?? false;

    public IEnumerable<VarDecl> VariableDecls => varsAndFns.Values.OfType<VarDecl>();

    public LexicalScope(LexicalScope? parent, bool? isExpression = null) {
        this.Parent = parent;
        if (parent is not DMKScope)
            parent?.Children.Add(this);
        this.isExpression = isExpression;
    }

    public virtual void DeclareArgs(params IDelegateArg[] args) {
        foreach (var a in args)
            if (!string.IsNullOrWhiteSpace(a.Name))
                varsAndFns[a.Name!] = a.MakeArgDecl();
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
        return Unit.Default;
    }

    /// <summary>
    /// Find a declared variable in this scope or any parent scope.
    /// </summary>
    public virtual VarDecl? FindDeclaration(string name) {
        if (varsAndFns.TryGetValue(name, out var d))
            return d;
        return Parent?.FindDeclaration(name);
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
        return varsAndFns.Values
            .Select(v => v.FinalizeType(u))
            .SequenceL()
            .BindL(_ => varPromises.Values
                .Select( p => p.FinalizeType(u))
                .SequenceL())
            .BindL(_ => Children
                .Select(c => c.FinalizeVariableTypes(u))
                .SequenceL()
                .FMapL(_ => Unit.Default));
    }
}

/// <summary>
/// The scope that contains all DMK reflection methods. Variables cannot be declared in this scope.
/// </summary>
public class DMKScope : LexicalScope {
    public static readonly DMKScope Singleton = new();
    public static readonly IImplicitTypeConverter[] TypeConversions = {
        new ConstantToExprConv(),
        new FixedImplicitTypeConv<ExVTP, GCXU<VTP>>(Compilers.GCXU) { ScopeArgs = Compilers.VTPArgs },
        new FixedImplicitTypeConv<ExVTP, VTP>(Compilers.VTP) { ScopeArgs = Compilers.VTPArgs }
    };
    public DMKScope() : base(null, false) { }
    public override VarDecl? FindDeclaration(string name) {
        return null;
    }

    public override List<Reflector.IMethodSignature>? FindStaticMethodDeclaration(string name) {
        if (Reflector.ReflectionData.AllBDSL2Methods.TryGetValue(name, out var results))
            return results;
        return null;
    }

    public override void DeclareArgs(params IDelegateArg[] args) =>
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
    ParameterExpression Parameter(TExArgCtx tac);
}

public record UntypedVariable(VarDecl Declaration) : TypeUnifyErr;

public record UnboundPromise(VarDeclPromise Promise) : TypeUnifyErr;

/// <summary>
/// A declaration of a variable.
/// </summary>
public record VarDecl(PositionRange Position, Type? Type, string Name) : IUsedVariable {
    public TypeDesignation TypeDesignation { get; } = Type == null ?
        new TypeDesignation.Variable() : TypeDesignation.FromType(Type);
    public TypeDesignation? FinalizedType { get; protected set; }
    public virtual bool IsFunctionArgument => false;
    private ParameterExpression? _Parameter { get; set; }
    public virtual ParameterExpression Parameter(TExArgCtx tac) => 
        _Parameter ?? throw new ReflectionException(Position, $"Variable declaration {Name} not finalized");

    public virtual Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        FinalizedType = TypeDesignation.Simplify(u);
        var td = FinalizedType.Resolve(u);
        if (td.IsRight)
            return new UntypedVariable(this);
        _Parameter = Ex.Variable(td.Left, Name);
        return Unit.Default;
    }
        
    public IEnumerable<PrintToken> DebugPrint() {
        if (Type == null)
            yield return $"var &{Name}";
        else
            yield return $"var &{Name}::{Type.RName()}";
    }
}

public record ArgumentDecl<T> : VarDecl {
    public override bool IsFunctionArgument => true;
    public override ParameterExpression Parameter(TExArgCtx tac) =>
        tac.GetByName<T>(Name);
    
    public ArgumentDecl(PositionRange Position, string Name) : base(Position, typeof(T), Name) {
        FinalizedType = TypeDesignation;
    }
    public override Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        return Unit.Default;
    }
}

/// <summary>
/// A variable declaration that has not yet been made. 
/// </summary>
public record VarDeclPromise(PositionRange UsedAt, string Name) : IUsedVariable {
    public TypeDesignation TypeDesignation { get; private set; } = new TypeDesignation.Variable();
    public TypeDesignation? FinalizedType { get; private set; }
    private VarDecl? _binding;
    public bool IsBound => _binding != null;
    public virtual ParameterExpression Parameter(TExArgCtx tac) => 
         (_binding ?? throw new Exception($"The variable {Name} is unbound.")).Parameter(tac);
    
    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        if (!IsBound) {
            FinalizedType = TypeDesignation.Simplify(u);
            return new UnboundPromise(this);
        } else {
            FinalizedType = _binding!.FinalizedType;
            return Unit.Default;
        }
    }

    public void MaybeBind(LexicalScope scope) {
        if (scope.FindDeclaration(Name) is { } decl)
            _binding = decl;
    }

    public override string ToString() => _binding?.ToString() ?? $"Unbound variable {Name}";
}


}