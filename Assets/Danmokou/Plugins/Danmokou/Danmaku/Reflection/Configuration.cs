using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BagoumLib;
using BagoumLib.Unification;
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
    
    /// <summary>
    /// Return the type Func&lt;t1, t2&gt;. Results are cached.
    /// </summary>
    public static Type Func2Type(Type t1, Type t2) {
        if (!func2TypeCache.TryGetValue((t1, t2), out var tf)) {
            tf = func2TypeCache[(t1, t2)] = typeof(Func<,>).MakeGenericType(t1, t2);
        }
        return tf;
    }
    private static readonly Dictionary<(Type, Type), Type> func2TypeCache = new();

    public static class ReflectionData {
        public static Dictionary<string, T> SanitizedKeyDict<T>() => new(SanitizedStringComparer.Singleton);
        /// <summary>
        /// Contains all reflectable methods, generic and non-generic, except those tagged as BDSL2Operator.
        /// <br/>Methods are keyed by lowercase method name and may be overloaded.
        /// <br/>Liftable methods are *not* lifted,
        ///   and generic methods are non-specialized except when specializations
        ///   are specified via <see cref="GAliasAttribute"/>.
        /// </summary>
        public static readonly Dictionary<string, List<MethodSignature>> AllBDSL2Methods =
            SanitizedKeyDict<List<MethodSignature>>();
        public static readonly Dictionary<Type, Dictionary<string, List<MethodSignature>>> BDSL2ExtensionMethods =
            new();

        public static void RecordExtensionMethodsInClass(Type declaring) {
            foreach (var mi in declaring.GetMethods())
                MaybeRecordExtensionMethod(MethodSignature.Get(mi));
        }
        private static void MaybeRecordExtensionMethod(MethodSignature sig) {
            if (sig.Member is TypeMember.Method { IsExtension: true }) {
                var thisTd = sig.SharedType.Arguments[0];
                if ((thisTd.Resolve().LeftOrNull ?? (thisTd as TypeDesignation.Known)?.Typ) is { } thisTyp) {
                    BDSL2ExtensionMethods.AddToList2(thisTyp, sig.Name, sig);
                }
            }
        }

        public static IEnumerable<MethodSignature> ExtensionMethods(Type instTyp, string? name) {
            IEnumerable<MethodSignature> _extForResolvedOrConstr(Type t) =>
                BDSL2ExtensionMethods.TryGetValue(t, out var map) ?
                    (name is null ? map.Values.SelectMany(x => x) :
                        map.TryGetValue(name, out var named) ? named : Array.Empty<MethodSignature>()) :
                    Array.Empty<MethodSignature>();
            foreach (var ms in _extForResolvedOrConstr(instTyp))
                yield return ms;
            if (instTyp.IsConstructedGenericType)
                foreach (var ms in _extForResolvedOrConstr(instTyp.GetGenericTypeDefinition()))
                    yield return ms;
        }
        
        /// <summary>
        /// Contains non-generic methods keyed by return type.
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<string, MethodInfo>> methodsByReturnType
            = new();
    #if UNITY_EDITOR
        /// <summary>
        /// Exposing methodsByReturnType for AoT expression baking.
        /// </summary>
        public static Dictionary<Type, Dictionary<string, MethodInfo>> MethodsByReturnType => methodsByReturnType;
    #endif
        /// <summary>
        /// All generic methods recorded, keyed by method name.
        /// </summary>
        private static readonly Dictionary<string, MethodInfo> genericMethods =
            SanitizedKeyDict<MethodInfo>();
        
        [PublicAPI]
        public static Dictionary<string, MethodInfo> AllMethods() {
            var dict = new Dictionary<string, MethodInfo>();
            foreach (var tdict in methodsByReturnType.Values)
                tdict.CopyInto(dict);
            foreach (var (m, mi) in genericMethods)
                dict[m] = mi;
            return dict;
        }

        /// <summary>
        /// Record public static methods in a class for reflection use.
        /// </summary>
        /// <param name="repo">Class of methods.</param>
        /// <param name="returnType">Optional. If provided, only records methods with this return type.</param>
        public static void RecordPublic(Type repo, Type? returnType = null) => 
            Record(repo, returnType, BindingFlags.Static | BindingFlags.Public);
        
        private static void Record(Type repo, Type? returnType, BindingFlags flags) {
            foreach (var m in repo
                         .GetMethods(flags)
                         .Where(mi => (returnType == null || mi.ReturnType == returnType) 
                                      && !mi.Attributes.HasFlag(MethodAttributes.SpecialName)))
                RecordMethod(m);
        }

        private static void RecordMethod(MethodInfo mi) {
            void AddBDSL2(string name, MethodInfo method) {
                AddBDSL2_Sig(name, MethodSignature.Get(method));
            }
            void AddBDSL2_Sig(string name, MethodSignature sig) {
                AllBDSL2Methods.AddToList(name.ToLower(), sig);
            }
            void AddMI(string name, MethodInfo method) {
                name = name.ToLower();
                if (method.IsGenericMethodDefinition) {
                    if (genericMethods.ContainsKey(name))
                        throw new Exception($"Duplicate generic method by name {name}");
                    genericMethods[name] = method;
                } else {
                    if (!methodsByReturnType.TryGetValue(method.ReturnType, out var d))
                        methodsByReturnType[method.ReturnType] = d = SanitizedKeyDict<MethodInfo>();
                    d[name] = method;
                }
            }
            bool addNormalBDSL1 = true;
            bool addNormalBDSL2 = true;
            var addBDSL1 = true;
            var addBDSL2 = true;
            bool isExBoundary = false;
            FallthroughAttribute? fallthrough = null;
            var attrs = Attribute.GetCustomAttributes(mi);
            foreach (var attr in attrs) {
                switch (attr) {
                    case DontReflectAttribute:
                        return;
                    case ExpressionBoundaryAttribute:
                        isExBoundary = true;
                        break;
                    case FallthroughAttribute fa:
                        fallthrough = fa;
                        break;
                    case BDSL2OperatorAttribute or BDSL1OnlyAttribute:
                        addBDSL2 = false;
                        break;
                    case BDSL2OnlyAttribute:
                        addBDSL1 = false;
                        break;
                }
            }
            foreach (var attr in attrs) {
                if (attr is AliasAttribute aa) {
                    if (addBDSL1) AddMI(aa.alias, mi);
                    if (addBDSL2) AddBDSL2(aa.alias, mi);
                } else if (attr is GAliasAttribute ga) {
                    var gsig = MethodSignature.Get(mi) as GenericMethodSignature;
                    var rsig = gsig!.Specialize(ga.type);
                    if (addBDSL1 && rsig.Member is TypeMember.Method m) AddMI(ga.alias, m.Mi);
                    if (addBDSL2) AddBDSL2_Sig(ga.alias, rsig);
                    addNormalBDSL1 = false;
                    addNormalBDSL2 &= ga.reflectOriginal;
                }
            }
            if (fallthrough != null) {
                var sig = MethodSignature.Get(mi);
                if (sig.Params.Length != 1)
                    throw new StaticException($"Fallthrough methods must have exactly one argument: {mi.Name}");
                if (FallThroughOptions.ContainsKey(mi.ReturnType))
                    throw new StaticException($"Cannot have multiple fallthroughs for the same return type {mi.ReturnType}");
                if (isExBoundary)
                    AddCompileOption(mi);
                else 
                    FallThroughOptions[mi.ReturnType] = (fallthrough, sig);
            }
            if (addNormalBDSL1 && addBDSL1) AddMI(mi.Name, mi);
            if (addNormalBDSL2 && addBDSL2) AddBDSL2(mi.Name, mi);
        }

        /// <inheritdoc cref="TryGetMember"/>
        public static MethodSignature? TryGetMember<R>(string member) => TryGetMember(typeof(R), member);

        /// <summary>
        /// Get the method description for a method named `member` that returns type `rt`
        /// (or return null if it does not exist).
        /// <br/>If the method is generic, will specialize it before returning.
        /// </summary>
        public static MethodSignature? TryGetMember(Type rt, string member) {
            if (getArgTypesCache.TryGetValue((member, rt), out var res))
                return res;
            if (methodsByReturnType.TryGetValue(rt, out var dct) && dct.TryGetValue(member, out var mi))
                return getArgTypesCache[(member, rt)] = MethodSignature.Get(mi);
            else if (genericMethods.TryGetValue(member, out var gmi)) {
                if (ConstructedGenericTypeMatch(rt, gmi.ReturnType, out var mapper)) {
                    var sig = (MethodSignature.Get(gmi) as GenericMethodSignature)!;
                    var specTypes = gmi.GetGenericArguments().ToArray();
                    for (int ii = 0; ii < specTypes.Length; ++ii) {
                        if (mapper.TryGetValue(specTypes[ii], out var mt))
                            specTypes[ii] = mt;
                        else if (sig.Member.BaseMi.GetCustomAttributes<BDSL1AutoSpecializeAttribute>()
                                     .FirstOrDefault(x => x.typeIndex == ii) is { } attr)
                            specTypes[ii] = attr.specializeAs;
                        else
                            throw new StaticException($"{sig.AsSignature} cannot be thoroughly specialized in BDSL1");
                    }
                    return getArgTypesCache[(member, rt)] = sig.Specialize(specTypes);
                //Memo the null result in this case since we don't want to recompute ConstructedGenTypeMatch
                } else return getArgTypesCache[(member, rt)] = null;
            }
            //Don't memo the null result in the trivial case
            return null;
        }

        //for language server use
        public static IEnumerable<(string, MethodSignature)> MethodsAndGenericsForType(Type rt) {
            if (methodsByReturnType.TryGetValue(rt, out var dct))
                foreach (var (k, v) in dct)
                    yield return (k, MethodSignature.Get(v));
            foreach (var (k, v) in genericMethods) 
                if (ConstructedGenericTypeMatch(rt, v.ReturnType, out var mapper)) {
                    var gargs = v.GetGenericArguments()
                        //GetValueOrDefault doesn't work with language server
                        .Select(g => mapper.TryGetValue(g, out var v) ? v : null)
                        .ToArray();
                    if (gargs.All(x => x != null))
                        yield return (k, (MethodSignature.Get(v) as GenericMethodSignature)!.Specialize(gargs!));
                }
        }

        private static readonly Dictionary<(string method, Type returnType), MethodSignature?> getArgTypesCache 
            = new();

        private static readonly Dictionary<(Type toType, Type fromFuncType), Func<object, object, object>> funcConversions =
            new();
        
        /// <summary>
        /// De-funcify a source object whose type involves functions of one argument (eg. [TExArgCtx->tfloat]) into an
        ///  target type (eg. [tfloat]) by applying an object of that argument type (TExArgCtx) to the
        ///  source object according to the function registered in funcConversions.
        /// <br/>If no function is registered, return the source object as-is.
        /// </summary>
        public static object Defuncify(Type targetType, Type sourceFuncType, object sourceObj, object funcArg) {
            if (funcConversions.TryGetValue((targetType, sourceFuncType), out var conv)) 
                return conv(sourceObj, funcArg);
            return sourceObj;
        }

        public static LiftedMethodSignature<T, R>? GetFuncedSignature<T, R>(string member) {
            if (TryGetMember<R>(member) is { } sig)
                return LiftedMethodSignature<T, R>.Lift(sig);
            return null;
            
        }

        /// <summary>
        /// Where arg is the type of a parameter for a recorded function R member(...ARG, ...),
        /// construct the type of the corresponding parameter for the hypothetical function T->R member', such that
        /// the constructed type can be parsed by reflection code with maximum generality.
        /// <br/>In most cases, this is just T->ARG. See comments in the code for more details.
        /// <br/>If the type ARG does not need to be changed, return False (and copy ARG into RES).
        /// </summary>
        public static bool LiftType(Type t, Type arg, out Type res) {
            if (tryFuncifyCache.ContainsKey((t, arg))) {
                //Cached result
                res = tryFuncifyCache[(t, arg)];
                return true;
            }
            //eg. arg = TEx<float>, funcedType = Func<TExArgCtx, TEx<float>
            // func has "real" type (FuncedType) -> TExArgCtx -> (Arg)
            void AddDefuncifier(Type funcedType, Func<object, object, object> func) {
                funcConversions[(arg, funcedType)] = func;
            }
            
            if (funcifiableReturnTypes.Contains(arg) || (arg.IsGenericType && funcifiableReturnTypeGenerics.Contains(arg.GetGenericTypeDefinition()))) {
                //Explicitly marked F(B)=T->B.
                var ft = res = Func2Type(t, arg);
                AddDefuncifier(ft, (x, bpi) => FuncInvoke(x, ft, bpi));
            } else if (arg.IsArray && LiftType(t, arg.GetElementType()!, out var ftele)) {
                //Let B=[E]. F(B)=[F(E)].
                res = ftele.MakeArrayType();
                AddDefuncifier(res, (x, bpi) => {
                    var oa = x as Array ?? throw new StaticException("Couldn't arrayify");
                    var fa = Array.CreateInstance(arg.GetElementType()!, oa.Length);
                    for (int oi = 0; oi < oa.Length; ++oi) {
                        fa.SetValue(Defuncify(arg.GetElementType()!, ftele, oa.GetValue(oi), bpi), oi);
                    }
                    return fa;
                });
            } else if (arg.IsConstructedGenericType && arg.GetGenericTypeDefinition() == typeof(ValueTuple<,>) &&
                       arg.GenericTypeArguments.Any(x => LiftType(t, x, out _))) {
                //Let B=<C,D>. F(B)=<F(C),F(D)>.
                var base_gts = arg.GenericTypeArguments;
                var funced_gts = new Type[base_gts.Length];
                for (int ii = 0; ii < funced_gts.Length; ++ii) {
                    funced_gts[ii] = LiftType(t, base_gts[ii], out var gt) ? gt : base_gts[ii];
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
                    return Activator.CreateInstance(arg, argarr);
                });
            } else {
                res = arg;
                return false;
            }
            tryFuncifyCache[(t, arg)] = res;
            return true;
        }

        private static readonly Dictionary<(Type, Type), Type> tryFuncifyCache = new();

        /// <summary>
        /// Return func(arg).
        /// </summary>
        /// <param name="func">Function to execute.</param>
        /// <param name="funcType">Type of func.</param>
        /// <param name="arg">Argument with which to execute func.</param>
        /// <returns></returns>
        private static object FuncInvoke(object func, Type funcType, object? arg) {
            if (func.GetType() != funcType) //TODO verify usage of this exception for BDSL2
                funcType = func.GetType();
            
            if (!funcInvokeCache.TryGetValue(funcType, out var mi)) {
                mi = funcInvokeCache[funcType] = funcType.GetMethod("Invoke") ??
                                            throw new Exception($"No invoke method found for {funcType.SimpRName()}");
            }
            return mi.Invoke(func, new[] {arg});
        }
        private static readonly Dictionary<Type, MethodInfo> funcInvokeCache = new();
        


    }

}
}