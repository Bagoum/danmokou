using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection2;
using Danmokou.SM;
using JetBrains.Annotations;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using UnityEngine;
using static Danmokou.Reflection.Reflector;
using Parser = Danmokou.DMath.Parser;

namespace Danmokou.Reflection {
/// <summary>
/// A lightweight set of instructions for compiling an object
///  from 'code'.
/// </summary>
public interface IAST : IDebugAST {
    /// <summary>
    /// Return all nonfatal errors in the parse tree.
    /// </summary>
    List<AST.NestedFailure> Errors { get; }
    /// <summary>
    /// Returns <see cref="Errors"/> converted into <see cref="ReflectionException"/>.
    /// </summary>
    IEnumerable<ReflectionException> Exceptions => Errors.SelectMany(e => e.AsExceptions());
    
    /// <summary>
    /// Returns true iff the AST has problems that prevent it from being compiled.
    /// </summary>
    bool IsUnsound { get; }

    /// <summary>
    /// Get the type of the object that would be generated from this AST.
    /// </summary>
    Type ResultType => throw new NotImplementedException();

    LexicalScope LexicalScope { get; }
    
    /// <summary>
    /// Attach a lexical scope to the AST, deriving it and declaring variables where appropriate.
    /// <br/>This is implemented for compatibility with BDSL2 engine changes.
    /// </summary>
    void AttachLexicalScope(LexicalScope scope);
    
    /// <summary>
    /// Generate the object.
    /// </summary>
    object? EvaluateObject() => throw new NotImplementedException();

    IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx);
}

/// <summary>
/// <see cref="IAST"/> restricted by the return type of the object.
/// </summary>
public interface IAST<out T> : IAST {
    T Evaluate();
    object? IAST.EvaluateObject() => Evaluate();
    Type IAST.ResultType => typeof(T);
}

/// <summary>
/// Cast an untyped AST to a specific type.
/// </summary>
public record ASTRuntimeCast<T>(IAST Source) : IAST<T> {
    public PositionRange Position => Source.Position;
    public List<AST.NestedFailure> Errors => Source.Errors;
    public bool IsUnsound => Source.IsUnsound;

    public LexicalScope LexicalScope => Source.LexicalScope;

    public T Evaluate() => Source.EvaluateObject() is T result ?
        result :
        throw new StaticException("Runtime AST cast failed");
    public void AttachLexicalScope(LexicalScope scope) => Source.AttachLexicalScope(scope);

    public IEnumerable<IDebugAST> Children => new[] { Source };

    public IEnumerable<(IDebugAST, int?)>? NarrowestASTForPosition(PositionRange p)
        => Source.NarrowestASTForPosition(p);

    public string Explain() => $"{Position.Print(true)} Cast to type {typeof(T).SimpRName()}";
    public DocumentSymbol ToSymbolTree(string? descr = null) => Source.ToSymbolTree(descr);
    public IEnumerable<SemanticToken> ToSemanticTokens() => Source.ToSemanticTokens();

    public IEnumerable<PrintToken> DebugPrint() => Source.DebugPrint();
    public IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) => Source.WarnUsage(ctx);
}

/// <summary>
/// Map over the result of an AST.
/// </summary>
public record ASTFmap<T, U>(Func<T, U> Map, IAST<T> Source) : IAST<U> {
    public PositionRange Position => Source.Position;
    public List<AST.NestedFailure> Errors => Source.Errors;
    public bool IsUnsound => Source.IsUnsound;

    public LexicalScope LexicalScope => Source.LexicalScope;
    public U Evaluate() => Map(Source.Evaluate());
    
    public void AttachLexicalScope(LexicalScope scope) => Source.AttachLexicalScope(scope);
    
    public IEnumerable<IDebugAST> Children => new[] { Source };

    public IEnumerable<(IDebugAST, int?)>? NarrowestASTForPosition(PositionRange p)
        => Source.NarrowestASTForPosition(p);

    public string Explain() => $"{Position.Print(true)} " +
                               $"Map from type {typeof(T).RName()} to type {typeof(U).RName()}";

    public DocumentSymbol ToSymbolTree(string? descr = null) => Source.ToSymbolTree(descr);
    public IEnumerable<SemanticToken> ToSemanticTokens() => Source.ToSemanticTokens();

