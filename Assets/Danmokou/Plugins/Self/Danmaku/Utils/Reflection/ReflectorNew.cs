using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DMath;
using Core;
using JetBrains.Annotations;
using SM;
using SM.Parsing;
using UnityEngine;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;

public static partial class Reflector {
    public readonly struct NamedParam {
        public readonly Type type;
        public readonly string name;

        public NamedParam(Type t, string n) {
            type = t;
            name = n;
        }

        public override string ToString() => $"\"{name}\" (type {type.RName()})";

        public static implicit operator NamedParam(ParameterInfo pi) => new NamedParam(pi.ParameterType, pi.Name);
    }
    private delegate bool HasMember(string member);
    private delegate NamedParam[] TypeGet(string member);
    private delegate bool TryInvoke(IParseQueue q, string member, object[] prms, out object result);
    private static readonly Dictionary<Type, (HasMember has, TypeGet get, TryInvoke inv)> MathAllowedFuncify = new Dictionary<Type, (HasMember, TypeGet, TryInvoke)>();
    private static readonly Dictionary<Type, (HasMember has, TypeGet get, TryInvoke inv)> MathAllowed = new Dictionary<Type, (HasMember, TypeGet, TryInvoke)>();

    private static void AllowMath<ExT, ExR>() {
        MathAllowedFuncify[typeof(Func<ExT, ExR>)] = (MathConfig.HasMember<ExR, ExR>, MathConfig.FuncifyTypes<ExT, ExR>, MathConfig.TryInvokeFunced<ExT, ExR>);
        MathAllowed[typeof(Func<ExT, ExR>)] = (MathConfig.HasMember<Func<ExT, ExR>, ExR>, MathConfig.LazyGetTypes<Func<ExT, ExR>, ExR>, MathConfig.TryInvoke<Func<ExT, ExR>>);
        ReflConfig.AddType(typeof(Func<ExT, ExR>));
    }

    [CanBeNull]
    private static NamedParam[] TryLookForMethod(Type rt, string member, bool allowUpcast=true) {
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
            {typeof(ExBPRV2), ReflectEx.ReferenceLetBPI<V2RV2>},
        #if NO_EXPR
            {typeof(BPY), NoExprMath_2.ReferenceFloat}
        #endif
        };

    [CanBeNull]
    private static object TryInvokeMethod([CanBeNull] IParseQueue q, Type rt, string member, object[] prms, bool allowUpcast=true) {
        if (ReflConfig.HasMember(rt, member, out _)) return ReflConfig.Invoke(rt, member, prms);
        if (MathAllowedFuncify.TryGetValue(rt, out var fs) && fs.inv(q, member, prms, out var res)) return res;
        if (MathAllowed.TryGetValue(rt, out fs) && fs.inv(q, member, prms, out res)) return res;
        if (letFuncs.TryGetValue(rt, out var f) && member[0] == Parser.SM_REF_KEY_C) return f(member);
        return null;
    }
    private static object InvokeMethod(IParseQueue q, Type rt, string member, object[] prms) => 
        TryInvokeMethod(q, rt, member, prms) ?? throw new Exception(
        $"Type handling passed but object creation failed for type {rt.RName()}, method {member}. " +
        "This is an internal error. Please report it.");

    public static T ExtInvokeMethod<T>(string member, IEnumerable<object> prms) =>
        (T) (TryInvokeMethod(null, typeof(T), member.ToLower(), prms.ToArray()) ?? throw new Exception(
            $"External method invocation failed for type {typeof(T).RName()}, method {member}. " +
            $"This is probably an error in static code."));
    
    
    private static readonly Dictionary<Type, (Type source, MethodInfo mi)> CompileOptions = 
        new Dictionary<Type, (Type, MethodInfo)>();
    private static readonly Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>> FallThroughOptions = 
        new Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>>();
    private static readonly Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>> UpwardsCastOptions = 
        new Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>>();
    

}