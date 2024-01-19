using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Mizuhashi;

namespace Danmokou.Reflection2 {

public interface IAST : ITypeTree, IDebugAST {
    /// <summary>
    /// All ASTs that are direct children of this.
    /// </summary>
    public IAST[] Params { get; }
    
    /// <summary>
    /// The scope inside which this AST is declared.
    /// </summary>
    public LexicalScope EnclosingScope { get; }

    /// <summary>
    /// Return a deep copy of this tree.
    /// </summary>
    public IAST CopyTree();

    /// <summary>
    /// Get all nodes in this tree, enumerated in preorder.
    /// </summary>
    IEnumerable<IAST> EnumeratePreorder() => 
        Params.SelectMany(p => p.EnumeratePreorder()).Prepend(this);

    /// <summary>
    /// Get all nodes in this tree, enumerated breadth-first.
    /// </summary>
    IEnumerable<IAST> EnumerateByLevel() {
        var q = new Queue<IAST>();
        q.Enqueue(this);
        while (q.TryDequeue(out var ast)) {
            yield return ast;
            foreach (var arg in ast.Params)
                q.Enqueue(arg);
        }
    }

    /// <summary>
    /// Set warnings and other non-fatal messages on this level of the AST.
    /// </summary>
    public void SetDiagnostics(ReflectDiagnostic[] diagnostics);
    public IEnumerable<ReflectDiagnostic> WarnUsage();

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
    public static Either<Type, TypeUnifyErr> Typecheck(IAST root, TypeResolver resolver, LexicalScope rootScope) {
        var possible = root.PossibleUnifiers(resolver, Unifier.Empty);
        if (!possible.TryL(out var u1s))
            return possible.Right;
        if (u1s.Count != 1) {
            //Prefer to get a TooManyOverloads from the methods proper if possible
            foreach (var u in u1s) 
                if (root.ResolveUnifiers(u.Item1, resolver, Unifier.Empty).TryR(out var err))
                    return err;
            return new TypeUnifyErr.TooManyPossibleTypes(u1s.Select(t => t.Item1).ToList());
        }
        var tDes = root.ResolveUnifiers(u1s[0].Item1, resolver, u1s[0].Item2);
        if (tDes.IsRight)
            return tDes.Right;
        //var _ = globalScope.FinalizeVariableTypes(tDes.Left.Item2);
        root.FinalizeUnifiers(tDes.Left.Item2);
        var varErr = rootScope.FinalizeVariableTypes(tDes.Left.Item2);
        if (varErr.IsRight)
            return varErr.Right;
        if (!root.IsFullyResolved) {
            //This error is fairly rare, I think it only occurs when the top-level return type cannot be determined,
            // eg in 5 * 4 * 3 * x1, where x1 may have any type and the total expression may have any type.
            var (binding, tree) = root.UnresolvedVariables().First();
            return new TypeUnifyErr.UnboundRestr(binding, tree);
        }
        return tDes.Left.Item1.Resolve(tDes.Left.Item2);
    }

