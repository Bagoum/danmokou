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
using NUnit.Framework;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static BagoumLib.Unification.TypeDesignation;
using SemanticTokenModifiers = Danmokou.Reflection.SemanticTokenModifiers;
using SemanticTokenTypes = Danmokou.Reflection.SemanticTokenTypes;

namespace Danmokou.Reflection2 {
public abstract record AST(PositionRange Position, LexicalScope EnclosingScope, params IAST[] Params) {
    /// <inheritdoc cref="IAST.Position"/>
    public PositionRange Position { get; init; } = Position;
    protected string CompactPosition => Position.Print(true);
    protected string DebugPosition => $"D{EnclosingScope.Depth}|{Position.Print(true)}";
    
    /// <inheritdoc cref="IAST.EnclosingScope"/>
    public LexicalScope EnclosingScope { get; private set; } = EnclosingScope;

    /// <summary>
    /// The lexical scope that this AST creates for any nested ASTs.
    /// <br/>This is either null or a direct child of <see cref="EnclosingScope"/>.
    /// </summary>
    public LexicalScope? LocalScope { get; private set; }

    /// <summary>
    /// The local scope or the enclosing scope.
    /// </summary>
    public LexicalScope Scope => LocalScope ?? EnclosingScope;
    
    /// <inheritdoc cref="ITypeTree.ImplicitCast"/>
    public IRealizedImplicitCast? ImplicitCast { get; set; }
    
    /// <summary>
    /// An implicit cast that converts a type to itself. Used to attach metadata.
    /// <br/>Applied before ImplicitCast.
    /// </summary>
    public FixedImplicitTypeConv? SameTypeCast { get; set; }

    /// <inheritdoc cref="IAST.Params"/>
    public IAST[] Params { get; init; } = Params;
    public IEnumerable<IDebugAST> Children => Params;
    
    public SemanticToken[] AdditionalTokens { get; set; } = System.Array.Empty<SemanticToken>();

    public AST AddTokens(IEnumerable<SemanticToken?> tokens) {
        AdditionalTokens = AdditionalTokens.Concat(tokens.Where(t => t != null)).ToArray()!;
        return this;
    }
    public ReflectDiagnostic[] Diagnostics { get; private set; } = System.Array.Empty<ReflectDiagnostic>();

    //TODO envframe this isn't generally sound since it doesn't move declarations, but
    // i think the current usage for implicit cast only is sound.
    /// <inheritdoc cref="IAST.ReplaceScope"/>
    public virtual void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
        if (LocalScope?.Parent == prev) {
            LocalScope.UpdateParent(inserted);
        }
        if (EnclosingScope == prev) {
            EnclosingScope = inserted;
        }
        foreach (var a in Params)
            a.ReplaceScope(prev, inserted);
    }

    protected void WillSelectImplicitCast(IImplicitTypeConverterInstance? cast) {
        if (cast?.Converter is IScopedTypeConverter c) {
            if (LocalScope != null)
                LocalScope.UseConverter(c);
            else if (c.ScopeArgs != null || c.Kind != ScopedConversionKind.Trivial) {
                LocalScope = LexicalScope.Derive(EnclosingScope, c);
                foreach (var a in Params)
                    a.ReplaceScope(EnclosingScope, LocalScope);
            }
        }
    }

    public virtual Either<Unifier, TypeUnifyErr> WillSelectOverload(Reflector.InvokedMethod _, IImplicitTypeConverterInstance? cast, Unifier u) {
        WillSelectImplicitCast(cast);
        return u;
    }
    public Either<Unifier, TypeUnifyErr> WillSelectOverload(Dummy _, IImplicitTypeConverterInstance? cast, Unifier u) {
        WillSelectImplicitCast(cast);
        return u;
    }
    public Either<Unifier, TypeUnifyErr> WillSelectOverload(TypeDesignation _, IImplicitTypeConverterInstance? cast, Unifier u) {
        WillSelectImplicitCast(cast);
        return u;
    }

    /// <inheritdoc cref="IAST.Realize"/>
    public TEx Realize(TExArgCtx tac) {
        if (ImplicitCast == null)
            return SameTypeCast == null ?
                _RealizeWithoutCast(tac) :
                SameTypeCast.Convert((IAST)this, _RealizeWithoutCast, tac);

        Func<TExArgCtx, TEx> caller = SameTypeCast == null ?
            _RealizeWithoutCast :
            tac => SameTypeCast.Convert((IAST)this, _RealizeWithoutCast, tac);
        var conv = ImplicitCast.Converter.Converter;
        if (conv is FixedImplicitTypeConv fixedConv) {
            return fixedConv.Convert((IAST)this, caller, tac);
        } else if (conv is GenericTypeConv1 gtConv) {
            return gtConv.ConvertForType(ImplicitCast.Variables[0].Resolve(Unifier.Empty).LeftOrThrow, (IAST)this,
                caller, tac);
        } else
            throw new NotImplementedException();
    }

    /// <summary>
    /// Create a TEx&lt;T&gt; representing the contents of this AST.
    /// </summary>
    public abstract TEx _RealizeWithoutCast(TExArgCtx tac);

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

    //private static Type GetUnwrappedType(TypeDesignation texType) =>
    //    texType.UnwrapTExFunc().Resolve(Unifier.Empty).LeftOrThrow;

    /// <summary>
    /// The return type of this AST (unlifted, not including implicit casts).
    /// </summary>
    private Type ReturnType(TypeDesignation selectedOverload) => 
        (selectedOverload is Dummy d ?
        d.Last :
        selectedOverload).Resolve().LeftOrThrow;

    public void SetDiagnostics(ReflectDiagnostic[] diagnostics) {
        this.Diagnostics = diagnostics;
    }
    public virtual IEnumerable<ReflectDiagnostic> WarnUsage() =>
        Diagnostics.Concat(Params.SelectMany(p => p.WarnUsage()));

    public IEnumerable<SemanticToken> ToSemanticTokens() {
        var baseTokens = _ToSemanticTokens();
        if (AdditionalTokens.Length > 0)
            return baseTokens.Concat(AdditionalTokens);
        return baseTokens;
    }

    protected virtual IEnumerable<SemanticToken> _ToSemanticTokens() =>
        Params.SelectMany(p => p.ToSemanticTokens());

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
            if (DefaultFlatten(p)) {
                foreach (var s in sym.Children ?? System.Array.Empty<DocumentSymbol>())
                    if (s != null)
                        yield return s;
            } else if (sym != null!)
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
        private readonly ScriptImport? InImport;
        public PositionRange NameOnlyPosition { get; init; }
        public string Name { get; }
        public string NameWithImport => InImport is null ? Name : $"{InImport.Name}.{Name}";
        public Either<VarDecl, List<(Type type, object value)>> Value { get; init; }
        /// <inheritdoc cref="AST.Reference"/>
        public Reference(PositionRange Position, LexicalScope EnclosingScope, ScriptImport? inImport, string Name, Either<VarDecl, List<(Type type, object value)>> Value) : base(Position,
            EnclosingScope) {
            this.Name = Name;
            this.NameOnlyPosition = Position;
            this.Value = Value;
            this.InImport = inImport;
            PossibleTypes = Value.IsLeft ? 
                //If importing from another file, then FinalizedTypeDesignation will be present
                new[] { Value.Left.FinalizedTypeDesignation ?? Value.Left.TypeDesignation } : 
                Value.Right.Select(a => TypeDesignation.FromType(a.type)).ToArray();
        }
        
        /// <inheritdoc cref="IAtomicTypeTree.SelectedOverload"/>
        public TypeDesignation? SelectedOverload { get; set; }
        /// <inheritdoc cref="IAtomicTypeTree.PossibleTypes"/>
        public TypeDesignation[] PossibleTypes { get; }

