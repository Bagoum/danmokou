using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using BagoumLib.DataStructures;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;
using UnityEngine;
using Random = System.Random;

namespace Danmokou.DataHoist {
public static class PublicDataHoisting {

    public static void ClearAllValues() {
        ClearValues(v2Data);
        ClearValues(fData);
    }

    private static void ClearValues<T>(Dictionary<string, SafeResizableArray<T>> vals) {
        foreach (var v in vals.Values)
            v.EmptyAndReset();
    }

    private static readonly Dictionary<string, SafeResizableArray<Vector2>> v2Data = new();
    private static readonly Dictionary<string, SafeResizableArray<float>> fData = new();
    public static SafeResizableArray<Vector2> RegisterV2(string name) {
        return v2Data.TryGetValue(name, out var data) ? data : v2Data[name] = new();
    }
    public static SafeResizableArray<float> RegisterF(string name) {
        return fData.TryGetValue(name, out var data) ? data : fData[name] = new();
    }

    public static SafeResizableArray<T> Register<T>(string name) {
        var t = typeof(T);
        if (t == ExUtils.tv2) return (RegisterV2(name) as SafeResizableArray<T>)!;
        if (t == ExUtils.tfloat) return (RegisterF(name) as SafeResizableArray<T>)!;
        throw new Exception($"No generic PublicDataHoisting registration for type {t.Name}");
    }

    private static Dictionary<string, SafeResizableArray<T>> GetDictForType<T>() {
        var t = typeof(T);
        if (t == ExUtils.tv2) return (v2Data as Dictionary<string, SafeResizableArray<T>>)!;
        if (t == ExUtils.tfloat) return (fData as Dictionary<string, SafeResizableArray<T>>)!;
        throw new Exception($"No PublicDataHoisting dictionary for type {t.Name}");
    }

    public static string GetRandomValidKey<T>() {
        var dict = GetDictForType<T>();
        while (true) {
            var s = RNG.RandString();
            if (!dict.ContainsKey(s)) return s;
        }
    }

    public static float GetF(string name, int key) => fData[name].SafeGet(key);
}
}