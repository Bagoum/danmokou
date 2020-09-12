using System;
using System.Collections.Generic;
using System.Reflection;
using DMath;
using Core;
using JetBrains.Annotations;
using UnityEngine;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;


public static partial class Reflector {
    private delegate bool HasMember(string member);
    private delegate Type[] TypeGet(string member);
    private delegate bool TryInvoke(ReflCtx ctx, string member, object[] prms, out object result);
    private static readonly Dictionary<Type, (HasMember has, TypeGet get, TryInvoke inv)> MathAllowedFuncify = new Dictionary<Type, (HasMember, TypeGet, TryInvoke)>();
    private static readonly Dictionary<Type, (HasMember has, TypeGet get, TryInvoke inv)> MathAllowed = new Dictionary<Type, (HasMember, TypeGet, TryInvoke)>();

    private static void AllowMath<ExT, ExR>() {
        MathAllowedFuncify[typeof(Func<ExT, ExR>)] = (MathConfig.HasMember<ExR, ExR>, MathConfig.FuncifyTypes<ExT, ExR>, MathConfig.TryInvokeFunced<ExT, ExR>);
        MathAllowed[typeof(Func<ExT, ExR>)] = (MathConfig.HasMember<Func<ExT, ExR>, ExR>, MathConfig.LazyGetTypes<Func<ExT, ExR>, ExR>, MathConfig.TryInvoke<Func<ExT, ExR>>);
        ReflConfig.AddType(typeof(Func<ExT, ExR>));
    }

    [CanBeNull]
    private static Type[] TryLookForMethod(Type rt, string member, bool allowUpcast=true) {
        if (ReflConfig.HasMember(rt, member, out _)) return ReflConfig.LazyGetTypes(rt, member);
        if (MathAllowedFuncify.TryGetValue(rt, out var fs) && fs.has(member)) return fs.get(member);
        if (MathAllowed.TryGetValue(rt, out fs) && fs.has(member)) return fs.get(member);
        if (letFuncs.TryGetValue(rt, out var f) && member[0] == Parser.SM_REF_KEY_C) return fudge;
        return null;
    }

    private static readonly Dictionary<Type, Func<string, object>> letFuncs = new Dictionary<Type, Func<string, object>>() {
            {typeof(ExBPY), ReflectEx.ReferenceLetBPI<float>},
            {typeof(ExTP), ReflectEx.ReferenceLetBPI<Vector2>},
            {typeof(ExTP3), ReflectEx.ReferenceLetBPI<Vector3>},
            {typeof(ExBPRV2), ReflectEx.ReferenceLetBPI<V2RV2>}
        };

    [CanBeNull]
    private static object TryInvokeMethod(ReflCtx ctx, Type rt, string member, object[] prms, bool allowUpcast=true) {
        if (ReflConfig.HasMember(rt, member, out _)) return ReflConfig.Invoke(rt, member, prms);
        if (MathAllowedFuncify.TryGetValue(rt, out var fs) && fs.inv(ctx, member, prms, out var res)) return res;
        if (MathAllowed.TryGetValue(rt, out fs) && fs.inv(ctx, member, prms, out res)) return res;
        if (letFuncs.TryGetValue(rt, out var f) && member[0] == Parser.SM_REF_KEY_C) return f(member);
        return null;
    }
    private static object InvokeMethod(ReflCtx ctx, Type rt, string member, object[] prms) => 
        TryInvokeMethod(ctx, rt, member, prms) ?? throw new Exception(
        $"Type handling passed but object creation failed for type {rt.RName()}, method {member}. " +
        "This is an internal error. Please report it.");
    
    
    private static readonly Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>> FallThroughOptions = 
        new Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>>();
    private static readonly Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>> UpwardsCastOptions = 
        new Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>>();
    

}