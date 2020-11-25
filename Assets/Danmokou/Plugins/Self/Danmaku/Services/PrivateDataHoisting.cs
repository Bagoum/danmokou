using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DMath;
using UnityEngine;

namespace Danmaku {
public static class PrivateDataHoisting {
    public static void DestroyAll() {
        ClearNames();
        ClearAllValues();
    }
    private static void ClearNames() {
        keyNames.Clear();
        lastKey = 0;
    }

    public static uint GetKey(string name) {
        if (keyNames.TryGetValue(name, out var res)) return res;
        keyNames[name] = lastKey;
        return lastKey++;
    }
    private static readonly Dictionary<string, uint> keyNames = new Dictionary<string, uint>();
    private static uint lastKey = 0;
    private static readonly HashSet<uint> idsInUse = new HashSet<uint>();
    private static readonly Dictionary<uint, Dictionary<uint, float>> fData = new Dictionary<uint, Dictionary<uint, float>>();
    private static readonly Dictionary<uint, Dictionary<uint, Vector2>> v2Data = new Dictionary<uint, Dictionary<uint, Vector2>>();
    private static readonly Dictionary<uint, Dictionary<uint, Vector3>> v3Data = new Dictionary<uint, Dictionary<uint, Vector3>>();
    private static readonly Dictionary<uint, Dictionary<uint, V2RV2>> rv2Data = new Dictionary<uint, Dictionary<uint, V2RV2>>();
    public static Func<TExPI, TEx> GetValue(Reflector.ExType typ, string name) => bpi => GetValue(bpi, typ, name);
    public static TEx GetValue(TExPI bpi, Reflector.ExType typ, string name) {
        if (typ == Reflector.ExType.RV2) return GetValue(rv2Data, bpi, name);
        if (typ == Reflector.ExType.V3) return GetValue(v3Data, bpi, name);
        if (typ == Reflector.ExType.V2) return GetValue(v2Data, bpi, name);
        return GetValue(fData, bpi, name);
    }

    private static TEx GetValue<K1, K2, V>(Dictionary<K1, Dictionary<K2, V>> dict, TExPI bpi, string name) =>
        Expression.Constant(dict).DictGet(bpi.id).DictGet(Expression.Constant(GetKey(name)));
    //Expression.Constant(dict)
    //    .DictSafeGet<K1, Dictionary<K2, V>>(bpi.id, "PrivateHoist data")
    //    .DictSafeGet<K2, V>(Expression.Constant(GetKey(name)), 
    //    $"<{typeof(K2).RName()},{typeof(V).RName()}>: {name}, {GetKey(name)}");

    private static T GetValue<T>(Dictionary<uint, Dictionary<uint, T>> data, uint id, uint key) {
        return data[id][key];
    }

    public static float GetFloat(uint id, uint key) => GetValue(fData, id, key);
    
    private static TEx UpdateValue<K1, K2, V>(Dictionary<K1, Dictionary<K2, V>> dict, TExPI bpi, string name, Expression value) =>
        Expression.Constant(dict).DictGet(bpi.id).DictSet(Expression.Constant(GetKey(name)), value);
    public static Expression UpdateValue(TExPI bpi, Reflector.ExType typ, string name, Expression value) {
        if (typ == Reflector.ExType.RV2) return UpdateValue(rv2Data, bpi, name, value);
        if (typ == Reflector.ExType.V3) return UpdateValue(v3Data, bpi, name, value);
        if (typ == Reflector.ExType.V2) return UpdateValue(v2Data, bpi, name, value);
        return UpdateValue(fData, bpi, name, value);
    }

    private static void UpdateValue<T>(Dictionary<uint, Dictionary<uint, T>> dict, uint id, uint key, T value) {
        dict[id][key] = value;
    }

    public static void UpdateValue(uint id, uint key, float value) => UpdateValue(fData, id, key, value);
    public static void UpdateValue(uint id, uint key, Vector2 value) => UpdateValue(v2Data, id, key, value);

    public static void ClearValues(HashSet<uint> preserve) {
        foreach (var id in idsInUse.ToArray()) {
            if (!preserve.Contains(id)) idsInUse.Remove(id);
        }
        fData.TryRemoveAndCacheAllExcept(preserve);
        v2Data.TryRemoveAndCacheAllExcept(preserve);
        v3Data.TryRemoveAndCacheAllExcept(preserve);
        rv2Data.TryRemoveAndCacheAllExcept(preserve);
    }

