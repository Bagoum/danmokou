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

    public static void DestroyAll() {
        v2Data.Clear();
        v2Names.Clear();
        fData.Clear();
        fNames.Clear();
    }

    private static void ClearValues<T>(List<SafeResizableArray<T>> vals) where T : struct {
        for (int ii = 0; ii < vals.Count; ++ii) {
            vals[ii].Empty();
        }
    }

    //Hoisted data has two layers: a string-based name layer, and an int-based indexing layer.
    private static readonly List<SafeResizableArray<Vector2>> v2Data = new List<SafeResizableArray<Vector2>>();
    private static readonly Dictionary<string, int> v2Names = new Dictionary<string, int>();

    public static SafeResizableArray<Vector2> RegisterV2(string name) {
        if (!v2Names.TryGetValue(name, out int ind)) {
            v2Names[name] = ind = v2Data.Count;
            v2Data.Add(new SafeResizableArray<Vector2>());
        }
        return v2Data[ind];
    }

    private static readonly List<SafeResizableArray<float>> fData = new List<SafeResizableArray<float>>();
    private static readonly Dictionary<string, int> fNames = new Dictionary<string, int>();

    public static SafeResizableArray<float> RegisterF(string name) {
        if (!fNames.TryGetValue(name, out int ind)) {
            fNames[name] = ind = fData.Count;
            fData.Add(new SafeResizableArray<float>());
        }
        return fData[ind];
    }

    public static SafeResizableArray<T> Register<T>(string name) {
        var t = typeof(T);
        if (t == ExUtils.tv2) return (RegisterV2(name) as SafeResizableArray<T>)!;
        if (t == ExUtils.tfloat) return (RegisterF(name) as SafeResizableArray<T>)!;
        throw new Exception($"No generic PublicDataHoisting registration for type {t.Name}");
    }

    public static Dictionary<string, int> GetNameDictForType<T>() {
        var t = typeof(T);
        if (t == ExUtils.tv2) return v2Names;
        if (t == ExUtils.tfloat) return fNames;
        throw new Exception($"No PublicDataHoisting dictionary for type {t.Name}");
    }

    public static string GetRandomValidKey<T>() {
        var dict = GetNameDictForType<T>();
        while (true) {
            var s = RNG.RandString();
            if (!dict.ContainsKey(s)) return s;
        }
    }

    public static float GetF(string name, int key) => fData[fNames[name]].SafeGet(key);
}
}