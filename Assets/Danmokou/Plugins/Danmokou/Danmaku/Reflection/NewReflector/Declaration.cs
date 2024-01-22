using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reflection;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Mizuhashi;

namespace Danmokou.Reflection2 {

public record UntypedVariable(VarDecl Declaration) : TypeUnifyErr;

public interface IDeclaration {
    PositionRange Position { get; }
    string Name { get; }

    /// <summary>
    /// True if this variable should be hoisted into the parent scope of the location where its declaration occured.
    /// </summary>
    bool Hoisted => false;
    
    /// <summary>
    /// The scope in which this variable is declared. Assigned by <see cref="LexicalScope.Declare"/>
    /// </summary>
    LexicalScope DeclarationScope { get; set; }
    Either<Unit, TypeUnifyErr> FinalizeType(Unifier u);
}

/// <summary>
/// A declaration of a variable.
/// </summary>
public class VarDecl : IDeclaration {
    public PositionRange Position { get; }
    
    public bool Hoisted { get; }
    
    /// <summary>
    /// The type of this variable as provided in the declaration. May be empty, in which case it will be inferred.
    /// </summary>
    public Type? KnownType { get; }
    public string Name { get; }
    public TypeDesignation TypeDesignation { get; }
    public VarDecl Bound => this;

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
    public VarDecl(PositionRange Position, bool Hoist, Type? knownType, string Name) {
        this.Position = Position;
        this.Hoisted = Hoist;
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
        DeclarationScope.UseEF ? null : _parameter ??= Expression.Variable(
                FinalizedType ?? 
                throw new ReflectionException(Position, $"Variable declaration {Name} not finalized"), Name);

    /// <summary>
    /// Get the read/write expression representing this variable, where `envFrame`'s scope is the scope in which
    ///  this variable was declared (and tac.EnvFrame is the frame of usage).
    /// </summary>
    public virtual Expression Value(Expression envFrame, TExArgCtx tac) =>
        DeclarationScope.UseEF ?
            EnvFrame.Value(envFrame, Expression.Constant(TypeIndex), Expression.Constant(Index), FinalizedType!) :
            _parameter ?? throw new StaticException($"{nameof(Value)} called before {nameof(DeclaredParameter)}");


    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
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
            yield return $"var &{Name}::{KnownType.ExRName()}";
    }
}

/// <summary>
/// A declaration implicitly provided through TExArgCtx.
/// </summary>
public class ImplicitArgDecl : VarDecl, IDelegateArg {
    Type IDelegateArg.Type => FinalizedType!;
    public override ParameterExpression? DeclaredParameter(TExArgCtx tac) => null;
    public override Expression Value(Expression envFrame, TExArgCtx tac) =>
        tac.GetByName(FinalizedType ?? 
                      throw new CompileException("Cannot retrieve ImplicitArgDecl before type is finalized"), Name);

    public ImplicitArgDecl(PositionRange Position, Type? knownType, string Name) : base(Position, false, knownType,
        Name) {
        ++Assignments;
    }

    public virtual TExArgCtx.Arg MakeTExArg(int index) => TExArgCtx.Arg.MakeAny(
        FinalizedType ?? throw new Exception("Implicit arg declaration type not yet finalized"), Name, false, false);
    public ImplicitArgDecl MakeImplicitArgDecl() => this;
    public override string ToString() => $"{Name}<{FinalizedType?.ExRName()}>";
    public string AsParam => $"{FinalizedType?.ExRName()} {Name}";

}

/// <inheritdoc cref="ImplicitArgDecl"/>
public class ImplicitArgDecl<T> : ImplicitArgDecl {
    public ImplicitArgDecl(PositionRange Position, string Name) : base(Position, typeof(T), Name) { }
    public override TExArgCtx.Arg MakeTExArg(int index) => TExArgCtx.Arg.Make<T>(Name, false, false);
}

public record ScriptFnDecl(AST.ScriptFunctionDef Tree, string Name, ImplicitArgDecl[] Args, TypeDesignation.Dummy CallType) : IDeclaration {
    public AST.ScriptFunctionDef Tree { get; set; } = Tree;
    public PositionRange Position => Tree.Position;
    public LexicalScope DeclarationScope { get; set; } = null!;
    private (MethodInfo, object)? _compiled = null;
    public (MethodInfo invoker, object func) Compile() => _compiled ??= Tree.CompileFunc();

    public Type? ReturnType => Tree.Body.LocalScope!.Return!.FinalizedType;

    public string AsSignature => $"{ReturnType?.SimpRName()} {Name}({string.Join(", ", Args.Select(a => a.AsParam))})";
    public string TypeOnlySignature =>
        $"({string.Join(", ", Args.Select(a => a.FinalizedType?.SimpRName()))}): {ReturnType?.SimpRName()}";

    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        //For now we don't need any particular resolution here since the implicit args
        // and block should resolve all variables
        return Unit.Default;
    }
}


public record AutoVars {
    public record None : AutoVars;

    /// <summary>
    /// </summary>
    /// <param name="i">Loop iteration</param>
    /// <param name="pi">Parent loop iteration</param>
    /// <param name="rv2">Current rotation</param>
    /// <param name="brv2">Base rotation provided by parent</param>
    /// <param name="st">Summon time</param>
    /// <param name="times">Maximum number of loops</param>
    public record GenCtx(VarDecl i, VarDecl pi, VarDecl rv2, VarDecl brv2, VarDecl st, VarDecl times) : AutoVars {
        public VarDecl? bindItr;
        public VarDecl? bindAngle;
        public (VarDecl lr, VarDecl rl)? bindLR;
        public (VarDecl ud, VarDecl du)? bindUD;
        public (VarDecl axd, VarDecl ayd, VarDecl aixd, VarDecl aiyd)? bindArrow;
    }

    public override string ToString() => "-Scope Autovars-";
}


}