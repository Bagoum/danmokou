using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using System.Reflection;
using System.Runtime.CompilerServices;
using DMath;
using JetBrains.Annotations;
using UnityEngine;

public static partial class Reflector {
    private static readonly Type[] fudge = new Type[0];

    private class GenericReflectionConfig {
        //Note that this will contain both generic and non-generic types
        private readonly Dictionary<Type, Dictionary<string, MethodInfo>> methodsByReturnType;
        private readonly Dictionary<(string, Type), Type[]> lazyTypes;
        private readonly Dictionary<(string, Type, Type), Type[]> funcedTypes;
        private readonly Type sourceType;

        private GenericReflectionConfig(Type t, Dictionary<Type, Dictionary<string, MethodInfo>> typedmethods) {
            this.sourceType = t;
            this.methodsByReturnType = typedmethods;
            this.lazyTypes = new Dictionary<(string, Type), Type[]>();
            this.funcedTypes = new Dictionary<(string, Type, Type), Type[]>();
        }
        public static GenericReflectionConfig Public(Type t) => Read(t, BindingFlags.Static | BindingFlags.Public);
        public static GenericReflectionConfig ManyPublic(params Type[] t) => ReadMany(t, BindingFlags.Static | BindingFlags.Public);
        public static GenericReflectionConfig Private(Type t) => Read(t, BindingFlags.Static | BindingFlags.NonPublic);
        private static GenericReflectionConfig Read(Type t, BindingFlags flags) {
            var methods = new Dictionary<Type, Dictionary<string, MethodInfo>>();
            foreach (MethodInfo mi in t.GetMethods(flags)) RecordMethod(methods, mi);
            return new GenericReflectionConfig(t, methods);
        }
        private static GenericReflectionConfig ReadMany(Type[] ts, BindingFlags flags) {
            var methods = new Dictionary<Type, Dictionary<string, MethodInfo>>();
            foreach (var t in ts) {
                foreach (MethodInfo mi in t.GetMethods(flags)) RecordMethod(methods, mi);
            }
            return new GenericReflectionConfig(ts[0], methods);
        }

        private static void RecordMethod(Dictionary<Type, Dictionary<string, MethodInfo>> methods, MethodInfo mi) {
            var rt = mi.ReturnType;
            //Note that rt is NOT equal to eg typeof(TEx<>); it is a generic-constructed something something.
            if (mi.IsGenericMethodDefinition) rt = rt.GetGenericTypeDefinition();
            var attrs = Attribute.GetCustomAttributes(mi);
            if (attrs.Any(x => x is DontReflectAttribute)) return;
            methods.SetDefaultSet(rt, mi.Name.ToLower(), mi);
            foreach (var attr in attrs) {
                if (attr is AliasAttribute aa) methods.SetDefaultSet(rt, aa.alias.ToLower(), mi);
            }
        }
        public bool HasMember(Type rt, Type gt, string member) {
            ResolveGeneric(rt, member, gt);
            return methodsByReturnType.Has2(rt, member);
        }
        public bool HasMember<RT, GT>(string member) => HasMember(typeof(RT), typeof(GT), member);

        private void ResolveGeneric(Type rt, string member, [CanBeNull] Type gt = null) {
            if (methodsByReturnType.Has2(rt, member) || !genericMathTypes.Contains(gt ?? rt)) return;
            if (rt.IsConstructedGenericType &&
                methodsByReturnType.TryGetValue(rt.GetGenericTypeDefinition(), out var dct) && 
                dct.TryGetValue(member, out var mi) &&
                rt.GetGenericTypeDefinition() == mi.ReturnType.GetGenericTypeDefinition()) {
                methodsByReturnType.SetDefaultSet(rt, member, mi.MakeGenericMethod((gt ?? rt).GenericTypeArguments));
            }
        }
        public Type[] LazyGetTypes(Type rt, string member, [CanBeNull] Type gt = null) {
            if (!lazyTypes.ContainsKey((member, rt))) {
                ResolveGeneric(rt, member, gt);
                if (methodsByReturnType.TryGetValue(rt, out var dct) && dct.TryGetValue(member, out var mi)) {
                    ParameterInfo[] prms = mi.GetParameters();
                    Type[] typs = new Type[prms.Length];
                    for (int ii = 0; ii < prms.Length; ++ii) {
                        typs[ii] = prms[ii].ParameterType;
                    }
                    lazyTypes[(member, rt)] = typs;
                } else
                    throw new NotImplementedException($"The method \"{sourceType.RName()}.{member}\" was not found.\n");
            }
            return lazyTypes[(member, rt)];
        }

        public Type[] LazyGetTypes<RT, GT>(string member) => LazyGetTypes(typeof(RT), member, typeof(GT));
        
