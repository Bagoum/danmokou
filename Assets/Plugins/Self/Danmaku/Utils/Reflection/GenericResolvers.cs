using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DMath;
using FParser;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using ExFXY = System.Func<TEx<float>, TEx<float>>;

public static partial class Reflector {
    public class ReflCtx {
        public struct Layer {
            public int paren;
            public bool isAggregating;

            public Layer(int p, bool agg) {
                paren = p;
                isAggregating = agg;
            }
            public static Layer Default => new Layer() { paren = 0, isAggregating = false };

            public Layer SubParen() => new Layer(paren - 1, isAggregating);
        }
        private readonly Stack<Layer> parenLayers = new Stack<Layer>();
        public Layer layer = Layer.Default;
        public readonly ParsingQueue q;
        public enum Strictness {
            NONE = 0,
            COMMAS = 1
        }
        public readonly ParsingProperties props;
        public bool AllowPostAggregate => !layer.isAggregating &&
            (layer.paren > 0 || (parenLayers.Count > 0 && parenLayers.Peek().paren > 0 && !parenLayers.Peek().isAggregating)) 
            && props.strict >= Strictness.COMMAS;

        public ReflCtx(ParsingQueue queue) {
            q = queue;
            List<ParsingProperty> properties = new List<ParsingProperty>();
            props = new ParsingProperties(new ParsingProperty[0]);
            while (q.Scan() == SMParser.PROP2_KW) {
                q.Next();
                properties.Add(this.Into<ParsingProperty>());
                if (!q.IsNewline()) throw new Exception($"Line {q.GetLastLine()} is missing a newline at the end of the the property declaration. Instead, it found \"{q.Scan()}\".");
            }
            props = new ParsingProperties(properties);
        }

        public void OpenLayer() {
            parenLayers.Push(layer);
            layer = Layer.Default;
        }

        public void ReadOpenParens() {
            while (ReadOneParen()) { }
        }

        public bool ReadOneParen() {
            if (q.SoftScan(out _).value == SMParser.PAREN_OPEN_KW) {
                ++layer.paren;
                q.Next();
                return true;
            } else return false;
        }

        public void CloseLayer() {
            CheckParens();
            layer = parenLayers.Pop();
        }
        public bool TryCurryParentParen() {
            if (parenLayers.Count > 0) {
                var parentParen = parenLayers.Pop();
                if (parentParen.paren > 0) {
                    parenLayers.Push(parentParen.SubParen());
                    return true;
                } else {
                    var success = TryCurryParentParen();
                    parenLayers.Push(parentParen);
                    return success;
                }
            } else return false;
        }

        private void CheckParens() {
            if (layer.paren > 0) throw new ParsingException(
                $"Line {q.GetLastLine(q.Index)}: The next character is \"{q.SoftScan(out _).display}\", " +
                $"when a closing parentheses was expected.\n\t" +
                $"{q.PrintLine(q.Index, true)}");
        }
    }
    public static R LazyLoadAndReflectExternalSourceType<R>(Type containing, ReflCtx ctx) {
        ReflConfig.RecordPublicByClass<R>(containing);
        return ReflectExternalSourceType<R>(containing, ctx);
    }
    public static bool LazyLoadAndCheckIfCanReflectExternalSourceType<R>(Type containing, string method) {
        ReflConfig.RecordPublicByClass<R>(containing);
        return _CanReflectRestrainedMethod(containing, typeof(R), method, out _);
    }

