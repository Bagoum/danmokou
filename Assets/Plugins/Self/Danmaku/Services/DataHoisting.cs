using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DMath;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace Danmaku {
public static class DataHoisting {
    public static void ClearValues() {
        PublicDataHoisting.ClearValues();
        PrivateDataHoisting.ClearValues();
        foreach (var d in fData) d.Clear();
        foreach (var v2 in v2Data) v2.Clear();
        foreach (var h in keyData) h.Clear();
    }

    public static void DestroyAll() {
        PublicDataHoisting.DestroyAll();
        PrivateDataHoisting.ClearValuesAndNames();
        PlayerFireDataHoisting.DestroyAll();
        fData.Clear();
        v2Data.Clear();
        keyData.Clear();
    }

    private static readonly List<Dictionary<uint, float>> fData = new List<Dictionary<uint, float>>();
    private static readonly List<Dictionary<uint, Vector2>> v2Data = new List<Dictionary<uint, Vector2>>();
    private static readonly List<HashSet<uint>> keyData = new List<HashSet<uint>>();
    public static Ex GetClearableDictV2() {
        var d = new Dictionary<uint, Vector2>();
        v2Data.Add(d);
        return Ex.Constant(d);
    }
    public static Ex GetClearableDict<T>() {
        var t = typeof(T);
        if (t == ExUtils.tv2) return GetClearableDictV2();
        throw new Exception($"Inline data hoisting not supported for type {t.RName()}. This is a static error.");
    }
    public static Ex GetClearableSet() {
        var d = new HashSet<uint>();
        keyData.Add(d);
        return Ex.Constant(d);
    }
    
    public static Dictionary<uint, Vector2> GetClearableDictV2_() {
        var d = new Dictionary<uint, Vector2>();
        v2Data.Add(d);
        return d;
    }
}
}