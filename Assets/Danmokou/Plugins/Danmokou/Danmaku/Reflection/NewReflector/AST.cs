using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
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
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.SM;
using JetBrains.Annotations;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static BagoumLib.Unification.TypeDesignation;
using SemanticTokenModifiers = Danmokou.Reflection.SemanticTokenModifiers;
using SemanticTokenTypes = Danmokou.Reflection.SemanticTokenTypes;

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

    public static Exception EnrichError(TypeUnifyErr e, PositionRange? pos = null) {
        var sb = new StringBuilder();
        void UntypedRef(VarDecl decl) {
            pos = decl.Position;
            sb.Append($"The type of the variable `{decl.Name}` could not be determined.");
            if (decl.FinalizedTypeDesignation is Variable { RestrictedTypes: { } rt })
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
            } else 
                throw new NotImplementedException(mover.Tree.GetType().ExRName());
        } else if (e is UntypedVariable ut) {
            UntypedRef(ut.Declaration);
        } else if (e is TypeUnifyErr.TooManyPossibleTypes pt) {
            sb.Append("Couldn't determine the final type of the entire script.\n It might be any of the following: " +
                      $"{string.Join(", ", pt.PossibleTypes.Select(r => r.ExRName()))}");
        } else
            throw new NotImplementedException(e.GetType().ExRName());

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
    void ReplaceScope(LexicalScope prev, LexicalScope inserted);
    
    /// <summary>
    /// (Stage 4) Create an executable script out of this AST.
    /// </summary>
    object? Realize();
    
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
    public LexicalScope? LocalScope { get; private set; }
    
    /// <inheritdoc cref="ITypeTree.ImplicitCast"/>
    public IRealizedImplicitCast? ImplicitCast { get; set; }

    /// <inheritdoc cref="IAST.Params"/>
    public IAST[] Params { get; init; } = Params;
    public IEnumerable<IDebugAST> Children => Params;
    
    public ReflectDiagnostic[] Diagnostics { get; private set; } = System.Array.Empty<ReflectDiagnostic>();

    /// <inheritdoc cref="IAST.CopyTree"/>
    public abstract IAST CopyTree();

    private IAST[] CopyParams() {
        var copied = new IAST[Params.Length];
        for (int ii = 0; ii < Params.Length; ++ii)
            copied[ii] = Params[ii].CopyTree();
        return copied;
    }

    //TODO envframe this isn't generally sound since it doesn't move declarations, but
    // i think the current usage for implicit cast only is sound.
    /// <inheritdoc cref="IAST.ReplaceScope"/>
    public void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
        if (LocalScope?.Parent == prev) {
            LocalScope.UpdateParent(inserted);
        }
        if (EnclosingScope == prev) {
            EnclosingScope = inserted;
        }
        foreach (var a in Params)
            a.ReplaceScope(prev, inserted);
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

    public void SetDiagnostics(ReflectDiagnostic[] diagnostics) {
        this.Diagnostics = diagnostics;
    }
    public virtual IEnumerable<ReflectDiagnostic> WarnUsage() =>
        Diagnostics.Concat(Params.SelectMany(p => p.WarnUsage()));

    public virtual IEnumerable<(IDebugAST tree, int? childIndex)>? NarrowestASTForPosition(PositionRange p) {
        if (p.Start.Index < Position.Start.Index || p.End.Index > Position.End.Index) return null;
        for (int ii = 0; ii < Params.Length; ++ii) {
            var arg = Params[ii];
            if (arg.NarrowestASTForPosition(p) is { } results)
                return results.Append(((IDebugAST)this, ii));
        }
        return new (IDebugAST, int?)[] { ((IDebugAST)this, null) };
    }
    
    //By default, flatten List<SM>, SM[], AsyncPattern[], SyncPattern[]
    //This drastically improves readability as these are often deeply nested
    private static readonly Type[] flattenArrayTypes =
        { typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern) };
    protected IEnumerable<DocumentSymbol> FlattenParams(Func<IAST, int, DocumentSymbol>? mapper) {
        bool DefaultFlatten(IAST ast) =>
            ast is Array && 
            flattenArrayTypes.Contains(ast.SelectedOverloadReturnType?.Resolve(Unifier.Empty).LeftOrNull?.GetElementType());
        foreach (var (p, sym) in Params.Select((p, i) => (p, mapper?.Invoke(p, i) ?? p.ToSymbolTree()))) {
            if (DefaultFlatten(p))
                foreach (var s in sym.Children ?? System.Array.Empty<DocumentSymbol>())
                    yield return s;
            else
                yield return sym;
        }
    }

    protected string? JoinDescr(string? a, string? b) {
        if (a == null)
            return b;
        if (b == null)
            return a;
        return $"{a} {b}";
    }


    // ----- Subclasses -----
    

    /// <summary>
    /// A reference to a declaration of a variable, function, enum value, etc, that is used as a value (eg. in `x`++ or `f`(2)).
    /// </summary>
    //Always a tex func type (references are bound to expressions or EnvFrame) or a constant (in case of enum reference)
    public record Reference : AST, IAST, IAtomicTypeTree {
        private string Name { get; }
        public VarDecl? Declaration { get; init; }
        public readonly List<(Type type, object value)>? AsEnumTypes;
        /// <inheritdoc cref="AST.Reference"/>
        public Reference(PositionRange Position, LexicalScope EnclosingScope, string Name, VarDecl? Declaration, List<(Type type, object value)>? asEnumTypes) : base(Position,
            EnclosingScope) {
            this.Name = Name;
            this.Declaration = Declaration;
            PossibleTypes = Declaration != null ? 
                new TypeDesignation[] { Declaration.TypeDesignation.MakeTExFunc() } : 
                System.Array.Empty<TypeDesignation>();
            if ((AsEnumTypes = asEnumTypes) != null) {
                PossibleTypes = PossibleTypes
                    .Concat(AsEnumTypes.Select(a => new Known(a.type)))
                    .ToArray();
            }
        }
        
        public override IAST CopyTree() => this with { };
        
        /// <inheritdoc cref="IAtomicTypeTree.SelectedOverload"/>
        public TypeDesignation? SelectedOverload { get; set; }
        /// <inheritdoc cref="IAtomicTypeTree.PossibleTypes"/>
        public TypeDesignation[] PossibleTypes { get; }

        public bool TryGetAsEnum(out object val, out Type type) {
            if (AsEnumTypes != null && SelectedOverload is Known { Arguments: { Length: 0 }, Typ: { } t }) {
                for (int ii = 0; ii < AsEnumTypes.Count; ++ii) {
                    if (AsEnumTypes[ii].type == t) {
                        type = t;
                        val = AsEnumTypes[ii].value;
                        return true;
                    }
                }
            }
            type = null!;
            val = default!;
            return false;
        }
        public override object _RealizeWithoutCast() {
            if (TryGetAsEnum(out var eVal, out _))
                return eVal;
            if (Declaration == null)
                throw new ReflectionException(Position, $"Reference {Name} is not a variable or enum");
            return GetUnwrappedType(SelectedOverload!).MakeTypedLambda(tac => {
                var v = Declaration.Bound;
                return EnclosingScope.LocalOrParent(tac, tac.EnvFrame, v, out _);
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
        public string Explain() {
            if (TryGetAsEnum(out var v, out var t))
                return $"{CompactPosition} {t.RName()}.{v}";
            else {
                var typeInfo = Declaration?.FinalizedType is { } ft ? $"{ft.SimpRName()}" : "Variable";
                return $"{CompactPosition} {typeInfo} `{Name}`";
            }
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            var knownType = SelectedOverload is Known { Arguments: { Length: 0 }, Typ: { } t } ? t : null;
            var symbolType = SymbolKind.Variable;
            if (TryGetAsEnum(out _, out var typ))
                symbolType = typ.IsEnum ? SymbolKind.Enum : SymbolKind.Constant;
            return new DocumentSymbol(Name, knownType?.SimpRName() ?? Declaration?.FinalizedType?.SimpRName(), 
                symbolType, Position.ToRange());
        }

        public IEnumerable<SemanticToken> ToSemanticTokens() {
            var tokenType = SemanticTokenTypes.Variable;
            if (TryGetAsEnum(out _, out _))
                tokenType = SemanticTokenTypes.EnumMember;
            else if (Declaration is ImplicitArgDecl)
                tokenType = SemanticTokenTypes.Parameter;
            yield return new(Position, tokenType);
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} &{Name}";
        }
    }

    /// <summary>
    /// A reference to a dynamically-scoped variable.
    /// </summary>
    //Always a tex func type (references are bound to expressions or EnvFrame)
    public record WeakReference : AST, IAST, IAtomicTypeTree {
        private string Name { get; }
        /// <inheritdoc cref="AST.WeakReference"/>
        public WeakReference(PositionRange Position, LexicalScope EnclosingScope, string Name) : base(Position, EnclosingScope) {
            this.Name = Name;
            PossibleTypes = new TypeDesignation[1] { new Variable().MakeTExFunc() };
        }
        
        public override IAST CopyTree() => this with { };
        
        /// <inheritdoc cref="IAtomicTypeTree.SelectedOverload"/>
        public TypeDesignation? SelectedOverload { get; set; }
        /// <inheritdoc cref="IAtomicTypeTree.PossibleTypes"/>
        public TypeDesignation[] PossibleTypes { get; }

        public override object _RealizeWithoutCast() {
            var t = GetUnwrappedType(SelectedOverload!);
            return t.MakeTypedLambda(tac => LexicalScope.VariableWithoutLexicalScope(tac, Name, t));
        }

        /// <summary>
        /// Special handling for writing to a dynamically-scoped variable, which is nontrivial
        /// </summary>
        public TEx RealizeAsWeakWriteable(TExArgCtx tac, Func<Ex, Ex> opOnValue) {
            var t = GetUnwrappedType(SelectedOverload!);
            return t.MakeTypedLambda(tac => LexicalScope.VariableWithoutLexicalScope(tac, Name, t, null, opOnValue))(tac);
        }
        
        public string Explain() {
            var typeInfo = SelectedOverload is {IsResolved: true} sr 
                ? $"{GetUnwrappedType(sr).SimpRName()}" : "Variable";
            return $"{CompactPosition} {typeInfo} `{Name}` (dynamically scoped)";
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol($"dynamic {Name}", descr, SymbolKind.Variable, Position.ToRange());
        }

        public IEnumerable<SemanticToken> ToSemanticTokens() {
            yield return new(Position, SemanticTokenTypes.Variable, new[]{SemanticTokenModifiers.DynamicVar});
        }

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
    //Not necessarily a tex func type-- depends on method def
    public record MethodCall : AST, IMethodAST<Reflector.InvokedMethod> {
        //We have to hide subtypes of StateMachine since the unifier can't generally handle subtypes
        public Reflector.InvokedMethod[] Methods { get; }
        public IReadOnlyList<Reflector.InvokedMethod> Overloads => Methods;
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Reflector.InvokedMethod>? RealizableOverloads { get; set; }
        public (Reflector.InvokedMethod method, Dummy simplified)? SelectedOverload { get; set; }
        /// <inheritdoc/>
        public bool OverloadsAreInterchangeable { get; init; } = false;
        /// <summary>Position of the method name alone (ie. just `MethodName`)</summary>
        public PositionRange MethodPosition { get; }
        public SemanticToken[] PrecedingTokens { get; set; } = System.Array.Empty<SemanticToken>();

        public override IAST CopyTree() => this with { Params = CopyParams() };
        
        private MethodCall(PositionRange Position, PositionRange MethodPosition,
            LexicalScope EnclosingScope, Reflector.InvokedMethod[] Methods, params IAST[] Params) : base(Position, EnclosingScope, Params) {
            this.MethodPosition = MethodPosition;
            this.Methods = Methods.Select(m => m.HideSMReturn()).ToArray();
        }

        /// <param name="Position">Position of the entire method call, including all arguments (ie. all of `MethodName(arg1, arg2)`)</param>
        /// <param name="MethodPosition">Position of the method name alone (ie. just `MethodName`)</param>
        /// <param name="EnclosingScope">The lexical scope in which this function is called. Certain functions may also create a <see cref="AST.LocalScope"/></param>.
        /// <param name="Methods">Method signatures. These may be generic, requiring specialization before invocation.</param>
        /// <param name="Params">Arguments to the method</param>
        /// <param name="overloadsEq"><see cref="OverloadsAreInterchangeable"/></param>
        public static MethodCall Make(PositionRange Position, PositionRange MethodPosition, LexicalScope EnclosingScope, 
            Reflector.InvokedMethod[] Methods, IEnumerable<ST> Params, bool overloadsEq = false) {
            var localScope = MaybeMakeLocalScope(EnclosingScope, MethodPosition, Methods);
            return new MethodCall(Position, MethodPosition, EnclosingScope, Methods,
                Params.Select(p => p.Annotate(localScope ?? EnclosingScope)).ToArray()) {
                LocalScope = localScope,
                OverloadsAreInterchangeable = overloadsEq
            };
        }

        private static LexicalScope? MaybeMakeLocalScope(LexicalScope enclosing, PositionRange methodPos, Reflector.InvokedMethod[] methods) {
            var nWithScope = methods.Count(m => m.Mi.GetAttribute<CreatesInternalScopeAttribute>() != null);
            if (nWithScope > 0) {
                if (nWithScope != methods.Length)
                    throw new StaticException(
                        $"Some overloads for method {methods[0].Name} have local scopes, and some don't." +
                        $"This is not permitted by the language design. Please report this.");
                var cfg = methods[0].Mi.GetAttribute<CreatesInternalScopeAttribute>()!;
                var sc = cfg.dynamic ?
                    new DynamicLexicalScope(enclosing) :
                    LexicalScope.Derive(enclosing);
                sc.AutoDeclareVariables(methodPos, cfg.type);
                return sc;
            } else if (methods.All(m => m.Mi.GetAttribute<ExtendsInternalScopeAttribute>() != null)) {
                enclosing.AutoDeclareExtendedVariables(methodPos, 
                    methods[0].Mi.GetAttribute<ExtendsInternalScopeAttribute>()!.type);
            }
            return null;
        }
        

        public Either<Unifier, TypeUnifyErr> WillSelectOverload(Reflector.InvokedMethod method, IImplicitTypeConverterInstance? cast, Unifier u) {
            if (cast?.Converter is IScopedTypeConverter c && (c.ScopeArgs != null || c.Kind != ScopedConversionKind.Trivial)) {
                LocalScope = LexicalScope.Derive(EnclosingScope, c);
                foreach (var a in Params)
                    a.ReplaceScope(EnclosingScope, LocalScope);
            }
            return u;
        }

        public void FinalizeUnifiers(Unifier unifier) {
            IMethodTypeTree<Reflector.InvokedMethod>._FinalizeUnifiers(this, unifier);
            if (SelectedOverload!.Value.method.Mi.GetAttribute<AssignsAttribute>() is { } attr) {
                foreach (var ind in attr.Indices) {
                    foreach (var refr in Params[ind].EnumeratePreorder().OfType<Reference>())
                        if (refr.Declaration != null)
                            refr.Declaration.Bound.Assignments++;
                }
            }
        }

        public override object? _RealizeWithoutCast() {
            var mi = SelectedOverload!.Value.method.Mi;
            using var _ = LocalScope == null || mi.GetAttribute<CreatesInternalScopeAttribute>() == null ?
                null :
                new LexicalScope.ParsingScope(LocalScope);
            var prms = new object?[Params.Length];
            for (int ii = 0; ii < prms.Length; ++ii)
                prms[ii] = Params[ii].Realize();
            if (mi is IGenericMethodSignature gm) {
                //ok to use shared type as we are doing a check on empty unifier that is discarded
                if (mi.SharedType.Unify(SelectedOverload.Value.simplified, Unifier.Empty) is { IsLeft: true } unifier) {
                    var resolution = gm.SharedGenericTypes.Select(g => g.Resolve(unifier.Left)).SequenceL();
                    if (resolution.TryR(out var err)) {
                        if (err is TypeUnifyErr.UnboundRestr ure)
                            err = ure with { Tree = this };
                        throw IAST.EnrichError(err, MethodPosition);
                    }
                    mi = gm.Specialize(resolution.Left.ToArray());
                } else
                    throw new ReflectionException(Position, 
                        $"SelectedOverload has unsound types for generic method {mi.AsSignature}");
            }
            if (LocalScope != null)
                AttachScope(prms, LocalScope, LocalScope.AutoVars);
            return mi.Invoke(this, prms);
        }

        public static void AttachScope(object?[] prms, LexicalScope localScope, AutoVars? autoVars) {
            for (int ii = 0; ii < prms.Length; ++ii)
                switch (prms[ii]) {
                    case IAutoVarRequestor<AutoVars.GenCtx> props:
                        props.Assign(localScope, (AutoVars.GenCtx?)autoVars ?? 
                                         throw new StaticException("Autovars not configured correctly"));
                        break;
                    case GenCtxProperty[] props:
                        prms[ii] = props.Append(
                            GenCtxProperty._AssignLexicalScope(localScope, (AutoVars.GenCtx?)autoVars ?? 
                                                                           throw new StaticException("Autovars not configured correctly"))).ToArray();
                        break;
                    case ILexicalScopeRequestor lsr:
                        lsr.Assign(localScope);
                        break;
                }
        }
    
        public ReflectionException Raise(Helpers.NotWriteableException exc) =>
            new (Position, Params[exc.ArgIndex].Position, exc.Message, exc.InnerException);

        public string Explain() => $"{CompactPosition} {(SelectedOverload?.method ?? Methods[0]).Mi.AsSignature}";
        
        public DocumentSymbol ToSymbolTree(string? descr = null) {
            if (SelectedOverload?.method is { } m) {
                try {
                    if (m.Mi.IsCtor && m.Mi.ReturnType == typeof(PhaseSM) &&
                        Params[1].Realize() is PhaseProperties props) {
                        return new($"{props.phaseType?.ToString() ?? "Phase"}", props.cardTitle?.Value ?? "",
                            SymbolKind.Method, Position.ToRange(), 
                            FlattenParams((p, i) => p.ToSymbolTree($"({m.Params[i].Name})")));
                    }
                } catch {
                    //pass
                }
                return m.Mi.IsFallthrough ? 
                        Params[0].ToSymbolTree() :
                        new(m.Name, m.Mi.TypeOnlySignature, SymbolKind.Method, Position.ToRange(), 
                            FlattenParams((p, i) => p.ToSymbolTree($"({m.Params[i].Name})")));
            } else
                return new DocumentSymbol(Methods[0].Name, null, SymbolKind.Method, Position.ToRange(),
                    FlattenParams((p, i) => p.ToSymbolTree($"({Methods[0].Params[i].Name})")));
        }

        public IEnumerable<SemanticToken> ToSemanticTokens() =>
            PrecedingTokens.Concat(Params.SelectMany(p => p.ToSemanticTokens()).Prepend(
                SelectedOverload?.method is {} m ? 
                    SemanticToken.FromMethod(m.Mi, MethodPosition) : 
                    new SemanticToken(MethodPosition, SemanticTokenTypes.Method)));

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} {(SelectedOverload?.method ?? Methods[0]).TypeEnclosedName}(";
            foreach (var w in IDebugPrint.PrintArgs(Params))
                yield return w;
            yield return ")";
        }
    }

    //Always a tex func type
    public record Block : AST, IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; private init; }
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public (VarDecl variable, ImplicitArgDecl fnArg)[]? TopLevelArgs { get; private set; }
        public Block(PositionRange Position, LexicalScope EnclosingScope, LexicalScope? localScope, params IAST[] Params) : base(Position,
            EnclosingScope, Params) {
            this.LocalScope = localScope;
            Overloads = MakeOverloads(Params);
        }

        private static IReadOnlyList<Dummy> MakeOverloads(IAST[] prms) {
            var typs = prms.Select(p => new Variable() as TypeDesignation).ToArray();
            typs[^1] = typs[^1].MakeTExFunc();
            return new[] {
                Dummy.Method(typs[^1], typs), //(T1,T2...,TExArgCtx->TEx<R>)->(TExArgCtx->TEx<R>)
            };
        }

        public override IAST CopyTree() {
            var nparams = CopyParams();
            return this with { Params = nparams, Overloads = MakeOverloads(nparams) };
        }

        public Block WithTopLevelArgs(params (VarDecl, ImplicitArgDecl)[] args) {
            foreach (var (v, _) in (TopLevelArgs = args)) {
                v.Assignments++;
            }
            return this;
        }

        public bool ImplicitParameterCastEnabled(int index) => index == Params.Length - 1;

        public override object _RealizeWithoutCast() {
            //TODO copy handling to array?
            return GetUnwrappedType(SelectedOverload!.Value.simplified.Last).MakeTypedLambda(tac => {
                IEnumerable<Ex> Stmts() {
                    var prmStmts = Params.Select<IAST, Ex>((p, i) => {
                        try {
                            return p.Realize() switch {
                                Func<TExArgCtx, TEx> f => f(tac),
                                TEx t => t,
                                Ex ex => ex,
                                { } v => Ex.Constant(v),
                                null => throw new ReflectionException(Position, $"Block statement #{i} returned null")
                            };
                        } catch (Exception e) {
                            if (e is ReflectionException)
                                throw;
                            else
                                throw new ReflectionException(p.Position, "Couldn't invoke this code.", e);
                        }
                    });
                    if (TopLevelArgs is not { } decls)
                        return prmStmts;
                    return decls
                        .Select(d => d.variable.Value(tac.EnvFrame, tac).Is(d.fnArg.Value(tac.EnvFrame, tac)))
                        .Concat(prmStmts);
                }
                if (LocalScope is null)
                    return Ex.Block(Stmts());
                else if (!LocalScope.UseEF)
                    return Ex.Block(
                        LocalScope.VariableDecls.SelectMany(v => v.decls)
                            .SelectNotNull(v => v.DeclaredParameter(tac)).ToArray(),
                        Stmts());
                else {
                    var parentEf = tac.MaybeGetByType<EnvFrame>(out _) ?? Ex.Constant(null, typeof(EnvFrame));
                    var ef = Ex.Parameter(typeof(EnvFrame), "ef");
                    tac = tac.MaybeGetByType<EnvFrame>(out _) is { } ?  
                        tac.MakeCopyForType<EnvFrame>(ef) :
                        tac.Append("rootEnvFrame", ef);
                    var makeEf = ef.Is(EnvFrame.exCreate.Of(Ex.Constant(LocalScope), parentEf));
                    
                    return Ex.Block(new[] { ef }, Stmts().Prepend(makeEf));
                }
            });
        }
        
        public string Explain() {
            return $"{CompactPosition} block<{GetReturnTypeDescr(this)}>";
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            var children = FlattenParams(null).ToList();
            if (children.Count == 1)
                return children[0];
            return new("Block", $"({children.Count} statements)", SymbolKind.Object, Position.ToRange(), children);
        }

        public IEnumerable<SemanticToken> ToSemanticTokens() => Params.SelectMany(p => p.ToSemanticTokens());

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} block<{GetReturnTypeDescr(this)}>({{";
            foreach (var w in IDebugPrint.PrintArgs(Params, ";"))
                yield return w;
            yield return "})";
        }

    }

    //Type dependent on elements
    public record Array : AST, IMethodAST<Dummy> {
        private Variable ElementType { get; init; } = new();
        public IReadOnlyList<Dummy> Overloads { get; private init; }
        public IReadOnlyList<ITypeTree> Arguments => Params;
        
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public Array(PositionRange Position, LexicalScope EnclosingScope, params IAST[] Params) : base(Position,
            EnclosingScope, Params) {
            Overloads = MakeOverloads(Params, ElementType);
        }

        private static IReadOnlyList<Dummy> MakeOverloads(IAST[] prms, Variable elementType) {
            return new[]{ Dummy.Method(new Known(Known.ArrayGenericType, elementType), 
                prms.Select(_ => elementType as TypeDesignation).ToArray()) };
        }
        
        public override IAST CopyTree() {
            var neleType = new Variable();
            var nparams = CopyParams();
            return this with { ElementType = neleType, Params = nparams, Overloads = MakeOverloads(nparams, neleType) };
        }

        public override object _RealizeWithoutCast() {
            var typ = SelectedOverload!.Value.simplified.Resolve(Unifier.Empty).LeftOrThrow;
            var array = System.Array.CreateInstance(typ.GetElementType()!, Params.Length);
            for (int ii = 0; ii < Params.Length; ++ii)
                array.SetValue(Params[ii].Realize(), ii);
            return array;
        }

        public string Explain() {
            return $"{CompactPosition} {GetReturnTypeDescr(this)}[{Params.Length}]";
        }
        
        public DocumentSymbol ToSymbolTree(string? descr = null) {
            if (ImplicitCast is { } cast) {
                var props = Params.Length == 1 ? "property" : "properties";
                return new DocumentSymbol(cast.ResultType.ExRName(), $"({Params.Length} {props})", SymbolKind.Object, Position.ToRange(), FlattenParams(null));
            } else {
                var props = Params.Length == 1 ? "element" : "elements";
                return new DocumentSymbol((this as IAST).SelectedOverloadReturnType!.ExRName(), 
                    $"({Params.Length} {props})", SymbolKind.Array, Position.ToRange(), FlattenParams(null));
            }
        }

        public IEnumerable<SemanticToken> ToSemanticTokens() => Params.SelectMany(p => p.ToSemanticTokens());
        
        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} {GetReturnTypeDescr(this)}[{Params.Length}]{{";
            foreach (var w in IDebugPrint.PrintArgs(Params, ","))
                yield return w;
            yield return "}";
        }

    }
    
    
    //Type dependent on elements
    public record Tuple : AST, IMethodAST<Dummy> {
        private TypeDesignation[] ElementTypes { get; init; }
        public IReadOnlyList<Dummy> Overloads { get; private init; }
        public IReadOnlyList<ITypeTree> Arguments => Params;
        
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public Tuple(PositionRange Position, LexicalScope EnclosingScope, params IAST[] Params) : base(Position,
            EnclosingScope, Params) {
            ElementTypes = Params.Length.Range().Select(_ => new Variable() as TypeDesignation).ToArray();
            Overloads = new[]{ Dummy.Method(Known.MakeTupleType(ElementTypes), ElementTypes) };
        }

        public override IAST CopyTree() {
            return new Tuple(Position, EnclosingScope, CopyParams());
        }

        public override object _RealizeWithoutCast() {
            var typ = SelectedOverload!.Value.simplified.Resolve(Unifier.Empty).LeftOrThrow;
            var prms = new object?[Params.Length];
            for (int ii = 0; ii < prms.Length; ++ii)
                prms[ii] = Params[ii].Realize();
            return Activator.CreateInstance(typ, prms);
        }

        public string Explain() {
            var typStrings = SelectedOverload?.simplified is { } m ?
                m.Arguments.Take(Params.Length).Select(t => t.Resolve().LeftOrNull?.SimpRName() ?? "Variable")
                : Params.Select((p, i) => $"Element #{i+1}");
            return $"{CompactPosition} (${string.Join(", ", typStrings)})";
        }
        
        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol("Tuple", descr, SymbolKind.Array, Position.ToRange(), FlattenParams(null));
        }

        public IEnumerable<SemanticToken> ToSemanticTokens() => Params.SelectMany(p => p.ToSemanticTokens());
        
        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} (";
            foreach (var w in IDebugPrint.PrintArgs(Params, ","))
                yield return w;
            yield return ")";
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
        private readonly string content;
        public float Value { get; }
        private Variable Var { get; init; }
        public TypeDesignation[] PossibleTypes { get; private init; }
        public TypeDesignation? SelectedOverload { get; set; }
        public Number(PositionRange Position, LexicalScope EnclosingScope, string content) : base(Position,
            EnclosingScope) {
            this.content = content;
            this.Value = content == "inf" ? M.IntFloatMax : DMath.Parser.Float(content);
            Var = MakeVariableType(content, Value);
            PossibleTypes = new TypeDesignation[] { Var };
        }

        private static Variable MakeVariableType(string content, float value) => new Variable() {
            RestrictedTypes = (!content.Contains('.') && Math.Abs(value - Math.Round(value)) < 0.00001f) ?
                FloatTypes.Concat(IntTypes).ToArray() :
                FloatTypes
        };
        
        public override IAST CopyTree() {
            var nVar = new Variable() { RestrictedTypes = Var.RestrictedTypes };
            return this with { Var = nVar, PossibleTypes = new TypeDesignation[] { nVar } };
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
        
        public string Explain() {
            string exp;
            if (content == "inf") {
                exp = "`infinity`";
            } else {
                exp = $"`{content}`";
                for (int ii = 0; ii < content.Length; ++ii)
                    if (char.IsLetter(content[ii])) {
                        exp += $"(=`{Value:F4}`)";
                        break;
                    }
            }
            return $"{CompactPosition} Number {exp}";
            
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) =>
            new(content, descr, SymbolKind.Number, Position.ToRange());

        public IEnumerable<SemanticToken> ToSemanticTokens() {
            yield return new(Position, SemanticTokenTypes.Number);
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return content;
        }
    }
    
    public record TypedValue<T>(PositionRange Position, LexicalScope EnclosingScope, T Value, SymbolKind Kind) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        public TypeDesignation[] PossibleTypes { get; } = {
            TypeDesignation.FromType(typeof(T)).MakeTExFunc(),
            TypeDesignation.FromType(typeof(T)),
        };
        public TypeDesignation? SelectedOverload { get; set; }

        public override IAST CopyTree() => this with { };
        public override object? _RealizeWithoutCast() {
            var typ = SelectedOverload!.Resolve(Unifier.Empty);
            if (typ == typeof(T))
                return Value;
            return (Func<TExArgCtx, TEx<T>>)(_ => Ex.Constant(Value));
        }

        public string Explain() => $"{CompactPosition} {typeof(T).SimpRName()} `{Value?.ToString()}`";

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol(Value?.ToString() ?? "null", descr, Kind, Position.ToRange());
        }

        public IEnumerable<SemanticToken> ToSemanticTokens() {
            yield return new(Position, Kind switch {
                SymbolKind.Boolean => SemanticTokenTypes.Keyword,
                SymbolKind.Number => SemanticTokenTypes.Number,
                _ => SemanticTokenTypes.String
            });
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return Value?.ToString() ?? "<null>";
        }
    }
    
    public record Failure : AST, IAST, IAtomicTypeTree {
        public ReflectionException Exc { get; }
        public IAST? Basis { get; }
        //Allow typechecking Failure in order to get more debug information
        private Variable Var { get; init; }
        public TypeDesignation[] PossibleTypes { get; private init; }
        public TypeDesignation? SelectedOverload { get; set; }
        [PublicAPI]
        public List<MethodSignature>? Completions { get; init; }
        public IEnumerable<NestedFailure> FirstPassErrors() =>
            new NestedFailure[1] {
                new(this, Basis?.FirstPassErrors().ToList() ?? new List<NestedFailure>())
            };
        
        public string Explain() => Basis switch {
            Failure f => f.Explain(),
            { } b => $"(ERROR) {b.Explain()}",
            _ => $"(ERROR) {Exc.Message}"
        };
        
        public override IEnumerable<(IDebugAST, int?)>? NarrowestASTForPosition(PositionRange p) {
            if (p.Start.Index < Position.Start.Index || p.End.Index > Position.End.Index) return null;
            return new (IDebugAST, int?)[] { (this, null) };
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) => throw Exc;

        public IEnumerable<SemanticToken> ToSemanticTokens() => throw Exc;

        public Failure(ReflectionException exc, IAST basis) : base(basis.Position, basis.EnclosingScope, basis) {
            Exc = exc;
            Basis = basis;
            PossibleTypes = new TypeDesignation[] { Var = new Variable() };
        }

        public Failure(ReflectionException exc, LexicalScope scope) :
            base(exc.Position, scope) {
            Exc = exc;
            PossibleTypes = new TypeDesignation[] { Var = new Variable() };
        }

        //NB -- deep copy not required here
        public override IAST CopyTree() => this with { };
        
        public override object? _RealizeWithoutCast() => throw new StaticException("Cannot realize a Failure");
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