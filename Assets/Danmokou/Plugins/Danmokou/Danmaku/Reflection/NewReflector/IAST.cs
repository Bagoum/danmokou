using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
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
    IEnumerable<ReflectionException> FirstPassExceptions() =>
        Params.SelectMany(p => p.FirstPassExceptions());

    private static TypeUnifyErr? PinBlockTooManyTypesErr(IAST root, TypeResolver resolver, Unifier u) {
        if (root is AST.Block b) {
            foreach (var child in b.Params) {
                if (child.PossibleUnifiers(resolver, u).TryL(out var possible) && possible.Count > 1) {
                    return PinBlockTooManyTypesErr(child, resolver, u) ?? 
                           new TypeUnifyErr.TooManyPossibleTypes(child, possible.Select(p => p.Item1).ToList());
                }
            }
        }
        return null;
    }
    
    /// <summary>
    /// (Stage 2) A function called on the root AST to typecheck the entire structure.
    /// </summary>
    public static Either<Type, TypeUnifyErr> Typecheck(IAST root, TypeResolver resolver, LexicalScope rootScope, Type? finalType = null) {
        var possible = root.PossibleUnifiers(resolver, Unifier.Empty);
        if (!possible.TryL(out var u1s))
            return possible.Right;
        List<(TypeDesignation, TypeDesignation, Unifier, Either<IImplicitTypeConverter, bool>)> LookupCasts(
            Func<TypeDesignation, TypeDesignation, IImplicitTypeConverter?> lookup) =>
            u1s.SelectNotNull(u1 => {
                var finalTypeDesig = u1.Item1;
                Either<IImplicitTypeConverter, bool> finalCast = false;
                var u = u1.Item2;
                if (finalType != null && finalType != typeof(void)) {
                    if (finalTypeDesig.Resolve().LeftOrNull?.IsWeakSubclassOf(finalType) is not true) {
                        finalTypeDesig = TypeDesignation.FromType(finalType);
                        if (finalTypeDesig.Unify(u1.Item1, u).TryL(out var sameU)) {
                            u = sameU;
                            finalCast = false;
                        } else if (lookup(finalTypeDesig, u1.Item1) is { } conv)
                            finalCast = new(conv);
                        else
                            return null as (TypeDesignation, TypeDesignation, Unifier, Either<IImplicitTypeConverter, bool>)?;
                    }
                }
                return (finalTypeDesig, u1.Item1, u1.Item2, finalCast);
            }).ToList();
        
        var cu1s = LookupCasts(DMKScope.TryFindConversion);
        if (cu1s.Count == 0) {
            cu1s = LookupCasts(DMKScope.TryFindLowPriorityConversion);
            if (cu1s.Count == 0)
                throw new BadTypeException($"This script was supposed to have a return type of {finalType!.RName()}, " +
                                           $"but the typechecker determined that its return type was one of: " +
                                           $"{string.Join("; ", u1s.Select(u1 => u1.Item1.SimpRName()))}.");
        }
        
        if (cu1s.Count != 1) {
            //Prefer to get a TooManyOverloads or TooManyTypes from the methods proper if possible
            /*foreach (var u in cu1s) 
                if (root.ResolveUnifiers(u.Item2, resolver, Unifier.Empty).TryR(out var err))
                    return err;*/
            foreach (var u in cu1s)
                if (PinBlockTooManyTypesErr(root, resolver, u.Item3) is { } err)
                    return err;
            return new TypeUnifyErr.TooManyPossibleTypes(root, cu1s.Select(t => t.Item2).ToList());
        }

        Either<(TypeDesignation, Unifier), TypeUnifyErr> tDes;
        if (root is AST.Block block && block.Overloads[0].Last is TypeDesignation.Variable vT) {
            block.FinalCast = cu1s[0].Item4;
            tDes = root.ResolveUnifiers(cu1s[0].Item1, resolver, cu1s[0].Item3.Without(vT), false, true);
        } else
            tDes = root.ResolveUnifiers(cu1s[0].Item1, resolver, cu1s[0].Item3, cu1s[0].Item4, true);
        if (tDes.IsRight)
            return tDes.Right;
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
                sb.Append($"\nIt might be any of the following: {string.Join(", ", rt.Select(r => r.SimpRName()))}");
            sb.Append($"\nYou can specify the type as eg. `var x::float = 5`.");
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
            if (unbound.Tree.SelectedOverloadReturnTypeNoCast?.IsResolved is true &&
                unbound.Tree.ImplicitCast?.ResultType is { IsResolved: false} rt) {
                sb.Append("The result type of this expression could not be determined because this expression is " +
                          $"implicitly cast to an underdetermined type {rt.SimpRName()}.");
            } else if (unbound.Tree is AST.Reference r && r.Value.TryL(out var decl)) {
                UntypedRef(decl);
            } else if (unbound.Tree is AST.Number n) {
                sb.Append($"It's not clear whether this number is an int or a float.");
            } else
                sb.Append("The result type of this expression could not be determined.");
        } else if (e is TypeUnifyErr.RestrictionFailure rf) {
            sb.Append($"Type {rf.LReq} and {rf.RReq} could not be unified, " +
                      $"because the first resolved to {rf.LRes} and the second to {rf.RRes}, " +
                      $"which is specified to have one of the following restricted types:" +
                      $"\n\t{string.Join("; ", rf.LRes.RestrictedTypes!.Select(r => r.SimpRName()))}");
        }  else if (e is TypeUnifyErr.RecursionBinding rbr) {
            sb.Append($"Type {rbr.LReq} and {rbr.RReq} could not be unified because of a circular binding:" +
                      $"the first resolved to {rbr.LResolved}, which occurs in {rbr.RResolved}.");
        } else if (e is TypeUnifyErr.NoPossibleOverload npo) {
            pos = (npo.Tree as IAST)!.Position;
            string setsDisplay(IAST?[] prams) {
                var ai = 0;
                return string.Join("\n\t",
                    prams.Select((p, pi) => 
                        $"Parameter #{pi + 1}: " + (p == null ? "<N/A>" : 
                            (string.Join(" or ", npo.ArgSets[ai++].Select(s => s.Item1.SimpRName()).Distinct()) +
                            $" (at {p.Position})"))
                    ));
            }
            if (npo.Tree is AST.MethodCall m) {
                pos = m.MethodPosition;
                var calledAs = (m is AST.InstanceMethodCall im) ? im.Name : m.Overloads[0].CalledAs;
                var plural = m.Overloads.Count != 1 ? "s" : "";
                if (m.Overloads.Count == 1) {
                    sb.Append($"Typechecking failed for method {m.Overloads[0].Mi.AsSignatureWithRestrictions}.");
                } else {
                    sb.Append($"Typechecking failed for method call `{calledAs}`, " +
                              $"which maps to {m.Overloads.Count} overloaded methods:\n\t" +
                              $"{string.Join("\n\t", m.Overloads.Select(o => o.Mi.AsSignatureWithRestrictions))}");
                }
                sb.Append($"\nThe parameters were inferred to have the following types:\n\t{setsDisplay(m.Params)}");
                sb.Append($"\nThese parameters cannot be applied to the above method{plural}.");
            } else if (npo.Tree is AST.Block) {
                throw new StaticException("Code blocks should not return NoPossibleOverload");
            } else if (npo.Tree is AST.Array ar) {
                sb.Append($"Typechecking failed for this array.\nAll elements of an array must have the same type, " +
                          $"but the parameters were inferred to have the following types:\n\t{setsDisplay(ar.Params)}");
            } else if (npo.Tree is AST.Conditional cond) {
                sb.Append($"Typechecking failed for this conditional.\nThe condition (param 1) must have type bool " +
                          $"and the blocks (param 2/3) must have the same type, but they were inferred to have " +
                          $"the following types:\n\t{setsDisplay(cond.Params)}");
            } else if (npo.Tree is AST.PartialMethodCall pm) {
                pos = pm.MethodPosition;
                var plural = pm.Overloads.Count != 1 ? "s" : "";
                if (pm.Overloads.Count == 1) {
                    sb.Append($"Typechecking failed for method {pm.Overloads[0].Meth.Mi.AsSignatureWithRestrictions}.");
                } else {
                    sb.Append($"Typechecking failed for method call `{pm.Overloads[0].Meth.CalledAs}`, " +
                              $"which maps to {pm.Overloads.Count} overloaded methods:\n\t" +
                              $"{string.Join("\n\t", pm.Overloads.Select(o => o.Meth.Mi.AsSignatureWithRestrictions))}");
                }
                sb.Append($"\nThe parameters were inferred to have the following types:\n\t{setsDisplay(pm.Params)}");
                sb.Append($"\nThese parameters cannot be partially applied to the above method{plural}.");
            } else if (npo.Tree is AST.LambdaCall lc) {
                pos = lc.MethodPosition;
                sb.Append($"Typechecking failed for lambda invocation. The first parameter must be a Func," +
                          $" and the remaining parameters must satisfy the required types of the Func." +
                          $"\nHowever, the parameters were inferred to have the following types:\n\t{setsDisplay(lc.Params)}");
            } else if (npo.Tree is AST.PartialLambdaCall plc) {
                pos = plc.MethodPosition;
                sb.Append($"Typechecking failed for partial lambda invocation. The first parameter must be a Func," +
                          $" and the remaining parameters must satisfy some of the required types of the Func." +
                          $"\nHowever, the parameters were inferred to have the following types:\n\t{setsDisplay(plc.Params)}");
            } else if (npo.Tree is AST.Return ret) {
                pos = ret.Position;
                sb.Append($"This return statement was expected to have type {ret.Overloads[0].Last.SimpRName()}," +
                          $" but the return parameter was inferred to have a different type:\n\t{setsDisplay(ret.Params)}");
            } else if (npo.Tree is AST.ScriptFunctionCall sfc) {
                pos = sfc.Position;
                sb.Append($"Typechecking failed for this invocation of script function {sfc.Definition.AsSignature()}.");
                sb.Append($"\nThe parameters were inferred to have the following types:\n\t{setsDisplay(sfc.Params)}");
                sb.Append($"\nThese parameters cannot be applied to the above script function.");
            } else if (npo.Tree is AST.ScriptFunctionDef sfd) {
                pos = sfd.Position;
                sb.Append($"Typechecking failed for the default values of script function {sfd.Definition.AsSignature()}.");
                var u = npo.ArgSets[^1][0].Item2;
                var args = sfd.Definition.Args.Select((x, i) => $"Parameter #{i + 1}: {u[x.TypeDesignation].SimpRName()}");
                sb.Append($"\nThe parameters were inferred to have the following types:\n\t{string.Join("\n\t", args)}");
                sb.Append($"\nThe default values were inferred to have the following types:\n\t{setsDisplay(sfd.Definition.Defaults)}");
            } else
                throw new NotImplementedException(npo.Tree.GetType().SimpRName());
        } else if (e is TypeUnifyErr.MultipleOverloads mover) {
            if (mover.Tree is AST.MethodCall m) {
                pos = m.MethodPosition;
                sb.Append($"Typechecking failed for method call `{m.Overloads[0].CalledAs}`, " +
                          $"which maps to {m.RealizableOverloads!.Count} overloaded methods:\n\t" +
                          $"{string.Join("\n\t", m.RealizableOverloads.Select(o => o.Mi.AsSignature))}");
                sb.Append($"\nThe typechecker could not determine whether the method call should return " +
                          $"type {mover.First.SimpRName()} or {mover.Second.SimpRName()}.");
            } else if (mover.Tree is AST.Reference r) {
                pos = r.Position;
                sb.Append(
                    $"The typechecker could not determine whether `{r.Name}` is of type {mover.First.SimpRName()} or {mover.Second.SimpRName()}.");
            } else {
                pos = (mover.Tree as IAST)?.Position ?? pos;
                sb.Append($"The typechecker could not determine whether this expression is of type " +
                          $"{mover.First.SimpRName()} or {mover.Second.SimpRName()}.");
            }
        } else if (e is UntypedVariable ut) {
            UntypedRef(ut.Declaration);
        } else if (e is VoidTypedVariable vt) {
            pos = vt.Declaration.Position;
            sb.Append($"The variable `{vt.Declaration.Name}` was inferred to have a type of void.");
        } else if (e is UntypedReturn ur) {
            var fn = ur.Return.Function;
            pos = fn.Position;
            sb.Append($"The return type of the function {fn.Name} could not be determined.");
        } else if (e is TypeUnifyErr.TooManyPossibleTypes pt) {
            pos = (pt.Tree as IAST)!.Position;
            sb.Append("Couldn't determine a unique type for this expression.\n It might be any of the following: " +
                      $"{string.Join("; ", pt.PossibleTypes.Select(r => r.SimpRName()))}");
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
            throw new NotImplementedException(e.GetType().SimpRName());
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
    ///  <see cref="ITypeTree.ResolveUnifiers"/> is T, then this function returns a TEx&lt;T&gt;,
    ///  which the caller can be compile into a delegate with any top-level arguments.
    /// </summary>
    TEx Realize(TExArgCtx tac);
    
    IEnumerable<PrintToken> IDebugPrint.DebugPrint() => new PrintToken[] { Explain() };

}

public interface IMethodAST<T> : IAST, IMethodTypeTree<T> where T : IMethodDesignation {
}

}