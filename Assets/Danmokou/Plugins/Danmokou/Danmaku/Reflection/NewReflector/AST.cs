using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection;
using JetBrains.Annotations;
using Mizuhashi;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static BagoumLib.Reflection.TypeDesignation;

namespace Danmokou.Reflection2 {

public interface IAST : ITypeTree, IDebugPrint {
    /// <summary>
    /// Position of the code that will generate this object.
    /// <br/>This is used for debugging/logging/error messaging.
    /// </summary>
    PositionRange Position { get; }
    
    /// <summary>
    /// All ASTs that are direct children of this.
    /// </summary>
    public IAST[] Params { get; }
    
    /// <summary>
    /// The scope inside which this AST is declared.
    /// </summary>
    public LexicalScope EnclosingScope { get; }

    /// <summary>
    /// (Stage 1) Returns all errors in the parse tree that prevent it from being typechecked.
    /// </summary>
    IEnumerable<AST.NestedFailure> FirstPassErrors() {
        int maxIndex = -1;
        foreach (var err in Params
                     //We order by param position, not by error position, only to deal with cases
                     // where params are out of order (such as implicit arguments).
                     .OrderBy(p => p.Position.Start.Index)
                     .SelectMany(p => p.FirstPassErrors())) {
            if (err.Head.Position.Start.Index >= maxIndex) {
                yield return err;
                maxIndex = err.Head.Position.End.Index;
            }
        }
    }
    
    /// <summary>
    /// Returns <see cref="FirstPassErrors"/> converted into <see cref="ReflectionException"/>.
    /// </summary>
    IEnumerable<ReflectionException> FirstPassExceptions => FirstPassErrors().SelectMany(e => e.AsExceptions());

    /// <summary>
    /// (Stage 2) A function called on the root AST to typecheck the entire structure.
    /// </summary>
    public static Either<Type, TypeUnifyErr> Typecheck(IAST root, TypeResolver resolver, LexicalScope globalScope) {
        var tDes = root.PossibleUnifiers(resolver, Unifier.Empty).BindL(ts => ts.Count != 1 ?
            new TypeUnifyErr.TooManyPossibleTypes(ts.Select(t => t.Item1).ToList()) :
            root.ResolveUnifiers(ts[0].Item1, resolver, ts[0].Item2)
        );
        if (tDes.IsRight)
            return tDes.Right;
        //var _ = globalScope.FinalizeVariableTypes(tDes.Left.Item2);
        root.FinalizeUnifiers(tDes.Left.Item2);
        var varErr = globalScope.FinalizeVariableTypes(tDes.Left.Item2);
        if (varErr.IsRight)
            return varErr.Right;
        if (!root.IsFullyResolved)
            //This shouldn't occur, FinalizeVariableTypes should throw an error first
            return new TypeUnifyErr.UnboundRestr(root.UnresolvedVariables().First());
        return tDes.Left.Item1.Resolve(tDes.Left.Item2);
    }

