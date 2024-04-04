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
using Danmokou.Expressions;
using Danmokou.Reflection;
using Mizuhashi;

namespace Danmokou.Reflection2 {
[Flags]
public enum DeclarationLookup {
    /// <summary>
    /// A declaration with a constant value (declared via const var).
    /// </summary>
    CONSTANT = 1 << 0,
    
    /// <summary>
    /// A lexically scoped declaration.
    /// </summary>
    LEXICAL_SCOPE = 1 << 1,
    
    /// <summary>
    /// A dynamically scoped declaration.
    /// </summary>
    DYNAMIC_SCOPE = 1 << 2,
    
    ConstOnly = CONSTANT,
    Standard = ConstOnly | LEXICAL_SCOPE,
    Dynamic = Standard | DYNAMIC_SCOPE,
}
public record UntypedVariable(VarDecl Declaration) : TypeUnifyErr;
public record VoidTypedVariable(VarDecl Declaration) : TypeUnifyErr;

public interface IDeclaration {
    PositionRange Position { get; }
    string Name { get; }

    /// <summary>
    /// True if this variable should be hoisted into the parent scope of the location where its declaration occured.
    /// </summary>
    bool Hoisted { get; }
    
    /// <summary>
    /// The scope in which this variable is declared. Assigned by <see cref="LexicalScope.Declare"/>
    /// </summary>
    LexicalScope DeclarationScope { get; set; }
    
    /// <summary>
    /// An optional comment describing the declaration.
    /// </summary>
    string? DocComment { get; set; }
    
    Either<Unit, TypeUnifyErr> FinalizeType(Unifier u);

    public void TrySetDocComment(LexerMetadata comments) {
        foreach (var (p, c) in comments.Comments) {
            if (p.End.Line + 1 == Position.Start.Line) {
                //Space before newline required for VSCode newlining
                DocComment = c.Replace("\n", "  \n");
                return;
            } else if (p.End.Line >= Position.Start.Line)
                break;
        }
    }
}

/// <summary>
/// A declaration of a variable.
/// </summary>
public class VarDecl : IDeclaration {
    public PositionRange Position { get; }
    
    public bool Hoisted { get; }
    
    /// <summary>
    /// If true, then this declaration has a constant value.
    /// </summary>
    public bool Constant { get; set; }
    /// <summary>
    /// If <see cref="Constant"/> is true, then this stores the constant value of the declaration during execution.
    /// </summary>
    public Maybe<ConstantExpression> ConstantValue { get; set; } = Maybe<ConstantExpression>.None;
    
    /// <summary>
    /// The type of this variable as provided in the declaration. May be empty, in which case it will be inferred.
    /// </summary>
    public Type? KnownType { get; }
    public string Name { get; }
    public TypeDesignation TypeDesignation { get; }
    
    /// <summary>
    /// If present, then this VarDecl is copied into an EnvFrame from the arguments of a function.
    /// </summary>
    public ImplicitArgDecl? SourceImplicit { get; }

    public LexicalScope DeclarationScope { get; set; } = null!;
    public string? DocComment { get; set; }

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
    public VarDecl(PositionRange Position, bool Hoist, Type? knownType, string Name, ImplicitArgDecl? sourceImplicit = null) {
        this.Position = Position;
        this.Hoisted = Hoist;
        this.KnownType = knownType;
        this.Name = Name;
        this.SourceImplicit = sourceImplicit;
        if (knownType != null) {
            TypeDesignation = TypeDesignation.FromType(knownType);
            FinalizedType = knownType;
        } else {
            TypeDesignation = sourceImplicit?.TypeDesignation ??
                              new TypeDesignation.Variable();
        }
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
    public virtual Expression Value(Expression? envFrame, TExArgCtx tac) =>
        DeclarationScope.UseEF ?
            EnvFrame.Value(envFrame ?? throw new CompileException(
                $"The variable `{Name}` (declared at {Position}) is stored in the environment frame, "+
                "but is accessed from a callsite that has no environment frame."), 
                Expression.Constant(TypeIndex), Expression.Constant(Index), FinalizedType!) :
            _parameter ?? throw new StaticException($"{nameof(Value)} called before {nameof(DeclaredParameter)}");


    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        FinalizedTypeDesignation = TypeDesignation.Simplify(u);
        var td = FinalizedTypeDesignation.Resolve(u);
        if (td.IsRight)
            return new UntypedVariable(this);
        FinalizedType = td.Left;
        if (FinalizedType == typeof(void))
            return new VoidTypedVariable(this);
        return SourceImplicit?.FinalizeType(u) ?? Unit.Default;
    }
        
    public IEnumerable<PrintToken> DebugPrint() {
        if (KnownType == null)
            yield return $"var &{Name}";
        else
            yield return $"var &{Name}::{KnownType.SimpRName()}";
    }
    public string AsParam {
        get { return $"{FinalizedType?.SimpRName()} {Name}"; }
    }
}

/// <summary>
/// A declaration implicitly provided through TExArgCtx or a fixed expression.
/// </summary>
public class ImplicitArgDecl : VarDecl, IDelegateArg {
    Type IDelegateArg.Type => FinalizedType!;
    public override ParameterExpression? DeclaredParameter(TExArgCtx tac) => null;
    public override Expression Value(Expression? envFrame, TExArgCtx tac) =>
        tac.GetByName(FinalizedType ?? 
                      throw new CompileException("Cannot retrieve ImplicitArgDecl before type is finalized"), Name);

