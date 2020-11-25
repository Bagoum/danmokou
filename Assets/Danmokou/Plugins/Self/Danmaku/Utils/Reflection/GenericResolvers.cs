using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DMath;
using FParser;
using JetBrains.Annotations;
using SM;
using SM.Parsing;
using UnityEngine;
using ExFXY = System.Func<TEx<float>, TEx<float>>;

public static partial class Reflector {
    public class ReflCtx {
        public enum Strictness {
            NONE = 0,
            COMMAS = 1
        }
        public readonly ParsingProperties props;
        public bool AllowPostAggregate => props.strict >= Strictness.COMMAS;
        //public bool UnparsedFaulted { get; set; }
        
        public List<PhaseProperty> QueuedProps { get; } = new List<PhaseProperty>();

        public ReflCtx(IParseQueue q) {
            List<ParsingProperty> properties = new List<ParsingProperty>();
            props = new ParsingProperties(new ParsingProperty[0]);
            while (q.MaybeScan() == SMParser.PROP2_KW) {
                q.Advance();
                properties.Add(q.NextChild().Into<ParsingProperty>());
                if (!q.IsNewline) throw new Exception($"Line {q.GetLastLine()} is missing a newline at the end of the the property declaration. Instead, it found \"{q.Scan()}\".");
            }
            props = new ParsingProperties(properties);
        }

    }
    public static R LazyLoadAndReflectExternalSourceType<R>(Type containing, IParseQueue q) {
        ReflConfig.RecordPublicByClass<R>(containing);
        return ReflectExternalSourceType<R>(containing, q);
    }
    public static bool LazyLoadAndCheckIfCanReflectExternalSourceType<R>(Type containing, string method) {
        ReflConfig.RecordPublicByClass<R>(containing);
        return _CanReflectRestrainedMethod(containing, typeof(R), method, out _);
    }

