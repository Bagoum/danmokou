using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using JetBrains.Annotations;
using Danmokou.SM;
using Danmokou.SM.Parsing;
using UnityEngine;
using static BagoumLib.Reflection.ReflectionUtils;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector3>>;
using ExBPRV2 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>>;

namespace Danmokou.Reflection {
public static partial class Reflector {
    public readonly struct NamedParam {
        public readonly Type type;
        public readonly string name;
        public readonly bool lookupMethod;
        public readonly bool nonExplicit;

        public NamedParam(Type t, string n, bool lookupMethod, bool nonExplicit) {
            type = t;
            name = n;
            this.lookupMethod = lookupMethod;
            this.nonExplicit = nonExplicit;
        }

        public override string ToString() => $"\"{name}\" (type {type.RName()})";

        public static implicit operator NamedParam(ParameterInfo pi) => 
            new NamedParam(pi.ParameterType, pi.Name, 
                Attribute.GetCustomAttributes(pi).Any(x => x is LookupMethodAttribute),
                Attribute.GetCustomAttributes(pi).Any(x => x is NonExplicitParameterAttribute));

        public NamedParam WithType(Type t) => new NamedParam(t, name, lookupMethod, nonExplicit);
    }
    
    /// <summary>
    /// Within the context of a given return type, a function that returns whether or not a function named 'member'
    /// matching that return type exists.
    /// </summary>
    private delegate bool HasMember(string member);
    
    /// <summary>
    /// Within the context of a given return type, a function that returns the parameter types for a function
    ///  named 'member' that has the given return type.
    /// </summary>
    private delegate NamedParam[] TypeGet(string member);

    /// <summary>
    /// Within the context of a given return type, a function that tries to execute the function 'member'
    ///  to construct a value of that type.
    /// </summary>
    /// <param name="q">Parsing queue. Not required, used only for syntax-related warnings.</param>
    /// <param name="member">Name of the function to execute.</param>
    /// <param name="prms">Arguments to provide to the function.</param>
    /// <param name="result">Out value in which the return value of 'member' is stored.</param>
    private delegate bool TryInvoke(IParseQueue? q, string member, object?[] prms, out object result);

    /// <summary>
    /// A dictionary containing reflection information for types that can be "funcified", where
    /// funcification transforms a function (A,B,C...)->R into a function (A',B',C'...)->(T->R), where
    /// A', B', C' may relate to the introduced type T.
    /// <br/>The keys are of the form type(T->R).
    /// </summary>
    private static readonly Dictionary<Type, (HasMember has, TypeGet get, TryInvoke inv)> funcifiableTypes =
        new Dictionary<Type, (HasMember, TypeGet, TryInvoke)>();
    private static readonly HashSet<Type> funcifiableReturnTypes = new HashSet<Type>();

    private static void AllowFuncification<ExR>() {
        funcifiableTypes[typeof(Func<TExArgCtx, ExR>)] = (ReflectionData.HasMember<ExR>,
            ReflectionData.FuncifyTypes<TExArgCtx, ExR>,
            ReflectionData.TryInvokeFunced<TExArgCtx, ExR>);
        funcifiableReturnTypes.Add(typeof(ExR));
    }

    private static NamedParam[]? TryLookForMethod(Type rt, string member) {
        if (funcifiableTypes.TryGetValue(rt, out var fs) && fs.has(member)) 
            return fs.get(member);
        if (ReflectionData.HasMember(rt, member)) 
            return ReflectionData.GetArgTypes(rt, member);
        if (letFuncs.TryGetValue(rt, out _) && member[0] == Parser.SM_REF_KEY_C) 
            return fudge;
        return null;
    }

    private static readonly Dictionary<Type, Func<string, object>> letFuncs =
        new Dictionary<Type, Func<string, object>>() {
            {typeof(ExBPY), ReflectEx.ReferenceLet<float>},
            {typeof(ExTP), ReflectEx.ReferenceLet<Vector2>},
            {typeof(ExTP3), ReflectEx.ReferenceLet<Vector3>},
            {typeof(ExBPRV2), ReflectEx.ReferenceLet<V2RV2>},
        };