        //dst type, (source type, converter)
        private static readonly Dictionary<Type, (Type, object)> conversions = new Dictionary<Type, (Type, object)>() {
            { typeof(EEx<bool>), (typeof(TEx<bool>), (Func<TEx<bool>, EEx<bool>>) (x => x)) },
            { typeof(EEx<float>), (typeof(TEx<float>), (Func<TEx<float>, EEx<float>>) (x => x)) },
            { typeof(EEx<Vector2>), (typeof(TEx<Vector2>), (Func<TEx<Vector2>, EEx<Vector2>>) (x => x)) },
            { typeof(EEx<Vector3>), (typeof(TEx<Vector3>), (Func<TEx<Vector3>, EEx<Vector3>>) (x => x)) },
            { typeof(EEx<Vector4>), (typeof(TEx<Vector4>), (Func<TEx<Vector4>, EEx<Vector4>>) (x => x)) },
            { typeof(EEx<V2RV2>), (typeof(TEx<V2RV2>), (Func<TEx<V2RV2>, EEx<V2RV2>>) (x => x)) }
        };
        private static readonly Dictionary<(Type,Type), Type> funcMapped = new Dictionary<(Type,Type), Type>();
        private static readonly Dictionary<Type, Func<object, object, object>> funcConversions = new Dictionary<Type, Func<object, object, object>>();
        private static readonly HashSet<Type> funcableTypes = new HashSet<Type>() {
            typeof(TEx<bool>),
            typeof(TEx<float>),
            typeof(TEx<Vector2>),
            typeof(TEx<Vector3>),
            typeof(TEx<Vector4>),
            typeof(TEx<V2RV2>)
        };
        private static readonly HashSet<Type> genericMathTypes = new HashSet<Type>() {
            typeof(TEx<bool>),
            typeof(TEx<float>),
            typeof(TEx<Vector2>),
            typeof(TEx<Vector3>),
            typeof(TEx<Vector4>),
            typeof(TEx<V2RV2>)
        };
        public Type[] FuncifyTypes<T, R>(string member) => FuncifyTypes(typeof(T), typeof(R), member);
        public Type[] FuncifyTypes(Type t, Type r, string member) {
            bool TryFuncify(Type bt, out Type res) {
                if (funcMapped.ContainsKey((t,bt))) {
                    res = funcMapped[(t,bt)];
                } else if (funcableTypes.Contains(bt)) {
                    var ft = res = funcMapped[(t,bt)] = Func2Type(t, bt);
                    funcConversions[ft] = (x, bpi) => FuncInvoke(x, ft, bpi);
                } else if (bt.IsArray && TryFuncify(bt.GetElementType(), out var ftele)) {
                    var ft = res = funcMapped[(t,bt)] = ftele.MakeArrayType();
                    funcConversions[ft] = (x, bpi) => {
                        var oa = x as Array;
                        var fa = Array.CreateInstance(bt.GetElementType(), oa.Length);
                        for (int oi = 0; oi < oa.Length; ++oi) {
                            fa.SetValue(funcConversions[ftele](oa.GetValue(oi), bpi), oi);
                        }
                        return fa;
                    };
                } else if (bt.IsConstructedGenericType && bt.GetGenericTypeDefinition() == typeof(ValueTuple<,>) && 
                           bt.GenericTypeArguments.Any(x => TryFuncify(x, out _))) {
                    var gts = bt.GenericTypeArguments;
                    for (int ii = 0; ii < gts.Length; ++ii) {
                        if (TryFuncify(gts[ii], out var gt)) gts[ii] = gt;
                    }
                    var ft = res = funcMapped[(t, bt)] = typeof(ValueTuple<,>).MakeGenericType(gts);
                    var tupToArr = typeof(Reflector).GetMethod($"TupleToArr{gts.Length}", BindingFlags.Static | BindingFlags.Public)
                        ?.MakeGenericMethod(gts) ?? throw new StaticException("Couldn't find tuple decomposition method");
                    funcConversions[ft] = (x, bpi) => {
                        var argarr = tupToArr.Invoke(null, new[] {x}) as object[] ??
                                     throw new StaticException("Couldn't decompose tuple to array");
                        for (int ii = 0; ii < gts.Length; ++ii) {
                            if (funcConversions.TryGetValue(gts[ii], out var conv)) argarr[ii] = conv(argarr[ii], bpi);
                        }
                        return Activator.CreateInstance(bt, argarr);
                    };
                } else {
                    res = default;
                    return false;
                }
                return true;
            }
            if (!funcedTypes.ContainsKey((member, t, r))) {
                Type[] baseTypes = LazyGetTypes(r, member).ToArray();
                for (int ii = 0; ii < baseTypes.Length; ++ii) {
                    var bt = baseTypes[ii];
                    if (conversions.ContainsKey(bt)) {
                        bt = baseTypes[ii] = conversions[bt].Item1;
                    }
                    if (TryFuncify(bt, out var result)) {
                        baseTypes[ii] = result;
                    }
                }
                funcedTypes[(member, t, r)] = baseTypes;
            }
            return funcedTypes[(member, t, r)];
        }

