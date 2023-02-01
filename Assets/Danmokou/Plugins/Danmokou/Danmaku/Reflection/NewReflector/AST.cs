using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection;
using JetBrains.Annotations;
using Mizuhashi;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static BagoumLib.Unification.TypeDesignation;

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
    /// Get all nodes in this tree, enumerated in preorder.
    /// </summary>
    IEnumerable<IAST> EnumeratePreorder() => 
        Params.SelectMany(p => p.EnumeratePreorder()).Prepend(this);

    /// <summary>
    /// Get all nodes in this tree, enumerated one level at a time.
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
        } else if (e is TypeUnifyErr.UnboundRestr) {
            //this should occur as unboundpromise/untypedvariable instead
            sb.Append($"There are unbound variables. (This error should not occur. Please report it.)");
        } else if (e is TypeUnifyErr.RecursionBinding rbr) {
            sb.Append($"Type {rbr.LReq} and {rbr.RReq} could not be unified because of a circular binding:" +
                      $"the first resolved to {rbr.LResolved}, which occurs in {rbr.RResolved}.");
        } else if (e is TypeUnifyErr.NoPossibleOverload npo) {
            pos = (npo.Tree as IAST)!.Position;
            string setsDisplay(IAST[] prams) => 
                string.Join("\n\t", npo.ArgSets.Select((set, i) => 
                    $"Parameter #{i + 1}: {string.Join(" or ", set.Select(s => s.Item1.SimpRName()).Distinct())}" +
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
                throw new NotImplementedException(npo.Tree.GetType().RName());
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
    /// (Stage 3) Verify that types, declarations, etc. are sound, and make simplifications/specializations based on the full AST context.
    /// </summary>
    public IEnumerable<ReflectionException> Verify() => VerifyChildren(this);

    public static IEnumerable<ReflectionException> VerifyChildren(IAST ast) => 
        ast.Params.Length > 0 ? ast.Params.SelectMany(p => p.Verify()) : Array.Empty<ReflectionException>();

    /// <summary>
    /// Call this function to insert a scope `inserted` below the scope `prev` in descendants of this tree.
    /// </summary>
    Either<Unifier, TypeUnifyErr> ReplaceScope(LexicalScope prev, LexicalScope inserted, Unifier u);
    
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
    public virtual Either<Unifier, TypeUnifyErr> ReplaceScope(LexicalScope prev, LexicalScope inserted, Unifier u) {
        if (LocalScope?.Parent == prev) {
            LocalScope.UpdateParent(inserted);
        }
        if (EnclosingScope == prev) {
            EnclosingScope = inserted;
        }
        //Necessary to keep this outside so Reference can rebind at arbitrary depths
        return ThreadChildReplaceScope(prev, inserted, u);
    }

    protected Either<Unifier, TypeUnifyErr>
        ThreadChildReplaceScope(LexicalScope prev, LexicalScope inserted, Unifier u) {
        foreach (var a in Params) {
            var uerr = a.ReplaceScope(prev, inserted, u);
            if (uerr.IsRight)
                return uerr.Right;
            u = uerr.Left;
        }
        return u;
    }

    public object? Realize() {
        var withoutCast = _RealizeWithoutCast();
        if (ImplicitCast == null)
            return withoutCast;
        var conv = ImplicitCast.Converter.Converter;
        if (conv is FixedImplicitTypeConv fixedConv) {
            return fixedConv.Convert(withoutCast);
        } else if (conv is GenericTypeConv1 gtConv) {
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

    private static Type GetUnwrappedType(TypeDesignation texType) =>
        texType.UnwrapTExFunc().Resolve(Unifier.Empty).LeftOrThrow;


    // ----- Subclasses -----
    

    //todo: enum typing via ident?
    /// <summary>
    /// A reference to a declaration of a variable, function, etc, that is used as a value (eg. in `x`++ or `f`(2)).
    /// </summary>
    //Always a tex func type-- references are bound to expressions or EnvFrame
    public record Reference(PositionRange Position, LexicalScope EnclosingScope, IUsedVariable Declaration) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        /// <inheritdoc cref="IAtomicTypeTree.SelectedOverload"/>
        public TypeDesignation? SelectedOverload { get; set; }
        /// <inheritdoc cref="IAtomicTypeTree.PossibleTypes"/>
        public TypeDesignation[] PossibleTypes { get; } = { Declaration.TypeDesignation.MakeTExFunc() };

        public override Either<Unifier, TypeUnifyErr> ReplaceScope(LexicalScope prev, LexicalScope inserted, Unifier u) {
            var uerr = base.ReplaceScope(prev, inserted, u);
            if (uerr.IsRight)
                return uerr.Right;
            if (Declaration is VarDeclPromise promise) {
                return promise.MaybeBind(inserted, uerr.Left);
            }
            return uerr.Left;
        }

        public IEnumerable<ReflectionException> Verify() {
            if (!Declaration.IsBound)
                yield return new ReflectionException(Position, 
                    $"The variable {Name} was used but was never declared.");
        }

        public override object _RealizeWithoutCast() {
            return GetUnwrappedType(SelectedOverload!).MakeTypedLambda(tac => {
                var v = Declaration.Bound;
                //if the value is unchanging and we are within a GCXU,
                // then automatically expose the value via ICRR, as with the standard auto-expose pattern.
                if (v.Assignments == 1) {
                    if (tac.Ctx.GCXURefs is { } icrr 
                        && icrr.TryResolve(v.FinalizedType!, v.Name, out var ex))
                        return ex;
                    if (tac.Ctx.GCXURefs is CompilerHelpers.GCXUDummyResolver &&
                        ReflectEx.GetAliasFromStack(v.Name, tac) is { } stackEx)
                        return stackEx;
                }
                return EnclosingScope.GetLocalOrParent(tac, tac.EnvFrame, v);
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

        public string Name => Declaration.Name;
        public string Explain() => $"{CompactPosition} Variable `{Name}`";
        
        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} &{Name}";
        }
    }

    
    /// <summary>
    /// An AST that creates an object through (possibly overloaded) method invocation.
    /// <br/>Methods may be lifted; ie. for a recorded method `R member (A, B, C...)`,
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

        public Either<Unifier, TypeUnifyErr> WillSelectOverload(Reflector.InvokedMethod method, IImplicitTypeConverterInstance? cast, Unifier u) {
            if (cast?.Converter is IScopedTypeConverter c && (c.ScopeArgs != null || c.Kind != ScopedConversionKind.Trivial)) {
                LocalScope = new ScopedConversionScope(c, EnclosingScope);
                LocalScope.DeclareImplicitArgs(c.ScopeArgs ?? System.Array.Empty<IDelegateArg>());
                var uerr = ThreadChildReplaceScope(EnclosingScope, LocalScope, u);
                if (uerr.IsRight)
                    return uerr.Right;
                u = uerr.Left;
            }
            if (method.Mi.GetAttribute<CreatesInternalScopeAttribute>() is { } cis) {
                foreach (var mcall in Params[cis.Index].EnumerateByLevel().OfType<MethodCall>()) {
                    var indices = mcall.Overloads
                        .SelectNotNull(o => o.Mi.GetAttribute<ScopeRaiseSourceAttribute>()?.Index)
                        .Distinct()
                        .ToList();
                    if (indices.Count == 0) continue;
                    //this needs to be done before type resolution on any children,
                    // so we can't wait for the child overload to be determined
                    if (indices.Count > 1)
                        throw new StaticException(
                            $"{nameof(ScopeRaiseSourceAttribute)} should not be used differently between overloads");
                    if (mcall.Params[indices[0]] is not Block { LocalScope : { } ls } b)
                        break;
                    if (LocalScope != null)
                        throw new Exception(
                            $"MethodCall already has a local scope, it cannot be subject to {nameof(CreatesInternalScopeAttribute)}");
                    LocalScope = ls;
                    b.LocalScope = null;
                    
                    var uerr = ThreadChildReplaceScope(EnclosingScope, LocalScope, u);
                    if (uerr.IsRight)
                        return uerr.Right;
                    return uerr.Left;
                }
            }
            return u;
        }

        public void FinalizeUnifiers(Unifier unifier) {
            IMethodTypeTree<Reflector.InvokedMethod>._FinalizeUnifiers(this, unifier);
            if (SelectedOverload!.Value.method.Mi.GetAttribute<AssignsAttribute>() is { } attr) {
                foreach (var ind in attr.Indices) {
                    foreach (var refr in Params[ind].EnumeratePreorder().OfType<Reference>())
                        if (refr.Declaration.IsBound)
                            refr.Declaration.Bound.Assignments++;
                }
            }
        }

        public override object? _RealizeWithoutCast() {
            var prms = new object?[Params.Length];
            for (int ii = 0; ii < prms.Length; ++ii)
                prms[ii] = Params[ii].Realize();
            var mi = SelectedOverload!.Value.method.Mi;
            if (mi is Reflector.IGenericMethodSignature gm) {
                //ok to use shared type as we are doing a check on empty unifier that is discarded
                if (mi.SharedType.Unify(SelectedOverload.Value.simplified, Unifier.Empty) is { IsLeft: true } unifier) {
                    var resolution = gm.SharedGenericTypes.Select(g => g.Resolve(unifier.Left)).SequenceL();
                    if (resolution.IsRight)
                        throw IAST.EnrichError(resolution.Right, MethodPosition);
                    mi = gm.Specialize(resolution.Left.ToArray());
                } else
                    throw new ReflectionException(Position, 
                        $"SelectedOverload has unsound types for generic method {mi.AsSignature}");
            }
            if (LocalScope != null && mi.GetAttribute<CreatesInternalScopeAttribute>() is { } cis) {
                switch (prms[cis.Index]) {
                    case GenCtxProperties props:
                        props.IterationScope = LocalScope;
                        break;
                    case GenCtxProperty[] props:
                        prms[cis.Index] = props.Append(GenCtxProperty._AssignLexicalScope(LocalScope)).ToArray();
                        break;
                    default:
                        throw new StaticException(
                            $"CreatesInternalScope annotation on method {mi.AsSignature} is incorrect; {cis.Index}th" +
                            $"argument is not GCXProps or GCXProp[]");
                }
            }
            return mi.InvokeStatic(this, prms);
        }
    
        public ReflectionException Raise(Helpers.NotWriteableException exc) =>
            new (Position, Params[exc.ArgIndex].Position, exc.Message, exc.InnerException);

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
        public (VarDecl variable, ImplicitArgDecl fnArg)[]? TopLevelArgs { get; private set; }
        public Block(PositionRange Position, LexicalScope EnclosingScope, LexicalScope localScope, params IAST[] Params) : base(Position,
            EnclosingScope, Params) {
            this.LocalScope = localScope;
            var typs = Params.Select(p => new Variable() as TypeDesignation).ToArray();
            typs[^1] = typs[^1].MakeTExFunc();
            Overloads = new[] {
                Dummy.Method(typs[^1], typs), //(T1,T2...,TExArgCtx->TEx<R>)->(TExArgCtx->TEx<R>)
            };
        }

        public Block WithTopLevelArgs(params (VarDecl, ImplicitArgDecl)[] args) {
            foreach (var (v, _) in (TopLevelArgs = args)) {
                v.Assignments++;
            }
            return this;
        }

        public bool ImplicitParameterCastEnabled(int index) => index == Params.Length - 1;

        public override object _RealizeWithoutCast() {
            //TODO copy handling to array
            return GetUnwrappedType(SelectedOverload!.Value.simplified.Last).MakeTypedLambda(tac => {
                IEnumerable<Ex> Stmts() {
                    var prmStmts = Params.Select<IAST, Ex>((p, i) => p.Realize() switch {
                        Func<TExArgCtx, TEx> f => f(tac),
                        TEx t => t,
                        Ex ex => ex,
                        { } v => Ex.Constant(v),
                        null => throw new ReflectionException(Position, $"Block statement #{i} returned null")
                    });
                    if (TopLevelArgs is not { } decls)
                        return prmStmts;
                    return decls
                        .Select(d => d.variable.Value(tac.EnvFrame, tac).Is(d.fnArg.Value(tac.EnvFrame, tac)))
                        .Concat(prmStmts);
                }
                //LocalScope can be "stolen" by MethodCall, in which case we just use an expression
                if (LocalScope is null)
                    return Ex.Block(Stmts());
                else if (LocalScope.UseEF) {
                    var parentEf = tac.MaybeGetByType<EnvFrame>(out _) ?? Ex.Constant(null, typeof(EnvFrame));
                    var ef = Ex.Parameter(typeof(EnvFrame), "ef");
                    tac = tac.MaybeGetByType<EnvFrame>(out _) is { } ?  
                        tac.MakeCopyForType<EnvFrame>(ef) :
                        tac.Append("rootEnvFrame", ef);
                    var makeEf = ef.Is(EnvFrame.exCreate.Of(Ex.Constant(LocalScope), parentEf));
                    
                    return Ex.Block(new[] { ef }, Stmts().Prepend(makeEf));
                } else {
                    return Ex.Block(
                        LocalScope.VariableDecls.SelectMany(v => v.decls)
                        .SelectNotNull(v => v.DeclaredParameter(tac)).ToArray(),
                        Stmts());
                }
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
        public Array(PositionRange Position, LexicalScope EnclosingScope, params IAST[] Params) : base(Position,
            EnclosingScope, Params) {
            Overloads = new[]{ Dummy.Method(new Known(Known.ArrayGenericType, elementType), 
                Params.Select(_ => elementType as TypeDesignation).ToArray()) };
        }

        public string Explain() {
            return $"{CompactPosition} {GetReturnTypeDescr(this)}[{Params.Length}]";
        }

        public override object _RealizeWithoutCast() {
            var typ = SelectedOverload!.Value.simplified.Resolve(Unifier.Empty).LeftOrThrow;
            var array = System.Array.CreateInstance(typ.GetElementType()!, Params.Length);
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
    public record Number : AST, IAST, IAtomicTypeTree {
        private static readonly Known[] FloatTypes = {
            new Known(typeof(float)).MakeTExFunc(),
            new Known(typeof(float)),
        };
        private static readonly Known[] IntTypes = {
            new Known(typeof(int)).MakeTExFunc(),
            new Known(typeof(int)),
        };
        public float Value { get; }
        private Variable Var { get; }
        public TypeDesignation[] PossibleTypes { get; }
        public TypeDesignation? SelectedOverload { get; set; }
        public Number(PositionRange Position, LexicalScope EnclosingScope, float Value) : base(Position,
            EnclosingScope) {
            this.Value = Value;
            Var = new Variable() {
                RestrictedTypes = Math.Abs(Value - Math.Round(Value)) < 0.00001f ?
                    FloatTypes.Concat(IntTypes).ToArray() :
                    FloatTypes
            };
            PossibleTypes = new TypeDesignation[] { Var };
        }

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
        
        public string Explain() => $"{CompactPosition} Number `{Value}`";
        
        public IEnumerable<PrintToken> DebugPrint() {
            yield return Value.ToString(CultureInfo.InvariantCulture);
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

        public TypeDesignation SelectedOverloadReturnType =>
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