    public static Exception EnrichError(TypeUnifyErr e, PositionRange? pos = null) {
        var sb = new StringBuilder();
        if (e is TypeUnifyErr.NotEqual ne)
            sb.Append($"Type {ne.LReq} and {ne.RReq} could not be unified, " +
                      $"because the first resolved to {ne.LResolved} and the second to {ne.RResolved}.");
        else if (e is TypeUnifyErr.ArityNotEqual ane) {
            sb.Append($"Type {ane.LeftReq} and {ane.RightReq} could not be unified, " +
                      $"because the first resolved to {ane.LResolved} and the second to {ane.RResolved}," +
                      $"which have different arities " +
                      $"({ane.LResolved.Arguments.Length} and {ane.RResolved.Arguments.Length} respectively).");
        } else if (e is TypeUnifyErr.UnboundRestr ubr) {
            //this should occur as unboundpromise/untypedvariable instead
            sb.Append($"The are unbound variables. (This error should not occur. Please report it.)");
        } else if (e is TypeUnifyErr.RecursionBinding rbr) {
            sb.Append($"Type {rbr.LReq} and {rbr.RReq} could not be unified because of a circular binding:" +
                      $"the first resolved to {rbr.LResolved}, which occurs in {rbr.RResolved}.");
        } else if (e is TypeUnifyErr.NoPossibleOverload npo) {
            pos = (npo.Tree as IAST).Position;
            if (npo.Tree is AST.MethodCall m) {
                pos = m.MethodPosition;
                var plural = m.Overloads.Count > 1 ? "s" : "";
                if (m.Overloads.Count == 1) {
                    sb.Append($"Typechecking failed for method {m.Overloads[0].Mi.AsSignature}.");
                } else {
                    sb.Append($"Typechecking failed for method call `{m.Overloads[0].CalledAs}`, " +
                              $"which maps to {m.Overloads.Count} overloaded methods:\n\t" +
                              $"{string.Join("\n\t", m.Overloads.Select(o => o.Mi.AsSignature))}");
                }
                var setsDisplay = npo.ArgSets.Select((set, i) => 
                    $"Parameter #{i + 1}: {string.Join(" or ", set.Select(s => s.Item1.SimpRName()))}" +
                    $" (at {m.Params[i].Position})");
                sb.Append($"\nThe parameters were inferred to have the following types:\n\t{string.Join("\n\t", setsDisplay)}");
                sb.Append($"\nThese parameters cannot be applied to the above method{plural}.");
            } else if (npo.Tree is AST.Block) {
                throw new Exception("Code blocks should not return NoPossibleOverload");
            } else throw new NotImplementedException(npo.Tree.GetType().RName());
        } else if (e is UnboundPromise { Promise: {} pr}) {
            pos = pr.UsedAt;
            var typ = pr.FinalizedType is { IsResolved: true } ft ?
                $" (inferred type: {ft.SimpRName()})" : "";
            sb.Append($"The variable {pr.Name}{typ} was used, but was never declared.");
        } else if (e is UntypedVariable ut) {
            pos = ut.Declaration.Position;
            sb.Append($"The type of the variable {ut.Declaration.Name} could not be determined.");
        } else
            throw new NotImplementedException(e.GetType().RName());

        return pos is { } p ? new ReflectionException(p, sb.ToString()) : new Exception(sb.ToString());
    }
    
    /// <summary>
    /// (Stage 3) Verify that types, declarations, etc. are sound, and makes simplifications/specializations based on the full AST context.
    /// </summary>
    public IEnumerable<ReflectionException> Verify() => 
        Params.Length > 0 ? Params.SelectMany(p => p.Verify()) : Array.Empty<ReflectionException>();


    /// <summary>
    /// Call this function to insert a scope `inserted` below the scope `prev` in descendants of this tree.
    /// </summary>
    void ReplaceScope(LexicalScope prev, LexicalScope inserted);
    
    /// <summary>
    /// (Stage 4) Create an executable script out of this AST.
    /// </summary>
    object? Realize();
    
    
    /// <summary>
    /// Print out a readable, preferably one-line description of the AST (not including its children). Consumed by language server.
    /// </summary>
    [PublicAPI]
    string Explain();
    
    IEnumerable<PrintToken> IDebugPrint.DebugPrint() => new PrintToken[] { Explain() };

}

public interface IMethodAST<T> : IAST, IMethodTypeTree<T> where T : IMethodDesignation {
}

//This is similar to IAST, but it's more powerful in order to support BDSL2
//IAST may eventually be moved onto this
public abstract record AST(PositionRange Position, LexicalScope EnclosingScope, params IAST[] Params) {
    /// <inheritdoc cref="IAST.Position"/>
    public PositionRange Position { get; init; } = Position;
    protected string CompactPosition => Position.Print(true);
    
    /// <inheritdoc cref="IAST.EnclosingScope"/>
    public LexicalScope EnclosingScope { get; private set; } = EnclosingScope;

    /// <summary>
    /// The lexical scope that this AST creates for any nested ASTs.
    /// <br/>This is either null or a direct child of <see cref="EnclosingScope"/>.
    /// </summary>
    public LexicalScope? LocalScope { get; set; }
    