    private static void ClearAllValues() {
        idsInUse.Clear();
        fData.TryRemoveAndCacheAll();
        v2Data.TryRemoveAndCacheAll();
        v3Data.TryRemoveAndCacheAll();
        rv2Data.TryRemoveAndCacheAll();
    }

    /// <summary>
    /// DO NOT USE, THIS IS FOR ACCESS BY DATAHOISTING ONLY
    /// </summary>
    public static void __Destroy(uint id) {
        idsInUse.Remove(id);
        fData.TryRemoveAndCache(id);
        v2Data.TryRemoveAndCache(id);
        v3Data.TryRemoveAndCache(id);
        rv2Data.TryRemoveAndCache(id);
    }

    private static void UploadAddOne(Reflector.ExType ext, string varName, GenCtx gcx, uint id) {
        uint varId = GetKey(varName);
        if (ext == Reflector.ExType.Float) fData.SetDefaultSet(id, varId, gcx.GetFloatOrThrow(varName));
        else if (ext == Reflector.ExType.V2) v2Data.SetDefaultSet(id, varId, gcx.V2s.GetOrThrow(varName, "GCX V2 values"));
        else if (ext == Reflector.ExType.V3) v3Data.SetDefaultSet(id, varId, gcx.V3s.GetOrThrow(varName, "GCX V3 values"));
        else if (ext == Reflector.ExType.RV2) rv2Data.SetDefaultSet(id, varId, gcx.RV2s.GetOrThrow(varName, "GCX RV2 values"));
        else throw new Exception($"Cannot hoist GCX data {varName}<{ext}>.");
    }

    //Webgl demo hackery
    public static void UploadAllFloats(GenCtx gcx, uint id) {
        idsInUse.Add(id);
        foreach (var v in gcx.fs.Keys) UploadAddOne(Reflector.ExType.Float, v, gcx, id);
    }

    /// <summary>
    /// Uploads the provided variables on the GCX into private data hoisting.
    /// If the id already exists and newUpload is set, assigns a new id. 
    /// </summary>
    /*public static void Upload(bool newUpload, (Reflector.ExType, string)[] boundVars, GenCtx gcx, ref uint id) {
        if (newUpload) UploadNew(boundVars, gcx, ref id);
        else UploadAdd(boundVars, gcx, id);
    }*/
    public static void UploadNew((Reflector.ExType, string)[] boundVars, GenCtx gcx, ref uint id) {
        ConfirmId(ref id);
        UploadAdd(boundVars, gcx, id);
    }
    public static void UploadAdd((Reflector.ExType, string)[] boundVars, GenCtx gcx, uint id) {
        idsInUse.Add(id);
        for (int ii = 0; ii < boundVars.Length; ++ii) {
            var (ext, varNameS) = boundVars[ii];
            UploadAddOne(ext, varNameS, gcx, id);
        }
        for (int ii = 0; ii < gcx.exposed.Count; ++ii) {
            var (ext, varNameS) = gcx.exposed[ii];
            UploadAddOne(ext, varNameS, gcx, id);
        }
    }

    private static void ConfirmId(ref uint id) {
        while (true) {
            if (!idsInUse.Contains(id)) break;
            ++id;
        }
    }

    public static uint Copy(uint from, uint to) {
        ConfirmId(ref to);
        fData.DuplicateIfExists(from, to);
        v2Data.DuplicateIfExists(from, to);
        v3Data.DuplicateIfExists(from, to);
        rv2Data.DuplicateIfExists(from, to);
        return to;
    }

    public static GenCtx GetGCX(uint id) {
        var gcx = GenCtx.New(null, V2RV2.Zero);
        var fd = fData.GetOrDefault(id);
        var v2d = v2Data.GetOrDefault(id);
        var v3d = v3Data.GetOrDefault(id);
        var rvd = rv2Data.GetOrDefault(id);
        foreach (var sk in keyNames) {
            if (fd?.ContainsKey(sk.Value) ?? false) gcx.fs[sk.Key] = fd[sk.Value];
            if (v2d?.ContainsKey(sk.Value) ?? false) gcx.v2s[sk.Key] = v2d[sk.Value];
            if (v3d?.ContainsKey(sk.Value) ?? false) gcx.v3s[sk.Key] = v3d[sk.Value];
            if (rvd?.ContainsKey(sk.Value) ?? false) gcx.rv2s[sk.Key] = rvd[sk.Value];
        }
        return gcx;
    }
    
#if UNITY_EDITOR
    public static HashSet<uint> IDs => idsInUse;
    public static Dictionary<uint, Dictionary<uint, float>> Fd => fData;
#endif
}
}