        private static readonly Dictionary<(Type, Type), Type> func2Types = new Dictionary<(Type, Type), Type>();
        private Type Func2Type(Type t1, Type t2) {
            if (!func2Types.TryGetValue((t1, t2), out var tf)) {
                tf = func2Types[(t1, t2)] = typeof(Func<,>).MakeGenericType(t1, t2);
            }
            return tf;
        }

        private static readonly Dictionary<Type, MethodInfo> funcInvoke = new Dictionary<Type, MethodInfo>();
        private Func<object, object> FuncInvoker(object func, Type funcType) => x => FuncInvoke(func, funcType, x);
        private object FuncInvoke(object func, Type funcType, object over) {
            if (!funcInvoke.TryGetValue(funcType, out var mi)) {
                mi = funcInvoke[funcType] = funcType.GetMethod("Invoke") ?? throw new Exception($"No invoke method found for {funcType.RName()}");
            }
            return mi.Invoke(func, new[] {over});
        }

        public bool TryInvokeFunced<T, R>(ReflCtx ctx, string member, object[] _prms, out object result) {
            var rt = typeof(R);
            ResolveGeneric(rt, member);
            if (methodsByReturnType.TryGet2(rt, member, out var f)) {
                if (ctx.props.warnPrefix && Attribute.GetCustomAttributes(f).Any(x =>
                    x is WarnOnStrictAttribute wa && (int) ctx.props.strict >= wa.strictness)) {
                    Log.Unity($"Line {ctx.q.GetLastLine()}: The method \"{member}\" is not permitted for use in a script with strictness {ctx.props.strict}. You might accidentally be using the prefix version of an infix function.", true, Log.Level.WARNING);
                }
                result = (Func<T,R>)(bpi => {
                    Type[] baseTypes = LazyGetTypes(rt, member);
                    Type[] funcTypes = FuncifyTypes<T, R>(member);
                    var prms = _prms.ToArray();
                    for (int ii = 0; ii < baseTypes.Length; ++ii) {
                        Func<object, object> converter = null;
                        if (conversions.ContainsKey(baseTypes[ii])) {
                            var (ntype, rawconv) = conversions[baseTypes[ii]];
                            converter = FuncInvoker(rawconv, Func2Type(ntype, baseTypes[ii]));
                        }
                        if (funcConversions.TryGetValue(funcTypes[ii], out var fconv)) {
                            prms[ii] = fconv(prms[ii], bpi);
                        }
                        if (converter != null) {
                            prms[ii] = converter(prms[ii]);
                        }
                    }
                    return (R)f.Invoke(null, prms);
                });
                
                return true;
            }
            result = default;
            return false;
        }

        public object Invoke(Type rt, string member, object[] prms) => methodsByReturnType[rt][member].Invoke(null, prms);

        public bool TryInvoke<T>(ReflCtx ctx, string member, object[] prms, out object result) =>
            TryInvoke(typeof(T), member, prms, out result);
        public bool TryInvoke(Type rt, string member, object[] prms, out object result) {
            ResolveGeneric(rt, member);
            if (methodsByReturnType.Has2(rt, member)) {
                result = Invoke(rt, member, prms);
                return true;
            }
            result = default;
            return false;
        }
    }
    
    /// <summary>
    /// A struct that lazily collects method and type information for reflected classes.
    /// </summary>
    private static class ReflConfig {
        /// <summary>
        /// Reflectable methods from default sources. Dict[ReturnType][MethodName] = MethodInfo
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<string, MethodInfo>> methods = new Dictionary<Type, Dictionary<string, MethodInfo>>();
        public static bool RequiresMethodRefl(Type t) => methods.ContainsKey(t);
        /// <summary>
        /// Reflectable methods from RecordByClass. Dict[ReturnType][MethodName, DeclaredClass] = MethodInfo
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<(string, Type), MethodInfo>> methodsByDecl = new Dictionary<Type, Dictionary<(string, Type), MethodInfo>>();
        private static readonly HashSet<Type> recordedRepos = new HashSet<Type>();
        /// <summary>
        /// Cached dictionary of method parameter types. Dict[ReturnType][MethodInfo] = Types
        /// </summary>
        private static readonly Dictionary<MethodInfo, Type[]> allLazyTypes = new Dictionary<MethodInfo, Type[]>();
        
        //Allows supporting types via math-generalization even if no specific methods exist
        public static void AddType(Type t) {
            if (!methods.ContainsKey(t)) methods[t] = new Dictionary<string, MethodInfo>();
        }

