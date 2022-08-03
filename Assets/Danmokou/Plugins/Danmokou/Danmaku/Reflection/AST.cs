using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Danmaku;
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
    /// Generate the object.
    /// </summary>
    object? EvaluateObject() => throw new Exception();

    /// <summary>
    /// Return the furthest-down AST in the tree that encloses the given position,
    /// then its ancestors up to the root.
    /// <br/>Returns null if the AST does not enclose the given position.
    /// </summary>
    IEnumerable<IAST>? NarrowestASTForPosition(PositionRange p);

    /// <summary>
    /// Print out a readable, preferably one-line description of the AST (not including its children).
    /// </summary>
    [PublicAPI]
    string Explain();

    [PublicAPI]
    DocumentSymbol ToSymbolTree();

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
    T Evaluate();
    object? IAST.EvaluateObject() => Evaluate();
}

/// <summary>
/// Cast an untyped AST to a specific type.
/// </summary>
public record ASTRuntimeCast<T>(IAST Source) : IAST<T> {
    public PositionRange Position => Source.Position;

    public T Evaluate() => Source.EvaluateObject() is T result ?
        result :
        throw new StaticException("Runtime AST cast failed");

    public IEnumerable<IAST>? NarrowestASTForPosition(PositionRange p)
        => Source.NarrowestASTForPosition(p);

    public string Explain() => $"{Position.Print(true)} Cast to type {typeof(T).SimpRName()}";
    public DocumentSymbol ToSymbolTree() => Source.ToSymbolTree();

    public IEnumerable<PrintToken> DebugPrint() => Source.DebugPrint();
    public IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) => Source.WarnUsage(ctx);
}

/// <summary>
/// Map over the result of an AST.
/// </summary>
public record ASTFmap<T, U>(Func<T, U> Map, IAST<T> Source) : IAST<U> {
    public PositionRange Position => Source.Position;
    public U Evaluate() => Map(Source.Evaluate());

    public IEnumerable<IAST>? NarrowestASTForPosition(PositionRange p)
        => Source.NarrowestASTForPosition(p);

    public string Explain() => $"{Position.Print(true)} " +
                               $"Map from type {typeof(T).SimpRName()} to type {typeof(U).SimpRName()}";

    public DocumentSymbol ToSymbolTree() => Source.ToSymbolTree();

    public IEnumerable<PrintToken> DebugPrint() => Source.DebugPrint().Prepend($"({typeof(U).SimpRName()})");
    public IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) => Source.WarnUsage(ctx);
}

public abstract record AST(PositionRange Position, params IAST[] Params) : IAST {
    public IEnumerable<IAST>? NarrowestASTForPosition(PositionRange p) {
        if (p.Start.Index < Position.Start.Index || p.End.Index > Position.End.Index) return null;
        foreach (var arg in Params) {
            if (arg.NarrowestASTForPosition(p) is { } results)
                return results.Append(this);
        }
        return new IAST[] { this };
    }

    public abstract string Explain();
    public abstract DocumentSymbol ToSymbolTree();