    [CanBeNull]
    public static NamedParam[] LazyLoadAndGetSignature<R>(Type declaringClass, string method, out MethodInfo mi) {
        ReflConfig.RecordPublicByClass<R>(declaringClass);
        return _GetTypesForRestrainedMethod(declaringClass, typeof(R), method, out mi);
    }
    public static R ReflectExternalSourceType<R>(Type containing, IParseQueue q) {
        try {
            if (_TryReflectRestrainedMethod(containing, typeof(R), q, out var res)) return (R) res;
            throw new StaticException(q.WrapThrow($"No reflection handling exists for this object in type {NameType(containing)}."));
        } catch (Exception e) {
            throw Log.StackInnerException(e);
        }
    } 
    public static void FillInvokeArray(object[] invoke_args, int starti, NamedParam[] prms, IParseQueue q, 
        Type nameType, string methodName) {
        try {
            _FillInvokeArray(invoke_args, starti, prms, q, nameType, methodName);
        } catch (Exception e) {
            throw Log.StackInnerException(e);
        }
    }
    private static object[] _FillInvokeArray(object[] invoke_args, int starti, NamedParam[] prms, IParseQueue q, 
        Type nameType, [CanBeNull] string methodName) {
        string MethodName() => string.IsNullOrWhiteSpace(methodName) ?
            nameType.RName() :
            $"{nameType.RName()}.{methodName}";
        var nargs = prms.Length - starti;
        if (nargs == 0) {
            if (!(q is ParenParseQueue) && !q.Empty) {
                switch (q._SoftScan(out _)?.Item1) {
                    case SMParser.ParsedUnit.P p:
                        //Zero-arg functions may absorb empty parentheses
                        if (p.Item.Length == 0) q.NextChild();
                        break;
                }
            }
            return invoke_args;
        }
        if (!(q is ParenParseQueue)) {
            var c = q.ScanChild();
            // + (x) 3
            if (c is ParenParseQueue p && p.paren.Length == 1 && nargs != 1) {}
            else q = q.NextChild();
        }

        if (q is ParenParseQueue p2 && nargs != p2.paren.Length) {
            throw new ParsingException(p2.WrapThrow($"Expected {nargs} arguments for {MethodName()}, " +
                                                    $"but the parentheses contains {p2.paren.Length}."));
        }

        
        void ThrowEmpty(IParseQueue lq, int ii) {
            if (lq.Empty) {
                throw new ParsingException(q.WrapThrowA(
                    $"Tried to construct {MethodName()}, but the parser ran out of text when looking for argument " +
                    $"#{ii+1}/{prms.Length} {prms[ii]}. " +
                    "This probably means you have parentheses that do not enclose the entire function.", 
                    $" | [Arg#{ii+1} Missing]"));
            }
        }
        for (int ii = starti; ii < prms.Length; ++ii) {
            ThrowEmpty(q, ii);
            var local = q.NextChild(out int ci);
            ThrowEmpty(local, ii);
            try {
                invoke_args[ii] = _ReflectTargetType(local, prms[ii].type);
            } catch (Exception ex) {
                throw new InvokeException(
                    $"Line {q.GetLastLine(ci)}: Tried to construct {MethodName()}, " +
                    $"but failed to create argument #{ii + 1}/{prms.Length} {prms[ii]}.", ex);
                
            }
            local.ThrowOnLeftovers(() => 
                $"Argument #{ii + 1}/{prms.Length} {prms[ii]} has extra text.");
        }
        q.ThrowOnLeftovers(() => $"{MethodName()} has extra text after all {prms.Length} arguments.");
        return invoke_args;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object[] _FillInvokeArray(NamedParam[] prms, IParseQueue q, Type nameType, [CanBeNull] string methodName) 
        => _FillInvokeArray(new object[prms.Length], 0, prms, q, nameType, methodName);
    
    

    #region TargetTypeReflect

    public static T Into<T>(this string argstring) {
        var p = IParseQueue.Lex(argstring);
        var ret = p.Into<T>();
        p.ThrowOnLeftovers();
        return ret;
    }
    
    [CanBeNull]
    public static T IntoIfNotNull<T>(this string argstring) where T: class {
        if (string.IsNullOrWhiteSpace(argstring)) return null;
        var p = IParseQueue.Lex(argstring);
        var ret = p.Into<T>();
        p.ThrowOnLeftovers();
        return ret;
    }

    public static T Into<T>(this IParseQueue ctx) => (T) ReflectTargetType(ctx, typeof(T));

    private static object ReflectTargetType(IParseQueue ctx, Type t) {
        try {
            return _ReflectTargetType(ctx, t);
        } catch (Exception e) {
            throw Log.StackInnerException(e);
        }
    }

    private static bool _ReflectTargetType_Method(string member, IParseQueue ctx, Type t, bool allowUpcast, out object obj) {
        obj = null;
        if (!ReflConfig.RequiresMethodRefl(t)) return false;
        if (_TryReflectMethod(member, t, ctx, out obj)) return true;
        if (FallThroughOptions.TryGetValue(t, out var mis)) {
            foreach (var (ft, mi) in mis) {
                if (__RestrictReflectTargetType(ctx, ReflConfig.RecordLazyTypes(mi)[0].type, allowUpcast, out obj)) {
                    obj = mi.Invoke(null, new[] {obj});
                    return true;
                }
            }
        }
        /*
        if (preAggregators.TryGetValue(t, out var pa)) {
            ctx.layer.isAggregating = true;
            var line = ctx.q.Index;
            object obj1;
            try {
                __RestrictReflectTargetType(ctx, pa.firstType, false, out obj1);
            } catch (Exception e) {
                throw new InvokeException(
                    $"Line {ctx.q.GetLastLine(line)}: Tried to create a {t.RName()} by first creating " +
                    $"type {pa.firstType.RName()}, but creating this object failed.\n\t{ctx.q.PrintLine(line, true)}", e);
            }
            if (obj1 != null) {
                foreach (var cont in pa.resolvers) {
                    if (ctx.q.Scan() == cont.op) {
                        ctx.q.Next();
                        try {
                            if (!__RestrictReflectTargetType(ctx, cont.secondType, false, out var obj2)) {
                                throw new Exception();
                            }
                            obj = cont.Invoke(obj1, obj2);
                        } catch (Exception e) {
                            throw new InvokeException(
                                $"Line {ctx.q.GetLastLine(line)}: Tried to create a {t.RName()} via the function " +
                                $"{pa.firstType.RName()} {cont.op} {cont.secondType.RName()}, but " +
                                $"couldn't create the second object.\n\t{ctx.q.PrintLine(line, true)}", e);
                        }
                    }
                }
            }
            ctx.layer.isAggregating = false;
            if (obj != null) return true;
        }*/
        if (allowUpcast && UpwardsCastOptions.TryGetValue(t, out mis)) {
            foreach (var (ft, mi) in mis) {
                if (__RestrictReflectTargetType(ctx, ReflConfig.RecordLazyTypes(mi)[0].type, false, out obj)) {
                    obj = mi.Invoke(null, new[] {obj});
                    return true;
                }
            }
        }
        return false;
    }

    private static void RecurseParens(ref IParseQueue q, Type t) {
        while (q is ParenParseQueue p) {
            if (p.paren.Length == 1) q = p.NextChild();
            else throw new Exception(p.WrapThrow(
                $"Tried to find an object of type {t.RName()}, but there is a parentheses with" +
                $" {p.paren.Length} elements. Any parentheses should only have one element."));
        }
    }

    /// <summary>
    /// Returns true if the parse queue must be recursed.
    /// </summary>
    private static bool RecurseScan(IParseQueue q, out IParseQueue rec, out string val) {
        var (pu, pos) = q._Scan(out var ii);
        switch (pu) {
            case SMParser.ParsedUnit.S s:
                val = s.Item;
                rec = null;
                return false;
            case SMParser.ParsedUnit.P p:
                if (p.Item.Length != 1) throw new Exception(q.WrapThrow(ii, 
                    "This parentheses must have exactly one argument."));
                rec = new PUListParseQueue(p.Item[0], pos, q.Ctx);
                val = null;
                return true;
            default:
                throw new StaticException(q.WrapThrow(ii,
                    $"Couldn't resolve parser object type {pu.GetType()}."));
        }
    }

    /// <summary>
    /// Top-level resolution function
    /// </summary>
    private static object _ReflectTargetType(IParseQueue q, Type t) {
        if (CompileOptions.TryGetValue(t, out var compiler)) {
            return compiler.mi.Invoke(null, new[] {_ReflectTargetType(q, compiler.source)});
        }
        RecurseParens(ref q, t);
        object obj;
        if (RecurseScan(q, out var rec, out var arg)) {
            obj = _ReflectTargetType(rec, t); 
            rec.ThrowOnLeftovers(t);
            q.Advance();
            obj = _PostAggregate(t, q, obj);
            q.ThrowOnLeftovers(t);
            return obj;
        }
        if (q.Empty) throw new ParsingException(q.WrapThrow($"Ran out of text when trying to create " +
                                                            $"an object of type {NameType(t)}."));
        else if (t == tsm) obj = ReflectSM(q);
        else if (_ReflectTargetType_Method(arg, q, t, true, out obj)) {} //this advances inside
        else if (q.AllowsScan && FuncTypeResolve(arg, t, out obj)) { q.Advance(); }
        else if (ResolveSpecialHandling(q, t, out obj)) {}
        else if (t.IsArray) obj = ResolveAsArray(t.GetElementType(), q);
        else if (MatchesGeneric(t, gtype_ienum)) obj = ResolveAsArray(t.GenericTypeArguments[0], q);
        else if (CastToType(arg, t, out obj)) { q.Advance(); }
        else throw new Exception(q.WrapThrowC($"Couldn't convert the object in ≪≫ to type {t.RName()}."));
        
        obj = _PostAggregate(t, q, obj);
        q.ThrowOnLeftovers(t);
        return obj;
    }
    private static bool __RestrictReflectTargetType(IParseQueue q, Type t, bool allowUpcast, out object obj) {
        if (CompileOptions.TryGetValue(t, out var compiler)) {
            obj = compiler.mi.Invoke(null, new[] {_ReflectTargetType(q, compiler.source)});
            return true;
        }
        RecurseParens(ref q, t);
        if (RecurseScan(q, out var rec, out var arg)) {
            obj = __RestrictReflectTargetType(rec, t, allowUpcast, out obj); 
            rec.ThrowOnLeftovers(t);
            q.Advance();
            obj = _PostAggregate(t, q, obj);
            q.ThrowOnLeftovers(t);
            return true;
        }
        if (q.Empty) throw new ParsingException(q.WrapThrow($"Ran out of text when trying to create " +
                                                              $"an object of type {NameType(t)}."));
        else if (_ReflectTargetType_Method(arg, q, t, allowUpcast, out obj)) {} //this advances inside
        else if (q.AllowsScan && FuncTypeResolve(arg, t, out obj)) { q.Advance();}
        else return false;
        
        obj = _PostAggregate(t, q, obj);
        return true;
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
            result = null;
            return false;
        }
    }

