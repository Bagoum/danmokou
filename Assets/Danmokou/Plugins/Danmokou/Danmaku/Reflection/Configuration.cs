using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BagoumLib;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using JetBrains.Annotations;
using Danmokou.SM.Parsing;
using UnityEngine;
using UnityEngine.Profiling;
using static BagoumLib.Reflection.ReflectionUtils;

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
        /// <summary>
        /// Exposing methodsByReturnType for AoT expression baking.
        /// </summary>
        public static Dictionary<Type, Dictionary<string, MethodInfo>> MethodsByReturnType => methodsByReturnType;
    #endif
        /// <summary>
        /// Array of all generic methods recorded.
        /// </summary>
        private static readonly List<(string method, MethodInfo mi)> genericMethods
            = new List<(string, MethodInfo)>();
        
        /// <summary>
        /// Generics are lazily matched against types, and the computed .MakeGenericMethod is only then sent to
        /// methodsByReturnType. This set contains all return types that have been matched.
        /// </summary>
        private static readonly HashSet<Type> computedGenericsTypes = new HashSet<Type>();
        
        /// <summary>
        /// Record public static methods in a class for reflection use.
        /// </summary>
        /// <param name="repo">Class of methods.</param>
        /// <param name="returnType">Optional. If provided, only records methods with this return type.</param>
        public static void RecordPublic(Type repo, Type? returnType = null) => 
            Record(repo, returnType, BindingFlags.Static | BindingFlags.Public);
        
        private static void Record(Type repo, Type? returnType, BindingFlags flags) {
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
                    AddMI(aa.alias, mi);
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

        /// <summary>
        /// Get the argument types for a method member that returns type rt.
        /// </summary>
        public static NamedParam[] GetArgTypes(Type rt, string member) {
            if (!getArgTypesCache.ContainsKey((member, rt))) {
                ResolveGeneric(rt);
                if (methodsByReturnType.TryGetValue(rt, out var dct) && dct.TryGetValue(member, out var mi))
                    getArgTypesCache[(member, rt)] = mi.GetParameters().Select(x => (NamedParam) x).ToArray();
                else
                    throw new NotImplementedException($"The method \"{rt.RName()}.{member}\" was not found.\n");
            }
            return getArgTypesCache[(member, rt)];
        }
        private static readonly Dictionary<(string method, Type returnType), NamedParam[]> getArgTypesCache 
            = new Dictionary<(string, Type), NamedParam[]>();


        private static readonly Dictionary<(Type toType, Type fromFuncType), Func<object, object, object>> funcConversions =
            new Dictionary<(Type, Type), Func<object, object, object>>();
        /// <summary>
        /// De-funcify a source object whose type involves functions of one argument (eg. [TExArgCtx->tfloat]) into an
        ///  target type (eg. [tfloat]) by applying an object of that argument type (TExArgCtx) to the
        ///  source object according to the function registered in funcConversions.
        /// <br/>If no function is registered, return the source object as-is.
        /// </summary>
        private static object Defuncify(Type targetType, Type sourceFuncType, object sourceObj, object funcArg) {
            if (funcConversions.TryGetValue((targetType, sourceFuncType), out var conv)) 
                return conv(sourceObj, funcArg);
            return sourceObj;
        }

        /// <summary>
        /// For a recorded function R member(A, B, C...), return the parameter types of the hypothetical function
        /// T->R member', such that those parameters can be meaningfully parsed by reflection code.
        /// <br/>In most cases, this is just [T->A, T->B, T->C], but it depends on rules in TryFuncify.
        /// </summary>
        public static NamedParam[] FuncifyTypes<T, R>(string member) => FuncifyTypes(typeof(T), typeof(R), member);
        public static NamedParam[] FuncifyTypes(Type t, Type r, string member) {
            if (!funcifyTypesCache.ContainsKey((member, t, r))) {
                NamedParam[] baseTypes = GetArgTypes(r, member);
                NamedParam[] fTypes = new NamedParam[baseTypes.Length];
                for (int ii = 0; ii < baseTypes.Length; ++ii) {
                    var bt = baseTypes[ii].type;
                    fTypes[ii] = baseTypes[ii].WithType(TryFuncify(t, bt, out var result) ? result : bt);
                }
                funcifyTypesCache[(member, t, r)] = fTypes;
            }
            return funcifyTypesCache[(member, t, r)];
        }
        private static readonly Dictionary<(string method, Type funcIn, Type funcOut), NamedParam[]> funcifyTypesCache 
            = new Dictionary<(string, Type, Type), NamedParam[]>();
        
        /// <summary>
        /// Dictionary mapping unparseable "wrapped" types that may occur in funcified function arguments
        /// to parseable "unwrapped" types from which they can be derived.
        /// </summary>
        public static readonly Dictionary<Type, (Type sourceType, object converter)> wrappers
            = new Dictionary<Type, (Type, object)>() {
                {typeof(EEx<bool>), (typeof(TEx<bool>), (Func<TEx<bool>, EEx<bool>>) (x => x))},
                {typeof(EEx<float>), (typeof(TEx<float>), (Func<TEx<float>, EEx<float>>) (x => x))},
                {typeof(EEx<Vector2>), (typeof(TEx<Vector2>), (Func<TEx<Vector2>, EEx<Vector2>>) (x => x))},
                {typeof(EEx<Vector3>), (typeof(TEx<Vector3>), (Func<TEx<Vector3>, EEx<Vector3>>) (x => x))},
                {typeof(EEx<Vector4>), (typeof(TEx<Vector4>), (Func<TEx<Vector4>, EEx<Vector4>>) (x => x))},
                {typeof(EEx<V2RV2>), (typeof(TEx<V2RV2>), (Func<TEx<V2RV2>, EEx<V2RV2>>) (x => x))}
            };
        
        /// <summary>
        /// Where arg is the type of a parameter for a recorded function R member(...ARG, ...),
        /// construct the type of the corresponding parameter for the hypothetical function T->R member', such that
        /// the constructed type can be parsed by reflection code with maximum generality.
        /// <br/>In most cases, this is just T->ARG. See comments in the code for more details.
        /// <br/>If the type ARG does not need to be changed, return False.
        /// </summary>
        private static bool TryFuncify(Type t, Type _arg, out Type res) {
            if (tryFuncifyCache.ContainsKey((t, _arg))) {
                //Cached result
                res = tryFuncifyCache[(t, _arg)];
                return true;
            }
            //If the type arg is a "wrapped type" like EEx<float>, then handle the unwrapped type TEx<float>
            // and add a rewrapping step to the defuncifier.
            Type wrappedType = _arg;
            (Type baseType, object? rewrapper) = wrappers.TryGetValue(wrappedType, out var wrapHandler) ?
                wrapHandler :
                (_arg, null);
            void AddDefuncifier(Type ft, Func<object, object, object> func) {
                funcConversions[(wrappedType, ft)] = rewrapper is null ?
                    func :
                    (fobj, x) => FuncInvoke(rewrapper!, Func2Type(baseType, wrappedType), func(fobj, x));
            }
            
            if (funcifiableReturnTypes.Contains(baseType)) {
                //Explicitly marked F(B)=T->B.
                var ft = res = Func2Type(t, baseType);
                AddDefuncifier(ft, (x, bpi) => FuncInvoke(x, ft, bpi));
            } else if (baseType.IsArray && TryFuncify(t, baseType.GetElementType()!, out var ftele)) {
                //Let B=[E]. F(B)=[F(E)].
                res = ftele.MakeArrayType();
                AddDefuncifier(res, (x, bpi) => {
                    var oa = x as Array ?? throw new StaticException("Couldn't arrayify");
                    var fa = Array.CreateInstance(baseType.GetElementType()!, oa.Length);
                    for (int oi = 0; oi < oa.Length; ++oi) {
                        fa.SetValue(Defuncify(baseType.GetElementType()!, ftele, oa.GetValue(oi), bpi), oi);
                    }
                    return fa;
                });
            } else if (baseType.IsConstructedGenericType && baseType.GetGenericTypeDefinition() == typeof(ValueTuple<,>) &&
                       baseType.GenericTypeArguments.Any(x => TryFuncify(t, x, out _))) {
                //Let B=<C,D>. F(B)=<F(C),F(D)>.
                var base_gts = baseType.GenericTypeArguments;
                var funced_gts = new Type[base_gts.Length];
                for (int ii = 0; ii < funced_gts.Length; ++ii) {
                    funced_gts[ii] = TryFuncify(t, base_gts[ii], out var gt) ? gt : base_gts[ii];
                }
                res = typeof(ValueTuple<,>).MakeGenericType(funced_gts);
                var tupToArr = typeof(Reflector)
                                   .GetMethod($"TupleToArr{funced_gts.Length}", BindingFlags.Static | BindingFlags.Public)
                                   ?.MakeGenericMethod(funced_gts) ??
                               throw new StaticException("Couldn't find tuple decomposition method");
                AddDefuncifier(res, (x, bpi) => {
                    var argarr = tupToArr.Invoke(null, new[] {x}) as object[] ??
                                 throw new StaticException("Couldn't decompose tuple to array");
                    for (int ii = 0; ii < funced_gts.Length; ++ii)
                        argarr[ii] = Defuncify(base_gts[ii], funced_gts[ii], argarr[ii], bpi);
                    return Activator.CreateInstance(baseType, argarr);
                });
            } else {
                res = default!;
                return false;
            }
            tryFuncifyCache[(t, wrappedType)] = res;
            return true;
        }
        private static readonly Dictionary<(Type, Type), Type> tryFuncifyCache = new Dictionary<(Type, Type), Type>();

        
        /// <summary>
        /// Return the type Func&lt;t1, t2&gt;. Results are cached.
        /// </summary>
        public static Type Func2Type(Type t1, Type t2) {
            if (!func2TypeCache.TryGetValue((t1, t2), out var tf)) {
                tf = func2TypeCache[(t1, t2)] = typeof(Func<,>).MakeGenericType(t1, t2);
            }
            return tf;
        }
        private static readonly Dictionary<(Type, Type), Type> func2TypeCache = new Dictionary<(Type, Type), Type>();

        
        /// <summary>
        /// Return a function x => func(x).
        /// </summary>
        /// <param name="func">Function to execute.</param>
        /// <param name="funcType">Type of func.</param>
        /// <returns></returns>
        public static Func<object?, object?> FuncInvoker(object func, Type funcType) =>
            x => FuncInvoke(func, funcType, x);

        /// <summary>
        /// Return func(arg).
        /// </summary>
        /// <param name="func">Function to execute.</param>
        /// <param name="funcType">Type of func.</param>
        /// <param name="arg">Argument with which to execute func.</param>
        /// <returns></returns>
        private static object FuncInvoke(object func, Type funcType, object? arg) {
            if (!funcInvokeCache.TryGetValue(funcType, out var mi)) {
                mi = funcInvokeCache[funcType] = funcType.GetMethod("Invoke") ??
                                            throw new Exception($"No invoke method found for {funcType.RName()}");
            }
            return mi.Invoke(func, new[] {arg});
        }
        private static readonly Dictionary<Type, MethodInfo> funcInvokeCache = new Dictionary<Type, MethodInfo>();

        /// <summary>
        /// For a recorded function R member(A, B, C...), given parameters of type [F(A), F(B), F(C)] (funcified on T),
        /// construct a function T->R that uses T to defuncify the parameters and pass them to R.
        /// </summary>
        /// <param name="q">Parse queue. Not required, only used to provide syntax-related warnings.</param>
        /// <param name="member">Name of the function to call.</param>
        /// <param name="funcedParams">Funcified parameters to pass to the function.
        ///  These should correspond to the types returned by <see cref="FuncifyTypes"/>.</param>
        /// <param name="result">Out value to which the resulting function T->R is written
        /// (only if the return value is true).</param>
        /// <returns>True iff 'member' exists and the function could be created.</returns>
        public static bool TryInvokeFunced<T, R>(IParseQueue? q, string member, object?[] funcedParams, out object result) {
            var rt = typeof(R);
            ResolveGeneric(rt);
            if (methodsByReturnType.TryGet2(rt, member, out var f)) {
                if (q?.Ctx.props.warnPrefix == true && Attribute.GetCustomAttributes(f).Any(x =>
                    x is WarnOnStrictAttribute wa && (int) q.Ctx.props.strict >= wa.strictness)) {
                    Logs.Log(
                        $"Line {q.GetLastLine()}: The method \"{member}\" is not permitted for use in a script with strictness {q.Ctx.props.strict}. You might accidentally be using the prefix version of an infix function.",
                        true, LogLevel.WARNING);
                }
                result = (Func<T, R>) (bpi => {
                    var baseTypes = GetArgTypes(rt, member);
                    var funcTypes = FuncifyTypes<T, R>(member);
                    var baseParams = new object[baseTypes.Length];
                    for (int ii = 0; ii < baseTypes.Length; ++ii) 
                        //Convert from funced object to base object (eg. TExArgCtx->TEx<float> to TEx<float>)
                        baseParams[ii] = Defuncify(baseTypes[ii].type, funcTypes[ii].type, funcedParams[ii]!, bpi!);
                    return (R) f.Invoke(null, baseParams);
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