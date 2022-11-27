using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.SM;
using JetBrains.Annotations;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using static Danmokou.Reflection.Reflector;

namespace Danmokou.Reflection {
/// <summary>
/// A lightweight set of instructions for compiling an object
///  from 'code'.
/// </summary>
public interface IAST {
    /// <summary>
    /// Position of the code that will generate this object.
    /// <br/>This is used for debugging/logging/error messaging.
    /// </summary>
    PositionRange Position { get; }

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
    
    /// <summary>
    /// Generate the object.
    /// </summary>
    object? EvaluateObject(ASTEvaluationData data) => throw new NotImplementedException();
    
    /// <summary>
    /// Get all ASTs that are children to this one.
    /// </summary>
    IEnumerable<IAST> Children { get; }

    /// <summary>
    /// Return the furthest-down AST in the tree that encloses the given position,
    /// then its ancestors up to the root.
    /// <br/>Each element is paired with the index of the child that preceded it,
    ///  or null if it is the lowermost AST returned.
    /// <br/>Returns null if the AST does not enclose the given position.
    /// </summary>
    IEnumerable<(IAST tree, int? childIndex)>? NarrowestASTForPosition(PositionRange p);

    /// <summary>
    /// Print out a readable, preferably one-line description of the AST (not including its children). Consumed by language server.
    /// </summary>
    [PublicAPI]
    string Explain();

    /// <summary>
    /// Return a parse tree for the AST. Consumed by language server.
    /// </summary>
    [PublicAPI]
    DocumentSymbol ToSymbolTree();

    /// <summary>
    /// Describe the semantics of all the parsed tokens in the source code.
    /// Consumed by language server.
    /// </summary>
    [PublicAPI]
    IEnumerable<SemanticToken> ToSemanticTokens();

    /// <summary>
    /// Print a readable description of the entire AST.
    /// </summary>
    IEnumerable<PrintToken> DebugPrint();

    IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx);

    string DebugPrintStringify() => new ExpressionPrinter().Stringify(DebugPrint().ToArray());
}

/// <summary>
/// <see cref="IAST"/> restricted by the return type of the object.
/// </summary>
public interface IAST<out T> : IAST {
    T Evaluate(ASTEvaluationData data);
    object? IAST.EvaluateObject(ASTEvaluationData data) => Evaluate(data);
    Type IAST.ResultType => typeof(T);
}

/// <summary>
/// Cast an untyped AST to a specific type.
/// </summary>
public record ASTRuntimeCast<T>(IAST Source) : IAST<T> {
    public PositionRange Position => Source.Position;
    public List<AST.NestedFailure> Errors => Source.Errors;
    public bool IsUnsound => Source.IsUnsound;

    public T Evaluate(ASTEvaluationData data) => Source.EvaluateObject(data) is T result ?
        result :
        throw new StaticException("Runtime AST cast failed");

    public IEnumerable<IAST> Children => new[] { Source };

    public IEnumerable<(IAST, int?)>? NarrowestASTForPosition(PositionRange p)
        => Source.NarrowestASTForPosition(p);

    public string Explain() => $"{Position.Print(true)} Cast to type {typeof(T).SimpRName()}";
    public DocumentSymbol ToSymbolTree() => Source.ToSymbolTree();
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
    public U Evaluate(ASTEvaluationData data) => Map(Source.Evaluate(data));
    
    public IEnumerable<IAST> Children => new[] { Source };

    public IEnumerable<(IAST, int?)>? NarrowestASTForPosition(PositionRange p)
        => Source.NarrowestASTForPosition(p);

    public string Explain() => $"{Position.Print(true)} " +
                               $"Map from type {typeof(T).SimpRName()} to type {typeof(U).SimpRName()}";

    public DocumentSymbol ToSymbolTree() => Source.ToSymbolTree();
    public IEnumerable<SemanticToken> ToSemanticTokens() => Source.ToSemanticTokens();