        public bool IsConstantDecl(out VarDecl decl) {
            return Value.TryL(out decl) && decl.Constant;
        }
        public bool TryGetAsEnum(out object val, out Type type) {
            if (Value.TryR(out var asEnumTypes) && SelectedOverload is Known { Arguments: { Length: 0 }, Typ: { } t }) {
                for (int ii = 0; ii < asEnumTypes.Count; ++ii) {
                    if (asEnumTypes[ii].type == t) {
                        type = t;
                        val = asEnumTypes[ii].value;
                        return true;
                    }
                }
            }
            type = null!;
            val = default!;
            return false;
        }
        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            if (TryGetAsEnum(out var eVal, out var t))
                return t.MakeTypedTEx(Ex.Constant(eVal));
            if (Value.IsRight)
                throw new ReflectionException(Position, $"Reference {Name} is not a variable or enum");
            try {
                return Value.Left.FinalizedType!.MakeTypedTEx(
                    InImport is null ?
                        Scope.LocalOrParentVariable(tac, tac.MaybeGetByType<EnvFrame>(out _)?.ex, Value.Left) :
                        InImport.Ef.Scope.LocalOrParentVariable(tac, Ex.Constant(InImport.Ef), Value.Left));
            } catch (CompileException ce) {
                throw new ReflectionException(Position, ce.Message);
            }
        }
        public string Explain() {
            if (TryGetAsEnum(out var v, out var t))
                return $"{CompactPosition} {t.RName()}.{v}";
            else if (Value.TryL(out var decl)) {
                var typeInfo = decl.FinalizedType is { } ft ? $"{ft.SimpRName()}" : "Variable";
                if (decl.Constant)
                    typeInfo = "const " + typeInfo;
                return $"{CompactPosition} {typeInfo} `{NameWithImport}`" +
                       (decl.DocComment is { } c ? $"  \n*{c}*" : "");
            } else
                return $"Unknown reference";
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            var knownType = SelectedOverload is Known { Arguments: { Length: 0 }, Typ: { } t } ? t : null;
            var symbolType = SymbolKind.Variable;
            if (TryGetAsEnum(out _, out var typ))
                symbolType = typ.IsEnum ? SymbolKind.Enum : SymbolKind.Constant;
            var typeInfo = knownType?.SimpRName() ?? Value.LeftOrNull?.FinalizedType?.SimpRName();
            if (Value.LeftOrNull?.Constant is true)
                typeInfo = "const " + (typeInfo ?? "");
            return new DocumentSymbol(NameWithImport, typeInfo, symbolType, Position.ToRange());
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
            var tokenType = SemanticTokenTypes.Variable;
            if (TryGetAsEnum(out _, out _))
                tokenType = SemanticTokenTypes.EnumMember;
            else if (Value.Left is ImplicitArgDecl)
                tokenType = SemanticTokenTypes.Parameter;
            yield return new SemanticToken(NameOnlyPosition, tokenType).WithConst(Value.Left?.Constant is true);
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{DebugPosition} {NameWithImport}";
        }
    }

    /// <summary>
    /// A reference to a dynamically-scoped variable.
    /// </summary>
    //Always a tex func type (references are bound to expressions or EnvFrame)
    public record WeakReference : AST, IAST, IAtomicTypeTree {
        private string Name { get; }
        private Type? KnownType { get; }
        /// <inheritdoc cref="AST.WeakReference"/>
        public WeakReference(PositionRange Position, LexicalScope EnclosingScope, string Name, Type? knownType = null) : base(Position, EnclosingScope) {
            this.Name = Name;
            this.KnownType = knownType;
            PossibleTypes = new TypeDesignation[1] {
                knownType != null ?
                    TypeDesignation.FromType(knownType) :
                    new Variable()
            };
        }
        
        /// <inheritdoc cref="IAtomicTypeTree.SelectedOverload"/>
        public TypeDesignation? SelectedOverload { get; set; }
        /// <inheritdoc cref="IAtomicTypeTree.PossibleTypes"/>
        public TypeDesignation[] PossibleTypes { get; }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var t = ReturnType(SelectedOverload!);
            return t.MakeTypedTEx(LexicalScope.VariableWithoutLexicalScope(tac, Name, t));
        }

        /// <summary>
        /// Special handling for writing to a dynamically-scoped variable, which is nontrivial
        /// </summary>
        public TEx RealizeAsWeakWriteable(TExArgCtx tac, Func<Ex, Ex> opOnValue) {
            var t = ReturnType(SelectedOverload!);
            return t.MakeTypedTEx(LexicalScope.VariableWithoutLexicalScope(tac, Name, t, null, opOnValue));
        }
        
        public string Explain() {
            var typeInfo = SelectedOverload is {IsResolved: true} sr 
                ? $"{sr.Resolve().Left.RName()}" : "Variable";
            return $"{CompactPosition} {typeInfo} `{Name}` (dynamically scoped)";
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol($"dynamic {Name}", descr, SymbolKind.Variable, Position.ToRange());
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
            yield return new(Position, SemanticTokenTypes.Variable, new[]{SemanticTokenModifiers.DynamicVar});
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{DebugPosition} &{Name}";
        }
    }

    public record TypeAs(PositionRange Position, LexicalScope EnclosingScope, Type CastToTyp, IAST Body) :
        AST(Position, EnclosingScope, Body),IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; } = new[] {
            Dummy.Method(TypeDesignation.FromType(CastToTyp), new Variable()), 
        };
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var body = Body.Realize(tac);
            try {
                var conv = FlattenVisitor.TryFlattenConversion(body, CastToTyp) ?? Ex.Convert(body, CastToTyp);
                return CastToTyp.MakeTypedTEx(conv);
            } catch (InvalidOperationException ex) {
                throw new ReflectionException(Position,
                    $"Can't convert type {body.ex.Type.RName()} to {CastToTyp.RName()}", ex);
            }
        }
        
        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Dummy>.ImplicitParameterCast(Dummy overload, int index) =>
            false;

        public string Explain() => $"({CastToTyp.RName()}) {Body.Explain()}";

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            var w = Body.ToSymbolTree(descr);
            return w with {
                Detail = (w.Detail ?? "") + $" (as {CastToTyp.RName()})"
            };
        }
    }

    /// <summary>
    /// An AST that invokes (possibly overloaded) methods.
    /// <br/>Methods may be lifted; ie. for a recorded method `R member (A, B, C...)`,
    ///  given parameters of type F(A), F(B), F(C) (lifted over (T->), eg. T->A, T->B, T->C),
    ///  this AST may construct a function T->R that uses T to realize the parameters and pass them to `member`.
    /// <br/>Methods may be generic.
    /// </summary>
    public record MethodCall(PositionRange Position, PositionRange MethodPosition,
        LexicalScope EnclosingScope, Reflector.InvokedMethod[] Methods, params IAST[] Params) : AST(Position, EnclosingScope, Params), IMethodAST<Reflector.InvokedMethod> {
        //We have to hide subtypes of StateMachine since the unifier can't generally handle subtypes
        public Reflector.InvokedMethod[] Methods { get; protected set; } = Methods.Select(m => m.HideSMReturn()).ToArray();
        public IReadOnlyList<Reflector.InvokedMethod> Overloads => Methods;
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Reflector.InvokedMethod>? RealizableOverloads { get; set; }
        public (Reflector.InvokedMethod method, Dummy simplified)? SelectedOverload { get; set; }
        /// <inheritdoc/>
        public bool OverloadsAreInterchangeable { get; init; } = false;
        protected bool AllowInvokeAsConst { get; set; } = false;
        public virtual bool AddMethodSemanticToken { get; set; } = true;

        /// <param name="Position">Position of the entire method call, including all arguments (ie. all of `MethodName(arg1, arg2)`)</param>
        /// <param name="MethodPosition">Position of the method name alone (ie. just `MethodName`)</param>
        /// <param name="EnclosingScope">The lexical scope in which this function is called. Certain functions may also create a <see cref="AST.LocalScope"/></param>.
        /// <param name="Methods">Method signatures. These may be generic, requiring specialization before invocation.</param>
        /// <param name="Params">Arguments to the method</param>
        /// <param name="overloadsEq"><see cref="OverloadsAreInterchangeable"/></param>
        public static MethodCall Make(PositionRange Position, PositionRange MethodPosition, STAnnotater EnclosingScope, 
            Reflector.InvokedMethod[] Methods, IEnumerable<ST> Params, bool overloadsEq = false) {
            var localScope = MaybeMakeLocalScope(EnclosingScope, MethodPosition, Methods);
            return new MethodCall(Position, MethodPosition, EnclosingScope.Scope, Methods,
                Params.Select(p => p.Annotate(localScope ?? EnclosingScope)).ToArray()) {
                LocalScope = localScope?.Scope,
                OverloadsAreInterchangeable = overloadsEq
            };
        }

        private static STAnnotater? MaybeMakeLocalScope(STAnnotater enclosing, PositionRange methodPos, Reflector.InvokedMethod[] methods) {
            var nWithScope = methods.Count(m => m.Mi.GetAttribute<CreatesInternalScopeAttribute>() != null);
            if (nWithScope > 0) {
                if (nWithScope != methods.Length)
                    throw new StaticException(
                        $"Some overloads for method {methods[0].Name} have local scopes, and some don't." +
                        $"This is not permitted by the language design. Please report this.");
                var cfg = methods[0].Mi.GetAttribute<CreatesInternalScopeAttribute>()!;
                var sc = cfg.dynamic ?
                    new DynamicLexicalScope(enclosing.Scope) :
                    LexicalScope.Derive(enclosing.Scope);
                sc.Type = LexicalScopeType.MethodScope;
                sc.AutoDeclareVariables(methodPos, cfg.type);
                return enclosing with { Scope = sc };
            } else if (methods.All(m => m.Mi.GetAttribute<ExtendsInternalScopeAttribute>() != null)) {
                enclosing.Scope.AutoDeclareExtendedVariables(methodPos, 
                    methods[0].Mi.GetAttribute<ExtendsInternalScopeAttribute>()!.type);
            }
            return null;
        }

        /// <inheritdoc cref="IMethodTypeTree{Dummy}.ImplicitParameterCast"/>
        public virtual Either<IImplicitTypeConverter, bool> ImplicitParameterCast(Reflector.InvokedMethod overload, int index) {
            if (DMKScope.GetConverterForCompiledExpressionType(overload.Mi.Params[index].Type) is { } conv)
                return new(conv);
            if (overload.Mi.GetAttribute<AssignsAttribute>() is { } attr)
                return attr.Indices.IndexOf(index) == -1;
            return true;
        }

        public override Either<Unifier, TypeUnifyErr> WillSelectOverload(Reflector.InvokedMethod mi, IImplicitTypeConverterInstance? cast, Unifier u) {
            //Only static methods can be converted to constants
            //TODO fix this
            /*AllowInvokeAsConst = mi.Mi is { IsStatic: true, Member: TypeMember.Method }
                                 && mi.Mi.GetAttribute<NonConstableAttribute>() == null;*/
            return base.WillSelectOverload(mi, cast, u).FMapL(u => {
                //Handles cases where compilation is done inside functions (eg. MoveTarget)
                if (mi.Mi.GetAttribute<ExpressionBoundaryAttribute>() != null && LocalScope == null) {
                    LocalScope = LexicalScope.Derive(EnclosingScope);
                    LocalScope.Type = LexicalScopeType.ExpressionBlock;
                    foreach (var a in Params)
                        a.ReplaceScope(EnclosingScope, LocalScope);
                }
                return u;
            });
        }

        public virtual IEnumerable<ReflectionException> Verify() {
            if (ThisIsConstantVarInitialize(out _)) {
                foreach (var refr in Params[1].EnumeratePreorder().OfType<Reference>())
                    if (refr.Value.TryL(out var d) && !d.Constant && !d.DeclarationScope.IsIssueOf((Params[1] as AST)!.Scope)) {
                        yield return new ReflectionException(refr.Position, 
                            $"The variable `{d.Name}` (declared at {d.Position}) cannot be referenced from inside a constant expression.");
                    }
            }
            if (SelectedOverload!.Value.method.Mi.GetAttribute<AssignsAttribute>() is { } attr) {
                foreach (var ind in attr.Indices) {
                    foreach (var refr in Params[ind].EnumeratePreorder().OfType<Reference>())
                        if (refr.Value.TryL(out var decl)) {
                            if (decl is { Constant: true, Assignments: > 0 })
                                yield return new ReflectionException(Position,
                                    $"`{decl.Name}` (declared at {decl.Position}) is a constant variable. It can only be assigned to once.");
                            decl.Assignments++;
                        }
                }
            }
            foreach (var exc in IAST.VerifyChildren(this))
                yield return exc;
        }


        private bool ThisIsConstantVarInitialize(out VarDecl decl) {
            decl = null!;
            return SelectedOverload!.Value.method.Mi is MethodSignature {
                       Member: { BaseMi: MethodInfo { IsGenericMethodDefinition: true } meth }
                   } &&
                   meth == ST.VarDeclAssign.VarInitialize.Member.BaseMi && Params[0] is Reference r &&
                   r.IsConstantDecl(out decl);
        }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var (inv, typ) = SelectedOverload!.Value;
            
            //Special casing for assigning the constant value of a const var
            if (ThisIsConstantVarInitialize(out var decl)) {
                var valex = Params[1].Realize(tac);
                if ((Ex)valex is ConstantExpression cex)
                    decl.ConstantValue = Ex.Constant(cex.Value, decl.FinalizedType!);
                else {
                    try {
                        var del = Ex.Lambda(valex).Compile();
                        decl.ConstantValue =
                            Ex.Constant(del.GetType().GetMethod("Invoke")!
                                .Invoke(del, System.Array.Empty<object>()),
                                decl.FinalizedType!
                            );
                    } catch (Exception e) {
                        throw new ReflectionException(Position, "Failed to assign constant value:", e);
                    }
                }
                return decl.ConstantValue.Value;
            }
            
            //shouldn't need ParsingScope, that's bdsl1 only
            var mi = SpecializeMethod(this, MethodPosition, inv.Mi, typ);
            return RealizeMethod(this, mi, tac, (ii, tac) => Params[ii].Realize(tac), AllowInvokeAsConst);
        }

        public static IMethodSignature SpecializeMethod(IAST ast, PositionRange methodPosition, IMethodSignature mi,
            Dummy typ) {
            if (mi is IGenericMethodSignature gm) {
                //ok to use shared type as we are doing a check on empty unifier that is discarded
                if (mi.SharedType.Unify(typ, Unifier.Empty) is { IsLeft: true } unifier) {
                    var resolution = gm.SharedGenericTypes.Select(g => g.Resolve(unifier.Left)).SequenceL();
                    if (resolution.TryR(out var err)) {
                        if (err is TypeUnifyErr.UnboundRestr ure)
                            err = ure with { Tree = ast };
                        throw IAST.EnrichError(err, methodPosition);
                    }
                    return gm.Specialize(resolution.Left.ToArray());
                } else
                    throw new ReflectionException(ast.Position,
                        $"SelectedOverload has unsound types for generic method {mi.AsSignature}");
            } else
                return mi;
        }

        public static TEx RealizeMethod(IAST? ast, IMethodSignature mi, TExArgCtx tac, Func<int, TExArgCtx, TEx> prmGetter, bool allowInvokeAsConst=false) {
            //During method typechecking, the types T, TEx<T>, and Func<TExArgCtx,TEx<T>> in parameters
            // and in return types will all be treated as T.
            // This helper method deals with converting all possible combinations of these types
            // universally to a method invocation returning TEx<T>.
            
            if (mi.ReturnType.IsTExOrTExFuncType(out var dataTyp)) {
                //If the method is a tex-func method or a tex-method, we invoke it in place.
                var prms = new object?[mi.Params.Length];
                Ex? ParseArg(int ii) {
                    if (mi.Params[ii].Type.IsTExFuncType(out var inner)) {
                        prms[ii] = inner.MakeTypedLambda(tac => prmGetter(ii, tac));
                        return null;
                    } else if (mi.Params[ii].Type.IsTExType(out inner)) {
                        prms[ii] = inner.MakeTypedTEx(prmGetter(ii, tac));
                        return (TEx)prms[ii]!;
                    } else if((Ex)prmGetter(ii, tac) is ConstantExpression ce) {
                        prms[ii] = ce.Value;
                        return ce;
                    } else {
                        if (ast != null)
                            throw new ReflectionException(ast.Position, $"Argument #{ii + 1} to method {mi.Name} must be constant");
                        throw new CompileException($"Argument #{ii + 1} to method {mi.Name} must be constant");
                    }
                }
                void ParseArgs(int? except) {
                    for (int ii = 0; ii < prms.Length; ++ii)
                        if (ii != except)
                            ParseArg(ii);
                }
                TEx Finalize() {
                    try {
                        var raw = mi.Invoke(ast as MethodCall, prms);
                        if (raw is Func<TExArgCtx, TEx> f)
                            return f(tac);
                        else
                            return dataTyp.MakeTypedTEx(
                                raw as TEx ?? throw new StaticException($"Incorrect return from function {mi.Name}"));
                    } catch (Exception exc) {
                        if (ast != null)
                            throw new ReflectionException(ast.Position, $"Method invocation for `{mi.AsSignature}` failed.", exc);
                        throw;
                    }
                }
                //Handle the exceptional case where we assign to a dynamically scoped variable
                var writesTo = mi.GetAttribute<AssignsAttribute>()?.Indices ?? System.Array.Empty<int>();
                foreach (var writeable in writesTo) {
                    ParseArg(writeable);
                    if (ParseArg(writeable) is {} ex && Helpers.AssertWriteable(writeable, ex) is { } exc) {
                        if (writesTo.Length == 1 && ast?.Params[writeable] is WeakReference wr) {
                            //Dynamic scoped references don't return a writeable expression from the get method,
                            // so we need special handling to write to them
                            return wr.RealizeAsWeakWriteable(tac, setter => {
                                //setter is Ex, the function requires TEx<T>
                                prms[writeable] = Activator.CreateInstance(prms[writeable]!.GetType(), setter);
                                //Execute the rest of the base args inside this lambda so we can get caching
                                // on the lexical scope lookup
                                ParseArgs(except: writeable);
                                return Finalize();
                            });
                        }
                        throw (ast as MethodCall)?.Raise(exc) as Exception ?? exc;
                    }
                }
                ParseArgs(null);
                return Finalize();
            } else {
                //If the method is a "normal" method in the engine, we use Ex.Call.
                var prms = new Ex[mi.Params.Length];
                for (int ii = 0; ii < prms.Length; ++ii) {
                    //If a parameter requires TAC->TEx<T> or TEx<T> and the function does not return such a func type,
                    // then it's some kind of compilation function, so we use the parameter as is.
                    if (mi.Params[ii].Type.IsTExFuncType(out var inner)) {
                        var index = ii;
                        prms[ii] = Ex.Constant(inner.MakeTypedLambda(tac => prmGetter(index, tac)));
                    } else if (mi.Params[ii].Type.IsTExType(out inner))
                        prms[ii] = Ex.Constant(inner.MakeTypedTEx(prmGetter(ii, tac)));
                    else if (ast is MethodCall meth)
                        prms[ii] = meth.AttachScope(prmGetter(ii, tac), mi.Params[ii].Type);
                    else
                        prms[ii] = prmGetter(ii, tac);
                }
                var invoked = allowInvokeAsConst ?
                    mi.InvokeExIfNotConstant(ast as MethodCall, prms) :
                    mi.InvokeEx(ast as MethodCall, prms);
                if (mi.ReturnType.IsSubclassOf(typeof(StateMachine))) {
                    if (invoked is ConstantExpression ce)
                        invoked = Ex.Constant(ce.Value, typeof(StateMachine));
                    return (TEx<StateMachine>)invoked;
                }
                return mi.ReturnType.MakeTypedTEx(invoked);
            }
        }
        
        private Ex AttachScope(Ex prm, Type typ) {
            if (LocalScope == null) return prm;
            if (prm is ConstantExpression ce)
                return Ex.Constant(AttachScope(ce.Value, LocalScope), ce.Type);
            if (typ == typeof(StateMachine))
                return EnvFrameAttacher.attachScopeSM.Of(prm, Ex.Constant(LocalScope));
            if (typ == typeof(GenCtxProperty[])) {
                if (prm is NewArrayExpression ne && prm.NodeType == ExpressionType.NewArrayInit) {
                    return Ex.NewArrayInit(typeof(GenCtxProperty), ne.Expressions.Append(
                        Ex.Constant(GenCtxProperty._AssignLexicalScope(LocalScope))));
                } else
                    return EnvFrameAttacher.extendScopeProps.Of(prm, Ex.Constant(LocalScope));
            }
            if (typ.IsGenericType && typ.GetGenericTypeDefinition() == typeof(GenCtxProperties<>)) {
                return EnvFrameAttacher.attachScopeProps.Specialize(prm.Type.GetGenericArguments())
                    .InvokeEx(this, prm, Ex.Constant(LocalScope));
            }
            return prm;
        }
        public static object? AttachScope(object? prm, LexicalScope localScope) => prm switch {
                GenCtxProperties props => EnvFrameAttacher.AttachScopePropsAny(props, localScope),
                GenCtxProperty[] props => EnvFrameAttacher.ExtendScopeProps(props, localScope),
                StateMachine sm => EnvFrameAttacher.AttachScopeSM(sm, localScope),
                _ => prm
            };

        public ReflectionException Raise(Helpers.NotWriteableException exc) =>
            new (Position, Params[exc.ArgIndex].Position, exc.Message, exc.InnerException);

        public string Explain() {
            var meth = "<No method found>";
            if (SelectedOverload?.method is { } im)
                meth = im.Mi.AsSignature;
            else if (Methods.Length > 0)
                meth = Methods[0].Mi.AsSignature;
            else if (this is InstanceMethodCall imc)
                meth = imc.Name + " (undetermined signature)";
            return $"{CompactPosition} {meth}";
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            if (SelectedOverload?.method is { } m) {
                try {
                    if (m.Mi.IsCtor && m.Mi.ReturnType == typeof(PhaseSM) &&
                        (Ex)Params[1].Realize(new TExArgCtx()) is ConstantExpression { Value: PhaseProperties props }) {
                        return new($"{props.phaseType?.ToString() ?? "Phase"}", props.cardTitle?.Value ?? "",
                            m.Mi.Member.Symbol, Position.ToRange(), 
                            FlattenParams((p, i) => p.ToSymbolTree($"({m.Params[i].Name})")));
                    }
                } catch {
                    //pass
                }
                return m.Mi.IsFallthrough ? 
                        Params[0].ToSymbolTree() :
                        new(m.Name, m.Mi.TypeOnlySignature, m.Mi.Member.Symbol, Position.ToRange(), 
                            FlattenParams((p, i) => p.ToSymbolTree($"({m.Params[i].Name})")));
            } else if (Methods.Length > 0)
                return new(Methods[0].Name, null, Methods[0].Mi.Member.Symbol, Position.ToRange(),
                    FlattenParams((p, i) => p.ToSymbolTree($"({Methods[0].Params[i].Name})")));
            else
                return new DocumentSymbol("<No method found>", null, SymbolKind.Method, Position.ToRange(),
                    FlattenParams(null));
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
            var children = base._ToSemanticTokens();
            return AddMethodSemanticToken ?
                children.Prepend(
                    SelectedOverload is { } m ?
                        SemanticToken.FromMethod(m.method.Mi, MethodPosition,
                            retType: m.simplified.Resolve().LeftOrNull) :
                        new SemanticToken(MethodPosition, SemanticTokenTypes.Method)) :
                children;
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{DebugPosition} {(SelectedOverload?.method ?? Methods[0]).TypeEnclosedName}(";
            foreach (var w in IDebugPrint.PrintArgs(Params))
                yield return w;
            yield return ")";
        }
    }
    
    /// <summary>
    /// An AST that invokes (possibly overloaded) instance methods, fields, or properties.
    /// </summary>
    //no local scope handling for instance methods
    public record InstanceMethodCall(PositionRange Position, PositionRange MethodPosition, LexicalScope EnclosingScope, string Name, params IAST[] Params) : MethodCall(Position, MethodPosition, EnclosingScope, System.Array.Empty<Reflector.InvokedMethod>(), Params), IMethodTypeTree<Reflector.InvokedMethod> {
        public List<(TypeDesignation, Unifier)>? Arg0PossibleTypes { get; protected set; }

        void IMethodTypeTree<Reflector.InvokedMethod>.GenerateOverloads(List<(TypeDesignation, Unifier)>[] arguments) {
            Methods = (Arg0PossibleTypes = arguments[0]).SelectMany(tu => {
                var td = tu.Item1;
                Type t;
                if (td.IsResolved) {
                    t = td.Resolve().LeftOrThrow;
                } else if (td is Known k) {
                    t = k.Typ;
                } else
                    return System.Array.Empty<MethodSignature>();
                return t.GetMember(Name).Where(x => x.GetCustomAttribute<DontReflectAttribute>() is null)
                    .SelectNotNull(MethodSignature.MaybeGet)
                    .Concat(Reflector.ReflectionData.ExtensionMethods(t, Name));
            }).Where(sig => (!sig.Member.Static || sig.Member is TypeMember.Method {IsExtension:true}) 
                            && sig.Params.Length == Params.Length)
                .Select(x => x.Call(Name)).ToArray();
        }
        
        public override Either<IImplicitTypeConverter, bool> ImplicitParameterCast(Reflector.InvokedMethod overload, int index) {
            if (index == 0)
                return false;
            return base.ImplicitParameterCast(overload, index);
        }
    }

    public record Indexer(PositionRange Position, LexicalScope EnclosingScope, IAST Object, IAST Index)
        : InstanceMethodCall(Position, Position, EnclosingScope, "[indexer]", Object, Index), IMethodTypeTree<Reflector.InvokedMethod> {
        private static readonly MethodSignature[] arrayIndexMethod = {
            MethodSignature.Get(typeof(ExM).GetMethod(nameof(ExM.ArrayIndex))!)
        };

        public override bool AddMethodSemanticToken => false;

        void IMethodTypeTree<Reflector.InvokedMethod>.GenerateOverloads(List<(TypeDesignation, Unifier)>[] arguments) {
            Methods = (Arg0PossibleTypes = arguments[0]).SelectMany(tu => {
                    var td = tu.Item1;
                    if (td is Known { IsArrayTypeConstructor: true })
                        return arrayIndexMethod;
                    Type t;
                    if (td.IsResolved) {
                        t = td.Resolve().LeftOrThrow;
                    } else if (td is Known k) {
                        t = k.Typ;
                    } else
                        return System.Array.Empty<MethodSignature>();
                    return t.GetMember("Item").SelectNotNull(MethodSignature.MaybeGet)
                        .Where(sig => !sig.Member.Static && sig.Params.Length == Params.Length);
                }).Select(x => x.Call(Name)).ToArray();
        }
    }
    
    
    public record PartialInvokedMethod(Reflector.InvokedMethod Meth, int Curry) : IMethodDesignation {
        public Dummy Method { get; } = PartialFn.PartiallyApply(Meth.Method, Curry, false);
    }

    /// <summary>
    /// A partial invocation of a lambda object.
    /// <br/> eg. var a = $(staticMethod, x); var b = $(a, y).
    /// <br/> $(a, y) is a PartialLambdaCall (and $(staticMethod, x) is a PartialMethodCall).
    /// </summary>
    public record PartialLambdaCall(PositionRange Position, LexicalScope EnclosingScope, params IAST[] Params) :
        AST(Position, EnclosingScope, Params), IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; private set; } = null!;
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public PositionRange MethodPosition => Params[0].Position;
        
        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var rt = ReturnType(SelectedOverload!.Value.simplified);
            return rt.MakeTypedTEx(PartialFn.PartiallyApply(Params[0].Realize(tac), 
                Params.Skip(1).Select(p => (Ex)p.Realize(tac))));
        }

        void IMethodTypeTree<Dummy>.GenerateOverloads(List<(TypeDesignation, Unifier)>[] arguments) {
            //if pargs = 3 (eg. Func<A,B,C,D>, A, B), then we have the following overloads:
            // Func<A,B,C>, A, B -> Func<C>
            // Func<A,B,C,D>, A, B -> Func<C, D>
            // Func<A,B,C,D,E>, A, B -> Func<C, D, E>
            //...
            //This set is theoretically infinite, so it's better to generate it dynamically based 
            // on the possible types 

            var applied = arguments.Length - 1;

            Dummy? OverloadForLambda(int arity) {
                if (arity <= applied)
                    return null;
                var origFnAsMethod = new Dummy(Dummy.METHOD_KEY,
                    arity.Range().Select(_ => new Variable() as TypeDesignation).ToArray());
                return PartialFn.PartiallyApply(origFnAsMethod, applied, true);
            }

            //No choice but to generate all of them... let's generate 8 for now
            if (arguments[0].Any(tdu => tdu.Item1 is Variable))
                Overloads = 8.Range().SelectNotNull(OverloadForLambda).ToArray();
            else {
                var arities = new HashSet<int>();
                foreach (var tdu in arguments[0])
                    if (tdu.Item1 is Known kt && ReflectionUtils.FuncTypesByArity.Contains(kt.Typ))
                        arities.Add(kt.Arguments.Length);
                Overloads = arities.SelectNotNull(OverloadForLambda).ToArray();
            }
        }
        
        public string Explain() {
            return $"{CompactPosition} Lambda invocation";
        }
        
        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new("Lambda invocation", descr, SymbolKind.Method, Position.ToRange(), FlattenParams(null));
        }
        
    }

    /// <summary>
    /// An invocation of a lambda object.
    /// <br/> eg. var a = $(staticMethod, x, y); print(a(z)).
    /// <br/> a(z) is a LambdaCall.
    /// </summary>
    public record LambdaCall(PositionRange Position, LexicalScope EnclosingScope, params IAST[] Params) : 
        AST(Position, EnclosingScope, Params), IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; } = new[] {
            FuncOverloadForArgCount(Params.Length),
            ActionOverloadForArgCount(Params.Length),
        };
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public PositionRange MethodPosition => Params[0].Position;

        
        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var rt = ReturnType(SelectedOverload!.Value.simplified);
            return rt.MakeTypedTEx(PartialFn.Execute(Params[0].Realize(tac), 
                Params.Skip(1).Select(p => (Ex)p.Realize(tac))));
        }

        private static Dummy FuncOverloadForArgCount(int pargs) {
            var pfnTyps = pargs.Range().Select(_ => new Variable() as TypeDesignation).ToArray();
            var argTyps = pfnTyps.Take(pargs - 1);
            return Dummy.Method(pfnTyps[^1], argTyps.Prepend(PartialFn.MakeFuncType(pfnTyps)).ToArray());
        }
        private static Dummy ActionOverloadForArgCount(int pargs) {
            var pfnTyps = (pargs - 1).Range().Select(_ => new Variable() as TypeDesignation)
                .Append(TypeDesignation.FromType(typeof(void))).ToArray();
            var argTyps = pfnTyps.Take(pargs - 1);
            return Dummy.Method(pfnTyps[^1], argTyps.Prepend(PartialFn.MakeFuncType(pfnTyps)).ToArray());
        }
        
        public string Explain() {
            return $"{CompactPosition} Lambda invocation";
        }
        
        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new("Lambda invocation", descr, SymbolKind.Method, Position.ToRange(), FlattenParams(null));
        }
    }
    
    /// <summary>
    /// A partial invocation of a static method (that would otherwise be a <see cref="AST.MethodCall"/>).
    /// <br/>In the form $(staticMethod, x, y).
    /// </summary>
    public record PartialMethodCall : AST, IMethodAST<PartialInvokedMethod> {
        private Reflector.InvokedMethod[] Methods { get; }
        public IReadOnlyList<PartialInvokedMethod> Overloads { get; } 
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<PartialInvokedMethod>? RealizableOverloads { get; set; }
        public (PartialInvokedMethod method, Dummy simplified)? SelectedOverload { get; set; }
        public PositionRange MethodPosition { get; init; }
        
        public PartialMethodCall(PositionRange Position, PositionRange MethodPosition, LexicalScope EnclosingScope,
            Reflector.InvokedMethod[] Methods, params IAST[] Params) : base(Position, EnclosingScope, Params) {
            this.MethodPosition = MethodPosition;
            this.Methods = Methods.Select(m => m.HideSMReturn()).ToArray();
            Overloads = this.Methods.Select(m => new PartialInvokedMethod(m, Params.Length)).ToArray();
        }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var (inv, typ) = SelectedOverload!.Value;
            var mi = MethodCall.SpecializeMethod(this, MethodPosition, inv.Meth.Mi, 
                PartialFn.PartiallyUnApply(typ, Params.Length, false));
            var pfn = ((MethodSignature)mi).AsFunc();
            return ReturnType(typ).MakeTypedTEx(
                PartialFn.PartiallyApply(Ex.Constant(pfn), Params.Select(p => (Ex)p.Realize(tac))));
        }
        
        public string Explain() {
            var meth = "<No method found>";
            if (SelectedOverload?.method is { } im)
                meth = im.Meth.Mi.AsSignature;
            else if (Methods.Length > 0)
                meth = Methods[0].Mi.AsSignature;
            return $"{CompactPosition} {meth} (invoked with {Params.Length} args)";
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            var m = SelectedOverload?.method.Meth ?? Methods[0];
            return new(m.Name, $"(invoked with {Params.Length} args)", m.Mi.Member.Symbol, Position.ToRange(), 
                FlattenParams((p, i) => p.ToSymbolTree($"({m.Params[i].Name})")));
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() =>
            base._ToSemanticTokens().Prepend(
                SelectedOverload is { } m ?
                    SemanticToken.FromMethod(m.method.Meth.Mi, MethodPosition, 
                        retType: m.simplified.Last.Arguments[^1].Resolve().LeftOrNull) :
                    new SemanticToken(MethodPosition, SemanticTokenTypes.Method));
    }


    /// <summary>
    /// A partial invocation of a script function (that would otherwise be a <see cref="AST.ScriptFunctionCall"/>).
    /// <br/>In the form $(scriptFunction, x, y).
    /// </summary>
    public record PartialScriptFunctionCall(PositionRange Position, PositionRange MethodPosition, LexicalScope EnclosingScope,
        ScriptImport? InImport, ScriptFnDecl Definition, params IAST[] Params) : AST(Position, EnclosingScope, Params), IMethodAST<Dummy>, IScriptFnCall {
        public IReadOnlyList<Dummy> Overloads { get; } = new[] {
            PartialFn.PartiallyApply(Definition.CallType, Params.Length, false)
        };
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public string NameWithImport => InImport is null ? Definition.Name : $"{InImport.Name}.{Definition.Name}";
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }

        public bool IsDynamicInvocation => false;

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            Ex raisedEf = IScriptFnCall.RaiseEfAndGetParams(this, tac, out var prms);
            var target = Definition.IsConstant ?
                PartialFn.PartiallyApply(Definition.Compile(), prms) :
                PartialFn.PartiallyApply(Definition.Compile(), prms.Prepend(raisedEf));
            return ReturnType(SelectedOverload?.simplified!).MakeTypedTEx(target);
        }

        public string Explain() => $"{CompactPosition} {Definition.AsSignature(InImport?.Name)} (invoked with {Params.Length} args)" +
                                   (Definition.DocComment is { } c ? $"  \n*{c}*" : "");

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol(NameWithImport, Definition.TypeOnlySignature + $" (invoked with {Params.Length} args)", SymbolKind.Method,
                Position.ToRange(),
                FlattenParams((p, i) => p.ToSymbolTree($"({Definition.Args[i]})")));
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() =>
            base._ToSemanticTokens().Prepend(new SemanticToken(MethodPosition, SemanticTokenTypes.Function)
                .WithConst(Definition.IsConstant));
    }

    public interface IScriptFnCall: IAST {
        LexicalScope Scope { get; }
        ScriptImport? InImport { get; }
        ScriptFnDecl Definition { get; }
        bool IsDynamicInvocation { get; }
        
        public static Ex RaiseEf(IScriptFnCall me, TExArgCtx tac) {
            if (me.Definition.IsConstant)
                return tac.EnvFrame;
            else if (me.InImport is null)
                return me.IsDynamicInvocation ?
                    tac.EnvFrame :
                    me.Scope.LocalOrParentFunctionEf(tac.EnvFrame, me.Definition);
            else
                return me.InImport.Ef.Scope.LocalOrParentFunctionEf(Ex.Constant(me.InImport.Ef), me.Definition);
        }

        public static Ex RaiseEfAndGetParams(IScriptFnCall me, TExArgCtx tac, out IEnumerable<Ex> prms) {
            Ex raisedEf = RaiseEf(me, tac);
            var raisedTac = tac.MakeCopyForType<EnvFrame>(raisedEf);
            prms = me.Params.Select((p, i) => {
                if (p is DefaultValue { AsFunctionArg: true }) {
                    Ex deflt = me.Definition.Defaults[i]!.Realize(raisedTac);
                    if (me.IsDynamicInvocation && deflt is not ConstantExpression)
                        throw new ReflectionException(p.Position, 
                            $"When dynamically invoking a method, any default values must be constants.");
                    return deflt;
                }
                return (Ex)p.Realize(tac);
            });
            return raisedEf;
        }
        
        IEnumerable<ReflectionException> IAST.Verify() {
            for (int ii = 0; ii < Params.Length; ++ii)
                if (Params[ii] is DefaultValue {AsFunctionArg: true} p && Definition.Defaults[ii] is null)
                    yield return new ReflectionException(p.Position,
                        $"No default value is configured for argument {Definition.Args[ii].AsParam} of script function {Definition.Name}.");
            foreach (var err in IAST.VerifyChildren(this))
                yield return err;
        }
    }

    public record ScriptFunctionCall(PositionRange Position, PositionRange MethodPosition,
        LexicalScope EnclosingScope, ScriptImport? InImport, ScriptFnDecl Definition, bool IsDynamicInvocation, params IAST[] Params) : AST(Position, EnclosingScope, Params), IMethodAST<Dummy>, IScriptFnCall {
        public IReadOnlyList<Dummy> Overloads { get; } = new[] { Definition.CallType };
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public string NameWithImport => InImport is null ? Definition.Name : $"{InImport.Name}.{Definition.Name}";
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        
        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Dummy>.ImplicitParameterCast(Dummy overload, int index) {
            if (overload.Arguments[index].Resolve().TryL(out var t) && 
                DMKScope.GetConverterForCompiledExpressionType(t) is { } conv)
                return new(conv);
            return true;
        }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            Ex raisedEf = IScriptFnCall.RaiseEfAndGetParams(this, tac, out var prms);
            Expression target;
            if (Definition.IsConstant) {
                var func = Definition.Compile();
                object[] cprms = new object[Params.Length];
                var aprms = prms.ToArray();
                if (func is not ConstantExpression cfunc)
                    goto call_ex;
                for (int ii = 0; ii < aprms.Length; ++ii) {
                    if (aprms[ii] is not ConstantExpression cex)
                        goto call_ex;
                    cprms[ii] = cex.Value;
                }
                //InvokeExIfNotConstant logic, but only for const functions.
                target = Ex.Constant(cfunc.Type.GetMethod("Invoke")!.Invoke(cfunc.Value, cprms));
                goto end;
                call_ex:
                target = PartialFn.Execute(func, aprms);
            } else if (InImport is null && IsDynamicInvocation) {
                target = LexicalScope.FunctionWithoutLexicalScope(tac, Definition, prms);
            } else
                target = PartialFn.Execute(Definition.Compile(), prms.Prepend(raisedEf));
            end:
            return ReturnType(SelectedOverload?.simplified!).MakeTypedTEx(target);
        }

        public string Explain() => $"{CompactPosition} {Definition.AsSignature(InImport?.Name)}" +
            (Definition.DocComment is { } c ? $"  \n*{c}*" : "");

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol(NameWithImport, Definition.TypeOnlySignature, SymbolKind.Method, Position.ToRange(),
                FlattenParams((p, i) => p.ToSymbolTree($"({Definition.Args[i]})")));
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() =>
            base._ToSemanticTokens().Prepend(new SemanticToken(MethodPosition, SemanticTokenTypes.Function)
                .WithConst(Definition.IsConstant));
    }
    
    public record ScriptFunctionDef(PositionRange Position, string Name, LexicalScope EnclosingScope, LexicalScope FnScope, ScriptFnDecl Definition, Block Body) : AST(Position, EnclosingScope, Definition.Defaults.FilterNone().Append(Body).ToArray()), IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; } = 
            new[] { Dummy.Method(new Known(typeof(void)), 
                Definition.Args.Select((a, i) => Definition.Defaults[i] != null ? a.TypeDesignation : null)
                    .FilterNone()
                    .Append(Body.Overloads[0].Last).ToArray()) };
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            return Ex.Empty();
        }

        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Dummy>.ImplicitParameterCast(Dummy overload, int index) => false;

        IEnumerable<ReflectionException> IAST.Verify() {
            if (Definition.IsConstant) {
                foreach (var refr in Params[0].EnumeratePreorder().OfType<Reference>())
                    if (refr.Value.TryL(out var d) && !d.Constant && d is not ImplicitArgDecl && !d.DeclarationScope.IsIssueOf(FnScope)) {
                        yield return new ReflectionException(refr.Position, 
                            $"The variable `{d.Name}` (declared at {d.Position}) cannot be referenced from inside a constant function.");
                    }
            }
            foreach (var err in IAST.VerifyChildren(this))
                yield return err;
        }

        public Type CompileFuncType() {
            var args = Definition.Args;
            var fTypes = new Type[args.Length + 2];
            fTypes[0] = typeof(EnvFrame);
            for (int ii = 0; ii < args.Length; ++ii)
                fTypes[ii + 1] = args[ii].FinalizedType ?? throw new ReflectionException(Position,
                    $"Script function argument {args[ii].Name}'s type is not finalized");
            fTypes[^1] = Body.LocalScope!.Return!.FinalizedType ??
                         throw new ReflectionException(Position, $"Script function return type is not finalized");
            if (Definition.IsConstant) //don't include ef
                fTypes = fTypes.Skip(1).ToArray();
            return ReflectionUtils.MakeFuncType(fTypes);
        }

        public object CompileFunc(Type fnType) {
            var args = Definition.Args;
            var ret = Body.LocalScope!.Return!;
            Func<TExArgCtx, TEx> body = tac => {
                return Ex.Block(
                    Body.Realize(tac),
                    ret.FinalizedType == typeof(void) ? Ex.Empty() :
                        Ex.Throw(Ex.Constant(new Exception($"The function {Definition.AsSignature()} ran to its end " +
                                                           $"without reaching a return statement."))),
                    Ex.Label(ret.Label, Ex.Default(ret.FinalizedType!))
                );
            };
            var argsWithEf = Definition.IsConstant ? args : //don't include ef
                args.Prepend(new DelegateArg<EnvFrame>("callerEf") as IDelegateArg).ToArray();
            var func = CompilerHelpers.CompileDelegateMeth.Specialize(fnType).Invoke(null, body, argsWithEf)!;
            return func;
        }

        public string Explain() => (Definition.IsConstant ? "Const function " : "Function ") 
                                   + $"definition: {Definition.AsSignature()}" +
                                    (Definition.DocComment is { } c ? $"  \n*{c}*" : "");

        public DocumentSymbol ToSymbolTree(string? descr = null) =>
            new((Definition.IsConstant ? "Const function ": "Function ") + Name, 
                Definition.TypeOnlySignature, SymbolKind.Function, Position.ToRange(),
                Params.Select(p => p.ToSymbolTree()));

    }

    
    
    /// <summary>
    /// A return statement in a function definition.
    /// </summary>
    public record Return(PositionRange Position, LexicalScope EnclosingScope, IAST? Value) : AST(Position,
        EnclosingScope, Value == null ? System.Array.Empty<IAST>() : new[]{Value}), IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; private set; } = SetOverloads(EnclosingScope, Value);
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }

        IEnumerable<ReflectionException> IAST.Verify() {
            var typ = ReturnType(SelectedOverload?.simplified!);
            if (typ == typeof(void) && Value != null)
                yield return new(Position, "This function has a return type of void. No return value can be provided.");
            if (typ != typeof(void) && Value == null)
                yield return new(Position, "A return value is required here.");
            foreach (var err in IAST.VerifyChildren(this))
                yield return err;
        }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var returnCfg = Scope.NearestReturn!;
            var typ = ReturnType(SelectedOverload?.simplified!);
            //Free everything up to and including the EF for the scope that is being returned
            var frees = Scope.FreeEfs(tac.EnvFrame, s => s != returnCfg.Scope.Parent);
            if (Value is null) {
                return Ex.Block(frees.Append(Ex.Return(returnCfg.Label)));
            } else {
                if (frees.Count > 0) {
                    Ex retVal = Value.Realize(tac);
                    var ret = Ex.Parameter(retVal.Type);
                    return Ex.Block(new[] { ret },
                        frees
                            .Prepend(ret.Is(retVal))
                            .Append(Ex.Return(returnCfg.Label, ret))
                    );
                } else
                    return typ.MakeTypedTEx(Ex.Return(returnCfg.Label, Value.Realize(tac)));
            }
        }

        public override void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
            var prevScope = EnclosingScope;
            base.ReplaceScope(prev, inserted);
            if (EnclosingScope != prevScope) {
                if (Scope.NearestReturn == null)
                    throw new ReflectionException(Position, "This return statement isn't contained within a function definition.");
                Overloads = SetOverloads(Scope, Value);
            }
        }

        private static Dummy[] SetOverloads(LexicalScope scope, IAST? Value) {
            var t = scope.NearestReturn?.Type ??
                    throw new Exception("No return statement found");
            return new[] {
                Value == null ?
                    Dummy.Method(t) :
                    Dummy.Method(t, t)
            };
        }
        
        //only allow implicit cast on return if the return type is declared
        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Dummy>.ImplicitParameterCast(Dummy overload, int index) =>
            EnclosingScope.NearestReturn?.Type is not Variable;

        public string Explain() => Value == null ? "return" : Value.Explain();

        public DocumentSymbol ToSymbolTree(string? descr = null) =>
            Value?.ToSymbolTree(descr) ?? new("return", "(void)", SymbolKind.Null, Position.ToRange());

    }
    
    
    /// <summary>
    /// A conditional expression (x ? y : z) or an if statement (if (x) { y; } else { z; }).
    /// </summary>
    public record Conditional : AST, IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; private init; }
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        private readonly bool asConditional;
        public Conditional(PositionRange Position, LexicalScope EnclosingScope, bool asConditional, IAST condition, IAST ifTrue, IAST? ifFalse) : base(Position,
            EnclosingScope, ifFalse == null ? new[]{condition, ifTrue} : new[]{condition, ifTrue, ifFalse}) {
            this.asConditional = asConditional;
            if (asConditional) {
                var t = new Variable();
                Overloads = new[] {
                    Dummy.Method(t, new Known(typeof(bool)), t, t),
                };
            } else {
                if (ifTrue is Block tb)
                    tb.DiscardReturnValue = true;
                if (ifFalse is Block fb)
                    fb.DiscardReturnValue = true;
                Overloads = new[] {
                    Dummy.Method(new Known(typeof(void)), Params.Select((_, i) => 
                        i == 0 ? new Known(typeof(bool)) : new Variable() as TypeDesignation).ToArray()),
                };
            }
        }

        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Dummy>.ImplicitParameterCast(Dummy overload, int index) =>
            asConditional;

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            if (asConditional) {
                return Ex.Condition(Params[0].Realize(tac), Params[1].Realize(tac), Params[2].Realize(tac));
            } else if (Params.Length == 2) {
                return Ex.IfThen(Params[0].Realize(tac), Params[1].Realize(tac));
            } else {
                return Ex.IfThenElse(Params[0].Realize(tac), Params[1].Realize(tac), Params[2].Realize(tac));
            }
        }
        
        public string Explain() {
            return $"{CompactPosition} if/then";
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new("If/Then", descr, SymbolKind.Object, Position.ToRange(), 
                Params.Select(p => p.ToSymbolTree()));
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{DebugPosition} (";
            foreach (var w in Params[0].DebugPrint())
                yield return w;
            yield return ") ?";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in Params[1].DebugPrint())
                yield return w;
            yield return ":";
            yield return PrintToken.newline;
            foreach (var w in Params[2].DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
        }

    }
    
    
    /// <summary>
    /// A for or while loop.
    /// </summary>
    public record Loop : AST, IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; }
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        
        public IAST? Initializer { get; }
        public IAST? Condition { get;}
        public IAST? Finalizer { get; }
        public IAST Body { get; }

        /// <summary>
        /// A for or while loop.
        /// </summary>
        public Loop(PositionRange Position, LexicalScope EnclosingScope, LexicalScope LocalScope, IAST? Initializer, IAST? Condition, IAST? Finalizer, IAST Body) : base(Position,
            EnclosingScope, new[]{Initializer, Condition, Finalizer, Body}.Where(x => x != null).ToArray()!) {
            //loops have a small local scope where the initializer variable and continue/break targets are located
            this.LocalScope = LocalScope;
            this.Initializer = Initializer;
            this.Condition = Condition;
            this.Finalizer = Finalizer;
            this.Body = Body;
            if (Body is Block b)
                b.DiscardReturnValue = true;
            Overloads = new[] {
                Dummy.Method(new Known(typeof(void)), new TypeDesignation?[] {
                    Initializer == null ? null : new Variable(),
                    Condition == null ? null : new Known(typeof(bool)),
                    Finalizer == null ? null : new Variable(),
                    new Variable()
                }.Where(x => x != null).ToArray()!),
            };
        }

        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Dummy>.ImplicitParameterCast(Dummy overload, int index) => 
            (index == 0 && Initializer == null) || (index == 1 && Initializer != null);

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var (c, b, _) = LocalScope!.NearestContinueBreak!.Value;
            return Block.MakeExpressionBlock(this, LocalScope, tac, tac => new Ex[] {
                Initializer?.Realize(tac) ?? Ex.Empty(),
                Ex.Loop(Ex.IfThenElse(Ex.Not(Condition?.Realize(tac) ?? Ex.Constant(true)), Ex.Break(b),
                    Ex.Block(
                        //The continue label is set as Body.EndWithLabel
                        Body.Realize(tac), 
                        Finalizer?.Realize(tac) ?? Ex.Empty())), b)
            });
        }
        
        public string Explain() {
            return $"{CompactPosition} loop";
        }

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new("Loop", descr, SymbolKind.Object, Position.ToRange(), 
                new[]{Body.ToSymbolTree()});
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{DebugPosition} for (";
            if (Initializer != null)
                foreach (var w in Initializer.DebugPrint())
                    yield return w;
            yield return "; ";
            if (Condition != null)
                foreach (var w in Condition.DebugPrint())
                    yield return w;
            yield return "; ";
            if (Finalizer != null)
                foreach (var w in Finalizer.DebugPrint())
                    yield return w;
            yield return ") {";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in Body.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
            yield return "}";
        }
    }
    
    
    /// <summary>
    /// A continue statement in a for/while loop.
    /// </summary>
    public record Continue(PositionRange Position, LexicalScope EnclosingScope) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        public TypeDesignation[] PossibleTypes { get; } = { new Known(typeof(void)) };
        public TypeDesignation? SelectedOverload { get; set; }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var (c, _, sc) = Scope.NearestContinueBreak!.Value;
            //We don't need to free the loop's inner block scope, only any nested scopes
            return Ex.Block(Scope.FreeEfs(tac.EnvFrame, s => s.Parent != sc).Append(Ex.Continue(c)));
        }

        public override void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
            base.ReplaceScope(prev, inserted);
            if (Scope.NearestContinueBreak == null)
                throw new ReflectionException(Position, "This continue statement isn't contained within a loop.");
        }

        public string Explain() => "continue";

        public DocumentSymbol ToSymbolTree(string? descr = null) =>
            new("continue", null, SymbolKind.Null, Position.ToRange());
        
        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
            yield return new(Position, SemanticTokenTypes.Keyword);
        }
    }
    
    /// <summary>
    /// A break statement in a for/while loop.
    /// </summary>
    public record Break(PositionRange Position, LexicalScope EnclosingScope) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        public TypeDesignation[] PossibleTypes { get; } = { new Known(typeof(void)) };
        public TypeDesignation? SelectedOverload { get; set; }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var (_, b, sc) = Scope.NearestContinueBreak!.Value;
            //Free the loop's inner block scope, but not the loop scope itself (that will be freed after the break jump)
            return Ex.Block(Scope.FreeEfs(tac.EnvFrame, s => s != sc).Append(Ex.Break(b)));
        }

        public override void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
            base.ReplaceScope(prev, inserted);
            if (Scope.NearestContinueBreak == null)
                throw new ReflectionException(Position, "This break statement isn't contained within a loop.");
        }

        public string Explain() => "continue";

        public DocumentSymbol ToSymbolTree(string? descr = null) =>
            new("continue", null, SymbolKind.Null, Position.ToRange());
        
        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
            yield return new(Position, SemanticTokenTypes.Keyword);
        }
    }
    
    public record Block : AST, IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; private init; }
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public (VarDecl variable, ImplicitArgDecl prm)[]? FunctionParams { get; private set; }
        public bool DiscardReturnValue { get; set; } = false;
        /// <summary>
        /// A label placed right before envframe freeing and returns.
        /// </summary>
        public LabelTarget? EndWithLabel { get; set; }
        public Block(PositionRange Position, LexicalScope EnclosingScope, LexicalScope localScope, TypeDesignation? retType, params IAST[] Params) : base(Position,
            EnclosingScope, Params) {
            this.LocalScope = localScope;
            Overloads = MakeOverloads(retType, Params);
        }

        private static IReadOnlyList<Dummy> MakeOverloads(TypeDesignation? retType, IAST[] prms) {
            //Note: for function bodies, retType is set to null because `return` statements determine the actual
            // return type
            var typs = prms.Select((p, i) => {
                if (i < prms.Length - 1 || retType is null)
                    return new Variable();
                else
                    return retType;
            }).ToArray();
            return new[] {
                Dummy.Method(typs[^1], typs), //(T1,T2...,R)->(R)
            };
        }
        public Block WithFunctionParams(params (VarDecl, ImplicitArgDecl)[] args) {
            foreach (var (v, _) in (FunctionParams = args)) {
                v.Assignments++;
            }
            return this;
        }

        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Dummy>.ImplicitParameterCast(Dummy overload, int index) => false;

        public void FinalizeUnifiers(Unifier unifier) {
            IMethodTypeTree<Dummy>._FinalizeUnifiers(this, unifier);
            void AttachEnvFrame(IAST child) {
                if (child.EnclosingScope != LocalScope)
                    return;
                if (child is MethodCall { SelectedOverload: {} so }  meth
                    && so.method.Mi.GetAttribute<AssignsAttribute>() is null
                    && so.simplified.Last.Resolve(unifier).TryL(out var typ)
                    && DMKScope.useEfWrapperTypes.Contains(typ)) {
                    //We attach the envframe to the highest non-assign method call which is not in a subscope
                    // and returns a type of SM/AP/SP
                    if (typ == typeof(StateMachine))
                        meth.SameTypeCast = DMKScope.AttachEFtoSMImplicit;
                    else if (typ == typeof(AsyncPattern))
                        meth.SameTypeCast = DMKScope.AttachEFtoAPImplicit;
                    else if (typ == typeof(SyncPattern))
                        meth.SameTypeCast = DMKScope.AttachEFtoSPImplicit;
                    else throw new StaticException($"Wrong type for attaching envframe: {typ.RName()}");
                    return;
                }
                foreach (var prm in child.Params)
                    AttachEnvFrame(prm);
            }
            foreach (var prm in Params)
                AttachEnvFrame(prm);
        }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var typ = ReturnType(SelectedOverload?.simplified!);
            return typ.MakeTypedTEx(MakeExpressionBlock(this, LocalScope!, tac, tac => 
                Params.Select<IAST, Ex>((p, i) => {
                    try {
                        return p.Realize(tac);
                    } catch (Exception e) {
                        if (e is ReflectionException)
                            throw;
                        else
                            throw new ReflectionException(p.Position, "Couldn't invoke this code.", e);
                    }
                }))
            );
        }

        public static Ex MakeExpressionBlock(AST me, LexicalScope localScope, TExArgCtx tac, Func<TExArgCtx, IEnumerable<Expression>> stmts) {
            var endLabel = (me is Block { EndWithLabel: { } label }) ? Ex.Label(label) : null;
            if (localScope.UseEF) {
                Ex? parentEf = null;
                bool hasEfOutPrm = false;
                ParameterExpression? ef = null;
                if (tac.MaybeGetByType<EnvFrame>(out _) is { } tex) {
                    if (localScope.Parent is DMKScope) {
                        if (((Ex)tex) is ParameterExpression { IsByRef: true } pex) {
                            hasEfOutPrm = true;
                            ef = pex;
                        }
                    } else //even if we have access to an EF, potentially as an implicit from ParametricInfo,
                           // we can't derive it if the parent scope is DMKScope
                        parentEf = tex;
                }
                parentEf ??= Ex.Constant(null, typeof(EnvFrame));
                ef ??= Ex.Parameter(typeof(EnvFrame), "$ef");
                var efPrm = hasEfOutPrm ? System.Array.Empty<ParameterExpression>() : new[]{ef};
                tac = tac.MaybeGetByType<EnvFrame>(out _) != null ?
                    tac.MakeCopyForType<EnvFrame>(ef) :
                    tac.Append("rootEnvFrame", ef);
                var fp = (me as Block)?.FunctionParams;
                //Copy function params into envframe so they can be captured in SM/AP/SP returns
                var copyFromParams = fp != null ?
                    fp.SelectNotNull(d => {
                        var prm = d.prm.Value(tac.EnvFrame, tac);
                        if (prm == ef)
                            return null;
                        return d.variable.Value(tac.EnvFrame, tac).Is(prm);
                    }) :
                    System.Array.Empty<Expression>();
                var copyBackToRefParams = fp != null ?
                    fp.SelectNotNull(d => {
                        var prm = d.prm.Value(tac.EnvFrame, tac);
                        if (prm == ef || prm is not ParameterExpression { IsByRef: true })
                            return null;
                        return prm.Is(d.variable.Value(tac.EnvFrame, tac));
                    }) :
                    System.Array.Empty<Expression>();
                var statements = 
                    copyFromParams.Concat(stmts(tac))
                    .Prepend(ef.Is(EnvFrame.exCreate.Of(Ex.Constant(localScope), parentEf)))
                    .ToList();
                if (me is Block b && b.Params.Any(p => p is Return or Continue or Break))
                    //Don't dispose envframe (the ret/continue/break will handle it)
                    return Ex.Block(efPrm, statements.AppendIfNonnull(endLabel).Concat(copyBackToRefParams));
                else if (statements[^1].Type == typeof(void) || me is Block { DiscardReturnValue: true })
                    //Dispose envframe as last statement
                    return Ex.Block(efPrm, 
                            statements
                            .AppendIfNonnull(endLabel)
                            .Concat(copyBackToRefParams)
                            .AppendIfNonnull(hasEfOutPrm ? null : EnvFrame.exFree.InstanceOf(ef)));
                else {
                    if (endLabel != null)
                        throw new ReflectionException(me.Position, "An end label cannot be used with a non-void block");
                    //Assign last statement to temp var, then dispose envframe
                    var ret = Ex.Parameter(statements[^1].Type, "$ret");
                    statements[^1] = ret.Is(statements[^1]);
                    return Ex.Block(efPrm.Append(ret), 
                        statements
                            .Concat(copyBackToRefParams)
                            .AppendIfNonnull(hasEfOutPrm ? null : EnvFrame.exFree.InstanceOf(ef))
                            .Append(ret));
                }
            } else if (localScope.VariableDecls.Length > 0) {
                return Ex.Block(
                    localScope.VariableDecls.SelectMany(v => v.decls)
                        .SelectNotNull(v => v.DeclaredParameter(tac)).ToArray(),
                    stmts(tac).AppendIfNonnull(endLabel));
            } else 
                return Ex.Block(stmts(tac).AppendIfNonnull(endLabel));
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

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{DebugPosition} block<{GetReturnTypeDescr(this)}>({{";
            foreach (var w in IDebugPrint.PrintArgs(Params, ";"))
                yield return w;
            yield return "})";
        }

    }

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
        

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var typ = ReturnType(SelectedOverload?.simplified!);
            bool allConst = true;
            var prms = new Ex[Params.Length];
            for (int ii = 0; ii < Params.Length; ++ii) {
                prms[ii] = Params[ii].Realize(tac);
                allConst &= prms[ii] is ConstantExpression;
            }
            if (allConst) {
                var carr = System.Array.CreateInstance(typ.GetElementType()!, Params.Length);
                for (int ii = 0; ii < Params.Length; ++ii)
                    carr.SetValue(((ConstantExpression)prms[ii]).Value, ii);
                return typ.MakeTypedTEx(Ex.Constant(carr, typ));
            }
            return typ.MakeTypedTEx(Ex.NewArrayInit(typ.GetElementType()!, prms));
        }

        public string Explain() {
            return $"{CompactPosition} {GetReturnTypeDescr(this)[..^2]}[{Params.Length}]";
        }
        
        public DocumentSymbol ToSymbolTree(string? descr = null) {
            if (ImplicitCast is { } cast) {
                var props = Params.Length == 1 ? "property" : "properties";
                return new DocumentSymbol(cast.ResultType.SimpRName(), $"({Params.Length} {props})", SymbolKind.Object, Position.ToRange(), FlattenParams(null));
            } else {
                var props = Params.Length == 1 ? "element" : "elements";
                return new DocumentSymbol((this as IAST).SelectedOverloadReturnType?.SimpRName() ?? "Array", 
                    $"({Params.Length} {props})", SymbolKind.Array, Position.ToRange(), FlattenParams(null));
            }
        }
        
        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{DebugPosition} {GetReturnTypeDescr(this)[..^2]}[{Params.Length}]{{";
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

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var typ = SelectedOverload!.Value.simplified.Resolve().LeftOrThrow;
            bool allConst = true;
            var prms = new Ex[Params.Length];
            for (int ii = 0; ii < Params.Length; ++ii) {
                prms[ii] = Params[ii].Realize(tac);
                allConst &= prms[ii] is ConstantExpression;
            }
            if (allConst) {
                var oprms = new object[Params.Length];
                for (int ii = 0; ii < Params.Length; ++ii) {
                    oprms[ii] = ((ConstantExpression)prms[ii]).Value;
                }
                return typ.MakeTypedTEx(Ex.Constant(typ.GetConstructors()[0].Invoke(oprms)));
            }
            return typ.MakeTypedTEx(Ex.New(typ.GetConstructors()[0], prms));
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
        
        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{DebugPosition} (";
            foreach (var w in IDebugPrint.PrintArgs(Params, ","))
                yield return w;
            yield return ")";
        }

    }
    
    
    public interface IAnyTypedValueAST { }

    //hardcoded values (number/typedvalue) may be tex-func or normal types depending on usage
    //eg. phase 10 <- 10 is Float/Int
    //    px 10 <- 10 is Func<TExArgCtx, TEx<float>>
    public record Number : AST, IAST, IAtomicTypeTree, IAnyTypedValueAST {
        private static readonly Known FloatType = new Known(typeof(float));
        private static readonly Known IntType = new Known(typeof(int));
        private readonly string content;
        public float Value { get; }
        public TypeDesignation[] PossibleTypes { get; private init; }
        public TypeDesignation? SelectedOverload { get; set; }
        public Number(PositionRange Position, LexicalScope EnclosingScope, string content) : base(Position,
            EnclosingScope) {
            this.content = content;
            this.Value = content == "inf" ? M.IntFloatMax : DMath.Parser.Float(content);
            if (content.EndsWith('.')) {
                PossibleTypes = new TypeDesignation[] { new Known(typeof(int)) };
            } else if (!content.Contains('.') && Math.Abs(Value - Math.Round(Value)) < 0.00001f) {
                PossibleTypes = new TypeDesignation[] { new Variable() { RestrictedTypes = new[] { FloatType, IntType } } };
            } else {
                PossibleTypes = new TypeDesignation[] { new Known(typeof(float)) };
            }
        }
        
        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var typ = ReturnType(SelectedOverload!);
            if (typ == typeof(float))
                return (TEx<float>)Ex.Constant(Value);
            if (typ == typeof(int))
                return (TEx<int>)Ex.Constant(Mathf.RoundToInt(Value));
            throw new Exception($"Undetermined numeric type {typ.RName()}");
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

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
            yield return new(Position, SemanticTokenTypes.Number);
        }

        public IEnumerable<PrintToken> DebugPrint() {
            yield return content;
        }
    }
    
    public record DefaultValue(PositionRange Position, LexicalScope EnclosingScope, Type? Typ = null, bool AsFunctionArg = false) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree, IAnyTypedValueAST {
        public string? TokenType { get; init; } = SemanticTokenTypes.Keyword;
        public string? Description { get; init; }
        public TypeDesignation[] PossibleTypes { get; } = {
            Typ != null ? TypeDesignation.FromType(Typ) : new Variable(),
        };
        public TypeDesignation? SelectedOverload { get; set; }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            if (AsFunctionArg)
                throw new ReflectionException(Position, "The `default` keyword can only be used as an argument to a script function.");
            var t = ReturnType(SelectedOverload!);
            if (t == typeof(void))
                return Ex.Empty();
            return t.MakeTypedTEx(Ex.Default(t));
        }

        public string Explain() => $"{CompactPosition} {Description ?? (AsFunctionArg ? "`default`" : "`null`")}";

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            if (AsFunctionArg && TokenType == null)
                return null!; //implicit default, hide me
            return new DocumentSymbol(Description ?? (AsFunctionArg ? "default" : "null"), 
                descr, SymbolKind.Constant, Position.ToRange());
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
            if (TokenType != null)
                yield return new(Position, TokenType);
        }

        public override IEnumerable<(IDebugAST tree, int? childIndex)>? NarrowestASTForPosition(PositionRange p) {
            if (AsFunctionArg && TokenType == null)
                return null;
            return base.NarrowestASTForPosition(p);
        }
    }

    public record TypedValue<T>(PositionRange Position, LexicalScope EnclosingScope, T Value, SymbolKind Kind) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree, IAnyTypedValueAST {
        public TypeDesignation[] PossibleTypes { get; } = {
            TypeDesignation.FromType(typeof(T)),
        };
        public TypeDesignation? SelectedOverload { get; set; }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            return (TEx<T>)Ex.Constant(Value);
        }

        public string Explain() => $"{CompactPosition} {typeof(T).SimpRName()} `{Value?.ToString()}`";

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol(
                Value switch {
                    null => "<null>",
                    "" => "<empty string>",
                    _ => Value.ToString()
                }, descr, Kind, Position.ToRange());
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
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

    public record InstanceFailure(ReflectionException Exc, LexicalScope EnclosingScope, IAST Inst) : 
        AST(Exc.Position, EnclosingScope, Inst), IAST, IMethodTypeTree<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; } = new[] {
            Dummy.Method(new Variable(), new Variable()), 
        };
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }

        
        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Dummy>.ImplicitParameterCast(Dummy overload, int index) =>
            false;

        public IEnumerable<ReflectionException> FirstPassExceptions() {
            var myExc = Exc;
            if (SelectedOverload?.simplified is { IsResolved : true } td)
                myExc = ReflectionException.Make(myExc.Position, myExc.HighlightedPosition,
                    myExc.MessageWithoutPosition +
                    $"\nThis expression should probably have a type of {td.Resolve().LeftOrThrow.RName()}.",
                    myExc.InnerException);
            return new[] { myExc };
        }

        public string Explain() => $"(ERROR) {Exc.Message}";

        public DocumentSymbol ToSymbolTree(string? descr = null) =>
            new("(Error)", null, SymbolKind.Null, Position.ToRange());
        
        public override TEx _RealizeWithoutCast(TExArgCtx tac) => throw new StaticException("Cannot realize a Failure");
    }
    
    public record Failure : AST, IAST, IAtomicTypeTree {
        public ReflectionException Exc { get; }
        //Allow typechecking Failure in order to get more debug information
        private Variable Var { get; init; }
        public TypeDesignation[] PossibleTypes { get; init; }
        public TypeDesignation? SelectedOverload { get; set; }
        
        /// <summary>
        /// Completion information for language server.
        /// <br/>Left: A list of methods for completion, or if null, all possible completions.
        /// <br/>Right: A failed member access for Type.Name.
        /// </summary>
        [PublicAPI]
        public Either<List<MethodSignature>?, (Type, string)>? Completions { get; init; }
        /// <summary>
        /// Completion information for language server.
        /// True if this error was due to a type that could not be parsed.
        /// </summary>
        [PublicAPI]
        public bool IsTypeCompletion { get; init; } = false;
        [PublicAPI]
        public ScriptImport? ImportedScript { get; init; }
        [PublicAPI]
        public bool IsImportedScriptMember { get; init; }
        
        public Failure(ReflectionException exc, LexicalScope scope, params IAST[] Params) :
            base(exc.Position, scope, Params) {
            Exc = exc;
            PossibleTypes = new TypeDesignation[] { Var = new Variable() };
        }
        
        public IEnumerable<ReflectionException> FirstPassExceptions() {
            var myExc = Exc;
            if (SelectedOverload is { IsResolved : true } td)
                myExc = ReflectionException.Make(myExc.Position, myExc.HighlightedPosition,
                    myExc.MessageWithoutPosition +
                    $"\nThis expression should probably have a type of {td.Resolve().LeftOrThrow.RName()}.",
                    myExc.InnerException);
            return new[] { myExc };
        }

        public string Explain() => $"(ERROR) {Exc.Message}";

        public DocumentSymbol ToSymbolTree(string? descr = null) =>
            new("(Error)", null, SymbolKind.Null, Position.ToRange());
        
        public override TEx _RealizeWithoutCast(TExArgCtx tac) => throw new StaticException("Cannot realize a Failure");
    }
}

}