        private static void RecordMethod(MethodInfo mi) {
            var attrs = Attribute.GetCustomAttributes(mi);
            if (attrs.Any(x => x is DontReflectAttribute)) return;
        #if NO_EXPR
            if (attrs.Any(x => x is ExprCompilerAttribute)) return;
        #endif
            methods.SetDefaultSet(mi.ReturnType, mi.Name.ToLower(), mi);
            foreach (var attr in attrs) {
                if (attr is AliasAttribute aa) methods.SetDefaultSet(mi.ReturnType, aa.alias.ToLower(), mi);
                else if (attr is GAliasAttribute ga) {
                    var gmi = mi.MakeGenericMethod(ga.type);
                    methods.SetDefaultSet(gmi.ReturnType, ga.alias.ToLower(), gmi);
                } else if (attr is FallthroughAttribute fa) {
                    if (ReflConfig.RecordLazyTypes(mi).Length != 1) {
                        throw new StaticException($"Fallthrough methods must have one argument: {mi.Name}");
                    }
                    if (fa.upwardsCast) UpwardsCastOptions.AddToList(mi.ReturnType, (fa, mi));
                    else FallThroughOptions.AddToList(mi.ReturnType, (fa, mi));
                }
            }
        }
        private static void RecordMethodByClass(Type d, MethodInfo mi) {
            methodsByDecl.SetDefaultSet(mi.ReturnType, (mi.Name.ToLower(), d), mi);
            foreach (var attr in Attribute.GetCustomAttributes(mi)) {
                if (attr is AliasAttribute aa) methodsByDecl.SetDefaultSet(mi.ReturnType, (aa.alias.ToLower(), d), mi);
                if (attr is GAliasAttribute ga) {
                    var gmi = mi.MakeGenericMethod(ga.type);
                    methodsByDecl.SetDefaultSet(gmi.ReturnType, (ga.alias.ToLower(), d), gmi);
                }
            }
            //No fallthrough/etc
        }
        public static Type[] RecordLazyTypes(MethodInfo mi) {
            if (allLazyTypes.TryGetValue(mi, out var ts)) return ts;
            ParameterInfo[] prms = mi.GetParameters();
            Type[] typs = new Type[prms.Length];
            for (int ii = 0; ii < prms.Length; ++ii) {
                typs[ii] = prms[ii].ParameterType;
            }
            return allLazyTypes[mi] = typs;
        }

        public static void RecordPublic(Type repo) => Record(repo, BindingFlags.Static | BindingFlags.Public);
        public static void RecordPublic<R>(Type repo) => Record(repo, typeof(R), BindingFlags.Static | BindingFlags.Public);
        public static void RecordPublicByClass<R>(Type repo) => RecordByClass(repo, typeof(R), BindingFlags.Static | BindingFlags.Public);

        private static void Record(Type repo, BindingFlags flags) {
            if (recordedRepos.Contains(repo)) return;
            recordedRepos.Add(repo);
            foreach (MethodInfo mi in repo.GetMethods(flags)) RecordMethod(mi);
        }
        private static void Record(Type repo, Type ret, BindingFlags flags) {
            if (recordedRepos.Contains(repo)) return;
            recordedRepos.Add(repo);
            foreach (MethodInfo mi in repo.GetMethods(flags)) {
                if (mi.ReturnType == ret) RecordMethod(mi);
            }
        }
        private static void RecordByClass(Type repo, Type ret, BindingFlags flags) {
            if (recordedRepos.Contains(repo)) return;
            recordedRepos.Add(repo);
            foreach (MethodInfo mi in repo.GetMethods(flags)) {
                if (mi.ReturnType == ret) RecordMethodByClass(repo, mi);
            }
        }

        public static void ShortcutAll(string source, string alias) {
            foreach (var m in methods.Values) {
                if (m.ContainsKey(source)) m[alias] = m[source];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasMember(Type retType, string member, out MethodInfo mi) =>
            methods.TryGet2(retType, member, out mi);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasMember(Type declaringClass, Type retType, string member, out MethodInfo mi) =>
            methodsByDecl.TryGet2(retType, (member, declaringClass), out mi);

        public static Type[] LazyGetTypes(Type r, string member) {
            if (!HasMember(r, member, out var mi)) throw new NotImplementedException($"No method \"{member}\" was found with return type {r.RName()}");
            return RecordLazyTypes(mi);
        }
        public static Type[] LazyGetTypes<R>(string member) => LazyGetTypes(typeof(R), member);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Invoke(Type r, string member, object[] prms) {
            return methods[r][member].Invoke(null, prms);
        }

    }

    private static readonly GenericReflectionConfig MathConfig;
}