    public IEnumerable<PrintToken> DebugPrint() => Source.DebugPrint().Prepend($"({typeof(U).SimpRName()})");
    public IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) => Source.WarnUsage(ctx);
}

public abstract record AST(PositionRange Position, params IAST[] Params) : IAST {
    public virtual IEnumerable<IDebugAST> Children => Params;
    public ReflectDiagnostic[] Diagnostics { get; init; } = Array.Empty<ReflectDiagnostic>();

    public LexicalScope LexicalScope { get; protected set; } = null!;
    public virtual List<NestedFailure> Errors {
        get {
            var errs = new List<NestedFailure>();
            int maxIndex = -1;
            foreach (var err in Params
                        //We order by param position, not by error position, only to deal with cases
                        // where params are out of order (such as implicit arguments).
                        .OrderBy(p => p.Position.Start.Index)
                         .SelectMany(p => p.Errors)) {
                if (err.Head.Position.Start.Index >= maxIndex) {
                    errs.Add(err);
                    maxIndex = err.Head.Position.End.Index;
                }
            }
            return errs;
        }
    }
    public virtual bool IsUnsound => Params.Any(p => p.IsUnsound);

    public virtual IEnumerable<(IDebugAST, int?)>? NarrowestASTForPosition(PositionRange p) {
        if (p.Start.Index < Position.Start.Index || p.End.Index > Position.End.Index) return null;
        for (int ii = 0; ii < Params.Length; ++ii) {
            var arg = Params[ii];
            if (arg.NarrowestASTForPosition(p) is { } results)
                return results.Append((this, ii));
        }
        return new (IDebugAST, int?)[] { (this, null) };
    }

    public virtual void AttachLexicalScope(LexicalScope scope) {
        LexicalScope = scope;
        foreach (var c in Params)
            c.AttachLexicalScope(scope);
    }
    public abstract string Explain();
    public abstract DocumentSymbol ToSymbolTree(string? descr = null);
    public abstract IEnumerable<SemanticToken> ToSemanticTokens();

