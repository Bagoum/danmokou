using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DMK.Core;
using DMK.DMath;
using DMK.Expressions;
using JetBrains.Annotations;
using DMK.SM;
using DMK.SM.Parsing;
using UnityEngine;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExTP = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector3>>;
using ExBPRV2 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<DMK.DMath.V2RV2>>;

namespace DMK.Reflection {
public static partial class Reflector {
    public readonly struct NamedParam {
        public readonly Type type;
        public readonly string name;
        public readonly bool lookupMethod;

        public NamedParam(Type t, string n, bool lookupMethod) {
            type = t;
            name = n;
            this.lookupMethod = lookupMethod;
        }

        public override string ToString() => $"\"{name}\" (type {type.RName()})";

        public static implicit operator NamedParam(ParameterInfo pi) => 
            new NamedParam(pi.ParameterType, pi.Name, 
                Attribute.GetCustomAttributes(pi).Any(x => x is LookupMethodAttribute));
    }

    private delegate bool HasMember(string member);

    private delegate NamedParam[] TypeGet(string member);

    private delegate bool TryInvoke(IParseQueue q, string member, object[] prms, out object result);

    private static readonly Dictionary<Type, (HasMember has, TypeGet get, TryInvoke inv)> MathAllowedFuncify =
        new Dictionary<Type, (HasMember, TypeGet, TryInvoke)>();
    private static readonly Dictionary<Type, (HasMember has, TypeGet get, TryInvoke inv)> MathAllowed =
        new Dictionary<Type, (HasMember, TypeGet, TryInvoke)>();

    private static void AllowMath<ExR>() {
        MathAllowed[typeof(Func<TExPI, ExR>)] = (MathConfig.HasMember<Func<TExPI, ExR>, ExR>,
            MathConfig.LazyGetTypes<Func<TExPI, ExR>, ExR>, MathConfig.TryInvoke<Func<TExPI, ExR>>);
    }
    private static void AllowFuncMath<ExT, ExR>() {
        MathAllowedFuncify[typeof(Func<ExT, ExR>)] = (MathConfig.HasMember<ExR, ExR>, MathConfig.FuncifyTypes<ExT, ExR>,
            MathConfig.TryInvokeFunced<ExT, ExR>);
        ReflConfig.AddType(typeof(Func<ExT, ExR>));
    }

    [CanBeNull]
    private static NamedParam[] TryLookForMethod(Type rt, string member, bool allowUpcast = true) {
        if (ReflConfig.HasMember(rt, member, out _)) return ReflConfig.LazyGetTypes(rt, member);
        if (MathAllowedFuncify.TryGetValue(rt, out var fs) && fs.has(member)) return fs.get(member);
        if (MathAllowed.TryGetValue(rt, out fs) && fs.has(member)) return fs.get(member);
        if (letFuncs.TryGetValue(rt, out var f) && member[0] == Parser.SM_REF_KEY_C) return fudge;
        return null;
    }

    private static readonly Dictionary<Type, Func<string, object>> letFuncs =
        new Dictionary<Type, Func<string, object>>() {
            {typeof(ExBPY), ReflectEx.ReferenceLetBPI<float>},
            {typeof(ExTP), ReflectEx.ReferenceLetBPI<Vector2>},
            {typeof(ExTP3), ReflectEx.ReferenceLetBPI<Vector3>},
            {typeof(ExBPRV2), ReflectEx.ReferenceLetBPI<V2RV2>},
#if NO_EXPR
            {typeof(BPY), NoExprMath_2.ReferenceFloat}
#endif
        };

    [CanBeNull]
    private static object TryInvokeMethod([CanBeNull] IParseQueue q, Type rt, string member, object[] prms,
        bool allowUpcast = true) {
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

    public static object ExtInvokeMethod(Type t, string member, object[] prms) {
        if (CompileOptions.TryGetValue(t, out var compiler)) {
            return compiler.mi.Invoke(null, new[] {
                ExtInvokeMethod(compiler.source, member, prms)
            });
        } else if (t == typeof(StateMachine)) {
            return StateMachine.Create(member, prms);
        }
        member = member.ToLower();
        var result = TryInvokeMethod(null, t, member, prms, false);
        if (result != null) return result;
        //this also requires fallthrough support, which is handled through parent methods in the inner call.
        if (FallThroughOptions.TryGetValue(t, out var mis)) {
            foreach (var (ft, mi) in mis) {
                if ((result = TryInvokeMethod(null, ReflConfig.RecordLazyTypes(mi)[0].type, member, prms, false)) !=
                    null) {
                    return mi.Invoke(null, new[] {result});
                }
            }
        }
        throw new Exception($"External method invocation failed for type {t.RName()}, method {member}. " +
                            $"This is probably an error in static code.");
    }

    public static object ExtCompile(Type t, object obj) {
        if (CompileOptions.TryGetValue(t, out var compiler)) {
            return compiler.mi.Invoke(null, new[] {obj});
        } else
            return obj;
    }

    public static T ExtInvokeMethod<T>(string member, object[] prms) => (T) ExtInvokeMethod(typeof(T), member, prms);

    public static T ExtCompile<T>(object obj) => (T) ExtCompile(typeof(T), obj);

    public static T ExtConstructor<T>(object[] constructor_args, out bool automaticallyHandled) {
        automaticallyHandled = UseConstructor(typeof(T));
        return (T) Activator.CreateInstance(typeof(T), constructor_args);
    }


    private static readonly Dictionary<Type, (Type source, MethodInfo mi)> CompileOptions =
        new Dictionary<Type, (Type, MethodInfo)>();
    private static readonly Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>> FallThroughOptions =
        new Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>>();
    private static readonly Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>> UpwardsCastOptions =
        new Dictionary<Type, List<(FallthroughAttribute, MethodInfo)>>();

}
}