    public virtual IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) => Array.Empty<ReflectDiagnostic>();
    public virtual IEnumerable<PrintToken> DebugPrint() => new PrintToken[] { Explain() };

    protected IEnumerable<ReflectDiagnostic> WarnUsageMethod(ReflCtx ctx, MethodSignature mi, IAST[] args) {
        if (ctx.props.warnPrefix && Attribute.GetCustomAttributes(mi.Mi).Any(x =>
                x is WarnOnStrictAttribute wa && (int)ctx.props.strict >= wa.strictness)) {
            yield return new ReflectDiagnostic.Warning(Position,
                $"The method \"{mi.TypeEnclosedName}\" is not permitted for use in a script with strictness {ctx.props.strict}. You might accidentally be using the prefix version of an infix function.");
        }
        foreach (var d in args.SelectMany(a => a.WarnUsage(ctx)))
            yield return d;
    }

    string CompactPosition => Position.Print(true);
    
    //By default, flatten List<SM>, SM[], AsyncPattern[], SyncPattern[]
    //This drastically improves readability as these are often deeply nested
    private static readonly Type[] flattenArrayTypes =
        { typeof(StateMachine), typeof(AsyncPattern), typeof(SyncPattern) };
    protected IEnumerable<DocumentSymbol> FlattenParams() {
        bool DefaultFlatten(IAST ast) =>
            ast is SequenceList<StateMachine> ||
            ast is Sequence seq && flattenArrayTypes.Contains(seq.ElementType);
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

    /// <summary>
    /// An AST that creates an object through method invocation.
    /// </summary>
    public record MethodInvoke(PositionRange Position, MethodSignature Method, params IAST[] Params) : AST(Position,
        Params), IAST {
        public enum InvokeType {
            Normal,
            SM,
            Fallthrough,
            Compiler,
            PostAggregate
        }

        public InvokeType Type { get; init; } = InvokeType.Normal;

        public MethodInvoke(IAST nested, MethodSignature sig) : this(nested.Position, sig, nested) { }

        public object? EvaluateObject() {
            var prms = new object?[Params.Length];
            for (int ii = 0; ii < prms.Length; ++ii)
                prms[ii] = Params[ii].EvaluateObject();
            return Method.InvokeMi(prms);
        }

        public override string Explain() => $"{CompactPosition} {Method.AsSignature}";
        public override DocumentSymbol ToSymbolTree() => MethodToSymbolTree(Method);

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintMethod(Method);

        public override IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) =>
            WarnUsageMethod(ctx, Method, Params);
    }

    /// <summary>
    /// An AST that creates an object through method invocation.
    /// <br/>The return type of the method is specified.
    /// </summary>
    public record MethodInvoke<T>(PositionRange Position, MethodSignature Method, params IAST[] Params) : MethodInvoke(
        Position, Method, Params), IAST<T> {
        public T Evaluate() => EvaluateObject() switch {
            T t => t,
            var x => throw new StaticException(
                $"AST method invocation for {Method.TypeEnclosedName} returned object of type {x?.GetType()} (expected {typeof(T)}")
        };
    }

    /// <summary>
    /// An AST that creates an object by funcifying a method returning R over type T.
    /// <br/>ie. For a recorded function R member(A, B, C...), given parameters of type [F(A), F(B), F(C)] (funcified on T, eg. [T->A, T->B, T->C]),
    /// this AST constructs a function T->R that uses T to defuncify the parameters and pass them to member.
    /// </summary>
    public record FuncedMethodInvoke<T, R>
        (PositionRange Position, FuncedMethodSignature<T, R> Method, IAST[] Params) : AST(Position, Params),
            IAST<Func<T, R>> {
        public Func<T, R> Evaluate() {
            var fprms = new object?[Params.Length];
            for (int ii = 0; ii < fprms.Length; ++ii)
                fprms[ii] = Params[ii].EvaluateObject();
            return Method.InvokeMiFunced(fprms);
        }

        public override string Explain() => $"{CompactPosition} {Method.AsSignature}";
        public override DocumentSymbol ToSymbolTree() => MethodToSymbolTree(Method);

        public override IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) =>
            WarnUsageMethod(ctx, Method, Params);

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintMethod(Method);
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
                if (Display != null && Display[0] == '&')
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

        public override string Explain() =>
            $"{CompactPosition} {MyType.SimpRName()} {Description}";

        public override DocumentSymbol ToSymbolTree() =>
            new DocumentSymbol(Description, MyType.SimpRName(), SymbolType, Position.ToRange());
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

        public object? EvaluateObject() {
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
    }

    /// <summary>
    /// An AST that generates a state machine by reading a file.
    /// </summary>
    public record SMFromFile(PositionRange Position, string Filename) : AST(Position), IAST<StateMachine?> {
        public StateMachine? Evaluate() => StateMachineManager.FromName(Filename);

        public override string Explain() => $"{CompactPosition} StateMachine from file: {Filename}";

        public override DocumentSymbol ToSymbolTree() =>
            new DocumentSymbol($"SMFromFile({Filename})", null, SymbolKind.Object, Position.ToRange());
    }


    /// <summary>
    /// An AST that generates a compile-time strongly typed list of objects.
    /// </summary>
    public record SequenceList<T>(PositionRange Position, IList<IAST<T>> Parts) : AST(Position,
        Parts.Cast<IAST>().ToArray()), IAST<List<T>> {
        public List<T> Evaluate() {
            var l = new List<T>(Parts.Count);
            for (int ii = 0; ii < Parts.Count; ++ii)
                l.Add(Parts[ii].Evaluate());
            return l;
        }

        public override string Explain() => $"{CompactPosition} List<{typeof(T).SimpRName()}>";

        public override DocumentSymbol ToSymbolTree()
            => new($"List<{typeof(T).SimpRName()}>", null, SymbolKind.Array, 
                Position.ToRange(), FlattenParams());

        public override IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) =>
            Parts.SelectMany(p => p.WarnUsage(ctx));

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintArray(typeof(T), Parts);
    }

    /// <summary>
    /// An AST that generates an array of objects.
    /// </summary>
    public record Sequence(PositionRange Position, Type ElementType, IAST[] Params) : AST(Position, Params), IAST<Array> {
        public Array Evaluate() {
            var array = Array.CreateInstance(ElementType, Params.Length);
            for (int ii = 0; ii < Params.Length; ++ii)
                array.SetValue(Params[ii].EvaluateObject(), ii);
            return array;
        }

        public override string Explain() => $"{CompactPosition} {ElementType.SimpRName()}[]";
        public override DocumentSymbol ToSymbolTree()
            => new($"{ElementType.SimpRName()}[]", null, SymbolKind.Array, 
                Position.ToRange(), FlattenParams());

        public override IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) =>
            Params.SelectMany(p => p.WarnUsage(ctx));

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintArray(ElementType, Params);
    }

    /// <summary>
    /// AST for <see cref="GCRule{T}"/>
    /// </summary>
    public record GCRule<T>(PositionRange Position, ExType Type, ReferenceMember Reference, GCOperator Operator,
        IAST<GCXF<T>> Rule) : AST(Position, Rule), IAST<Danmokou.Danmaku.GCRule<T>> {
        public Danmokou.Danmaku.GCRule<T> Evaluate() => new(Type, Reference, Operator, Rule.Evaluate());

        public override IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) => Rule.WarnUsage(ctx);

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
    public record Alias(PositionRange Position, string Name, IAST Content) : AST(Position, Content),
        IAST<ReflectEx.Alias> {
        public ReflectEx.Alias Evaluate() => new(Name,
            Content.EvaluateObject() as Func<TExArgCtx, TEx> ?? throw new StaticException("Alias failed cast"));

        public override IEnumerable<ReflectDiagnostic> WarnUsage(ReflCtx ctx) => Content.WarnUsage(ctx);

        public override string Explain() => $"{CompactPosition} Alias for '{Name}'";
        public override DocumentSymbol ToSymbolTree()
            => new(Name, $"Alias", SymbolKind.Variable, 
                Position.ToRange(), FlattenParams());

        public override IEnumerable<PrintToken> DebugPrint() =>
            Content.DebugPrint().Prepend($"{CompactPosition} {Name} = ");
    }
}
}