    private static object _PostAggregate(Type rt, IParseQueue q, object result) {
        if (!q.AllowPostAggregate || result == null || q.Empty) return result;
        if (!postAggregators.TryGet2(rt, q.MaybeScan() ?? "", out _)) return result;
        var varStack1 = new StackList<object>();
        var varStack2 = new StackList<object>();
        var opStack1 = new StackList<PostAggregate>();
        var opStack2 = new StackList<PostAggregate>();
        varStack1.Push(result);
        while (!q.Empty && postAggregators.TryGet2(rt, q.MaybeScan() ?? "", out var pa)) {
            opStack1.Push(pa);
            var op = q.Next();
            try {
                varStack1.Push(_ReflectTargetType(q.NextChild(), pa.searchType));
            } catch (Exception e) {
                throw new InvokeException($"Tried to construct infix operator {op}, but could not parse the second argument.", e);
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

    private delegate bool Resolver(IParseQueue q, out object result);

    private static bool _TryReflectMethod(string member, Type rt, IParseQueue q, out object result) {
        result = null;
        var typs = TryLookForMethod(rt, member) ?? TryLookForMethod(rt, member = Sanitize(member));
        if (typs == null) return false;
        q.Advance();
        result = InvokeMethod(q, rt, member, _FillInvokeArray(typs, q, rt, member));
        return true;
    }

    private static bool _CanReflectRestrainedMethod(Type containing, Type rt, string member, out MethodInfo mi) {
        return ReflConfig.HasMember(containing, rt, member, out mi) ||
                ReflConfig.HasMember(containing, rt, Sanitize(member), out mi);
    }
    private static bool _TryReflectRestrainedMethod(Type containing, Type rt, IParseQueue q, out object result) {
        if (RecurseScan(q, out var rec, out var member)) {
            if (_TryReflectRestrainedMethod(containing, rt, rec, out result)) {
                rec.ThrowOnLeftovers(rt);
                q.Advance();
                return true;
            } else return false;
        }
        result = null;
        if (!_CanReflectRestrainedMethod(containing, rt, member, out var mi)) return false;
        var typs = ReflConfig.RecordLazyTypes(mi);
        q.Advance();
        result = mi.Invoke(null, _FillInvokeArray(typs, q, rt, member));
        return true;
    }

    [CanBeNull]
    private static NamedParam[] _GetTypesForRestrainedMethod(Type declaringClass, Type returnType, 
        string member, out MethodInfo mi) => 
        _CanReflectRestrainedMethod(declaringClass, returnType, member, out mi) ?
            ReflConfig.RecordLazyTypes(mi) :
            null;
    
    #endregion
}