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

    public Either<Unifier, TypeUnifyErr> WillSelectOverload(Reflector.InvokedMethod _, IImplicitTypeConverterInstance? cast, Unifier u) {
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
            return _RealizeWithoutCast(tac);
        var conv = ImplicitCast.Converter.Converter;
        if (conv is FixedImplicitTypeConv fixedConv) {
            return fixedConv.Convert((IAST)this, _RealizeWithoutCast, tac);
        } else if (conv is GenericTypeConv1 gtConv) {
            return gtConv.ConvertForType(ImplicitCast.Variables[0].Resolve(Unifier.Empty).LeftOrThrow, (IAST)this,
                _RealizeWithoutCast, tac);
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
        public string Name { get; }
        public VarDecl? Declaration { get; init; }
        public readonly List<(Type type, object value)>? AsEnumTypes;
        /// <inheritdoc cref="AST.Reference"/>
        public Reference(PositionRange Position, LexicalScope EnclosingScope, string Name, VarDecl? Declaration, List<(Type type, object value)>? asEnumTypes) : base(Position,
            EnclosingScope) {
            this.Name = Name;
            this.Declaration = Declaration;
            this.AsEnumTypes = asEnumTypes;
            PossibleTypes = Declaration != null ? 
                new[] { Declaration.TypeDesignation } : 
                AsEnumTypes!.Select(a => new Known(a.type) as TypeDesignation)
                    .ToArray();
        }
        
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
        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            if (TryGetAsEnum(out var eVal, out var t))
                return t.MakeTypedTEx(Ex.Constant(eVal));
            if (Declaration == null)
                throw new ReflectionException(Position, $"Reference {Name} is not a variable or enum");
            return Declaration.FinalizedType!.MakeTypedTEx(
                EnclosingScope.LocalOrParentVariable(tac, tac.EnvFrame, Declaration.Bound, out _));
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

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
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
        private Type? KnownType { get; }
        /// <inheritdoc cref="AST.WeakReference"/>
        public WeakReference(PositionRange Position, LexicalScope EnclosingScope, string Name, Type? knownType = null) : base(Position, EnclosingScope) {
            this.Name = Name;
            this.KnownType = knownType;
            PossibleTypes = new TypeDesignation[1] {
                knownType != null ?
                    new Known(knownType) :
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
                sc.Type = LexicalScopeType.MethodScope;
                sc.AutoDeclareVariables(methodPos, cfg.type);
                return sc;
            } else if (methods.All(m => m.Mi.GetAttribute<ExtendsInternalScopeAttribute>() != null)) {
                enclosing.AutoDeclareExtendedVariables(methodPos, 
                    methods[0].Mi.GetAttribute<ExtendsInternalScopeAttribute>()!.type);
            }
            return null;
        }

        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Reflector.InvokedMethod>.ImplicitParameterCast(Reflector.InvokedMethod overload, int index) {
            if (DMKScope.GetConverterForCompiledExpressionType(overload.Mi.Params[index].Type) is { } conv)
                return new(conv);
            return true;
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

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var (inv, typ) = SelectedOverload!.Value;
            var mi = inv.Mi;
            using var __ = LocalScope == null || mi.GetAttribute<CreatesInternalScopeAttribute>() == null ?
                null :
                new LexicalScope.ParsingScope(LocalScope);
            
            if (mi is IGenericMethodSignature gm) {
                //ok to use shared type as we are doing a check on empty unifier that is discarded
                if (mi.SharedType.Unify(typ, Unifier.Empty) is { IsLeft: true } unifier) {
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
            return RealizeMethod(this, mi, tac, (ii, tac) => Params[ii].Realize(tac), doScopeAttach: true);
        }


        public static TEx RealizeMethod(IAST ast, Reflector.IMethodSignature mi, TExArgCtx tac, Func<int, TExArgCtx, TEx> prmGetter, bool doScopeAttach = false) {
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
                    } else if(doScopeAttach && (Ex)prmGetter(ii, tac) is ConstantExpression ce) {
                        prms[ii] = ce.Value;
                        return ce;
                    } else throw new ReflectionException(ast.Position,
                        $"Argument #{ii + 1} to method {mi.Name} must be constant");
                }
                void ParseArgs(int? except) {
                    for (int ii = 0; ii < prms.Length; ++ii)
                        if (ii != except)
                            ParseArg(ii);
                }
                TEx Finalize() {
                    var raw = mi.Invoke(ast as MethodCall, prms);
                    if (raw is Func<TExArgCtx, TEx> f)
                        return f(tac);
                    else
                        return dataTyp.MakeTypedTEx(
                            raw as TEx ?? throw new StaticException($"Incorrect return from function {mi.Name}"));
                }
                //Handle the exceptional case where we assign to a dynamically scoped variable
                var writesTo = mi.GetAttribute<AssignsAttribute>()?.Indices ?? System.Array.Empty<int>();
                foreach (var writeable in writesTo) {
                    ParseArg(writeable);
                    if (ParseArg(writeable) is {} ex && Helpers.AssertWriteable(writeable, ex) is { } exc) {
                        if (writesTo.Length == 1 && ast.Params[writeable] is WeakReference wr) {
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
                var invoked = mi.InvokeExIfNotConstant(ast as MethodCall, prms);
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
            if (typ == typeof(StateMachine))
                return EnvFrameAttacher.attachScopeSM.Of(prm, Ex.Constant(LocalScope));
            AutoVars.GenCtx Autovars() => (AutoVars.GenCtx?)LocalScope!.AutoVars ??
                                          throw new Exception("GenCtx scope not configured with autovars");
            if (typ == typeof(GenCtxProperty[])) {
                if (prm is NewArrayExpression ne && prm.NodeType == ExpressionType.NewArrayInit) {
                    return Ex.NewArrayInit(typeof(GenCtxProperty), ne.Expressions.Append(
                        Ex.Constant(GenCtxProperty._AssignLexicalScope(LocalScope, Autovars()))));
                } else if (prm is ConstantExpression ce) {
                    return Ex.Constant(
                        EnvFrameAttacher.ExtendScopeProps((GenCtxProperty[])ce.Value, LocalScope, Autovars()));
                } else
                    return EnvFrameAttacher.extendScopeProps.Of(prm, Ex.Constant(LocalScope), Ex.Constant(Autovars()));
            }
            if (typ.IsGenericType && typ.GetGenericTypeDefinition() == typeof(GenCtxProperties<>)) {
                return EnvFrameAttacher.attachScopeProps.Specialize(prm.Type.GetGenericArguments())
                    .InvokeEx(this, prm, Ex.Constant(LocalScope), Ex.Constant(Autovars()));
            }
            return prm;
        }
        public static void AttachScope(object?[] prms, LexicalScope localScope, AutoVars? autoVars) {
            for (int ii = 0; ii < prms.Length; ++ii)
                switch (prms[ii]) {
                    case GenCtxProperties props:
                        props.Assign(localScope, (AutoVars.GenCtx?)autoVars ?? 
                                         throw new StaticException("Autovars not configured correctly"));
                        break;
                    case GenCtxProperty[] props:
                        prms[ii] = props.Append(
                            GenCtxProperty._AssignLexicalScope(localScope, (AutoVars.GenCtx?)autoVars ?? 
                                                                           throw new StaticException("Autovars not configured correctly"))).ToArray();
                        break;
                    case StateMachine sm:
                        sm.Scope = localScope;
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
                        (Ex)Params[1].Realize(new TExArgCtx()) is ConstantExpression { Value: PhaseProperties props }) {
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

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() =>
            base._ToSemanticTokens().Prepend(
                SelectedOverload?.method is { } m ?
                    SemanticToken.FromMethod(m.Mi, MethodPosition) :
                    new SemanticToken(MethodPosition, SemanticTokenTypes.Method));

        public IEnumerable<PrintToken> DebugPrint() {
            yield return $"{CompactPosition} {(SelectedOverload?.method ?? Methods[0]).TypeEnclosedName}(";
            foreach (var w in IDebugPrint.PrintArgs(Params))
                yield return w;
            yield return ")";
        }
    }


    public record ScriptFunctionCall(PositionRange Position, PositionRange MethodPosition,
        LexicalScope EnclosingScope, ScriptFnDecl Definition, params IAST[] Params) : AST(Position, EnclosingScope, Params), IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; } = new[] { Definition.CallType };
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        
        public override TEx _RealizeWithoutCast(TExArgCtx tac) => 
            ReturnType(SelectedOverload?.simplified!).MakeTypedTEx(
                EnclosingScope.LocalOrParentFunction(tac.EnvFrame, Definition, Params.Select(p => (Ex)p.Realize(tac)))
            );

        public string Explain() => $"{CompactPosition} {Definition.AsSignature}";

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol(Definition.Name, Definition.TypeOnlySignature, SymbolKind.Method, Position.ToRange(),
                FlattenParams((p, i) => p.ToSymbolTree($"({Definition.Args[i]})")));
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() =>
            base._ToSemanticTokens().Prepend(new(MethodPosition, SemanticTokenTypes.Function));
    }
    
    public record ScriptFunctionDef(PositionRange Position, string Name, LexicalScope EnclosingScope, ScriptFnDecl Definition, Block Body) : AST(Position, EnclosingScope, Body), IMethodAST<Dummy> {
        public IReadOnlyList<Dummy> Overloads { get; } = 
            new[] { Dummy.Method(new Known(typeof(void)), Body.Overloads[0].Last) };
        public IReadOnlyList<ITypeTree> Arguments => Params;
        public List<Dummy>? RealizableOverloads { get; set; }
        public (Dummy method, Dummy simplified)? SelectedOverload { get; set; }
        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            return Ex.Empty();
        }

        public (MethodInfo invoke, object func) CompileFunc() {
            var args = Definition.Args;
            var fTypes = new Type[args.Length + 2];
            fTypes[0] = typeof(EnvFrame);
            for (int ii = 0; ii < args.Length; ++ii)
                fTypes[ii + 1] = args[ii].FinalizedType ?? throw new ReflectionException(Position,
                    $"Script function argument {args[ii].Name}'s type is not finalized");
            fTypes[^1] = Body.LocalScope!.Return!.FinalizedType ??
                         throw new ReflectionException(Position, $"Script function return type is not finalized");
            var fnType = ReflectionUtils.GetFuncType(fTypes.Length).MakeGenericType(fTypes);
            Func<TExArgCtx, TEx> body = tac => Ex.Block(
                Body.Realize(tac),
                Ex.Label(Body.LocalScope!.Return!.Label, Ex.Default(fTypes[^1]))
            );
            var argsWithEf = args.Prepend(new DelegateArg<EnvFrame>("callerEf") as IDelegateArg).ToArray();
            var func = CompilerHelpers.CompileDelegateMeth.Specialize(fnType).Invoke(null, body, argsWithEf)!;
            return (func.GetType().GetMethod("Invoke"), func);
        }

        public string Explain() => Definition.AsSignature;

        public DocumentSymbol ToSymbolTree(string? descr = null) =>
            new($"Function {Name}", Definition.TypeOnlySignature, SymbolKind.Function, Position.ToRange(),
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

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            if (Value is null)
                return Ex.Return(EnclosingScope.NearestReturn!.Label);
            return ReturnType(SelectedOverload?.simplified!).MakeTypedTEx(
                Ex.Return(EnclosingScope.NearestReturn!.Label, Value.Realize(tac)));
        }

        public override void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
            var prevScope = EnclosingScope;
            base.ReplaceScope(prev, inserted);
            if (EnclosingScope != prevScope) {
                if (EnclosingScope.NearestReturn == null)
                    throw new ReflectionException(Position, "This return statement isn't contained within a function definition.");
                Overloads = SetOverloads(EnclosingScope, Value);
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

        public string Explain() => Value == null ? "return" : Value.Explain();

        public DocumentSymbol ToSymbolTree(string? descr = null) =>
            Value?.ToSymbolTree(descr) ?? new("return", "(void)", SymbolKind.Null, Position.ToRange());

    }
    
    
    
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
            } else
                Overloads = new[] {
                    Dummy.Method(new Known(typeof(void)), Params.Select((_, i) => 
                        i == 0 ? new Known(typeof(bool)) : new Variable() as TypeDesignation).ToArray()),
                };
        }

        Either<IImplicitTypeConverter, bool> IMethodTypeTree<Dummy>.ImplicitParameterCast(Dummy overload, int index) => false;

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
            yield return $"{CompactPosition} (";
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
    /// A continue statement in a for/while loop.
    /// </summary>
    public record Continue(PositionRange Position, LexicalScope EnclosingScope) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        public TypeDesignation[] PossibleTypes { get; } = { new Known(typeof(void)) };
        public TypeDesignation? SelectedOverload { get; set; }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            return Ex.Continue(EnclosingScope.NearestContinue!);
        }

        public override void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
            base.ReplaceScope(prev, inserted);
            if (EnclosingScope.NearestContinue == null)
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
            return Ex.Continue(EnclosingScope.NearestBreak!);
        }

        public override void ReplaceScope(LexicalScope prev, LexicalScope inserted) {
            base.ReplaceScope(prev, inserted);
            if (EnclosingScope.NearestBreak == null)
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
        public Block(PositionRange Position, LexicalScope EnclosingScope, LexicalScope localScope, TypeDesignation? retType, params IAST[] Params) : base(Position,
            EnclosingScope, Params) {
            this.LocalScope = localScope;
            Overloads = MakeOverloads(retType, Params);
        }

        private static IReadOnlyList<Dummy> MakeOverloads(TypeDesignation? retType, IAST[] prms) {
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
                        meth.ImplicitCast = DMKScope.AttachEFtoSMImplicit.Realize(Unifier.Empty);
                    else if (typ == typeof(AsyncPattern))
                        meth.ImplicitCast = DMKScope.AttachEFtoAPImplicit.Realize(Unifier.Empty);
                    else if (typ == typeof(SyncPattern))
                        meth.ImplicitCast = DMKScope.AttachEFtoSPImplicit.Realize(Unifier.Empty);
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
            IEnumerable<Ex> Stmts() {
                return Params.Select<IAST, Ex>((p, i) => {
                    try {
                        return p.Realize(tac);
                    } catch (Exception e) {
                        if (e is ReflectionException)
                            throw;
                        else
                            throw new ReflectionException(p.Position, "Couldn't invoke this code.", e);
                    }
                });
            }
            if (LocalScope!.UseEF) {
                var parentEf = tac.MaybeGetByType<EnvFrame>(out _) ?? Ex.Constant(null, typeof(EnvFrame));
                var ef = Ex.Parameter(typeof(EnvFrame), "ef");
                tac = tac.MaybeGetByType<EnvFrame>(out _) is { } ?  
                    tac.MakeCopyForType<EnvFrame>(ef) :
                    tac.Append("rootEnvFrame", ef);
                var makeEf = ef.Is(EnvFrame.exCreate.Of(Ex.Constant(LocalScope), parentEf));
                
                return typ.MakeTypedTEx(Ex.Block(new[] { ef }, Stmts().Prepend(makeEf)));
            } else if (LocalScope.VariableDecls.Length > 0) {
                return typ.MakeTypedTEx(Ex.Block(
                    LocalScope.VariableDecls.SelectMany(v => v.decls)
                        .SelectNotNull(v => v.DeclaredParameter(tac)).ToArray(),
                    Stmts()));
            } else 
                return typ.MakeTypedTEx(Ex.Block(Stmts()));
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
            yield return $"{CompactPosition} block<{GetReturnTypeDescr(this)}>({{";
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
            if (!content.Contains('.') && Math.Abs(Value - Math.Round(Value)) < 0.00001f) {
                PossibleTypes = new TypeDesignation[] { new Variable() { RestrictedTypes = new[] { FloatType, IntType } } };
            } else {
                PossibleTypes = new TypeDesignation[] { new Known(typeof(float)) };
            }
        }

        private static Variable MakeVariableType(string content, float value) => new Variable() {
            RestrictedTypes = (!content.Contains('.') && Math.Abs(value - Math.Round(value)) < 0.00001f) ?
                new[]{FloatType,IntType} :
                new[]{FloatType}
        };
        
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

    public record DefaultValue(PositionRange Position, LexicalScope EnclosingScope, Type? Typ = null) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        public TypeDesignation[] PossibleTypes { get; } = {
            Typ != null ? new Known(Typ) : new Variable(),
        };
        public TypeDesignation? SelectedOverload { get; set; }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            var t = ReturnType(SelectedOverload!);
            if (t == typeof(void))
                return Ex.Empty();
            return t.MakeTypedTEx(Ex.Default(t));
        }

        public string Explain() => $"{CompactPosition} `null`";

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol("null", descr, SymbolKind.Constant, Position.ToRange());
        }

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() {
            yield return new(Position, SemanticTokenTypes.Keyword);
        }
        
    }

    public record TypedValue<T>(PositionRange Position, LexicalScope EnclosingScope, T Value, SymbolKind Kind) : AST(Position,
        EnclosingScope), IAST, IAtomicTypeTree {
        public TypeDesignation[] PossibleTypes { get; } = {
            TypeDesignation.FromType(typeof(T)),
        };
        public TypeDesignation? SelectedOverload { get; set; }

        public override TEx _RealizeWithoutCast(TExArgCtx tac) {
            return (TEx<T>)Ex.Constant(Value);
        }

        public string Explain() => $"{CompactPosition} {typeof(T).SimpRName()} `{Value?.ToString()}`";

        public DocumentSymbol ToSymbolTree(string? descr = null) {
            return new DocumentSymbol(Value?.ToString() ?? "null", descr, Kind, Position.ToRange());
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

        protected override IEnumerable<SemanticToken> _ToSemanticTokens() => throw Exc;

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
        
        public override TEx _RealizeWithoutCast(TExArgCtx tac) => throw new StaticException("Cannot realize a Failure");
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