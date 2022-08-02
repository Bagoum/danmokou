using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.SM;
using Danmokou.SM.Parsing;
using JetBrains.Annotations;
using UnityEngine.Profiling;

namespace Danmokou.Reflection {
public static partial class Reflector {
    public class ReflCtx {
        public enum Strictness {
            NONE = 0,
            COMMAS = 1
        }

        public readonly ParsingProperties props;
        public bool AllowPostAggregate => props.strict >= Strictness.COMMAS;
        public List<PhaseProperty> QueuedProps { get; } = new();
        
        public ReflCtx(IParseQueue q) {
            List<ParsingProperty> properties = new();
            while (q.MaybeScan() == SMParser.PROP2_KW) {
                q.Advance();
                properties.Add(q.NextChild().Into<ParsingProperty>());
                if (!q.IsNewline)
                    throw new Exception(
                        $"{q.GetLastPosition()} is missing a newline at the end of the the property declaration. Instead, it found \"{q.Scan()}\".");
            }
            props = new ParsingProperties(properties);
        }

        public ReflCtx() {
            props = new ParsingProperties(Array.Empty<ParsingProperty>());
        }

        public static ReflCtx Neutral = new ReflCtx();
    }


    /// <summary>
    /// Fill the argument array invoke_args by parsing elements from q according to type information in prms.
    /// <br/>Returns invoke_args.
    /// </summary>
    /// <param name="invoke_args">Argument array to fill.</param>
    /// <param name="starti">Index of invoke_args to start from.</param>
    /// <param name="sig">Type information of arguments.</param>
    /// <param name="q">Queue from which to parse elements.</param>
    public static void FillASTArray(IAST[] invoke_args, int starti, MethodSignature sig, IParseQueue q) {
        try {
            _FillASTArray(invoke_args, starti, sig, q);
        } catch (Exception e) {
            throw Exceptions.FlattenNestedException(e);
        }
    }