    /// <inheritdoc cref="ITypeTree.ImplicitCast"/>
    public IRealizedImplicitCast? ImplicitCast { get; set; }

    /// <inheritdoc cref="IAST.Params"/>
    public IAST[] Params { get; init; } = Params;

    /// <inheritdoc cref="IAST.ReplaceScope"/>
    public virtual void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
        if (LocalScope?.Parent == prev)
            LocalScope.Parent = prev;
        if (EnclosingScope == prev) {
            EnclosingScope = inserted;
            foreach (var a in Params)
                a.ReplaceScope(prev, inserted);
        }
    }

    public object? Realize() {
        var withoutCast = _RealizeWithoutCast();
        if (ImplicitCast == null)
            return withoutCast;
        if (ImplicitCast.Converter is FixedImplicitTypeConv fixedConv) {
            return fixedConv.Convert(withoutCast);
        } else if (ImplicitCast.Converter is GenericTypeConv1 gtConv) {
            return gtConv.ConvertForType(ImplicitCast.Variables[0].Resolve(Unifier.Empty).LeftOrThrow, withoutCast);
        } else
            throw new NotImplementedException();
    }

    public abstract object? _RealizeWithoutCast();

        /*
        Params.Select(p => p.Verify()).AccFailToR().Map<Either<AST, List<ReflectionException>>>(
            prms => this with { Params = prms.ToArray() }, errs => errs);
    
    protected virtual Either<AST, List<ReflectionException>> VerifySelf(AST[] newParams) =>*/
    
    private ReflectionException Throw(string message, Exception? inner = null) =>
        new(Position, message, inner);
    
    
    private static string GetReturnTypeDescr<T>(IMethodAST<T> me) where T : IMethodDesignation => 
        me.SelectedOverload?.simplified.Last is { IsResolved: true } s ?
            s.Resolve(Unifier.Empty).LeftOrThrow.SimpRName() :
            "T";

    private static Type GetUnwrappedReturnType(ITypeTree me) =>
        me.SelectedOverloadReturnType!.UnwrapTExFunc().Resolve(Unifier.Empty).LeftOrThrow;


    // ----- Subclasses -----
    

    //todo: enum typing via ident?
    /// <summary>
    /// A reference to a declaration of a variable, function, etc, that is used as a value (eg. in `x`++ or `f`(2)).
    /// <br/>Declaration is Left when it is bound to an explicit variable declaration, and Right when it will
    ///  be bound at runtime (eg. within bullet functions).
    /// </summary>
    //Always a tex func type-- references are bound to expressions or EnvFrame
    public record Reference(PositionRange Position, LexicalScope EnclosingScope, IUsedVariable Declaration) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        /// <inheritdoc cref="IAtomicTypeTree.SelectedOverload"/>
        public TypeDesignation? SelectedOverload { get; set; }
        /// <inheritdoc cref="IAtomicTypeTree.PossibleTypes"/>
        public TypeDesignation[] PossibleTypes { get; } = { Declaration.TypeDesignation.MakeTExFunc() };

