using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DMath;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using ExFXY = System.Func<TEx<float>, TEx<float>>;

public static partial class Reflector {
    public static R LazyLoadAndReflectExternalSourceType<R>(Type containing, ParsingQueue q) {
        ReflConfig.RecordPublicByClass<R>(containing);
        return ReflectExternalSourceType<R>(containing, q);
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
    public static R ReflectExternalSourceType<R>(Type containing, ParsingQueue q) {
        try {
            var arg = q.Scan();
            if (_TryReflectRestrainedMethod(containing, typeof(R), q, out var res)) return (R) res;
            throw new StaticException($"No reflection handling exists for method {NameType(containing)}.{arg}.");
        } catch (Exception e) {
            throw Log.StackInnerException(e);
        }
    } 
    public static void FillInvokeArray(object[] invoke_args, int starti, Type[] prms, ParsingQueue q, 
        Type nameType, string methodName) {
        try {
            _FillInvokeArray(invoke_args, starti, prms, q, nameType, methodName);
        } catch (Exception e) {
            throw Log.StackInnerException(e);
        }
    }
    private static object[] _FillInvokeArray(object[] invoke_args, int starti, Type[] prms, ParsingQueue q, 
        Type nameType, [CanBeNull] string methodName) {
        int startQI = q.Index - 1;
        for (int ii = starti; ii < prms.Length; ++ii) {
            string arg = q.SoftScan(out int tempInd);
            try {
                invoke_args[ii] = _ReflectTargetType(q, prms[ii]);
            } catch (Exception ex) {
                methodName = string.IsNullOrWhiteSpace(methodName) ? nameType.RName() : $"{nameType.RName()}.{methodName}";
                if (arg == null) {
                    throw new InvokeException(
                        $"Line {q.GetLastLine(startQI)}: Tried to construct {methodName}, but ran out of text " +
                        $"looking for argument #{ii+1}/{prms.Length} of type {NameType(prms[ii])}.\n\t" +
                        $"{q.PrintLine(tempInd, true)}", ex);
                }
                throw new InvokeException(
                    $"Line {q.GetLastLine(startQI)}: Tried to construct {methodName}, but failed to cast argument " +
                    $"#{ii+1}/{prms.Length} \"{arg}\" (line {q.GetLastLine(tempInd)}) to type {NameType(prms[ii])}.\n\t" +
                    $"{q.PrintLine(tempInd, true)}", ex);
            }
        }
        return invoke_args;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object[] _FillInvokeArray(Type[] prms, ParsingQueue q, Type nameType, [CanBeNull] string methodName) 
        => _FillInvokeArray(new object[prms.Length], 0, prms, q, nameType, methodName);
    
    

    #region TargetTypeReflect

    public static T Into<T>(this string argstring) {
        using (var p = ParsingQueue.Lex(argstring)) {
            return p.Into<T>();
        }
    }

    public static T Into<T>(this ParsingQueue q) => (T) ReflectTargetType(q, typeof(T));

    private static object ReflectTargetType(ParsingQueue q, Type t) {
        try {
            return _ReflectTargetType(q, t);
        } catch (Exception e) {
            throw Log.StackInnerException(e);
        }
    }

    private static object _ReflectTargetType(ParsingQueue q, Type t) {
        if (t == tpsmp) {
            var obj = new PhaseProperties(q.queuedProps);
            q.queuedProps.Clear();
            return obj;
        }
        if (q.Empty()) throw new ParsingException($"Ran out of text when trying to create an object of type {NameType(t)}.");
        if (t == tsm) return ReflectSM(q);
        if (ReflConfig.RequiresMethodRefl(t)) return ReflectMethod(t, q);
        return CastToType(q, t);
    }
    private static bool _TryReflectMethod(Type rt, ParsingQueue q, out object result) {
        var member = q.Scan();
        var typs = TryLookForMethod(rt, member, out result) ?? TryLookForMethod(rt, member = Sanitize(member), out result);
        if (typs == null) return false;
        q.Next();
        result = result ?? InvokeMethod(rt, member, _FillInvokeArray(typs, q, rt, member));
        return true;
    }

    private static object ReflectMethod(Type rt, ParsingQueue q) {
        if (_TryReflectMethod(rt, q, out var res)) return res;
        throw new Exception($"Couldn't reflect method \"{q.Scan()}\" for type {rt.RName()}.");
    }

    private static bool _CanReflectRestrainedMethod(Type containing, Type rt, string member, out MethodInfo mi) {
        return ReflConfig.HasMember(containing, rt, member, out mi) ||
                ReflConfig.HasMember(containing, rt, Sanitize(member), out mi);
    }
    private static bool _TryReflectRestrainedMethod(Type containing, Type rt, ParsingQueue q, out object result) {
        result = null;
        var member = q.Scan();
        if (!_CanReflectRestrainedMethod(containing, rt, member, out var mi)) return false;
        var typs = ReflConfig.RecordLazyTypes(mi);
        q.Next();
        result = mi.Invoke(null, _FillInvokeArray(typs, q, rt, member));
        return true;
    }

    [CanBeNull]
    private static Type[] _GetTypesForRestrainedMethod(Type declaringClass, Type returnType, string member, out MethodInfo mi) => 
        _CanReflectRestrainedMethod(declaringClass, returnType, member, out mi) ?
            ReflConfig.RecordLazyTypes(mi) :
            null;
    
    #endregion
}