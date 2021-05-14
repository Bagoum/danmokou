using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using JetBrains.Annotations;
using Danmokou.SM.Parsing;
using UnityEngine;
using UnityEngine.Profiling;
using static Danmokou.Core.ReflectionUtils;

namespace Danmokou.Reflection {
public static partial class Reflector {
    private static readonly NamedParam[] fudge = new NamedParam[0];

    private static class ReflectionData {
        /// <summary>
        /// Contains non-generic methods, whether non-generic in source or computed via .MakeGenericMethod,
        /// keyed by return type.
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<string, MethodInfo>> methodsByReturnType
            = new Dictionary<Type, Dictionary<string, MethodInfo>>();
    #if UNITY_EDITOR
        public static Dictionary<Type, Dictionary<string, MethodInfo>> MethodsByReturnType => methodsByReturnType;
    #endif
        /// <summary>
        /// Array of all generic methods recorded.
        /// </summary>
        private static readonly List<(string method, MethodInfo mi)> genericMethods
            = new List<(string, MethodInfo)>();
        /// <summary>
        /// Generics are lazily matched against types, and the computed .MakeGenericMethod is only then sent to
        /// methodsByReturnType. This set contains all types that have been matched.
        /// </summary>
        private static readonly HashSet<Type> computedGenericsTypes = new HashSet<Type>();
        /// <summary>
        /// Lazily loaded parameter types for functions in methodsByReturnType.
        /// </summary>
        private static readonly Dictionary<(string method, Type returnType), NamedParam[]> lazyTypes 
            = new Dictionary<(string, Type), NamedParam[]>();
        /// <summary>
        /// Lazily loaded parameter types for funcified functions in methodsByReturnType.
        /// Effective return type key = Func&lt;funcIn, funcOut&gt;
        /// </summary>
        private static readonly Dictionary<(string method, Type funcIn, Type funcOut), NamedParam[]> funcedTypes 
            = new Dictionary<(string, Type, Type), NamedParam[]>();


        /// <summary>
        /// Record public static methods in a class for reflection use.
        /// </summary>
        /// <param name="repo">Class of methods.</param>
        /// <param name="returnType">Optional. If provided, only records methods with this return type.</param>
        public static void RecordPublic(Type repo, Type? returnType = null) => 
            Record(repo, returnType, BindingFlags.Static | BindingFlags.Public);
        
        private static void Record(Type repo, Type? returnType, BindingFlags flags) {
            // Two generic methods never have the same return type:
            //  List<T> MakeList<T>()...
            //  List<T> MakeList2<T>()...
            //  mi1.ReturnType != mi2.ReturnType
            // However, if they have the same number of type params, we can test if they are "effectively the same"
            //  by constructing them both with the same array of unique types, and comparing the result types.
            // However, since the generic matching process on the return type requires that the types are the same
            //  as the method, each method will require its own type-matching map, and there is no way to meaningfully
            //  combine multiple generic methods under one type. 
            // (Short of running .MakeGenericType with generic type arguments-- DO NOT DO THIS.)
            repo
                .GetMethods(flags)
                .Where(mi => returnType == null || mi.ReturnType == returnType)
                .ForEach(RecordMethod);
        }

        private static void RecordMethod(MethodInfo mi) {
            void AddMI(string name, MethodInfo method) {
                if (method.IsGenericMethodDefinition) {
                    genericMethods.Add((name.ToLower(), method));
                } else {
                    methodsByReturnType.SetDefaultSet(method.ReturnType, name.ToLower(), method);
                }
            }
            var attrs = Attribute.GetCustomAttributes(mi);
            if (attrs.Any(x => x is DontReflectAttribute)) return;
            bool isExCompiler = (attrs.Any(x => x is ExprCompilerAttribute));
            bool addNormal = true;
            foreach (var attr in attrs) {
                if (attr is AliasAttribute aa) 
                    AddMI(aa.alias.ToLower(), mi);
                else if (attr is GAliasAttribute ga) {
                    var gmi = mi.MakeGenericMethod(ga.type);
                    AddMI(ga.alias, gmi);
                    addNormal = false;
                } else if (attr is FallthroughAttribute fa) {
                    if (mi.GetParameters().Length != 1) {
                        throw new StaticException($"Fallthrough methods must have exactly one argument: {mi.Name}");
                    }
                    if (FallThroughOptions.ContainsKey(mi.ReturnType))
                        throw new StaticException(
                            $"Cannot have multiple fallthroughs for the same return type {mi.ReturnType}");
                    if (isExCompiler) {
                        AddCompileOption(mi);
                    }
                    else FallThroughOptions[mi.ReturnType] = (fa, mi);
                }
            }
            if (addNormal) AddMI(mi.Name, mi);
        }

        public static bool HasMember(Type rt, string member) {
            ResolveGeneric(rt);
            return methodsByReturnType.Has2(rt, member);
        }

        public static bool HasMember<RT>(string member) => HasMember(typeof(RT), member);