        public override void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
            base.ReplaceScope(prev, inserted);
            if (Declaration is VarDeclPromise promise)
                promise.MaybeBind(inserted);
        }

        public IEnumerable<ReflectionException> Verify() {
            if (!Declaration.IsBound)
                yield return new ReflectionException(Position, 
                    $"The variable {Name} was used but was never declared.");
        }

        public override object? _RealizeWithoutCast() {
            return GetUnwrappedReturnType(this).MakeTypedLambda(tac => {
                //TODO: need a method similar to ICRR to determine variables to copy into bpi
                return Declaration.Parameter(tac);
                /*
                //var x = ...
                if (Declaration.IsLeft)
                    return Finalize(Declaration.Left.Parameter);
                //expose case/exm let
                //i don't think either of these cases will be supported in bdsl2,
                // both will be handled by block statements
                if (ReflectEx.GetAliasFromStack(Declaration.Right, tac) is { } ex)
                    return Finalize(ex);
                var typ = SelectedOverload.UnwrapTExFunc().AssertKnown();
                //function arguments in case of autocompiled functions like vtp
                try {
                    if (tac.MaybeGetByName(typ, Declaration.Right) is { } ex_)
                        return ex_; //already TEx<T>
                } catch (BadTypeException bte) {
                    throw new ReflectionException(Position, $"Type error for reference {Declaration.Right}:", bte);
                }
                //expose autocompile
                if (tac.Ctx.ICRR != null && tac.Ctx.ICRR.TryResolve(typ, Declaration.Right, out ex))
                    return Finalize(ex);
                throw new ReflectionException(Position, $"Reference {Declaration.Right} has no referent");*/
            });
        }

        private string Name => Declaration.Name;
        public string Explain() => $"{CompactPosition} Variable `{Name}`";
        
        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} &{Name}";
        }
    }

    
    /// <summary>
    /// An AST that creates an object through (possibly overloaded) method invocation.
    /// <br/>The methods may be lifted; ie. For a recorded method `R member (A, B, C...)`,
    ///  given parameters of type F(A), F(B), F(C) (lifted over (T->), eg. T->A, T->B, T->C),
    ///  this AST may construct a function T->R that uses T to realize the parameters and pass them to `member`.
    /// <br/>Methods may be generic.
    /// </summary>
    /// <param name="Position">Position of the entire method call, including all arguments (ie. all of `MethodName(arg1, arg2)`)</param>
    /// <param name="MethodPosition">Position of the method name alone (ie. just `MethodName`)</param>
    /// <param name="Methods">Method signatures. These may be generic, requiring specialization before invocation.</param>
    /// <param name="Params">Arguments to the method</param>
    //Not necessarily a tex func type-- depends on method def
    public record MethodCall(PositionRange Position, PositionRange MethodPosition,
        LexicalScope EnclosingScope, Reflector.InvokedMethod[] Methods, params IAST[] Params) : 
        AST(Position, EnclosingScope, Params), IMethodAST<Reflector.InvokedMethod> {
        public IReadOnlyList<Reflector.InvokedMethod> Overloads => Methods;
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Reflector.InvokedMethod>? RealizableOverloads { get; set; }
        public (Reflector.InvokedMethod method, Dummy simplified)? SelectedOverload { get; set; }

        public Unifier WillSelectOverload(Reflector.InvokedMethod method, IImplicitTypeConverter? cast, Unifier u) {
            if (cast is IScopedTypeConverter { ScopeArgs: { } args }) {
                LocalScope = new LexicalScope(EnclosingScope);
                LocalScope.DeclareArgs(args);
                foreach (var a in Params)
                    a.ReplaceScope(EnclosingScope, LocalScope);
            }
            return u;
        }

        public override object? _RealizeWithoutCast() {
            var prms = new object?[Params.Length];
            for (int ii = 0; ii < prms.Length; ++ii)
                prms[ii] = Params[ii].Realize();
            var mi = SelectedOverload.Value.method.Mi;
            if (mi is Reflector.IGenericMethodSignature gm) {
                if (mi.Type.Unify(SelectedOverload.Value.simplified, Unifier.Empty) is { IsLeft: true } unifier) {
                    var resolution = gm.GenericTypes.Select(g => g.Resolve(unifier.Left)).SequenceL();
                    if (resolution.IsRight)
                        throw IAST.EnrichError(resolution.Right, MethodPosition);
                    mi = gm.Specialize(resolution.Left.ToArray());
                } else
                    throw new Exception($"SelectedOverload has unsound types for generic method {mi.AsSignature}");
            }
            return mi.InvokeStatic(prms);
        }

        public string Explain() => $"{CompactPosition} {(SelectedOverload?.method ?? Methods[0]).Mi.AsSignature}";

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} {(SelectedOverload?.method ?? Methods[0]).TypeEnclosedName}(";
            foreach (var w in IDebugPrint.PrintArgs(Params))
                yield return w;
            yield return ")";
        }
    }

    //Always a tex func type
    public record Block : AST, IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; }
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public bool PreferFirstOverload => true;

        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public Block(PositionRange Position, LexicalScope EnclosingScope, LexicalScope localScope, params IAST[] Params) : base(Position,
            EnclosingScope, Params) {
            this.LocalScope = localScope;
            var typs = Params.Select(p => new Variable() as TypeDesignation).ToArray();
            typs[^1] = typs[^1].MakeTExFunc();
            Overloads = new[] {
                Dummy.Method(typs[^1], typs), //(T,T,T,TExArgCtx->TEx<R>)->(TExArgCtx->TEx<R>)
            };
        }

        public bool ImplicitParameterCastEnabled(int index) => index == Params.Length - 1;

        public override object _RealizeWithoutCast() {
            //TODO may need to construct a new EF if localScope has lambda/func definitions
            return GetUnwrappedReturnType(this).MakeTypedLambda(tac => {
                var exs = Params.Select<IAST, Ex>((p, i) => p.Realize() switch {
                    Func<TExArgCtx, TEx> f => f(tac),
                    TEx t => t,
                    Ex ex => ex,
                    { } v => Ex.Constant(v),
                    null => throw new ReflectionException(Position, $"Block statement #{i} returned null")
                });
                return Ex.Block(LocalScope!.VariableDecls
                            .Where(v => !v.IsFunctionArgument)
                            .Select(v => v.Parameter(tac)).ToArray(),
                        exs);
            });
        }
        
        public string Explain() {
            return $"{CompactPosition} block<{GetReturnTypeDescr(this)}>";
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} block<{GetReturnTypeDescr(this)}>({{";
            foreach (var w in IDebugPrint.PrintArgs(Params, ";"))
                yield return w;
            yield return "})";
        }

    }

    //Type dependent on elements
    public record Array : AST, IMethodAST<Dummy> {
        private readonly Variable elementType = new();
        public IReadOnlyList<Dummy> Overloads { get; }
        public IReadOnlyList<ITypeTree> Arguments => Params;
        
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public Array(PositionRange Position, LexicalScope EnclosingScope, LexicalScope localScope, params IAST[] Params) : base(Position,
            EnclosingScope, Params) {
            this.LocalScope = localScope;
            Overloads = new[]{ Dummy.Method(new Known(Known.ArrayGenericType, elementType), 
                Params.Select(_ => elementType as TypeDesignation).ToArray()) };
        }

        public string Explain() {
            return $"{CompactPosition} {GetReturnTypeDescr(this)}[{Params.Length}]";
        }

        public override object _RealizeWithoutCast() {
            var typ = SelectedOverload.Value.simplified.Resolve(Unifier.Empty).LeftOrThrow;
            var array = System.Array.CreateInstance(typ, Params.Length);
            for (int ii = 0; ii < Params.Length; ++ii)
                array.SetValue(Params[ii].Realize(), ii);
            return array;
        }
        
        
        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} {GetReturnTypeDescr(this)}[{Params.Length}]{{";
            foreach (var w in IDebugPrint.PrintArgs(Params, ","))
                yield return w;
            yield return "}";
        }

    }
    

    //hardcoded values (number/typedvalue) may be tex-func or normal types depending on usage
    //eg. phase 10 <- 10 is Float/Int
    //    px 10 <- 10 is Func<TExArgCtx, TEx<float>>
    public record Number(PositionRange Position, LexicalScope EnclosingScope, float Value) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        private static readonly TypeDesignation[] FloatTypes = {
            TypeDesignation.FromType(typeof(float)).MakeTExFunc(),
            TypeDesignation.FromType(typeof(float)),
        };
        private static readonly TypeDesignation[] IntTypes = {
            TypeDesignation.FromType(typeof(int)).MakeTExFunc(),
            TypeDesignation.FromType(typeof(int)),
        };
        
        public TypeDesignation[] PossibleTypes { get; } =
            Math.Abs(Value - Math.Round(Value)) < 0.00001f ?
                FloatTypes.Concat(IntTypes).ToArray() :
                FloatTypes;
        public TypeDesignation? SelectedOverload { get; set; }
        public override object _RealizeWithoutCast() {
            var typ = SelectedOverload!.Resolve(Unifier.Empty);
            if (typ == typeof(float))
                return Value;
            if (typ == typeof(int))
                return Mathf.RoundToInt(Value);
            if (typ == typeof(Func<TExArgCtx, TEx<float>>))
                return AtomicBPYRepo.Const(Value);
            if (typ == typeof(Func<TExArgCtx, TEx<int>>))
                return (Func<TExArgCtx, TEx<int>>)(_ => Ex.Constant(Mathf.RoundToInt(Value)));
            throw new Exception("Undetermined numeric type");
        }
        
        public string Explain() => $"{CompactPosition} Number `{Value.ToString()}`";
        
        public IEnumerable<PrintToken> DebugPrint() {
            yield return Value.ToString();
        }
    }
    
    public record TypedValue<T>(PositionRange Position, LexicalScope EnclosingScope, T Value) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        public TypeDesignation[] PossibleTypes { get; } = {
            TypeDesignation.FromType(typeof(T)).MakeTExFunc(),
            TypeDesignation.FromType(typeof(T)),
        };
        public TypeDesignation? SelectedOverload { get; set; }

        public override object? _RealizeWithoutCast() {
            var typ = SelectedOverload!.Resolve(Unifier.Empty);
            if (typ == typeof(T))
                return Value;
            return (Func<TExArgCtx, TEx<T>>)(_ => Ex.Constant(Value));
        }

        public string Explain() => $"{CompactPosition} {typeof(T).SimpRName()} `{Value?.ToString()}`";
        public IEnumerable<PrintToken> DebugPrint() {
            yield return Value?.ToString() ?? "<null>";
        }
    }
    
    public record Failure : AST, IAST {
        public ReflectionException Exc { get; }
        public IAST? Basis { get; }
        public IEnumerable<NestedFailure> FirstPassErrors() =>
            new NestedFailure[1] {
                new(this, Basis?.FirstPassErrors().ToList() ?? new List<NestedFailure>())
            };
        
        public string Explain() => Basis switch {
            Failure f => f.Explain(),
            { } b => $"(ERROR) {b.Explain()}",
            _ => $"(ERROR) {Exc.Message}"
        };

        public Failure(ReflectionException exc, IAST basis) : base(basis.Position, basis.EnclosingScope, basis) {
            Exc = exc;
            Basis = basis;
        }

        public Failure(ReflectionException exc, LexicalScope scope) :
            base(exc.Position, scope) {
            Exc = exc;
        }
        
        public override object? _RealizeWithoutCast() => throw new StaticException("Cannot realize a Failure");

        public Either<List<(TypeDesignation, Unifier)>, TypeUnifyErr> PossibleUnifiers(TypeResolver resolver, Unifier unifier) {
            throw new StaticException("Cannot typecheck a Failure");
        }

        public Either<(TypeDesignation, Unifier), TypeUnifyErr> ResolveUnifiers(TypeDesignation resultType, TypeResolver resolver, Unifier unifier, bool _) {
            throw new StaticException("Cannot typecheck a Failure");
        }

        void ITypeTree.FinalizeUnifiers(Unifier unifier) {
            throw new StaticException("Cannot typecheck a Failure");
        }

        public TypeDesignation? SelectedOverloadReturnType =>
            throw new StaticException("Cannot typecheck a Failure");
    }
    
    public record NestedFailure(Failure Head, List<NestedFailure> Children) {
        //Note: these are non-inverted,
        //so the leaf exception is eg. "Couldn't parse PXY arg#1: www is not a float"
        // and the root exception is "Couldn't parse PatternSM"
        public IEnumerable<ReflectionException> AsExceptions() {
            if (Children.Count > 0)
                return Children
                    .SelectMany(c => c.AsExceptions())
                    .Select(e => Head.Exc.Copy(e));
            //the inner exception in Exc is ignored unless this is a leaf
            // (there shouldn't be any inner exceptions unless this is a leaf)
            return new[] {Head.Exc};
        }

        /*inversion method
        private static IEnumerable<ReflectionException> AsExceptions(List<NestedFailure> children, ReflectionException inner) {
            if (children.Count == 0)
                return new[] { inner };
            return children.SelectMany(c => AsExceptions(c.Children, c.Head.Exc.Copy(inner)));
        }*/
    }
}

}