    public IEnumerable<PrintToken> DebugPrint() => Source.DebugPrint().Prepend($"({typeof(U).SimpRName()})");
    public IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) => Source.WarnUsage(ctx);
}

public abstract record AST(PositionRange Position, params IAST[] Params) : IAST {
    public virtual IEnumerable<IAST> Children => Params;
    public ReflectDiagnostic[] Diagnostics { get; init; } = Array.Empty<ReflectDiagnostic>();
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

    public virtual IEnumerable<(IAST, int?)>? NarrowestASTForPosition(PositionRange p) {
        if (p.Start.Index < Position.Start.Index || p.End.Index > Position.End.Index) return null;
        for (int ii = 0; ii < Params.Length; ++ii) {
            var arg = Params[ii];
            if (arg.NarrowestASTForPosition(p) is { } results)
                return results.Append((this, ii));
        }
        return new (IAST, int?)[] { (this, null) };
    }

    public abstract string Explain();
    public abstract DocumentSymbol ToSymbolTree();
    public abstract IEnumerable<SemanticToken> ToSemanticTokens();

    public virtual IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) =>
        Diagnostics.Concat(Params.SelectMany(p => p.WarnUsage(ctx)));
    public virtual IEnumerable<PrintToken> DebugPrint() => new PrintToken[] { Explain() };

    string CompactPosition => Position.Print(true);
    
    //By default, flatten List<SM>, SM[], AsyncPattern[], SyncPattern[]
    //This drastically improves readability as these are often deeply nested
    private static readonly Type[] flattenArrayTypes =
        { typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern) };
    protected IEnumerable<DocumentSymbol> FlattenParams() {
        bool DefaultFlatten(IAST ast) =>
            ast is SequenceList<StateMachine> ||
            ast is SequenceArray seq && flattenArrayTypes.Contains(seq.ElementType);
        foreach (var p in Params) {
            if (DefaultFlatten(p))
                foreach (var s in p.ToSymbolTree().Children ?? Array.Empty<DocumentSymbol>())
                    yield return s;
            else
                yield return p.ToSymbolTree();
        }
    }
    protected DocumentSymbol MethodToSymbolTree(MethodSignature Method) =>
        Method.IsFallthrough ? 
            Params[0].ToSymbolTree() :
            new(Method.Name, Method.TypeOnlySignature, SymbolKind.Method, Position.ToRange(),
                FlattenParams());

    protected IEnumerable<SemanticToken> MethodToSemanticTokens(MethodSignature method, PositionRange methodPosition) =>
        Params.SelectMany(p => p.ToSemanticTokens()).Prepend(SemanticToken.FromMethod(method, methodPosition));

    protected IEnumerable<PrintToken> DebugPrintMethod(MethodSignature Method) {
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
            for (int ii = 0; ii < Params.Count; ++ii) {
                foreach (var x in Params[ii].DebugPrint())
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
        MethodSignature BaseMethod, params IAST[] Params) : AST(Position, Params), IAST {
        /// <summary>
        /// Whether the argument list is provided in parentheses (ie. as `func(arg1, arg2)` as opposed to `func arg1 arg2`).
        /// </summary>
        public bool Parenthesized { get; init; } = false;
        public override string Explain() => $"{CompactPosition} {BaseMethod.AsSignature}";
        public override DocumentSymbol ToSymbolTree() {
            if (BaseMethod.isCtor && BaseMethod.ReturnType == typeof(PhaseSM) && !Params[1].IsUnsound && Params[1].EvaluateObject(new()) is PhaseProperties props) {
                return new($"{props.phaseType?.ToString() ?? "Phase"}", props.cardTitle?.Value ?? "",
                    SymbolKind.Method, Position.ToRange(), FlattenParams());
            }
            return MethodToSymbolTree(BaseMethod);
        }

        public override IEnumerable<SemanticToken> ToSemanticTokens() =>
            MethodToSemanticTokens(BaseMethod, MethodPosition);

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintMethod(BaseMethod);

        public override IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) {
            if (ctx.Props.warnPrefix && BaseMethod.Mi.GetCustomAttributes<WarnOnStrictAttribute>().Any(wa =>
                    (int)ctx.Props.strict >= wa.strictness)) {
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
    public record MethodInvoke(PositionRange Position, PositionRange MethodPosition, MethodSignature Method, params IAST[] Params) : BaseMethodInvoke(Position, MethodPosition, Method, Params), IAST {
        public enum InvokeType {
            Normal,
            SM,
            Fallthrough,
            Compiler,
            PostAggregate
        }

        public InvokeType Type { get; init; } = InvokeType.Normal;

        public MethodInvoke(IAST nested, MethodSignature sig) : this(nested.Position, new PositionRange(nested.Position.Start, nested.Position.Start), sig, nested) { }

        public Type ResultType => Method.ReturnType;

        private static readonly Type gcxPropsType = typeof(GenCtxProperties);
        private static readonly Type gcxPropArrType = typeof(GenCtxProperty[]);
        public object? EvaluateObject(ASTEvaluationData data) {
            var prms = new object?[Params.Length];
            for (int ii = 0; ii < prms.Length; ++ii) {
                //If we have a GCX Expose property, add it into EvaluationData before constructing the other children
                if (Params[ii].ResultType.IsWeakSubclassOf(gcxPropsType)) {
                    var props = (Params[ii].EvaluateObject(data) as GenCtxProperties) ?? throw new StaticException("Failed to get GCXProperties");
                    if (props.Expose?.Length > 0)
                        data = data.AddExposed(props.Expose);
                    prms[ii] = props;
                    for (int jj = 0; jj < prms.Length; ++jj)
                        if (jj != ii)
                            prms[jj] = Params[jj].EvaluateObject(data);
                    goto construct;
                } else if (Params[ii].ResultType == gcxPropArrType) {
                    var props = (Params[ii].EvaluateObject(data) as GenCtxProperty[]) ?? throw new StaticException("Failed to get GCXProperty[]");
                    foreach (var p in props)
                        if (p is GenCtxProperty.ExposeProp ep)
                            data = data.AddExposed(ep.value);
                    prms[ii] = props;
                    for (int jj = 0; jj < prms.Length; ++jj)
                        if (jj != ii)
                            prms[jj] = Params[jj].EvaluateObject(data);
                    goto construct;
                }
            }
            for (int ii = 0; ii < prms.Length; ++ii)
                prms[ii] = Params[ii].EvaluateObject(data);
            construct:
            var result = Method.InvokeMi(prms);
            if (Method.Mi.GetCustomAttribute<ExtendGCXUExposedAttribute>() != null && data.ExposedVariables.Count > 0) {
                var gcxu = (result as GCXU ?? throw new StaticException(
                    $"{nameof(ExtendGCXUExposedAttribute)} used on method {Method.Name} that does not return GCXU"));
                return gcxu with {
                    BoundAliases = gcxu.BoundAliases.Concat(
                        data.ExposedVariables.Select(x => (x.Item1.AsType(), x.Item2))).ToList()
                };
            }
            return result;
        }
    }

    /// <summary>
    /// An AST that creates an object through method invocation.
    /// <br/>The return type of the method is specified.
    /// </summary>
    public record MethodInvoke<T>(PositionRange Position, PositionRange MethodPosition, MethodSignature Method, params IAST[] Params) : MethodInvoke(
        Position, MethodPosition, Method, Params), IAST<T> {
        public T Evaluate(ASTEvaluationData data) => EvaluateObject(data) switch {
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
        (PositionRange Position, PositionRange MethodPosition, FuncedMethodSignature<T, R> Method, IAST[] Params) : BaseMethodInvoke(Position, MethodPosition, Method, Params),
            IAST<Func<T, R>> {
        public Func<T, R> Evaluate(ASTEvaluationData data) {
            var fprms = new object?[Params.Length];
            for (int ii = 0; ii < fprms.Length; ++ii)
                fprms[ii] = Params[ii].EvaluateObject(data);
            return Method.InvokeMiFunced(fprms);
        }
    }

    /// <summary>
    /// An AST with a precomputed value.
    /// </summary>
    public record Preconstructed<T>(PositionRange Position, T Value, string? Display = null) : AST(Position), IAST<T> {
        public T Evaluate(ASTEvaluationData data) => Value;
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

        public override DocumentSymbol ToSymbolTree() =>
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
            //The type should be Func<A,...R>, where A...R are types like TEx<float>
            FuncAllTypes = funcType.GenericTypeArguments;
            FuncRetType = FuncAllTypes[^1];
            //Look for methods returning type R
            if (!ReflectionData.HasMember(FuncRetType, methodName))
                throw new ReflectionException(p,
                    $"Method lookup for type {FuncType.SimpRName()} failed because no there is no function named " +
                    $"\"{methodName}\" with a return type of {FuncRetType.SimpRName()}.");
            Method = ReflectionData.GetArgTypes(FuncRetType, methodName);
            if (FuncAllTypes.Length - 1 != Method.Params.Length)
                throw new ReflectionException(p,
                    $"Provided method {methodName} has {FuncAllTypes.Length - 1} parameters " +
                    $"(required {Method.Params.Length})");
            for (int ii = 0; ii < FuncAllTypes.Length - 1; ++ii) {
                if (FuncAllTypes[ii] != Method.Params[ii].Type)
                    throw new ReflectionException(p,
                        $"Provided method {methodName} has parameter #{ii + 1} as type" +
                        $" {Method.Params[ii].Type.RName()} (required {FuncAllTypes[ii].RName()})");
            }
        }

        public object? EvaluateObject(ASTEvaluationData data) {
            var lambdaer = typeof(Reflector)
                               .GetMethod($"MakeLambda{Method.Params.Length}",
                                   BindingFlags.Static | BindingFlags.NonPublic)
                               ?.MakeGenericMethod(FuncAllTypes) ??
                           throw new StaticException($"Couldn't find lambda constructor method for " +
                                                     $"count {Method.Params.Length}");
            return lambdaer.Invoke(null, new object[] {
                (Func<object?[], object?>)Method.InvokeMi
            });
        }

        public override string Explain() => $"{CompactPosition} {Method.Name}";

        public override DocumentSymbol ToSymbolTree() =>
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
        public StateMachine? Evaluate(ASTEvaluationData data) => StateMachineManager.FromName(Filename);

        public override string Explain() => $"{CompactPosition} StateMachine from file: {Filename}";

        public override DocumentSymbol ToSymbolTree() =>
            new DocumentSymbol($"SMFromFile({Filename})", null, SymbolKind.Object, Position.ToRange());
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
        public List<T> Evaluate(ASTEvaluationData data) {
            var l = new List<T>(Parts.Count);
            for (int ii = 0; ii < Parts.Count; ++ii)
                l.Add(Parts[ii].Evaluate(data));
            return l;
        }

        public override string Explain() => $"{CompactPosition} List<{typeof(T).SimpRName()}>";

        public override DocumentSymbol ToSymbolTree()
            => new($"List<{typeof(T).SimpRName()}>", null, SymbolKind.Array, 
                Position.ToRange(), FlattenParams());

        public override IEnumerable<SemanticToken> ToSemanticTokens() => 
            Parts.SelectMany(p => p.ToSemanticTokens());

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintArray(typeof(T), Parts);
    }

    /// <summary>
    /// An AST that generates an array of objects.
    /// </summary>
    public record SequenceArray(PositionRange Position, Type ElementType, IAST[] Params) : BaseSequence(Position, Params), IAST<Array> {
        public Array Evaluate(ASTEvaluationData data) {
            var array = Array.CreateInstance(ElementType, Params.Length);
            for (int ii = 0; ii < Params.Length; ++ii)
                array.SetValue(Params[ii].EvaluateObject(data), ii);
            return array;
        }

        public Type ResultType => ElementType.MakeArrayType();
        public override string Explain() => $"{CompactPosition} {ElementType.SimpRName()}[]";
        public override DocumentSymbol ToSymbolTree()
            => new($"{ElementType.SimpRName()}[]", null, SymbolKind.Array, 
                Position.ToRange(), FlattenParams());
        
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
        public Danmokou.Danmaku.GCRule<T> Evaluate(ASTEvaluationData data) => 
            new(Type, Reference, Operator, Rule.Evaluate(data));

        public override IEnumerable<SemanticToken> ToSemanticTokens() => new[] {
            new SemanticToken(RefPosition, SemanticTokenTypes.Parameter),
            new SemanticToken(OpPosition, SemanticTokenTypes.Operator)
        }.Concat(Rule.ToSemanticTokens());

        public override string Explain() => $"{CompactPosition} GCRule<{typeof(T).SimpRName()}>";
        public override DocumentSymbol ToSymbolTree()
            => new($"GCRule<{typeof(T).SimpRName()}>", null, SymbolKind.Struct, 
                Position.ToRange(), FlattenParams());

        public override IEnumerable<PrintToken> DebugPrint() =>
            Rule.DebugPrint().Prepend($"{CompactPosition} {Reference} {Operator}{Type} ");
    }

    /// <summary>
    /// AST for <see cref="ReflectEx.Alias"/>
    /// </summary>
    public record Alias(PositionRange DeclPos, PositionRange AliasPos, Type Type, string Name, IAST Content) : AST(DeclPos.Merge(AliasPos).Merge(Content.Position), Content),
        IAST<ReflectEx.Alias> {
        public ReflectEx.Alias Evaluate(ASTEvaluationData data) => new(Type, Name,
            Content.EvaluateObject(data) as Func<TExArgCtx, TEx> ?? throw new StaticException("Alias failed cast"));

        public override IEnumerable<SemanticToken> ToSemanticTokens() => new[] {
            new SemanticToken(DeclPos, SemanticTokenTypes.Type),
            new SemanticToken(AliasPos, SemanticTokenTypes.Parameter)
        }.Concat(Content.ToSemanticTokens());

        public override string Explain() => $"{CompactPosition} Variable '{Name}' (type {Type.RName()})";
        public override DocumentSymbol ToSymbolTree()
            => new(Name, $"Alias", SymbolKind.Variable, 
                Position.ToRange(), FlattenParams());

        public override IEnumerable<PrintToken> DebugPrint() =>
            Content.DebugPrint().Prepend($"{CompactPosition} {Name} = ");
    }

    /// <summary>
    /// A failed AST parse.
    /// </summary>
    public record Failure(ReflectionException Exc, Type ReturnType) : AST(Exc.Position), IAST {
        public override IEnumerable<IAST> Children => Basis == null ? Array.Empty<IAST>() : new[] { Basis };
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

        public override IEnumerable<(IAST, int?)>? NarrowestASTForPosition(PositionRange p) {
            if (p.Start.Index < Position.Start.Index || p.End.Index > Position.End.Index) return null;
            if (Basis?.NarrowestASTForPosition(p) is { } results)
                return results.Append((this, 0));
            return new (IAST, int?)[] { (this, null) };
        }

        public object? EvaluateObject(ASTEvaluationData data) => throw FirstException;

        public override DocumentSymbol ToSymbolTree() => throw FirstException;

        public override IEnumerable<SemanticToken> ToSemanticTokens() => throw FirstException;

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
        public T Evaluate(ASTEvaluationData data) => throw FirstException;
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