        /// <summary>
        /// </summary>
        /// <param name="rt">Desired return type</param>
        private static void ResolveGeneric(Type rt) {
            if (computedGenericsTypes.Contains(rt) || !rt.IsConstructedGenericType) return;
            for (int ii = 0; ii < genericMethods.Count; ++ii) {
                var (member, mi) = genericMethods[ii];
                if (ConstructedGenericTypeMatch(rt, mi.ReturnType, out var typeMap)) {
                    var constrMethod = mi.MakeGeneric(typeMap);
                    methodsByReturnType.SetDefaultSet(rt, member, constrMethod);
                }
            }
            computedGenericsTypes.Add(rt);
        }

        public static NamedParam[] GetArgTypes(MethodInfo mi) =>
            mi.GetParameters().Select(x => (NamedParam) x).ToArray();
        /// <summary>
        /// Results are cached.
        /// </summary>
        public static NamedParam[] GetArgTypes(Type rt, string member) {
            if (!lazyTypes.ContainsKey((member, rt))) {
                ResolveGeneric(rt);
                if (methodsByReturnType.TryGetValue(rt, out var dct) && dct.TryGetValue(member, out var mi)) {
                    lazyTypes[(member, rt)] = GetArgTypes(mi);
                } else
                    throw new NotImplementedException($"The method \"{rt.RName()}.{member}\" was not found.\n");
            }
            return lazyTypes[(member, rt)];
        }

        public static NamedParam[] LazyGetTypes<RT>(string member) => GetArgTypes(typeof(RT), member);

        //dst type, (source type, converter)
        public static readonly Dictionary<Type, (Type sourceType, object converter)> conversions
            = new Dictionary<Type, (Type, object)>() {
                {typeof(EEx<bool>), (typeof(TEx<bool>), (Func<TEx<bool>, EEx<bool>>) (x => x))},
                {typeof(EEx<float>), (typeof(TEx<float>), (Func<TEx<float>, EEx<float>>) (x => x))},
                {typeof(EEx<Vector2>), (typeof(TEx<Vector2>), (Func<TEx<Vector2>, EEx<Vector2>>) (x => x))},
                {typeof(EEx<Vector3>), (typeof(TEx<Vector3>), (Func<TEx<Vector3>, EEx<Vector3>>) (x => x))},
                {typeof(EEx<Vector4>), (typeof(TEx<Vector4>), (Func<TEx<Vector4>, EEx<Vector4>>) (x => x))},
                {typeof(EEx<V2RV2>), (typeof(TEx<V2RV2>), (Func<TEx<V2RV2>, EEx<V2RV2>>) (x => x))}
            };
        
        private static readonly Dictionary<(Type, Type), Type> funcMapped = new Dictionary<(Type, Type), Type>();
        private static readonly Dictionary<(Type fromType, Type toType), Func<object, object, object>> funcConversions =
            new Dictionary<(Type, Type), Func<object, object, object>>();
        private static readonly HashSet<Type> funcableTypes = new HashSet<Type>() {
            typeof(TEx<bool>),
            typeof(TEx<float>),
            typeof(TEx<Vector2>),
            typeof(TEx<Vector3>),
            typeof(TEx<Vector4>),
            typeof(TEx<V2RV2>)
        };
        public static NamedParam[] FuncifyTypes<T, R>(string member) => FuncifyTypes(typeof(T), typeof(R), member);

        public static NamedParam[] FuncifyTypes(Type t, Type r, string member) {
            bool TryFuncify(Type bt, out Type res) {
                if (funcMapped.ContainsKey((t, bt))) {
                    res = funcMapped[(t, bt)];
                } else if (funcableTypes.Contains(bt)) {
                    var ft = res = funcMapped[(t, bt)] = Func2Type(t, bt);
                    funcConversions[(bt, ft)] = (x, bpi) => FuncInvoke(x, ft, bpi);
                } else if (bt.IsArray && TryFuncify(bt.GetElementType()!, out var ftele)) {
                    var ft = res = funcMapped[(t, bt)] = ftele.MakeArrayType();
                    funcConversions[(bt, ft)] = (x, bpi) => {
                        var oa = x as Array ?? throw new StaticException("Couldn't arrayify");
                        var fa = Array.CreateInstance(bt.GetElementType()!, oa.Length);
                        for (int oi = 0; oi < oa.Length; ++oi) {
                            fa.SetValue(funcConversions[(bt.GetElementType(), ftele)](oa.GetValue(oi), bpi), oi);
                        }
                        return fa;
                    };
                } else if (bt.IsConstructedGenericType && bt.GetGenericTypeDefinition() == typeof(ValueTuple<,>) &&
                           bt.GenericTypeArguments.Any(x => TryFuncify(x, out _))) {
                    var base_gts = bt.GenericTypeArguments;
                    var gts = new Type[base_gts.Length];
                    for (int ii = 0; ii < gts.Length; ++ii) {
                        if (TryFuncify(base_gts[ii], out var gt)) gts[ii] = gt;
                    }
                    var ft = res = funcMapped[(t, bt)] = typeof(ValueTuple<,>).MakeGenericType(gts);
                    var tupToArr = typeof(Reflector)
                                       .GetMethod($"TupleToArr{gts.Length}", BindingFlags.Static | BindingFlags.Public)
                                       ?.MakeGenericMethod(gts) ??
                                   throw new StaticException("Couldn't find tuple decomposition method");
                    funcConversions[(bt, ft)] = (x, bpi) => {
                        var argarr = tupToArr.Invoke(null, new[] {x}) as object[] ??
                                     throw new StaticException("Couldn't decompose tuple to array");
                        for (int ii = 0; ii < gts.Length; ++ii) {
                            if (funcConversions.TryGetValue((base_gts[ii], gts[ii]), out var conv)) 
                                argarr[ii] = conv(argarr[ii], bpi);
                        }
                        return Activator.CreateInstance(bt, argarr);
                    };
                } else {
                    res = default!;
                    return false;
                }
                return true;
            }
            if (!funcedTypes.ContainsKey((member, t, r))) {
                NamedParam[] baseTypes = GetArgTypes(r, member);
                NamedParam[] fTypes = new NamedParam[baseTypes.Length];
                for (int ii = 0; ii < baseTypes.Length; ++ii) {
                    var bt = baseTypes[ii].type;
                    if (conversions.ContainsKey(bt)) {
                        bt = conversions[bt].sourceType;
                    }
                    if (TryFuncify(bt, out var result)) {
                        bt = result;
                    }
                    fTypes[ii] = baseTypes[ii].WithType(bt);
                }
                funcedTypes[(member, t, r)] = fTypes;
            }
            return funcedTypes[(member, t, r)];
        }