    private static IAST[] _FillASTArray(IAST[] asts, int starti, MethodSignature sig, IParseQueue q) {
        int nargs = 0;
        var prms = sig.Params;
        for (int ii = starti; ii < prms.Length; ++ii) {
            if (!prms[ii].NonExplicit) ++nargs;
        }
        if (nargs == 0) {
            if (!(q is ParenParseQueue) && !q.Empty) {
                //Zero-arg functions may absorb empty parentheses
                if (q._SoftScan(out _) is SMParser.ParsedUnit.Paren p) {
                    if (p.Item.Length == 0) q.NextChild();
                }
            }
            return asts;
        }
        if (!(q is ParenParseQueue)) {
            var c = q.ScanChild();
            if (c is ParenParseQueue p && p.Items.Length == 1 && nargs != 1) {
                // mod | (x) 3
                //Leave the parentheses to be parsed by the first argument
            } else 
                // mod | (x, 3)
                //Use the parentheses to fill arguments
                q = q.NextChild();
        }

        if (q is ParenParseQueue p2 && nargs != p2.Items.Length) {
            throw new ParsingException(p2.WrapThrow($"Expected {nargs} explicit arguments for {sig.FileLink}, " +
                                                    $"but the parentheses contains {p2.Items.Length}."));
        }


        void ThrowEmpty(IParseQueue lq, int ii) {
            if (lq.Empty) {
                throw new ParsingException(q.WrapThrowA(
                    $"Tried to construct {sig.FileLink}, but the parser ran out of text when looking for argument " +
                    $"#{ii + 1}/{prms.Length} {prms[ii].SimplifiedDescription}. " +
                    "This probably means you have parentheses that do not enclose the entire function.",
                    $" | [Arg#{ii + 1} Missing]"));
            }
        }
        for (int ii = starti; ii < prms.Length; ++ii) {
            if (prms[ii].NonExplicit) {
                asts[ii] = ReflectNonExplicitParam(q, prms[ii]);
            } else {
                ThrowEmpty(q, ii);
                var local = q.NextChild(out int ci);
                ThrowEmpty(local, ii);
                try {
                    asts[ii] = ReflectParam(local, prms[ii]);
                } catch (Exception ex) {
                    throw new InvokeException(
                        $"{q.GetLastPosition(ci)}: Tried to construct {sig.FileLink}, " +
                        $"but failed to create argument #{ii + 1}/{prms.Length} {prms[ii].SimplifiedDescription}.", ex);

                }
                local.ThrowOnLeftovers(() =>
                    $"Argument #{ii + 1}/{prms.Length} {prms[ii].SimplifiedDescription} has extra text.");
            }
        }
        q.ThrowOnLeftovers(() => $"{sig.FileLink} has extra text after all {prms.Length} arguments.");
        return asts;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IAST[] _FillASTArray(MethodSignature sig, IParseQueue q)
        => _FillASTArray(new IAST[sig.Params.Length], 0, sig, q);




    #region TargetTypeReflect

    
    /// <summary>
    /// Parse a string into an object of type T.
    /// <br/>May throw an exception if parsing fails.
    /// </summary>
    public static object? Into(this string argstring, Type t) {
        using var _ = BakeCodeGenerator.OpenContext(BakeCodeGenerator.CookingContext.KeyType.INTO, argstring);
        var p = IParseQueue.Lex(argstring);
        try {
            var ret = p.Into(t);
            p.ThrowOnLeftovers();
            return ret;
        } catch (Exception e) {
            throw new Exception($"Failed to parse below string into type {t.RName()}:\n{argstring}", e);
        }
    }
    
    /// <summary>
    /// Parse a string into an object of type T.
    /// <br/>May throw an exception if parsing fails.
    /// </summary>
    public static T Into<T>(this string argstring) => ((T) Into(argstring, typeof(T))!)!;

    /// <summary>
    /// Parse a string into an object of type T. Returns null if the string is null or whitespace-only.
    /// <br/>May throw an exception if parsing fails.
    /// </summary>
    public static T? IntoIfNotNull<T>(this string? argstring) where T : class {
        if (string.IsNullOrWhiteSpace(argstring)) return null;
        return Into<T>(argstring!);
    }
    
    /// <summary>
    /// Parse a string into an object of type T. Returns null if the string is null or whitespace-only.
    /// <br/>May throw an exception if parsing fails.
    /// </summary>
    public static object? IntoIfNotNull(this string? argstring, Type t) {
        if (string.IsNullOrWhiteSpace(argstring)) return null;
        return Into(argstring!, t);
    }

    public static T Into<T>(this IParseQueue q) => ((T) Into(q, typeof(T))!)!;

    private static object? Into(this IParseQueue q, Type t) {
        Profiler.BeginSample("AST construction");
        var ast = q.IntoAST(t);
        Profiler.EndSample();
        //There's an odd circularity where the first constructed PUParseList
        // creates a ReflCtx, which then calls Into to construct parsing properties
        // *before* its constructor is complete and it gets assigned to q.Ctx.
        if (q.Ctx != null)
            ast.WarnUsage(q.Ctx);
        Profiler.BeginSample("AST realization");
        var obj = ast.EvaluateObject();
        Profiler.EndSample();
        return obj;
    }
/*
    private static object? ReflectTargetType(IParseQueue ctx, Type t) {
        try {
            return _ReflectTargetType(ctx, t);
        } catch (Exception e) {
            throw Exceptions.FlattenNestedException(e);
        }
    }*/
    
    public static IAST<T> IntoAST<T>(this IParseQueue ctx) => new ASTRuntimeCast<T>(IntoAST(ctx, typeof(T)));
    private static IAST IntoAST(this IParseQueue ctx, Type t) => ReflectTargetType(ctx, t);
    private static IAST ReflectTargetType(IParseQueue ctx, Type t) {
        try {
            return _ReflectTargetType(ctx, t);
        } catch (Exception e) {
            throw Exceptions.FlattenNestedException(e);
        }
    }

    private static void RecurseParens(ref IParseQueue q, Type t) {
        while (q is ParenParseQueue p) {
            if (p.Items.Length == 1) q = p.NextChild();
            else
                throw new Exception(p.WrapThrow(
                    $"Tried to find an object of type {t.RName()}, but there is a parentheses with" +
                    $" {p.Items.Length} elements. Any parentheses should only have one element."));
        }
    }

    /// <summary>
    /// Returns true if the parse queue must be recursed.
    /// This occurs when an argument in a parenlist has parentheses, eg. f((g), 4).
    /// The parentheses around g will be detected by this.
    /// </summary>
    private static bool RecurseScan(IParseQueue q, out IParseQueue rec, out string val) {
        var pu = q._Scan(out var ii);
        switch (pu) {
            case SMParser.ParsedUnit.Str s:
                val = s.Item;
                rec = null!;
                return false;
            case SMParser.ParsedUnit.Paren p:
                if (p.Item.Length != 1)
                    throw new Exception(q.WrapThrow(ii,
                        "This parentheses must have exactly one argument."));
                rec = new PUListParseQueue(p.Item[0], q.Ctx);
                val = "";
                return true;
            default:
                throw new StaticException(q.WrapThrow(ii,
                    $"Couldn't resolve parser object type {pu.GetType()}."));
        }
    }

    /// <summary>
    /// A fallthrough parse queue has the ability but not the obligation to post-aggregate.
    /// </summary>
    private static IParseQueue MakeFallthrough(IParseQueue q) {
        if (q is PUListParseQueue p)
            return new NonLocalPUListParseQueue(p, true);
        return q;
    }

    [UsedImplicitly]
    private static Func<T1, R> MakeLambda1<T1, R>(Func<object?[], object> invoker)
        => arg => (R) invoker(new object?[] {arg});

    private static AST ReflectNonExplicitParam(IParseQueue q, NamedParam p) {
        if (p.Type == tPhaseProperties) {
            var props = new PhaseProperties(q.Ctx.QueuedProps);
            q.Ctx.QueuedProps.Clear();
            return new AST.Preconstructed<PhaseProperties>(q.Position, props);
        } else
            throw new StaticException($"No non-explicit reflection handling existsfor type {p.Type.RName()}");
    }

    private static IAST ReflectParam(IParseQueue q, NamedParam p) {
        if (p.LookupMethod) {
            if (p.Type.GenericTypeArguments.Length == 0) 
                throw new Exception("Method-Lookup parameter must be generic");
            RecurseParens(ref q, p.Type);
            var method_str = q.Next().ToLower();
            q.ThrowOnLeftovers(p.Type);
            return new AST.MethodLookup(q.GetLastPosition(), p.Type, method_str);
        } else {
            return _ReflectTargetType(q, p.Type);
        }
    }

    private static readonly Type tPhaseProperties = typeof(PhaseProperties);
    
    /// <summary>
    /// Top-level resolution function.
    /// </summary>
    /// <param name="q">Parsing queue to read from.</param>
    /// <param name="t">Type to construct.</param>
    /// <param name="postAggregateContinuation">Optional code to execute after post-aggregation is complete.</param>
    private static IAST _ReflectTargetType(IParseQueue q, Type t, Func<IAST, Type, IAST>? postAggregateContinuation=null) {
        RecurseParens(ref q, t);
        IAST? ast = null!;
        if (RecurseScan(q, out var rec, out var arg)) {
            q.Advance();
            ast = _ReflectTargetType(rec, t, (x, pt) => (postAggregateContinuation ?? ((y, _) => y))(DoPostAggregation(pt, q, x), pt));
            rec.ThrowOnLeftovers(t);
            q.ThrowOnLeftovers(t);
            return ast;
        }
        if (q.Empty)
            throw new ParsingException(q.WrapThrow($"Ran out of text when trying to create " +
                                                   $"an object of type {t.RName()}."));
        else if (t == tsm)
            ast = ReflectSM(q);
        else if (ReflectMethod(arg, t, q) is { } methodAST) {
            //this advances inside
            ast = methodAST;
        } else if (letFuncs.TryGetValue(t, out var f) && arg[0] == Parser.SM_REF_KEY_C) {
            q.Advance();
            ast = new AST.Preconstructed<object?>(q.GetLastPosition(), f(arg));
        } else if (FuncTypeResolve(q, arg, t) is { } simpleParsedAST) {
            q.Advance();
            ast = simpleParsedAST;
        } else if (FallThroughOptions.TryGetValue(t, out var ftmi)) {
            //MakeFallthrough allows the nested lookup to not be required to consume all post-aggregation.
            var ftype = ftmi.mi.Params[0].Type;
            try {
                ast = _ReflectTargetType(MakeFallthrough(q), ftype, postAggregateContinuation);
            } catch (Exception e) {
                throw new Exception(q.WrapThrowC(
                    $"Failed to construct an object of type {t.SimpRName()}. Instead, tried to construct a" +
                    $" similar object of type {ftype.SimpRName()}, but that also failed."), e);
            }
            ast = new AST.MethodInvoke(ast, ftmi.mi) { Type = AST.MethodInvoke.InvokeType.Fallthrough };
        } else if (TryCompileOption(t, out var cmp)) {
            ast = _ReflectTargetType(MakeFallthrough(q), cmp.source, postAggregateContinuation);
            ast = new AST.MethodInvoke(ast, cmp.mi) { Type = AST.MethodInvoke.InvokeType.Compiler };
        } else if (ResolveSpecialHandling(q, t) is {} specialTypeAST) {
            ast = specialTypeAST;
        } else if (t.IsArray)
            ast = ResolveAsArray(t.GetElementType()!, q);
        else if (MatchesGeneric(t, gtype_ienum))
            ast = ResolveAsArray(t.GenericTypeArguments[0], q);
        else if (CastToType(arg, t, out var x)) {
            ast = new AST.Preconstructed<object?>(q.GetLastPosition(), x);
            q.Advance();
        } else
            throw new Exception(q.WrapThrowC($"Couldn't convert the object in ≪≫ to type {t.SimpRName()}."));

        ast = DoPostAggregation(t, q, ast);
        q.ThrowOnLeftovers(t);
        if (q.Empty && postAggregateContinuation != null) {
            ast = postAggregateContinuation(ast, t);
        }
        return ast;
    }

    private static bool CastToType(string arg, Type rt, out object result) {
        if (arg == "_") {
            // Max value shortcut for eg. repeating until cancel
            if (rt == tint) {
                result = M.IntFloatMax;
                return true;
            }
        }
        try {
            result = Convert.ChangeType(arg, rt);
            return true;
        } catch (Exception) {
            result = null!;
            return false;
        }
    }

    private static IAST DoPostAggregation(Type rt, IParseQueue q, IAST result) {
        if (!q.AllowPostAggregate || q.Empty) return result;
        if (!postAggregators.TryGet2(rt, q.MaybeScan() ?? "", out _)) return result;
        var varStack1 = new StackList<IAST>();
        var varStack2 = new StackList<IAST>();
        var opStack1 = new StackList<PostAggregate>();
        var opStack2 = new StackList<PostAggregate>();
        varStack1.Push(result);
        while (!q.Empty && postAggregators.TryGet2(rt, q.MaybeScan() ?? "", out var pa)) {
            opStack1.Push(pa);
            var op = q.Next();
            try {
                varStack1.Push(_ReflectTargetType(q.NextChild(), pa.searchType));
            } catch (Exception e) {
                throw new InvokeException(
                    $"Tried to construct infix operator {op}, but could not parse the second argument.", e);
            }
        }
        while (opStack1.Count > 0) {
            varStack2.Clear();
            opStack2.Clear();
            varStack2.Push(varStack1[0]);
            var resolvePriority = opStack1.Min(o => o.priority);
            for (int ii = 0; ii < opStack1.Count; ++ii) {
                var op = opStack1[ii];
                if (op.priority == resolvePriority) {
                    varStack2.Push(
                        new AST.MethodInvoke(q.GetLastPosition(), op.sig, varStack2.Pop(), varStack1[ii + 1]) {Type = AST.MethodInvoke.InvokeType.PostAggregate });
                } else {
                    varStack2.Push(varStack1[ii + 1]);
                    opStack2.Push(op);
                }
            }
            (varStack1, varStack2) = (varStack2, varStack1);
            (opStack1, opStack2) = (opStack2, opStack1);
        }
        return varStack1.Pop();
    }
    
    public static MethodSignature? TryGetSignature<T>(string member) => 
        TryGetSignature(member, typeof(T));

    public static MethodSignature? TryGetSignature(string member, Type rt) {
        Profiler.BeginSample("Signature lookup");
        var res = ASTTryLookForMethod(rt, member) ??
               ASTTryLookForMethod(rt, Sanitize(member));
        Profiler.EndSample();
        return res;
    }

    private static IAST? ReflectMethod(string member, Type rt, IParseQueue q) {
        if (TryGetSignature(member, rt) is { } sig) {
            q.Advance();
            return sig.ToAST(q.GetLastPosition(), _FillASTArray(sig, q));
        }
        return null;
    }

    #endregion
}
}