    public static (string, PositionRange?) _EnrichError(TypeUnifyErr e, PositionRange? pos = null) {
        var sb = new StringBuilder();
        void UntypedRef(VarDecl decl) {
            pos = decl.Position;
            sb.Append($"The type of the variable `{decl.Name}` could not be determined.");
            if (decl.FinalizedTypeDesignation is TypeDesignation.Variable { RestrictedTypes: { } rt })
                sb.Append($"\nIt might be any of the following: {string.Join(", ", rt.Select(r => r.ExRName()))}");
        }
        if (e is TypeUnifyErr.NotEqual ne)
            sb.Append($"Type {ne.LReq} and {ne.RReq} could not be unified, " +
                      $"because the first resolved to {ne.LResolved} and the second to {ne.RResolved}.");
        else if (e is TypeUnifyErr.ArityNotEqual ane) {
            sb.Append($"Type {ane.LeftReq} and {ane.RightReq} could not be unified, " +
                      $"because the first resolved to {ane.LResolved} and the second to {ane.RResolved}," +
                      $"which have different arities " +
                      $"({ane.LResolved.Arguments.Length} and {ane.RResolved.Arguments.Length} respectively).");
        } else if (e is TypeUnifyErr.UnboundRestr unbound) {
            pos = (unbound.Tree as IAST)!.Position;
            if (unbound.Tree is AST.Reference { Declaration: not null } r) {
                UntypedRef(r.Declaration);
            } else if (unbound.Tree is AST.Number n) {
                sb.Append($"It's not clear whether this number is an int or a float.");
            } else
                sb.Append("The result type of this expression could not be determined.");
        } else if (e is TypeUnifyErr.RecursionBinding rbr) {
            sb.Append($"Type {rbr.LReq} and {rbr.RReq} could not be unified because of a circular binding:" +
                      $"the first resolved to {rbr.LResolved}, which occurs in {rbr.RResolved}.");
        } else if (e is TypeUnifyErr.NoPossibleOverload npo) {
            pos = (npo.Tree as IAST)!.Position;
            string setsDisplay(IAST[] prams) =>
                string.Join("\n\t", npo.ArgSets.Select((set, i) =>
                    $"Parameter #{i + 1}: {string.Join(" or ", set.Select(s => s.Item1.ExRName()).Distinct())}" +
                    $" (at {prams[i].Position})"));
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
                sb.Append($"\nThe parameters were inferred to have the following types:\n\t{setsDisplay(m.Params)}");
                sb.Append($"\nThese parameters cannot be applied to the above method{plural}.");
            } else if (npo.Tree is AST.Block) {
                throw new StaticException("Code blocks should not return NoPossibleOverload");
            } else if (npo.Tree is AST.Array ar) {
                sb.Append($"Typechecking failed for this array.\nAll elements of an array must have the same type, " +
                          $"but the parameters were inferred to have the following types:\n\t{setsDisplay(ar.Params)}");
            } else
                throw new NotImplementedException(npo.Tree.GetType().ExRName());
        } else if (e is TypeUnifyErr.MultipleOverloads mover) {
            if (mover.Tree is AST.MethodCall m) {
                pos = m.MethodPosition;
                sb.Append($"Typechecking failed for method call `{m.Overloads[0].CalledAs}`, " +
                          $"which maps to {m.RealizableOverloads!.Count} overloaded methods:\n\t" +
                          $"{string.Join("\n\t", m.Overloads.Select(o => o.Mi.AsSignature))}");
                sb.Append($"\nThe typechecker could not determine whether the method call should return " +
                          $"type {mover.First.ExRName()} or {mover.Second.ExRName()}.");
            } else if (mover.Tree is AST.Reference r) {
                pos = r.Position;
                sb.Append(
                    $"The typechecker could not determine whether `{r.Name}` is of type {mover.First.ExRName()} or {mover.Second.ExRName()}.");
            } else
                throw new NotImplementedException(mover.Tree.GetType().ExRName());
        } else if (e is UntypedVariable ut) {
            UntypedRef(ut.Declaration);
        } else if (e is TypeUnifyErr.TooManyPossibleTypes pt) {
            sb.Append("Couldn't determine the final type of the entire script.\n It might be any of the following: " +
                      $"{string.Join(", ", pt.PossibleTypes.Select(r => r.ExRName()))}");
        } else if (e is TypeUnifyErr.NoResolvableOverload nro) {
            pos = (nro.Tree as IAST)!.Position;
            sb.Append(
                $"Couldn't determine an overload for this expression. It was expected to return type {nro.Required.SimpRName()}, " +
                $"but all overloads failed. The list of failures are as follows:\n\t" +
                string.Join("\n\t", nro.Overloads.Select(o => {
                    var (err, pos) = _EnrichError(o.Item2);
                    return $"{pos}: {err.Replace("\n", "\n\t")}";
                })));
        } else
            throw new NotImplementedException(e.GetType().ExRName());
        return (sb.ToString(), pos);
    }

    public static Exception EnrichError(TypeUnifyErr e, PositionRange? pos = null) {
        var (s, _pos) = _EnrichError(e, pos);
        return _pos is { } p ? new ReflectionException(p, s) : new Exception(s);
    }

    /// <summary>
    /// (Stage 3) Verify that types, declarations, etc. are sound, and make simplifications/specializations based on the full AST context.
    /// </summary>
    public IEnumerable<ReflectionException> Verify() => VerifyChildren(this);

    public static IEnumerable<ReflectionException> VerifyChildren(IAST ast) => 
        ast.Params.Length > 0 ? ast.Params.SelectMany(p => p.Verify()) : Array.Empty<ReflectionException>();

    /// <summary>
    /// Call this function to insert a scope `inserted` below the scope `prev` in descendants of this tree.
    /// </summary>
    void ReplaceScope(LexicalScope prev, LexicalScope inserted);
    
    /// <summary>
    /// (Stage 4) Create an executable script out of this AST. Specifically, if the return type provided to
    ///  <see cref="ITypeTree.ResolveUnifiers"/> is T, then this function returns a Func&lt;TExArgCtx,TEx&lt;T&gt;&gt;,
    ///  which the caller can be compile into a delegate with any top-level arguments.
    /// </summary>
    Func<TExArgCtx, TEx> Realize();
    
    IEnumerable<PrintToken> IDebugPrint.DebugPrint() => new PrintToken[] { Explain() };

}

public interface IMethodAST<T> : IAST, IMethodTypeTree<T> where T : IMethodDesignation {
}

}