        private static readonly Dictionary<(Type, Type), Type> func2Types = new Dictionary<(Type, Type), Type>();

        public static Type Func2Type(Type t1, Type t2) {
            if (!func2Types.TryGetValue((t1, t2), out var tf)) {
                tf = func2Types[(t1, t2)] = typeof(Func<,>).MakeGenericType(t1, t2);
            }
            return tf;
        }

        private static readonly Dictionary<Type, MethodInfo> funcInvoke = new Dictionary<Type, MethodInfo>();

        public static Func<object?, object?> FuncInvoker(object func, Type funcType) =>
            x => FuncInvoke(func, funcType, x);

        private static object FuncInvoke(object func, Type funcType, object? over) {
            if (!funcInvoke.TryGetValue(funcType, out var mi)) {
                mi = funcInvoke[funcType] = funcType.GetMethod("Invoke") ??
                                            throw new Exception($"No invoke method found for {funcType.RName()}");
            }
            return mi.Invoke(func, new[] {over});
        }

        public static bool TryInvokeFunced<T, R>(IParseQueue? q, string member, object?[] _prms, out object result) {
            var rt = typeof(R);
            ResolveGeneric(rt);
            if (methodsByReturnType.TryGet2(rt, member, out var f)) {
                if (q?.Ctx.props.warnPrefix == true && Attribute.GetCustomAttributes(f).Any(x =>
                    x is WarnOnStrictAttribute wa && (int) q.Ctx.props.strict >= wa.strictness)) {
                    Log.Unity(
                        $"Line {q.GetLastLine()}: The method \"{member}\" is not permitted for use in a script with strictness {q.Ctx.props.strict}. You might accidentally be using the prefix version of an infix function.",
                        true, Log.Level.WARNING);
                }
                result = (Func<T, R>) (bpi => {
                    var baseTypes = GetArgTypes(rt, member);
                    var funcTypes = FuncifyTypes<T, R>(member);
                    var prms = _prms.ToArray();
                    for (int ii = 0; ii < baseTypes.Length; ++ii) {
                        var eff_type = baseTypes[ii].type;
                        Func<object?, object?>? converter = null;
                        if (conversions.ContainsKey(baseTypes[ii].type)) {
                            var (ntype, rawconv) = conversions[baseTypes[ii].type];
                            converter = FuncInvoker(rawconv, Func2Type(eff_type = ntype, baseTypes[ii].type));
                        }
                        //Convert from funced object to base object (eg. ExFXY to TEx<float>)
                        if (funcConversions.TryGetValue((eff_type, funcTypes[ii].type), out var fconv)) {
                            prms[ii] = fconv(prms[ii] ?? 
                                             throw new Exception("Required funced object, found null"), bpi!);
                        }
                        //Make trivial conversions (eg. TEx<float> to EEx<float>)
                        if (converter != null) {
                            prms[ii] = converter(prms[ii]);
                        }
                    }
                    return (R) f.Invoke(null, prms);
                });

                return true;
            }
            result = default!;
            return false;
        }
        
        public static object Invoke(Type rt, string member, object?[] prms) =>
            methodsByReturnType[rt][member].Invoke(null, prms);

        public static bool TryInvoke<T>(IParseQueue? _, string member, object?[] prms, out object result) =>
            TryInvoke(typeof(T), member, prms, out result);

        public static bool TryInvoke(Type rt, string member, object?[] prms, out object result) {
            ResolveGeneric(rt);
            if (methodsByReturnType.Has2(rt, member)) {
                result = Invoke(rt, member, prms);
                return true;
            }
            result = default!;
            return false;
        }
    }

}
}