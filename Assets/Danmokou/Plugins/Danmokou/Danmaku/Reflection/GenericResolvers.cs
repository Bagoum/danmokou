using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BagoumLib;
using BagoumLib.DataStructures;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.SM;
using Danmokou.SM.Parsing;
using JetBrains.Annotations;

namespace Danmokou.Reflection {
public static partial class Reflector {
    public class ReflCtx {
        public enum Strictness {
            NONE = 0,
            COMMAS = 1
        }

        public readonly ParsingProperties props;
        public bool AllowPostAggregate => props.strict >= Strictness.COMMAS;
        public List<PhaseProperty> QueuedProps { get; } = new List<PhaseProperty>();
        
        public ReflCtx(IParseQueue q) {
            List<ParsingProperty> properties = new List<ParsingProperty>();
            props = new ParsingProperties(new ParsingProperty[0]);
            while (q.MaybeScan() == SMParser.PROP2_KW) {
                q.Advance();
                properties.Add(q.NextChild().Into<ParsingProperty>());
                if (!q.IsNewline)
                    throw new Exception(
                        $"Line {q.GetLastLine()} is missing a newline at the end of the the property declaration. Instead, it found \"{q.Scan()}\".");
            }
            props = new ParsingProperties(properties);
        }
    }


    /// <summary>
    /// Fill the argument array invoke_args by parsing elements from q according to type information in prms.
    /// <br/>Returns invoke_args.
    /// </summary>
    /// <param name="invoke_args">Argument array to fill.</param>
    /// <param name="starti">Index of invoke_args to start from.</param>
    /// <param name="prms">Type information of arguments.</param>
    /// <param name="q">Queue from which to parse elements.</param>
    /// <param name="nameType">The type that this argument array will eventually be used to construct.
    /// Used for error reporting.</param>
    /// <param name="methodName">The method by which nameType will be constructed, or null if using a constructor.
    /// Used for error reporting.</param>
    public static void FillInvokeArray(object?[] invoke_args, int starti, NamedParam[] prms, IParseQueue q,
        Type nameType, string methodName) {
        try {
            _FillInvokeArray(invoke_args, starti, prms, q, nameType, methodName);
        } catch (Exception e) {
            throw Exceptions.FlattenNestedException(e);
        }
    }
    private static object?[] _FillInvokeArray(object?[] invoke_args, int starti, NamedParam[] prms, IParseQueue q,
        Type nameType, string? methodName) {
        string MethodName() => string.IsNullOrWhiteSpace(methodName) ?
            nameType.RName() :
            $"{nameType.RName()}.{methodName}";
        int nargs = 0;
        for (int ii = starti; ii < prms.Length; ++ii) {
            if (!prms[ii].nonExplicit) ++nargs;
        }
        if (nargs == 0) {
            if (!(q is ParenParseQueue) && !q.Empty) {
                //Zero-arg functions may absorb empty parentheses
                if (q._SoftScan(out _)?.Item1 is SMParser.ParsedUnit.Paren p) {
                    if (p.Item.Length == 0) q.NextChild();
                }
            }
            return invoke_args;
        }
        if (!(q is ParenParseQueue)) {
            var c = q.ScanChild();
            // + (x) 3
            if (c is ParenParseQueue p && p.paren.Length == 1 && nargs != 1) {
            } else q = q.NextChild();
        }

        if (q is ParenParseQueue p2 && nargs != p2.paren.Length) {
            throw new ParsingException(p2.WrapThrow($"Expected {nargs} explicit arguments for {MethodName()}, " +
                                                    $"but the parentheses contains {p2.paren.Length}."));
        }


        void ThrowEmpty(IParseQueue lq, int ii) {
            if (lq.Empty) {
                throw new ParsingException(q.WrapThrowA(
                    $"Tried to construct {MethodName()}, but the parser ran out of text when looking for argument " +
                    $"#{ii + 1}/{prms.Length} {prms[ii]}. " +
                    "This probably means you have parentheses that do not enclose the entire function.",
                    $" | [Arg#{ii + 1} Missing]"));
            }
        }
        for (int ii = starti; ii < prms.Length; ++ii) {
            if (prms[ii].nonExplicit) {
                invoke_args[ii] = _ReflectNonExplicitParam(q.Ctx, prms[ii]);
            } else {
                ThrowEmpty(q, ii);
                var local = q.NextChild(out int ci);
                ThrowEmpty(local, ii);
                try {
                    invoke_args[ii] = _ReflectParam(local, prms[ii]);
                } catch (Exception ex) {
                    throw new InvokeException(
                        $"Line {q.GetLastLine(ci)}: Tried to construct {MethodName()}, " +
                        $"but failed to create argument #{ii + 1}/{prms.Length} {prms[ii]}.", ex);

                }
                local.ThrowOnLeftovers(() =>
                    $"Argument #{ii + 1}/{prms.Length} {prms[ii]} has extra text.");
            }
        }
        q.ThrowOnLeftovers(() => $"{MethodName()} has extra text after all {prms.Length} arguments.");
        return invoke_args;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object?[] _FillInvokeArray(NamedParam[] prms, IParseQueue q, Type nameType,
        string? methodName)
        => _FillInvokeArray(new object?[prms.Length], 0, prms, q, nameType, methodName);



    #region TargetTypeReflect

    /// <summary>
    /// Parse a string into an object of type T.
    /// <br/>May throw an exception if parsing fails.
    /// </summary>
    public static T Into<T>(this string argstring) => ((T) Into(argstring, typeof(T))!)!;
    
    /// <summary>
    /// Parse a string into an object of type T.
    /// <br/>May throw an exception if parsing fails.
    /// </summary>
    public static object? Into(this string argstring, Type t) {
        using var _ = BakeCodeGenerator.OpenContext(BakeCodeGenerator.CookingContext.KeyType.INTO, argstring);
        var p = IParseQueue.Lex(argstring);
        var ret = p.Into(t);
        p.ThrowOnLeftovers();
        return ret;
    }

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

    public static T Into<T>(this IParseQueue ctx) => ((T) Into(ctx, typeof(T))!)!;
    private static object? Into(this IParseQueue ctx, Type t) => ReflectTargetType(ctx, t);

    private static object? ReflectTargetType(IParseQueue ctx, Type t) {
        try {
            return _ReflectTargetType(ctx, t);
        } catch (Exception e) {
            throw Exceptions.FlattenNestedException(e);
        }
    }

    private static void RecurseParens(ref IParseQueue q, Type t) {
        while (q is ParenParseQueue p) {
            if (p.paren.Length == 1) q = p.NextChild();
            else
                throw new Exception(p.WrapThrow(
                    $"Tried to find an object of type {t.RName()}, but there is a parentheses with" +
                    $" {p.paren.Length} elements. Any parentheses should only have one element."));
        }
    }

    /// <summary>
    /// Returns true if the parse queue must be recursed.
    /// This occurs when an argument in a parenlist has parentheses, eg. f((g), 4).
    /// The parentheses around g will be detected by this.
    /// </summary>
    private static bool RecurseScan(IParseQueue q, out IParseQueue rec, out string val) {
        var (pu, pos) = q._Scan(out var ii);
        switch (pu) {
            case SMParser.ParsedUnit.Str s:
                val = s.Item;
                rec = null!;
                return false;
            case SMParser.ParsedUnit.Paren p:
                if (p.Item.Length != 1)
                    throw new Exception(q.WrapThrow(ii,
                        "This parentheses must have exactly one argument."));
                rec = new PUListParseQueue(p.Item[0], pos, q.Ctx);
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

    
    private static object _ReflectNonExplicitParam(ReflCtx ctx, NamedParam p) {
        if (p.type == tPhaseProperties) {
            var props = new PhaseProperties(ctx.QueuedProps);
            ctx.QueuedProps.Clear();
            return props;
        } else
            throw new StaticException($"No non-explicit reflection handling existsfor type {p.type.RName()}");
    }
    
    private static object? _ReflectParam(IParseQueue q, NamedParam p) {
        if (p.lookupMethod) {
            if (p.type.GenericTypeArguments.Length == 0) 
                throw new Exception("Method-Lookup parameter must be generic");
            RecurseParens(ref q, p.type);
            var method_str = q.Next().ToLower();
            q.ThrowOnLeftovers(p.type);
            //Not concerned with funced types, only the declared types.
            var funcAllTypes = p.type.GenericTypeArguments;
            var funcRetType = funcAllTypes[funcAllTypes.Length - 1];
            var funcPrmTypes = funcAllTypes.Take(funcAllTypes.Length - 1).ToArray();
            var methodPrmTypes = ReflectionData.GetArgTypes(funcRetType, method_str);
            if (funcPrmTypes.Length != methodPrmTypes.Length)
                throw new Exception($"Provided method {method_str} takes {funcPrmTypes.Length} parameters " +
                                    $"(required {methodPrmTypes.Length})");
            for (int ii = 0; ii < funcPrmTypes.Length; ++ii) {
                if (funcPrmTypes[ii] != methodPrmTypes[ii].type)
                    throw new Exception($"Provided method {method_str} has parameter #{ii + 1} as type" +
                                        $" {methodPrmTypes[ii].type.RName()} " +
                                        $"(required {funcPrmTypes[ii].RName()})");
            }
            var lambdaer = typeof(Reflector)
                               .GetMethod($"MakeLambda{funcPrmTypes.Length}", 
                                   BindingFlags.Static | BindingFlags.NonPublic)
                               ?.MakeGenericMethod(funcAllTypes) ??
                           throw new StaticException($"Couldn't find lambda constructor method for " +
                                                     $"count {funcPrmTypes.Length}");
            Func<object[], object> invoker = args => ReflectionData.Invoke(funcRetType, method_str, args);
            return lambdaer.Invoke(null, new object[] {invoker});
        } else {
            return _ReflectTargetType(q, p.type);
        }
    }

    private static readonly Type tPhaseProperties = typeof(PhaseProperties);
    
    /// <summary>
    /// Top-level resolution function.
    /// </summary>
    /// <param name="q">Parsing queue to read from.</param>
    /// <param name="t">Type to construct.</param>
    /// <param name="postAggregateContinuation">Optional code to execute after post-aggregation is complete.</param>
    private static object? _ReflectTargetType(IParseQueue q, Type t, Func<object?, Type, object?>? postAggregateContinuation=null) {
        RecurseParens(ref q, t);
        object? obj;
        if (RecurseScan(q, out var rec, out var arg)) {
            q.Advance();
            obj = _ReflectTargetType(rec, t, (x, pt) => (postAggregateContinuation ?? ((y, _) => y))(DoPostAggregate(pt, q, x), pt));
            rec.ThrowOnLeftovers(t);
            q.ThrowOnLeftovers(t);
            return obj;
        }
        if (q.Empty)
            throw new ParsingException(q.WrapThrow($"Ran out of text when trying to create " +
                                                   $"an object of type {NameType(t)}."));
        else if (t == tsm)
            obj = ReflectSM(q);
        else if (_TryReflectMethod(arg, t, q, out obj)) {
            //this advances inside
        } else if (q.AllowsScan && FuncTypeResolve(arg, t, out obj))
            q.Advance();
        else if (FallThroughOptions.TryGetValue(t, out var ftmi)) {
            //MakeFallthrough allows the nested lookup to not be required to consume all post-aggregation.
            var ftype = ftmi.mi.GetParameters()[0].ParameterType;
            try {
                obj = _ReflectTargetType(MakeFallthrough(q), ftype, postAggregateContinuation);
            } catch (Exception e) {
                throw new Exception(q.WrapThrowC($"Instead of constructing type {t.RName()}, tried to construct a" +
                                                 $" similar object of type {ftype.RName()}, but that also failed."), e);
            }
            obj = ftmi.mi.Invoke(null, new[] {obj});
        } else if (TryCompileOption(t, out var cmp)) {
            obj = _ReflectTargetType(MakeFallthrough(q), cmp.source, postAggregateContinuation);
            obj = cmp.mi.Invoke(null, new[] {obj});
        } else if (ResolveSpecialHandling(q, t, out obj)) {
            
        } else if (t.IsArray)
            obj = ResolveAsArray(t.GetElementType()!, q);
        else if (MatchesGeneric(t, gtype_ienum))
            obj = ResolveAsArray(t.GenericTypeArguments[0], q);
        else if (CastToType(arg, t, out obj))
            q.Advance();
        else
            throw new Exception(q.WrapThrowC($"Couldn't convert the object in ≪≫ to type {t.RName()}."));

        if (obj != null)
            obj = DoPostAggregate(t, q, obj);
        q.ThrowOnLeftovers(t);
        if (q.Empty && postAggregateContinuation != null) {
            obj = postAggregateContinuation(obj, t);
        }
        return obj;
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

    private static object? DoPostAggregate(Type rt, IParseQueue q, object? result) {
        if (!q.AllowPostAggregate || result == null || q.Empty) return result;
        if (!postAggregators.TryGet2(rt, q.MaybeScan() ?? "", out _)) return result;
        var varStack1 = new StackList<object?>();
        var varStack2 = new StackList<object?>();
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
                    varStack2.Push(op.Invoke(varStack2.Pop(), varStack1[ii + 1]));
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

    public static NamedParam[]? TryGetSignature<T>(ref string member) => 
        TryGetSignature(ref member, typeof(T));
    public static NamedParam[]? TryGetSignature(ref string member, Type rt) {
        var typs = TryLookForMethod(rt, member);
        if (typs != null)
            return typs;
        var smember = Sanitize(member);
        if ((typs = TryLookForMethod(rt, smember)) != null) {
            member = smember;
            return typs;
        }
        return null;
    }

    private static bool _TryReflectMethod(string member, Type rt, IParseQueue q, out object result) {
        result = null!;
        var typs = TryGetSignature(ref member, rt);
        if (typs == null) return false;
        q.Advance();
        result = InvokeMethod(q, rt, member, _FillInvokeArray(typs, q, rt, member));
        return true;
    }


    #endregion
}
}