    public ImplicitArgDecl(PositionRange Position, Type? knownType, string Name) : base(Position, false, knownType,
        Name) {
        ++Assignments;
    }

    public virtual TExArgCtx.Arg MakeTExArg(int index) => TExArgCtx.Arg.MakeAny(
        FinalizedType ?? throw new Exception("Implicit arg declaration type not yet finalized"), Name, false, false);
    public ImplicitArgDecl MakeImplicitArgDecl() => this;
    public override string ToString() => $"{Name}<{FinalizedType?.SimpRName()}>";

}

/// <inheritdoc cref="ImplicitArgDecl"/>
public class ImplicitArgDecl<T> : ImplicitArgDecl {
    public ImplicitArgDecl(PositionRange Position, string Name) : base(Position, typeof(T), Name) { }
    public override TExArgCtx.Arg MakeTExArg(int index) => TExArgCtx.Arg.Make<T>(Name, false, false);
}

public class ScriptFnDecl : IDeclaration {
    public string Name { get; init; }
    public bool Hoisted { get; }
    public ImplicitArgDecl[] Args { get; }
    public IAST?[] Defaults { get; }
    public AST.ScriptFunctionDef Tree { get; set; }
    public TypeDesignation.Dummy CallType { get; private set; }
    
    /// <summary>
    /// If true, then invocations of this function should be extracted as constants.
    /// </summary>
    public bool IsConstant { get; init; }
    public PositionRange Position => Tree.Position;
    public LexicalScope DeclarationScope { get; set; } = null!;
    public string? DocComment { get; set; }
    public Type? FuncType { get; private set; }
    private object? _compiled = null;
    private bool isCompiling = false;
    public ScriptFnDecl(AST.ScriptFunctionDef Tree, bool Hoist, string Name, ImplicitArgDecl[] Args, IAST?[] Defaults, TypeDesignation.Dummy CallType) {
        this.Name = Name;
        this.Hoisted = Hoist;
        this.Args = Args;
        this.Defaults = Defaults;
        this.Tree = Tree;
        this.CallType = CallType;
    }
    public Expression Compile() {
        if (_compiled is null) {
            if (isCompiling) //recursive function
                return Expression.Constant(this).Field(nameof(_compiled)).As(FuncType!);
            isCompiling = true;
            FuncType ??= Tree.CompileFuncType();
            _compiled = Tree.CompileFunc(FuncType);
            isCompiling = false;
        }
        return Expression.Constant(_compiled);
    }

    public Type? ReturnType => Tree.Body.LocalScope!.Return!.FinalizedType;

    public string AsSignature(string? namePrefix = null) {
        var name = namePrefix is null ? Name : $"{namePrefix}.{Name}";
        return $"{ReturnType?.SimpRName()} {name}({string.Join(", ", Args.Select(a => a.AsParam))})";
    }

    
    public string TypeOnlySignature =>
        $"({string.Join(", ", Args.Select(a => a.FinalizedType?.SimpRName()))}): {ReturnType?.SimpRName()}";

    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) {
        CallType = CallType.SimplifyDummy(u);
        //For now we don't need any particular resolution here since the implicit args
        // and block should resolve all variables. However, we still do need to 
        // simplify CallType in case this function is referenced in another script.
        return Unit.Default;
    }

    public override string ToString() => $"{Tree.Position} {AsSignature()}";
}

public record MacroDecl(ST.MacroDef Tree, string Name, string[] Args, ST?[] Defaults) : IDeclaration {
    public PositionRange Position => Tree.Position;
    public bool Hoisted => false;
    public LexicalScope DeclarationScope { get; set; } = null!;
    public string? DocComment { get; set; }
    
    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) => Unit.Default;
}

public record ScriptImport(PositionRange Position, EnvFrame Ef, string FileKey, string? ImportFrom, string? ImportAs) : IDeclaration {
    public string Name => ImportAs ?? FileKey;
    public bool Hoisted => false;
    public LexicalScope DeclarationScope { get; set; } = null!;
    public string? DocComment { get; set; }
    
    public Either<Unit, TypeUnifyErr> FinalizeType(Unifier u) => Unit.Default;
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
    /// <param name="ir">Loop ratio i/(times-1)</param>
    public record GenCtx(VarDecl i, VarDecl pi, VarDecl rv2, VarDecl brv2, VarDecl st, VarDecl times, VarDecl ir) : AutoVars {
        public VarDecl? bindItr;
        public VarDecl? bindAngle;
        public (VarDecl lr, VarDecl rl)? bindLR;
        public (VarDecl ud, VarDecl du)? bindUD;
        public (VarDecl axd, VarDecl ayd, VarDecl aixd, VarDecl aiyd)? bindArrow;
    }

    public override string ToString() => "-Scope Autovars-";
}


}