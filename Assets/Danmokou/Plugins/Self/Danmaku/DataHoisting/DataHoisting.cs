using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DMK.Core;
using DMK.Expressions;
using DMK.Reflection;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace DMK.DataHoist {
public static class DataHoisting {
    public static void ClearValues() {
        PublicDataHoisting.ClearValues();
        PrivateDataHoisting.ClearValues(preserve);
        //PlayerFire is not cleared
        foreach (var d in fData) d.ClearExcept(preserve);
        foreach (var i in intData) i.ClearExcept(preserve);
        foreach (var v2 in v2Data) v2.ClearExcept(preserve);
        foreach (var h in keyData) h.ClearExcept(preserve);
    }

    public static void DestroyAll() {
        PublicDataHoisting.DestroyAll();
        PrivateDataHoisting.DestroyAll();
        PlayerFireDataHoisting.DestroyAll();
        fData.Clear();
        intData.Clear();
        v2Data.Clear();
        keyData.Clear();
        preserve.Clear();
    }

    /// <summary>
    /// Mark the id so it won't be cleared via ClearValues (called on boss phase clear).
    /// Used exclusively for player bullets.
    /// </summary>
    public static void PreserveID(uint id) => preserve.Add(id);

    public static void Destroy(uint id) {
        preserve.Remove(id);
        PrivateDataHoisting.__Destroy(id);
    }

    private static readonly List<Dictionary<uint, float>> fData = new List<Dictionary<uint, float>>();
    private static readonly List<Dictionary<uint, int>> intData = new List<Dictionary<uint, int>>();
    private static readonly List<Dictionary<uint, Vector2>> v2Data = new List<Dictionary<uint, Vector2>>();
    private static readonly List<HashSet<uint>> keyData = new List<HashSet<uint>>();
    private static readonly HashSet<uint> preserve = new HashSet<uint>();

    public static Ex GetClearableDict<T>() {
        var t = typeof(T);
        if (t == ExUtils.tfloat) return GetClearableDictF();
        else if (t == ExUtils.tv2) return GetClearableDictV2();
        else throw new Exception($"No generic clearable-dict handling in DataHoisting for type {t.RName()}");
    }
    public static Ex GetClearableDictF() {
        var d = new Dictionary<uint, float>();
        fData.Add(d);
        return Ex.Constant(d);
    }
    public static Ex GetClearableDictV2() {
        var d = new Dictionary<uint, Vector2>();
        v2Data.Add(d);
        return Ex.Constant(d);
    }
    public static Ex GetClearableDictInt() {
        var d = new Dictionary<uint,int>();
        intData.Add(d);
        return Ex.Constant(d);
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