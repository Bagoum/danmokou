using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BagoumLib;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using JetBrains.Annotations;
using Danmokou.SM.Parsing;
using Scriptor;
using Scriptor.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using static BagoumLib.Reflection.ReflectionUtils;

namespace Danmokou.Reflection {
public static partial class Reflector {

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


        public static LiftedMethodSignature<T, R>? GetFuncedSignature<T, R>(string member) {
            if (TryGetMember<R>(member) is { } sig)
                return LiftedMethodSignature<T, R>.Lift(sig);
            return null;
        }
    }

}
}