    public virtual IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) =>
        Diagnostics.Concat(Params.SelectMany(p => p.WarnUsage(ctx)));
    public virtual IEnumerable<PrintToken> DebugPrint() => new PrintToken[] { Explain() };

    string CompactPosition => Position.Print(true);
    
    //By default, flatten List<SM>, SM[], AsyncPattern[], SyncPattern[]
    //This drastically improves readability as these are often deeply nested
    private static readonly Type[] flattenArrayTypes =
        { typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern) };
    protected IEnumerable<DocumentSymbol> FlattenParams(Func<IAST, int, DocumentSymbol> mapper) {
        bool DefaultFlatten(IAST ast) =>
            ast is SequenceList<StateMachine> ||
            ast is SequenceArray seq && flattenArrayTypes.Contains(seq.ElementType);
        foreach (var (p, sym) in Params.Select((p, i) => (p, mapper(p, i)))) {
            if (DefaultFlatten(p))
                foreach (var s in sym.Children ?? Array.Empty<DocumentSymbol>())
                    yield return s;
            else
                yield return sym;
        }
    }

    protected IEnumerable<PrintToken> DebugPrintMethod(InvokedMethod Method) {
        yield return $"{CompactPosition} {Method.TypeEnclosedName}(";
        if (Params.Length > 1) {
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            for (int ii = 0; ii < Params.Length; ++ii) {
                foreach (var x in Params[ii].DebugPrint())
                    yield return x;
                yield return ", ";
                yield return PrintToken.newline;
            }
            yield return PrintToken.undoNewline;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
        } else if (Params.Length == 1) {
            foreach (var x in Params[0].DebugPrint())
                yield return x;
        }
        yield return ")";
    }

    protected IEnumerable<PrintToken> DebugPrintArray<T>(Type Type, IList<T> Params) where T : IAST {
        yield return $"{CompactPosition} {Type.SimpRName()}[{Params.Count}] {{";
        if (Params.Count > 1) {
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var p in Params) {
                foreach (var x in p.DebugPrint())
                    yield return x;
                yield return ", ";
                yield return PrintToken.newline;
            }
            yield return PrintToken.undoNewline;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
        } else if (Params.Count == 1) {
            foreach (var x in Params[0].DebugPrint())
                yield return x;
        }
        yield return "}";
    }

    public abstract record BaseMethodInvoke(PositionRange Position, PositionRange MethodPosition,
        InvokedMethod BaseMethod, params IAST[] Params) : AST(Position, Params), IAST {
        /// <summary>
        /// Whether the argument list is provided in parentheses (ie. as `func(arg1, arg2)` as opposed to `func arg1 arg2`).
        /// </summary>
        public bool Parenthesized { get; init; } = false;
        protected LexicalScope? LocalScope { get; private set; }

        public override void AttachLexicalScope(LexicalScope scope) {
            if (BaseMethod.Mi.GetAttribute<CreatesInternalScopeAttribute>() is { } cis) {
                LocalScope = scope = cis.dynamic ? new DynamicLexicalScope(scope) : LexicalScope.Derive(scope);
                scope.AutoDeclareVariables(MethodPosition, cis.type);
                scope.Type = LexicalScopeType.MethodScope;
            } else if (BaseMethod.Mi.GetAttribute<ExtendsInternalScopeAttribute>() is { } eis) {
                scope.AutoDeclareExtendedVariables(MethodPosition, eis.type, 
                    //bindItr support- BDSL1 only
                    (Params.Try(0) as Preconstructed<object>)?.Value as string);
            } else if (BaseMethod.Mi.GetAttribute<ExpressionBoundaryAttribute>() is { }) {
                LocalScope = scope = LexicalScope.Derive(scope);
                scope.Type = LexicalScopeType.ExpressionBlock; //expressionEF is BDSL2 only
            }
            base.AttachLexicalScope(scope);
        }
        
        public override string Explain() => $"{CompactPosition} {BaseMethod.Mi.AsSignature}";
        public override DocumentSymbol ToSymbolTree(string? descr = null) {
            if (BaseMethod.Mi.IsCtor && BaseMethod.Mi.ReturnType == typeof(PhaseSM) && !Params[1].IsUnsound && Params[1].EvaluateObject() is PhaseProperties props) {
                return new($"{props.phaseType?.ToString() ?? "Phase"}", props.cardTitle?.Value ?? "",
                    SymbolKind.Method, Position.ToRange(), FlattenParams((p, i) => p.ToSymbolTree($"({BaseMethod.Params[i].Name})")));
            }
            return BaseMethod.Mi.IsFallthrough ? 
                    Params[0].ToSymbolTree(descr) :
                    new(BaseMethod.Name, BaseMethod.Mi.TypeOnlySignature, SymbolKind.Method, Position.ToRange(),
                        FlattenParams((p, i) => p.ToSymbolTree($"({BaseMethod.Params[i].Name})")));
        }

        public override IEnumerable<SemanticToken> ToSemanticTokens() =>
            Params.SelectMany(p => p.ToSemanticTokens()).Prepend(SemanticToken.FromMethod(BaseMethod.Mi, MethodPosition));

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintMethod(BaseMethod);

        public override IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) {
            if (ctx.Props.warnPrefix && BaseMethod.Mi.GetAttribute<WarnOnStrictAttribute>() is {} wa 
                                     && (int)ctx.Props.strict >= wa.strictness) {
                yield return new ReflectDiagnostic.Warning(Position,
                    $"The method \"{BaseMethod.TypeEnclosedName}\" is not permitted for use in a script with strictness {ctx.Props.strict}. You might accidentally be using the prefix version of an infix function.");
            }
            foreach (var d in base.WarnUsage(ctx))
                yield return d;
        }
    }

    /// <summary>
    /// An AST that creates an object through method invocation.
    /// </summary>
    /// <param name="Position">Position of the entire method call, including all arguments (ie. all of `MethodName(arg1, arg2)`)</param>
    /// <param name="MethodPosition">Position of the method name alone (ie. just `MethodName`)</param>
    /// <param name="Method">Method signature</param>
    /// <param name="Params">Arguments to the method</param>
    public record MethodInvoke(PositionRange Position, PositionRange MethodPosition, InvokedMethod Method, params IAST[] Params) : BaseMethodInvoke(Position, MethodPosition, Method, Params), IAST {
        public enum InvokeType {
            Normal,
            SM,
            Fallthrough,
            Compiler,
            PostAggregate
        }

        public InvokeType Type { get; init; } = InvokeType.Normal;

        public MethodInvoke(IAST nested, InvokedMethod sig) : this(nested.Position, new PositionRange(nested.Position.Start, nested.Position.Start), sig, nested) { }

        public Type ResultType => Method.Mi.ReturnType;

        private static readonly Type gcxPropsType = typeof(GenCtxProperties);
        private static readonly Type gcxPropArrType = typeof(GenCtxProperty[]);
        public object? EvaluateObject() {
            var prms = new object?[Params.Length];
            using var _ = LocalScope == null ?
                null :
                new LexicalScope.ParsingScope(LocalScope);
            for (int ii = 0; ii < prms.Length; ++ii) {
                prms[ii] = Params[ii].EvaluateObject();
            }
            if (LocalScope != null)
                for (int ii = 0; ii < prms.Length; ++ii)
                    prms[ii] = Reflection2.AST.MethodCall.AttachScope(prms[ii], LocalScope);
            try {
                return Method.Mi.Invoke(null, prms);
            } catch (Exception e) {
                throw new ReflectionException(Position, $"Failed to execute method {Method.Name}.", e);
            }
        }
    }

    /// <summary>
    /// An AST that creates an object through method invocation.
    /// <br/>The return type of the method is specified.
    /// </summary>
    public record MethodInvoke<T>(PositionRange Position, PositionRange MethodPosition, InvokedMethod Method, params IAST[] Params) : MethodInvoke(
        Position, MethodPosition, Method, Params), IAST<T> {
        public T Evaluate() => EvaluateObject() switch {
            T t => t,
            var x => throw new StaticException(
                $"AST method invocation for {BaseMethod.TypeEnclosedName} returned object of type {x?.GetType()} (expected {typeof(T)}")
        };
    }

    /// <summary>
    /// An AST that creates an object by funcifying a method returning R over type T.
    /// <br/>ie. For a recorded function R member(A, B, C...), given parameters of type [F(A), F(B), F(C)] (funcified on T, eg. [T->A, T->B, T->C]),
    /// this AST constructs a function T->R that uses T to defuncify the parameters and pass them to member.
    /// </summary>
    public record FuncedMethodInvoke<T, R>
        (PositionRange Position, PositionRange MethodPosition, LiftedInvokedMethod<T, R> Method, IAST[] Params) : BaseMethodInvoke(Position, MethodPosition, Method, Params),
            IAST<Func<T, R>> {
        public Func<T, R> Evaluate() {
            var fprms = new object?[Params.Length];
            for (int ii = 0; ii < fprms.Length; ++ii)
                fprms[ii] = Params[ii].EvaluateObject();
            return Method.TypedFMi.InvokeMiFunced(null, fprms);
        }
    }

    /// <summary>
    /// An AST with a precomputed value.
    /// </summary>
    public record Preconstructed<T>(PositionRange Position, T Value, string? Display = null) : AST(Position), IAST<T> {
        public T Evaluate() => Value;
        private Type MyType => Value?.GetType() ?? typeof(T);
        public string Description => Display ?? Value?.ToString() ?? "null";
        private SymbolKind SymbolType {
            get {
                if (Display != null && Display[0] == Parser.SM_REF_KEY_C)
                    return SymbolKind.Object;
                var t = MyType;
                if (t.IsEnum) return SymbolKind.Enum;
                if (t.IsArray) return SymbolKind.Array;
                //if (t == typeof(float) || t == typeof(int)) return SymbolKind.Number;
                //if (t == typeof(string)) return SymbolKind.String;
                //if (t == typeof(bool)) return SymbolKind.Boolean;
                return SymbolKind.Constant;
            }
        }
        private string SemanticTokenType {
            get {
                var t = MyType;
                if (t.IsEnum) return SemanticTokenTypes.EnumMember;
                if (t == typeof(float) || t == typeof(int) || t == typeof(V2RV2) || t == typeof(CRect) || t == typeof(CCircle)) 
                    return SemanticTokenTypes.Number;
                if (t == typeof(string) || t.IsSubclassOf(typeof(LString))) 
                    return SemanticTokenTypes.String;
                return SemanticTokenTypes.Variable;
            }
        }

        public override string Explain() =>
            $"{CompactPosition} {MyType.SimpRName()} {Description}";

        public override DocumentSymbol ToSymbolTree(string? descr = null) =>
            new DocumentSymbol(Description, MyType.SimpRName(), SymbolType, Position.ToRange());

        public override IEnumerable<SemanticToken> ToSemanticTokens() => 
            Display != null && Display[0] == Parser.SM_REF_KEY_C ?
            new[] {
                new SemanticToken(new(Position.Start, new(Position.Start.Index + 1, Position.Start.Line, Position.Start.IndexOfLineStart)), SemanticTokenTypes.Operator),
                new SemanticToken(new(new(Position.Start.Index + 1, Position.Start.Line, Position.Start.IndexOfLineStart), Position.End), SemanticTokenTypes.Parameter)
            } :
            new[] {
                new SemanticToken(Position, SemanticTokenType)
            };
    }

    /// <summary>
    /// An AST that looks up a method and returns it as a lambda.
    /// <br/>eg. Calling this with "EOutSine" will return a Func&lt;tfloat, tfloat&gt;, derived
    ///  directly from <see cref="ExMEasers.EOutSine"/>.
    /// </summary>
    public record MethodLookup : AST, IAST {
        private bool lifted;
        /// <summary>
        /// Type of the parameter, eg. Func&lt;tfloat, tfloat&gt;.
        /// <br/>The return type of the func type is the return type of the linked <see cref="Method"/>.
        /// </summary>
        public Type FuncType { get; }

        public Type ResultType => FuncType;
        /// <summary>
        /// When <see cref="FuncType"/>=Func&lt;A, B, C&gt;, this is [A, B, C].
        /// </summary>
        public Type[] FuncAllTypes { get; }
        /// <summary>
        /// When <see cref="FuncType"/>=Func&lt;A, B, C&gt;, this is C.
        /// </summary>
        public Type FuncRetType { get; }
        public MethodSignature Method { get; }

        public MethodLookup(PositionRange p, Type funcType, string methodName) : base(p) {
            this.FuncType = funcType;
            funcType.IsTExOrTExFuncType(out var simpType);
            //The type should be Func<A,...R>, where A...R are types like float (not TEx<float>)
            FuncAllTypes = simpType.GenericTypeArguments;
            FuncRetType = FuncAllTypes[^1];
            var typeDesig = new TypeDesignation.Dummy(TypeDesignation.Dummy.METHOD_KEY,
                FuncAllTypes.Select(TypeDesignation.FromType).ToArray());
            //Look for methods returning type R

            var methods = DMKScope.Singleton.FindStaticMethodDeclaration(methodName)?
                .Where(m => m.SharedType.Unify(typeDesig, Unifier.Empty).IsLeft)
                .ToList();
            if (methods == null || methods.Count == 0)
                throw new ReflectionException(p,
                    $"Method lookup for type {FuncType.SimpRName()} failed because no there is no function named " +
                    $"\"{methodName}\" matching this type signature.");
            if (methods.Count > 1)
                throw new ReflectionException(p,
                    $"Method lookup for type {FuncType.SimpRName()} failed because no there is more than one method named " +
                    $"\"{methodName}\" matching this type signature.");
            Method = methods[0];
        }

        public object? EvaluateObject() {
            var fn = Method.AsFunc();
            if (ResultType.IsTExType(out var inner))
                return inner.MakeTypedTEx(Expression.Constant(fn));
            else if (ResultType.IsTExFuncType(out inner))
                return inner.MakeTypedLambda(_ => Expression.Constant(fn));
            return fn;
        }

        public override string Explain() => $"{CompactPosition} {Method.Name}";

        public override DocumentSymbol ToSymbolTree(string? descr = null) =>
            new DocumentSymbol(Method.Name, FuncType.SimpRName(), SymbolKind.Function, Position.ToRange());

        public override IEnumerable<SemanticToken> ToSemanticTokens() =>
            new[] {
                SemanticToken.FromMethod(Method, Position, SemanticTokenTypes.Function)
            };
    }

    /// <summary>
    /// An AST that generates a state machine by reading a file.
    /// </summary>
    public record SMFromFile(PositionRange CallPosition, PositionRange FilePosition, string Filename) : AST(CallPosition.Merge(FilePosition)), IAST<StateMachine?> {
        public StateMachine? Evaluate() => StateMachineManager.FromName(Filename);

        public override string Explain() => $"{CompactPosition} StateMachine from file: {Filename}";

        public override DocumentSymbol ToSymbolTree(string? descr = null) =>
            new DocumentSymbol($"SMFromFile({Filename})", descr, SymbolKind.Object, Position.ToRange());
        public override IEnumerable<SemanticToken> ToSemanticTokens() =>
            new[] {
                new SemanticToken(CallPosition, SemanticTokenTypes.Keyword),
                new SemanticToken(FilePosition, SemanticTokenTypes.String)
            };
    }


    public abstract record BaseSequence(PositionRange Position, IAST[] Params) : AST(Position, Params) { }
    
    /// <summary>
    /// An AST that generates a compile-time strongly typed list of objects.
    /// </summary>
    public record SequenceList<T>(PositionRange Position, IList<IAST<T>> Parts) : BaseSequence(Position,
        Parts.Cast<IAST>().ToArray()), IAST<List<T>> {
        public List<T> Evaluate() {
            var l = new List<T>(Parts.Count);
            for (int ii = 0; ii < Parts.Count; ++ii)
                l.Add(Parts[ii].Evaluate());
            return l;
        }

        public override string Explain() => $"{CompactPosition} List<{typeof(T).SimpRName()}>";

        public override DocumentSymbol ToSymbolTree(string? descr = null)
            => new($"List<{typeof(T).SimpRName()}>", descr, SymbolKind.Array, 
                Position.ToRange(), FlattenParams((p, i) => p.ToSymbolTree()));

        public override IEnumerable<SemanticToken> ToSemanticTokens() => 
            Parts.SelectMany(p => p.ToSemanticTokens());

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintArray(typeof(T), Parts);
    }

    /// <summary>
    /// An AST that generates an array of objects.
    /// </summary>
    public record SequenceArray(PositionRange Position, Type ElementType, IAST[] Params) : BaseSequence(Position, Params), IAST<Array> {
        public Array Evaluate() {
            var array = Array.CreateInstance(ElementType, Params.Length);
            for (int ii = 0; ii < Params.Length; ++ii)
                array.SetValue(Params[ii].EvaluateObject(), ii);
            return array;
        }

        public Type ResultType => ElementType.MakeArrayType();
        public override string Explain() => $"{CompactPosition} {ElementType.SimpRName()}[]";
        public override DocumentSymbol ToSymbolTree(string? descr = null)
            => new($"{ElementType.SimpRName()}[]", descr, SymbolKind.Array, 
                Position.ToRange(), FlattenParams((p, i) => p.ToSymbolTree()));
        
        public override IEnumerable<SemanticToken> ToSemanticTokens() => 
            Params.SelectMany(p => p.ToSemanticTokens());

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintArray(ElementType, Params);
    }

    /// <summary>
    /// AST for <see cref="GCRule{T}"/>
    /// </summary>
    public record GCRule<T>(PositionRange RefPosition, PositionRange OpPosition, ExType Type, 
        ReferenceMember Reference, GCOperator Operator,
        IAST<GCXF<T>> Rule) : AST(RefPosition.Merge(OpPosition).Merge(Rule.Position), Rule), IAST<Danmokou.Danmaku.GCRule<T>> {
        public Danmokou.Danmaku.GCRule<T> Evaluate() => 
            new(Type, Reference, Operator, Rule.Evaluate());
        
        public override void AttachLexicalScope(LexicalScope scope) {
            if (scope.FindVariable(Reference.var) == null) {
                scope.Declare(new VarDecl(RefPosition, false, Type.AsType(), Reference.var));
            }
            base.AttachLexicalScope(scope);
        }
        
        public override IEnumerable<SemanticToken> ToSemanticTokens() => new[] {
            new SemanticToken(RefPosition, SemanticTokenTypes.Parameter),
            new SemanticToken(OpPosition, SemanticTokenTypes.Operator)
        }.Concat(Rule.ToSemanticTokens());

        public override string Explain() => $"{CompactPosition} GCRule<{typeof(T).SimpRName()}>";
        public override DocumentSymbol ToSymbolTree(string? descr = null)
            => new($"GCRule<{typeof(T).SimpRName()}>", null, SymbolKind.Struct, 
                Position.ToRange(), FlattenParams((p, i) => p.ToSymbolTree()));

        public override IEnumerable<PrintToken> DebugPrint() =>
            Rule.DebugPrint().Prepend($"{CompactPosition} {Reference} {Operator}{Type} ");
    }

    /// <summary>
    /// AST for <see cref="ReflectEx.Alias"/>
    /// </summary>
    public record Alias(PositionRange DeclPos, PositionRange AliasPos, Type Type, string Name, IAST Content) : AST(DeclPos.Merge(AliasPos).Merge(Content.Position), Content),
        IAST<ReflectEx.Alias> {
        public ReflectEx.Alias Evaluate() => new(Type, Name,
            Content.EvaluateObject() as Func<TExArgCtx, TEx> ?? throw new StaticException("Alias failed cast"));

        public override IEnumerable<SemanticToken> ToSemanticTokens() => new[] {
            new SemanticToken(DeclPos, SemanticTokenTypes.Type),
            new SemanticToken(AliasPos, SemanticTokenTypes.Parameter)
        }.Concat(Content.ToSemanticTokens());

        public override string Explain() => $"{CompactPosition} Variable '{Name}' (type {Type.SimpRName()})";
        public override DocumentSymbol ToSymbolTree(string? descr = null)
            => new(Name, $"Alias", SymbolKind.Variable, 
                Position.ToRange(), FlattenParams((p, i) => p.ToSymbolTree()));

        public override IEnumerable<PrintToken> DebugPrint() =>
            Content.DebugPrint().Prepend($"{CompactPosition} {Name} = ");
    }

    /// <summary>
    /// A failed AST parse.
    /// </summary>
    public record Failure(ReflectionException Exc, Type ReturnType) : AST(Exc.Position), IAST {
        public override IEnumerable<IDebugAST> Children => Basis == null ? Array.Empty<IDebugAST>() : new[] { Basis };
        public Type ResultType => ReturnType;
        /// <summary>
        /// In the case when an error invalidates a certain block of code, this contains the object initially
        ///  parsed from that block of code.
        /// </summary>
        public IAST? Basis { get; init; }
        public override List<NestedFailure> Errors => 
                new () {
                new(this, Basis?.Errors ?? new List<NestedFailure>())
            };
        public override bool IsUnsound => true;
        protected Exception FirstException => Errors.SelectMany(e => e.AsExceptions()).First();

        public override string Explain() => Basis switch {
                    Failure f => f.Explain(),
                    { } b => $"(ERROR) {b.Explain()}",
                    _ => $"(ERROR) {CompactPosition} Failed parse for type {ReturnType.SimpRName()}"
                };

        public override IEnumerable<(IDebugAST, int?)>? NarrowestASTForPosition(PositionRange p) {
            if (p.Start.Index < Position.Start.Index || p.End.Index > Position.End.Index) return null;
            if (Basis?.NarrowestASTForPosition(p) is { } results)
                return results.Append((this, 0));
            return new (IDebugAST, int?)[] { (this, null) };
        }

        public object? EvaluateObject() => throw FirstException;

        public override DocumentSymbol ToSymbolTree(string? descr = null) =>
            new("(Error)", null, SymbolKind.Null, Position.ToRange());

        public override IEnumerable<SemanticToken> ToSemanticTokens() => Array.Empty<SemanticToken>();

        public Failure(ReflectionException exc, NamedParam prm) : this(exc, prm.Type) { }

        public static IAST MaybeEnclose(IAST src, ReflectionException? exc) => exc == null ?
            src :
            new Failure(
                new ReflectionException(src.Position, exc.Position, exc.Message, exc.InnerException)
                , src.ResultType) { Basis = src };
        
        public static IAST<T> MaybeEnclose<T>(IAST<T> src, ReflectionException? exc) => exc == null ?
            src :
            new Failure<T>(
                new ReflectionException(src.Position, exc.Position, exc.Message, exc.InnerException)) 
                { Basis = src };
    }

    public record Failure<T>(ReflectionException Exc) : Failure(Exc, typeof(T)), IAST<T> {
        public T Evaluate() => throw FirstException;
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