    [CanBeNull]
    public static Type[] LazyLoadAndGetSignature<R>(Type declaringClass, string method, out MethodInfo mi) {
        ReflConfig.RecordPublicByClass<R>(declaringClass);
        return _GetTypesForRestrainedMethod(declaringClass, typeof(R), method, out mi);
    }
    public static R ReflectExternalSourceType<R>(Type containing, ReflCtx ctx) {
        try {
            var arg = ctx.q.Scan();
            if (_TryReflectRestrainedMethod(containing, typeof(R), ctx, out var res)) return (R) res;
            throw new StaticException($"No reflection handling exists for method {NameType(containing)}.{arg}.");
        } catch (Exception e) {
            throw Log.StackInnerException(e);
        }
    } 
    public static void FillInvokeArray(object[] invoke_args, int starti, Type[] prms, ReflCtx ctx, 
        Type nameType, string methodName) {
        try {
            _FillInvokeArray(invoke_args, starti, prms, ctx, nameType, methodName);
        } catch (Exception e) {
            throw Log.StackInnerException(e);
        }
    }
    private static object[] _FillInvokeArray(object[] invoke_args, int starti, Type[] prms, ReflCtx ctx, 
        Type nameType, [CanBeNull] string methodName) {
        if (prms.Length == 0) return invoke_args;
        int startQI = ctx.q.Index - 1;
        ctx.q.SoftScan(out var tempInd);
        ctx.OpenLayer();
        ctx.ReadOneParen();
        for (int ii = starti; ii < prms.Length; ++ii) {
            var (arg, darg) = ctx.q.SoftScan(out tempInd);
            try {
                invoke_args[ii] = _ReflectTargetType(ctx, prms[ii]);
            } catch (Exception ex) {
                methodName = string.IsNullOrWhiteSpace(methodName) ? nameType.RName() : $"{nameType.RName()}.{methodName}";
                if (arg == null) {
                    throw new InvokeException(
                        $"Line {ctx.q.GetLastLine(startQI)}: Tried to construct {methodName}, but ran out of text " +
                        $"looking for argument #{ii+1}/{prms.Length} of type {NameType(prms[ii])}.\n\t" +
                        $"{ctx.q.PrintLine(tempInd, true)}", ex);
                }
                throw new InvokeException(
                    $"Line {ctx.q.GetLastLine(startQI)}: Tried to construct {methodName}, but failed to cast argument " +
                    $"#{ii+1}/{prms.Length} \"{darg}\" (line {ctx.q.GetLastLine(tempInd)}) to type {NameType(prms[ii])}.\n\t" +
                    $"{ctx.q.PrintLine(tempInd, true)}", ex);
            }
            while (ctx.q.SoftScan(out _).value == SMParser.PAREN_CLOSE_KW) {
                if (ctx.layer.paren == 0) {
                    //Don't overparse parens on last arg
                    if (ii == prms.Length - 1) break;
                    else if (!ctx.TryCurryParentParen())
                        throw new ParsingException(
                            $"Line {ctx.q.GetLastLine(startQI)}: Tried to construct {methodName}," +
                            $"but found too many closing parentheses after argument " +
                            $"#{ii + 1}/{prms.Length} \"{darg}\" (line {ctx.q.GetLastLine(tempInd)}).\n\t" +
                            $"{ctx.q.PrintLine(tempInd, true)}");
                } else --ctx.layer.paren;
                ctx.q.Next();
            }
            if (ii < prms.Length - 1) {
                if (ctx.q.SoftScan(out _).value == SMParser.ARGSEP_KW) {
                    if (ctx.layer.paren > 0) ctx.q.Next();
                    else throw new ParsingException(
                        $"Line {ctx.q.GetLastLine(startQI)}: Tried to construct {methodName}. After parsing argument " +
                        $"#{ii+1}/{prms.Length} \"{darg}\" (line {ctx.q.GetLastLine(tempInd)}), found a comma, " +
                        $"but there is no enclosing argument list.\n\t" +
                        $"{ctx.q.PrintLine(ctx.q.Index, true)}");
                } else if (ctx.layer.paren > 0 && ctx.props.strict >= ReflCtx.Strictness.COMMAS) {
                    throw new ParsingException(
                        $"Line {ctx.q.GetLastLine(startQI)}: Tried to construct {methodName}. After parsing argument " +
                        $"#{ii+1}/{prms.Length} \"{darg}\" (line {ctx.q.GetLastLine(tempInd)}), could not find a comma (found {ctx.q.SoftScan(out _).display} instead).\n\t" +
                        $"{ctx.q.PrintLine(ctx.q.Index, true, ",")}");
                }
            }
        }
        //In case of zero-argument functions, this is not called in the loop. eg. + x() 3
        while (ctx.layer.paren > 0 && ctx.q.SoftScan(out _).value == SMParser.PAREN_CLOSE_KW) {
            --ctx.layer.paren;
            ctx.q.Next();
        }
        ctx.CloseLayer();
        return invoke_args;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object[] _FillInvokeArray(Type[] prms, ReflCtx q, Type nameType, [CanBeNull] string methodName) 
        => _FillInvokeArray(new object[prms.Length], 0, prms, q, nameType, methodName);
    
    

    #region TargetTypeReflect

    public static T Into<T>(this string argstring) {
        using (var p = ParsingQueue.Lex(argstring)) {
            return new ReflCtx(p).Into<T>();
        }
    }

    public static T Into<T>(this ReflCtx ctx) => (T) ReflectTargetType(ctx, typeof(T));

    private static object ReflectTargetType(ReflCtx ctx, Type t) {
        try {
            return _ReflectTargetType(ctx, t);
        } catch (Exception e) {
            throw Log.StackInnerException(e);
        }
    }

    private static bool _ReflectTargetType_Method(ReflCtx ctx, Type t, bool allowUpcast, out object obj) {
        obj = null;
        if (!ReflConfig.RequiresMethodRefl(t)) return false;
        if (_TryReflectMethod(t, ctx, out obj)) return true;
        if (FallThroughOptions.TryGetValue(t, out var mis)) {
            foreach (var (ft, mi) in mis) {
                if (__RestrictReflectTargetType(ctx, ReflConfig.RecordLazyTypes(mi)[0], allowUpcast, out obj)) {
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
                if (__RestrictReflectTargetType(ctx, ReflConfig.RecordLazyTypes(mi)[0], false, out obj)) {
                    obj = mi.Invoke(null, new[] {obj});
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Top-level resolution function
    /// </summary>
    private static object _ReflectTargetType(ReflCtx ctx, Type t) {
        ctx.OpenLayer();
        ctx.ReadOpenParens();
        object obj;
        if (t == tpsmp) {
            obj = new PhaseProperties(ctx.q.queuedProps);
            ctx.q.queuedProps.Clear();
        } else if (ctx.q.Empty()) throw new ParsingException($"Ran out of text when trying to create an object of type {NameType(t)}.");
        else if (t == tsm) obj = ReflectSM(ctx);
        else if (_ReflectTargetType_Method(ctx, t, true, out obj)) {}
        else if (FuncTypeResolve(ctx.q.Scan(), t, out obj)) { ctx.q.Next(); }
        else if (ResolveSpecialHandling(ctx, t, out obj)) {}
        else if (t.IsArray) obj = ResolveAsArray(t.GetElementType(), ctx);
        else if (MatchesGeneric(t, gtype_ienum)) obj = ResolveAsArray(t.GenericTypeArguments[0], ctx);
        else obj = CastToType(ctx.q.Next(out int index), t, ctx.q.GetLastLine(index));
        
        
        obj = _PostAggregate(t, ctx, obj);
        while (ctx.layer.paren > 0 && ctx.q.SoftScan(out _).value == SMParser.PAREN_CLOSE_KW) {
            --ctx.layer.paren;
            ctx.q.Next();
            obj = _PostAggregate(t, ctx, obj);
        }
        ctx.CloseLayer();
        return obj;
    }
    private static bool __RestrictReflectTargetType(ReflCtx ctx, Type t, bool allowUpcast, out object obj) {
        if (ctx.q.Empty()) throw new ParsingException($"Ran out of text when trying to create an object of type {NameType(t)}.");
        else if (_ReflectTargetType_Method(ctx, t, allowUpcast, out obj)) {}
        else if (FuncTypeResolve(ctx.q.Scan(), t, out obj)) ctx.q.Next();
        else return false;
        
        obj = _PostAggregate(t, ctx, obj);
        while (ctx.layer.paren > 0 && ctx.q.SoftScan(out _).value == SMParser.PAREN_CLOSE_KW) {
            --ctx.layer.paren;
            ctx.q.Next();
            obj = _PostAggregate(t, ctx, obj);
        }
        return true;
    }

    private static object _PostAggregate(Type rt, ReflCtx ctx, object result) {
        if (!ctx.AllowPostAggregate || result == null || ctx.q.Empty()) return result;
        if (!postAggregators.TryGet2(rt, ctx.q.Scan(), out _)) return result;
        ctx.layer.isAggregating = true;
        var varStack1 = new StackList<object>();
        var varStack2 = new StackList<object>();
        var opStack1 = new StackList<PostAggregate>();
        var opStack2 = new StackList<PostAggregate>();
        varStack1.Push(result);
        while (!ctx.q.Empty() && postAggregators.TryGet2(rt, ctx.q.Scan(), out var pa)) {
            opStack1.Push(pa);
            var op = ctx.q.Next();
            try {
                varStack1.Push(_ReflectTargetType(ctx, pa.searchType));
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
        ctx.layer.isAggregating = false;
        return varStack1.Pop();
    }
    private static bool _TryReflectMethod(Type rt, ReflCtx ctx, out object result) {
        var member = ctx.q.Scan();
        result = null;
        var typs = TryLookForMethod(rt, member) ?? TryLookForMethod(rt, member = Sanitize(member));
        if (typs == null) return false;
        ctx.q.Next();
        result = InvokeMethod(ctx, rt, member, _FillInvokeArray(typs, ctx, rt, member));
        return true;
    }


    private static object ReflectMethod(Type rt, ReflCtx ctx) {
        if (_TryReflectMethod(rt, ctx, out var res)) return res;
        throw new Exception($"Couldn't reflect \"{ctx.q.ScanDisplay(out _)}\" as a method for type {rt.RName()}.");
    }

    private static bool _CanReflectRestrainedMethod(Type containing, Type rt, string member, out MethodInfo mi) {
        return ReflConfig.HasMember(containing, rt, member, out mi) ||
                ReflConfig.HasMember(containing, rt, Sanitize(member), out mi);
    }
    private static bool _TryReflectRestrainedMethod(Type containing, Type rt, ReflCtx ctx, out object result) {
        result = null;
        var member = ctx.q.Scan();
        if (!_CanReflectRestrainedMethod(containing, rt, member, out var mi)) return false;
        var typs = ReflConfig.RecordLazyTypes(mi);
        ctx.q.Next();
        result = mi.Invoke(null, _FillInvokeArray(typs, ctx, rt, member));
        return true;
    }

    [CanBeNull]
    private static Type[] _GetTypesForRestrainedMethod(Type declaringClass, Type returnType, string member, out MethodInfo mi) => 
        _CanReflectRestrainedMethod(declaringClass, returnType, member, out mi) ?
            ReflConfig.RecordLazyTypes(mi) :
            null;
    
    #endregion
}