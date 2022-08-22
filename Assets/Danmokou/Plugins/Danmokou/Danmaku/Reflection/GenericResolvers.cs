using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.SM;
using Danmokou.SM.Parsing;
using JetBrains.Annotations;
using Mizuhashi;
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
        public bool UseFileLinks { get; set; } = true;
        public List<IAST<PhaseProperty>> QueuedProps { get; } = new();
        
        public ReflCtx(IParseQueue q) {
            List<ParsingProperty> properties = new();
            while (q.MaybeScan() == SMParser.PROP2_KW) {
                q.Advance();
                var child = q.NextChild();
                properties.Add(child.Into<ParsingProperty>());
                if (!q.IsNewline)
                    throw child.WrapThrow($"Missing a newline at the end of the the property " +
                                        $"declaration. Instead, it found \"{q.Scan()}\".");
            }
            props = new ParsingProperties(properties);
        }

        public ReflCtx() {
            props = new ParsingProperties(Array.Empty<ParsingProperty>());
        }

        /// <summary>
        /// Generates a file link for the method signature if permitted by the
        /// <see cref="UseFileLinks"/> property, else just use the method name.
        /// </summary>
        public string AsFileLink(MethodSignature sig) => 
            UseFileLinks ? sig.FileLink : sig.TypeEnclosedName;

        public static ReflCtx Neutral = new ReflCtx();
    }


    /// <summary>
    /// Fill the argument array invoke_args by parsing elements from q according to type information in prms.
    /// </summary>
    /// <param name="asts">Argument array to fill.</param>
    /// <param name="starti">Index of invoke_args to start from.</param>
    /// <param name="sig">Type information of arguments.</param>
    /// <param name="q">Queue from which to parse elements.</param>
    public static (IAST[] asts, PositionRange? argRange) FillASTArray(IAST[] asts, int starti, MethodSignature sig, IParseQueue q) {
        int nargs = sig.ExplicitParameterCount;
        var prms = sig.Params;
        if (nargs == 0) {
            if (!(q is ParenParseQueue) && !q.Empty) {
                //Zero-arg functions may absorb empty parentheses
                if (q.MaybeGetCurrentUnit(out _) is SMParser.ParsedUnit.Paren p) {
                    if (p.Items.Length == 0) {
                        q.Advance();
                        return (asts, p.Position);
                    }
                }
            }
            return (asts, null);
        }
        if (!(q is ParenParseQueue)) {
            if (q.MaybeGetCurrentUnit(out _) is SMParser.ParsedUnit.Paren p && p.Items.Length == 1 && nargs != 1) {
                // mod | (x) 3
                //Leave the parentheses to be parsed by the first argument
            } else 
                // mod | (x, 3)
                //Use the parentheses to fill arguments
                //OR
                // mod | x 3
                //Use NLParseList to fill arguments
                q = q.NextChild();
        }

        if (q is ParenParseQueue p2 && nargs != p2.Items.Length) {
            throw p2.WrapThrow($"Expected {nargs} explicit arguments for {q.AsFileLink(sig)}, " +
                                $"but the parentheses contains {p2.Items.Length}.");
        }


        void ThrowEmpty(IParseQueue lq, int ii) {
            if (lq.Empty) {
                throw q.WrapThrowAppend(
                    $"Tried to construct {q.AsFileLink(sig)}, but the parser ran out of text when looking for argument " +
                    $"#{ii + 1}/{prms.Length} {prms[ii].SimplifiedDescription}. " +
                    "This probably means you have parentheses that do not enclose the entire function.",
                    $" | [Arg#{ii + 1} Missing]");
            }
        }
        for (int ii = starti; ii < prms.Length; ++ii) {
            if (prms[ii].NonExplicit) {
                asts[ii] = ReflectNonExplicitParam(q, prms[ii]);
            } else {
                ThrowEmpty(q, ii);
                var local = q.NextChild();
                ThrowEmpty(local, ii);
                void Fail(Either<AST.Failure, Exception?> nested) {
                    asts[ii] = new AST.Failure(local.Position,
                        $"Tried to construct {q.AsFileLink(sig)}, but failed to create argument " +
                        $"#{ii + 1}/{prms.Length} {prms[ii].SimplifiedDescription}.", prms[ii].Type, nested);
                }
                try {
                    if ((asts[ii] = ReflectParam(local, prms[ii])) is AST.Failure f)
                        Fail(f);
                } catch (Exception ex) {
                    Fail(ex);
                }
                var ii1 = ii;
                local.ThrowOnLeftovers(() =>
                    $"Argument #{ii1 + 1}/{prms.Length} {prms[ii1].SimplifiedDescription} has extra text.");
            }
        }
        q.ThrowOnLeftovers(() => $"{q.AsFileLink(sig)} has extra text after all {prms.Length} arguments.");
        return (asts, q is ParenParseQueue pq ? pq.Position : asts.ToRange());
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (IAST[] asts, PositionRange? argRange) FillASTArray(MethodSignature sig, IParseQueue q)
        => FillASTArray(new IAST[sig.Params.Length], 0, sig, q);

    

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
    public static T Into<T>(this string argstring) => ((T) Into(argstring, typeof(T))!);

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

    public static T Into<T>(this IParseQueue q) => ((T) Into(q, typeof(T))!);

    private static object? Into(this IParseQueue q, Type t) {
        Profiler.BeginSample("AST construction");
        var ast = q.IntoAST(t);
        Profiler.EndSample();
        //There's an odd circularity where the first constructed PUParseList
        // creates a ReflCtx, which then calls Into to construct parsing properties
        // *before* its constructor is complete and it gets assigned to q.Ctx.
        if (q.Ctx != null)
            foreach (var d in ast.WarnUsage(q.Ctx))
                d.Log();
        Profiler.BeginSample("AST realization");
        var obj = ast.EvaluateObject();
        Profiler.EndSample();
        return obj;
    }
    
    public static IAST<T> IntoAST<T>(this IParseQueue ctx) => new ASTRuntimeCast<T>(IntoAST(ctx, typeof(T)));
    private static IAST IntoAST(this IParseQueue ctx, Type t) => ReflectTargetType(ctx, t);

    /// <summary>
    /// Remove superfluous ParenParseQueue wrappers,
    /// eg. by converting ParenPQ (mod 4 2). into PUListPQ {mod 4 2}.
    /// </summary>
    private static void RecurseParens(ref IParseQueue q, Type t) {
        //Note: this will only ever remove one layer, since ParenPQ.NextChild always returns PUListPQ.
        //That is why we also need to run RecurseScan below.
        while (q is ParenParseQueue p) {
            if (p.Items.Length == 1) q = p.NextChild();
            else
                throw p.WrapThrow(
                    $"Tried to find an object of type {t.RName()}, but there is a parentheses with" +
                    $" {p.Items.Length} elements. Any parentheses should only have one element.");
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

    private static IAST ReflectNonExplicitParam(IParseQueue q, NamedParam p) {
        if (p.Type == tPhaseProperties) {
            var props = q.Ctx.QueuedProps.Count > 0 ?
                new ASTFmap<List<PhaseProperty>, PhaseProperties>(ps => new PhaseProperties(ps), 
                new AST.SequenceList<PhaseProperty>(
                    q.Ctx.QueuedProps[0].Position.Merge(q.Ctx.QueuedProps[^1].Position), 
                    q.Ctx.QueuedProps.ToList())) :
                (IAST<PhaseProperties>)new AST.Preconstructed<PhaseProperties>(q.Position,
                    new PhaseProperties(Array.Empty<PhaseProperty>()), "No phase properties");
            q.Ctx.QueuedProps.Clear();
            return props;
        } else
            throw new StaticException($"No non-explicit reflection handling existsfor type {p.Type.RName()}");
    }

    private static IAST ReflectParam(IParseQueue q, NamedParam p) {
        if (p.LookupMethod) {
            if (p.Type.GenericTypeArguments.Length == 0) 
                throw new StaticException("Method-Lookup parameter must be generic");
            RecurseParens(ref q, p.Type);
            var (method, loc) = q.NextUnit(out _);
            q.ThrowOnLeftovers(p.Type);
            return new AST.MethodLookup(loc, p.Type, method.ToLower());
        } else {
            return ReflectTargetType(q, p.Type);
        }
    }

    private static readonly Type tPhaseProperties = typeof(PhaseProperties);
    
    /// <summary>
    /// Top-level resolution function to create an object from a parse queue.
    /// </summary>
    /// <param name="q">Parsing queue to read from.</param>
    /// <param name="t">Type to construct.</param>
    /// <param name="postAggregateContinuation">Optional code to execute after post-aggregation is complete.</param>
    private static IAST ReflectTargetType(IParseQueue q, Type t, Func<IAST, Type, IAST>? postAggregateContinuation=null) {
        //While try/catch is not the best way to handle partial parses,
        // the 'correct' way would be using an Either-like monad with extensive bind operations,
        // which is not possible in C#. 
        try {
            RecurseParens(ref q, t);
            IAST? ast;
            var pu = q.GetCurrentUnit(out var index);
            if (pu is SMParser.ParsedUnit.Paren p) {
                //given `PUList | (mod) 1 2`, the next child is (mod), which must be recursed.
                //However, we cannot accept `PUList | (mod, 1) 2` as this is not an arglist.
                if (p.Items.Length != 1)
                    throw q.WrapThrowHighlight(index, "This parentheses must have exactly one argument.");
                var rec = new PUListParseQueue(p.Items[0], q.Ctx);
                q.Advance();
                ast = ReflectTargetType(
                    rec, t, (x, pt) => (postAggregateContinuation ?? ((y, _) => y))(DoPostAggregation(pt, q, x), pt));
                rec.ThrowOnLeftovers(t);
                q.ThrowOnLeftovers(t);
                return ast;
            }
            var arg = (pu as SMParser.ParsedUnit.Str) ?? throw new StaticException($"Couldn't result {pu.GetType()}");
            if (q.Empty)
                throw q.WrapThrow($"Ran out of text when trying to create an object of type {t.RName()}.");
            else if (t == tsm)
                ast = ReflectSM(q);
            else if (ReflectMethod(arg, t, q) is { } methodAST) {
                //this advances inside
                ast = methodAST;
            } else if (letFuncs.TryGetValue(t, out var f) && arg.Item[0] == Parser.SM_REF_KEY_C) {
                q.Advance();
                ast = new AST.Preconstructed<object?>(arg.Position, f(arg.Item), arg.Item);
            } else if (FuncTypeResolve(q, arg, t) is { } simpleParsedAST) {
                q.Advance();
                ast = simpleParsedAST;
            } else if (FallThroughOptions.TryGetValue(t, out var ftmi)) {
                //MakeFallthrough allows the nested lookup to not be required to consume all post-aggregation.
                var ftype = ftmi.mi.Params[0].Type;
                try {
                    ast = ReflectTargetType(MakeFallthrough(q), ftype, postAggregateContinuation);
                } catch (Exception e) {
                    throw q.WrapThrowHighlight(index,
                        $"Failed to construct an object of type {t.SimpRName()}. Instead, tried to construct a" +
                        $" similar object of type {ftype.SimpRName()}, but that also failed.", e);
                }
                ast = new AST.MethodInvoke(ast, ftmi.mi) { Type = AST.MethodInvoke.InvokeType.Fallthrough };
            } else if (TryCompileOption(t, out var cmp)) {
                ast = ReflectTargetType(MakeFallthrough(q), cmp.source, postAggregateContinuation);
                ast = new AST.MethodInvoke(ast, cmp.mi) { Type = AST.MethodInvoke.InvokeType.Compiler };
            } else if (ResolveSpecialHandling(q, t) is { } specialTypeAST) {
                ast = specialTypeAST;
            } else if (t.IsArray)
                ast = ResolveAsArray(t.GetElementType()!, q);
            else if (MatchesGeneric(t, gtype_ienum))
                ast = ResolveAsArray(t.GenericTypeArguments[0], q);
            else if (CastToType(arg.Item, t, out var x)) {
                ast = new AST.Preconstructed<object?>(arg.Position, x);
                q.Advance();
            } else {
                q.Advance(); //improves error printing position accuracy
                throw q.WrapThrowHighlight(index, $"Couldn't convert the object in ≪≫ to type {t.SimpRName()}.");
            }

            ast = DoPostAggregation(t, q, ast);
            q.ThrowOnLeftovers(t);
            if (q.Empty && postAggregateContinuation != null) {
                ast = postAggregateContinuation(ast, t);
            }
            return ast;
        } catch (Exception e) {
            if (e is ReflectionException re)
                return new AST.Failure(re, t);
            return new AST.Failure(new ReflectionException(q.Position, e.Message, e.InnerException), t);
        }
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
        var opStack1 = new StackList<(PostAggregate pa, PositionRange loc)>();
        var opStack2 = new StackList<(PostAggregate pa, PositionRange loc)>();
        varStack1.Push(result);
        while (!q.Empty && postAggregators.TryGet2(rt, q.MaybeScan() ?? "", out var pa)) {
            var op = q.NextUnit(out var opInd);
            opStack1.Push((pa, op.Position));
            try {
                varStack1.Push(ReflectTargetType(q.NextChild(), pa.searchType));
            } catch (Exception e) {
                throw q.WrapThrowHighlight(opInd, 
                    $"Tried to construct infix operator {op}, but could not parse the second argument.", e);
            }
        }
        while (opStack1.Count > 0) {
            varStack2.Clear();
            opStack2.Clear();
            varStack2.Push(varStack1[0]);
            var resolvePriority = opStack1.Min(o => o.pa.priority);
            for (int ii = 0; ii < opStack1.Count; ++ii) {
                var op = opStack1[ii];
                if (op.pa.priority == resolvePriority) {
                    var arg1 = varStack2.Pop();
                    var arg2 = varStack1[ii + 1];
                    varStack2.Push(
                        new AST.MethodInvoke(arg1.Position.Merge(arg2.Position), op.loc, op.pa.sig, arg1, arg2) 
                            {Type = AST.MethodInvoke.InvokeType.PostAggregate });
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

    private static IAST? ReflectMethod(SMParser.ParsedUnit.Str member, Type rt, IParseQueue q) {
        if (TryGetSignature(member.Item, rt) is { } sig) {
            q.Advance();
            var (args, argsLoc) = FillASTArray(sig, q);
            return sig.ToAST(member.Position.Merge(argsLoc ?? member.Position), member.Position, args);
        }
        return null;
    }

    #endregion
}
}