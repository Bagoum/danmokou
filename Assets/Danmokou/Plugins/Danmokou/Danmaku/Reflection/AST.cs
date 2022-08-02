
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.SM;
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
    /// Print a readable description of the AST.
    /// </summary>
    IEnumerable<PrintToken> DebugPrint();
    void WarnUsage(ReflCtx ctx);

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
    public IEnumerable<PrintToken> DebugPrint() => Source.DebugPrint();
    public void WarnUsage(ReflCtx ctx) => Source.WarnUsage(ctx);
}

/// <summary>
/// Map over the result of an AST.
/// </summary>
public record ASTFmap<T,U>(Func<T,U> Map, IAST<T> Source) : IAST<U> {
    public PositionRange Position => Source.Position;
    public U Evaluate() => Map(Source.Evaluate());
    public IEnumerable<PrintToken> DebugPrint() => Source.DebugPrint().Prepend($"({typeof(U).SimpRName()})");
    public void WarnUsage(ReflCtx ctx) => Source.WarnUsage(ctx);
}

public abstract record AST(PositionRange Position) : IAST {
    //TODO call this
    public virtual void WarnUsage(ReflCtx ctx) { }
    public abstract IEnumerable<PrintToken> DebugPrint();
    protected void WarnUsageMethod(ReflCtx ctx, MethodSignature mi) {
        if (ctx.props.warnPrefix && Attribute.GetCustomAttributes(mi.Mi).Any(x =>
                x is WarnOnStrictAttribute wa && (int) ctx.props.strict >= wa.strictness)) {
            Logs.Log(
                $"{Position}: The method \"{mi.TypeEnclosedName}\" is not permitted for use in a script with strictness {ctx.props.strict}. You might accidentally be using the prefix version of an infix function.",
                true, LogLevel.WARNING);
        }
    }
    string CompactPosition => Position.Print(true);

    protected IEnumerable<PrintToken> DebugPrintMethod(MethodSignature Method, IAST[] Params) {
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
    
    protected IEnumerable<PrintToken> DebugPrintArray<T>(Type Type, IList<T> Params) where T: IAST {
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
    public record MethodInvoke(PositionRange Position, MethodSignature Method, params IAST[] Params) : AST(Position), IAST {
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

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintMethod(Method, Params);

        public override void WarnUsage(ReflCtx ctx) => WarnUsageMethod(ctx, Method);
    }
    
    /// <summary>
    /// An AST that creates an object through method invocation.
    /// <br/>The return type of the method is specified.
    /// </summary>
    public record MethodInvoke<T>(PositionRange Position, MethodSignature Method, params IAST[] Params) : MethodInvoke(Position, Method, Params), IAST<T> {
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
    public record FuncedMethodInvoke<T, R>(PositionRange Position, FuncedMethodSignature<T, R> Method, IAST[] Params) : AST(Position), IAST<Func<T, R>> {
        public Func<T,R> Evaluate() {
            var fprms = new object?[Params.Length];
            for (int ii = 0; ii < fprms.Length; ++ii)
                fprms[ii] = Params[ii].EvaluateObject();
            return Method.InvokeMiFunced(fprms);
        }
        public override void WarnUsage(ReflCtx ctx) => WarnUsageMethod(ctx, Method);
        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintMethod(Method, Params);
    }

    /// <summary>
    /// An AST with a precomputed value.
    /// </summary>
    public record Preconstructed<T>(PositionRange Position, T Value) : AST(Position), IAST<T> {
        public T Evaluate() => Value;
        public override IEnumerable<PrintToken> DebugPrint() =>
            new PrintToken[] { $"{CompactPosition} {Value?.ToString() ?? "null"}" };
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
                throw new Exception($"Provided method {methodName} takes {FuncAllTypes.Length - 1} parameters " +
                                    $"(required {Method.Params.Length})");
            for (int ii = 0; ii < FuncAllTypes.Length - 1; ++ii) {
                if (FuncAllTypes[ii] != Method.Params[ii].Type)
                    throw new Exception($"Provided method {methodName} has parameter #{ii + 1} as type" +
                                        $" {Method.Params[ii].Type.RName()} " +
                                        $"(required {FuncAllTypes[ii].RName()})");
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

        public override IEnumerable<PrintToken> DebugPrint() => new PrintToken[] { $"{CompactPosition} Method.Name" };

    }

    /// <summary>
    /// An AST that generates a state machine by reading a file.
    /// </summary>
    public record SMFromFile(PositionRange Position, string Filename) : AST(Position), IAST<StateMachine?> {
        public StateMachine? Evaluate() => StateMachineManager.FromName(Filename);

        public override IEnumerable<PrintToken> DebugPrint() =>
            new PrintToken[] { $"{CompactPosition} SMFromFile({Filename})" };
    }


    /// <summary>
    /// An AST that generates a compile-time strongly typed list of objects.
    /// </summary>
    public record SequenceList<T>(PositionRange Position, IList<IAST<T>> Parts) : AST(Position), IAST<List<T>> {
        public List<T> Evaluate() {
            var l = new List<T>(Parts.Count);
            for (int ii = 0; ii < Parts.Count; ++ii)
                l.Add(Parts[ii].Evaluate());
            return l;
        }

        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintArray(typeof(T), Parts);
    }
    
    /// <summary>
    /// An AST that generates an array of objects.
    /// </summary>
    public record Sequence(PositionRange Position, Type ElementType, IList<IAST> Parts) : AST(Position), IAST<Array> {
        public Array Evaluate() {
            var array = Array.CreateInstance(ElementType, Parts.Count);
            for (int ii = 0; ii < Parts.Count; ++ii)
                array.SetValue(Parts[ii].EvaluateObject(), ii);
            return array;
        }
        public override IEnumerable<PrintToken> DebugPrint() => DebugPrintArray(ElementType, Parts);
    }

    /// <summary>
    /// AST for <see cref="GCRule{T}"/>
    /// </summary>
    public record GCRule<T>(PositionRange Position, ExType Type, ReferenceMember Reference, GCOperator Operator,
        IAST<GCXF<T>> Rule) : AST(Position), IAST<Danmokou.Danmaku.GCRule<T>> {
        public Danmokou.Danmaku.GCRule<T> Evaluate() => new(Type, Reference, Operator, Rule.Evaluate());
        
        public override IEnumerable<PrintToken> DebugPrint() =>
            Rule.DebugPrint().Prepend($"{CompactPosition} {Reference} {Operator}{Type} ");
    }

    /// <summary>
    /// AST for <see cref="ReflectEx.Alias"/>
    /// </summary>
    public record Alias(PositionRange Position, string Name, IAST Content) : AST(Position), IAST<ReflectEx.Alias> {
        public ReflectEx.Alias Evaluate() => new(Name, Content.EvaluateObject() as Func<TExArgCtx, TEx> ?? throw new StaticException("Alias failed cast"));
        
        public override IEnumerable<PrintToken> DebugPrint() =>
            Content.DebugPrint().Prepend($"{CompactPosition} {Name} = ");
    }


}

}