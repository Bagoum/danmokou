﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DMK.Core;
using DMK.DMath;
using FastExpressionCompiler;
using UnityEngine;

namespace DMK.Reflection {
public static partial class Reflector {
#if UNITY_EDITOR
    private const string AOT_GEN = "Assets/Danmokou/Plugins/Self/Danmaku/AoTHelper_CG.cs";
    private static readonly Type[] autogenGenerics = {
        typeof(float), typeof(bool), typeof(Vector2), typeof(Vector3),
        typeof(Vector4), typeof(V2RV2)
    };

    private static string GenerateFile(IEnumerable<string> funcs) => $@"//----------------------
// <auto-generated>
//     Generated by Danmokou reflection analysis for use on AOT/IL2CPP platforms.
//     This file ensures that generic methods used by reflection are properly generated by the AOT compiler.
// </auto-generated>
//----------------------

using System;

namespace DMK.Reflection {{
public static class AoTHelper_CG {{
    public static void UsedOnlyForAOTCodeGeneration() {{
        {string.Join("\n\t\t", funcs)}

        throw new InvalidOperationException();
    }}

}}
}}
";
    
    public static void GenerateAoT() {
        List<string> funcs = new List<string>();
        HashSet<MethodInfo> mis = new HashSet<MethodInfo>(); //weed out alias duplicates
        void AddConstructedMethod(MethodInfo mi) {
            if (mis.Contains(mi)) return;
            mis.Add(mi);
            var type_prms = string.Join(", ", mi.GetGenericArguments().Select(t => t.ToCode()));
            var args = string.Join(", ", mi.GetParameters().Length.Range().Select(_ => "default"));
            funcs.Add($"{mi.DeclaringType.ToCode()}.{mi.Name}<{type_prms}>({args});");
        }
        foreach (var mi in postAggregators.Values.SelectMany(v => v.Values).Select(pa => pa.invoker).Concat(ReflectionData.MethodsByReturnType.Values.SelectMany(v => v.Values))) {
            if (!mi.IsGenericMethod) continue;
            if (mi.IsGenericMethodDefinition) {
                //nonconstructed method (eg. StopSampling<T>)
                if (mi.GetGenericArguments().Length == 1) {
                    foreach (var t in autogenGenerics) {
                        AddConstructedMethod(mi.MakeGenericMethod(t));
                    }
                } else {
                    //pray
                }
            } else {
                //constructed method (eg. ParticleControl)
                AddConstructedMethod(mi);
            }
        }
        FileUtils.WriteString(AOT_GEN, GenerateFile(funcs));
    }
    
#endif
}
}