    private static object? TryInvokeMethod(IParseQueue? q, Type rt, string member, object?[] prms) {
        if (funcifiableTypes.TryGetValue(rt, out var fs) && fs.inv(q, member, prms, out var res)) 
            return res;
        if (ReflectionData.HasMember(rt, member) && ReflectionData.TryInvoke(rt, member, prms, out res)) 
            return res;
        if (letFuncs.TryGetValue(rt, out var f) && member[0] == Parser.SM_REF_KEY_C) 
            return f(member);
        return null;
    }

    public static T InvokeMethod<T>(IParseQueue? q, string member, object?[] prms) =>
        (T)InvokeMethod(q, typeof(T), member, prms);
    public static object InvokeMethod(IParseQueue? q, Type rt, string member, object?[] prms) =>
        TryInvokeMethod(q, rt, member, prms) ?? throw new Exception(
            $"Type handling passed but object creation failed for type {rt.RName()}, method {member}. " +
            "This is an internal error. Please report it.");

    public static object ExtInvokeMethod(Type t, string member, object[] prms) {
        member = Sanitize(member);
        if (TryCompileOption(t, out var compiler)) {
            return compiler.mi.Invoke(null, new[] {
                ExtInvokeMethod(compiler.source, member, prms)
            });
        } else if (t == typeof(StateMachine)) {
            return StateMachine.Create(member, prms);
        }
        var result = TryInvokeMethod(null, t, member, prms);
        if (result != null) return result;
        //this also requires fallthrough support, which is handled through parent methods in the inner call.
        if (FallThroughOptions.TryGetValue(t, out var ftmi)) {
            if ((result = TryInvokeMethod(null, ftmi.mi.GetParameters()[0].ParameterType, member, prms)) != null) {
                return ftmi.mi.Invoke(null, new[] {result});
            }
        }
        throw new Exception($"External method invocation failed for type {t.RName()}, method {member}. " +
                            "This is probably an error in static code.");
    }

    public static T ExtInvokeMethod<T>(string member, object[] prms) => (T) ExtInvokeMethod(typeof(T), member, prms);



    private static readonly Dictionary<Type, (Type source, MethodInfo mi)> CompileOptions =
        new Dictionary<Type, (Type, MethodInfo)>();
    private static readonly HashSet<Type> checkedCompileOptions = new HashSet<Type>();
    private static readonly List<MethodInfo> genericCompileOptions = new List<MethodInfo>();

    private static void AddCompileOption(MethodInfo compiler) {
        if (compiler.IsGenericMethodDefinition)
            genericCompileOptions.Add(compiler);
        else {
            var compiledType = compiler.ReturnType;
            if (CompileOptions.ContainsKey(compiledType))
                throw new StaticException(
                    $"Cannot have multiple expression compilers for the same return type {compiledType}.");
            CompileOptions[compiledType] = (compiler.GetParameters()[0].ParameterType, compiler);
        }
    }
        
    
    private static bool TryCompileOption(Type compiledType, out (Type source, MethodInfo mi) compile) {
        if (CompileOptions.TryGetValue(compiledType, out compile)) return true;
        if (!checkedCompileOptions.Contains(compiledType)) {
            for (int ii = 0; ii < genericCompileOptions.Count; ++ii) {
                var mi = genericCompileOptions[ii];
                if (ConstructedGenericTypeMatch(compiledType, mi.ReturnType, out var typeMap)) {
                    AddCompileOption(mi.MakeGeneric(typeMap));
                    compile = CompileOptions[compiledType];
                    return true;
                }
            }
            checkedCompileOptions.Add(compiledType);
        }
        return false;
    }
    
    private static readonly Dictionary<Type, (FallthroughAttribute fa, MethodInfo mi)> FallThroughOptions =
        new Dictionary<Type, (FallthroughAttribute, MethodInfo)>();

}
}