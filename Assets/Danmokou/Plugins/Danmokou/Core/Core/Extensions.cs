﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Functional;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Danmokou.Core {
public static class Extensions {
    public static Maybe<T> ToMaybe<T>(this T? obj) where T : Object {
        return obj == null ? Maybe<T>.None : obj;
    }
    public static int CountOf(this string s, char c) {
        int ct = 0;
        for (int ii = 0; ii < s.Length; ++ii) {
            if (s[ii] == c) ++ct;
        }
        return ct;
    }

    public static void Show(this VisualElement ve) => ve.style.display = DisplayStyle.Flex;
    public static void Hide(this VisualElement ve) => ve.style.display = DisplayStyle.None;

    public static string? Or(this string? x, string? y) =>
        string.IsNullOrWhiteSpace(x) ? y : x;

    /// <summary>
    /// Map an array into a list from <see cref="ListCache{T}"/>.
    /// </summary>
    public static List<U> MapIntoCachedList<T, U>(this T[] arr, Func<T, U> map) {
        var lis = ListCache<U>.Get();
        for (int ii = 0; ii < arr.Length; ++ii) {
            lis.Add(map(arr[ii]));
        }
        return lis;
    }
}


public static class DictExtensions {

    public static void SetDefaultSet<K, K2, V>(this Dictionary<K, Dictionary<K2, V>> dict, K key, K2 key2, V value) {
        if (!dict.TryGetValue(key, out var data)) {
            data = dict[key] = DictCache<K2, V>.Get();
        }
        data[key2] = value;
    }

    public static void DuplicateIfExists<K, K2, V>(this Dictionary<K, Dictionary<K2, V>> source, K key, K key2) {
        if (source.TryGetValue(key, out var data)) {
            var into = source[key2] = DictCache<K2, V>.Get();
            data.CopyInto(into);
        }
    }

    public static void TryRemoveAndCache<K, K2, V>(this Dictionary<K, Dictionary<K2, V>> dict, K key) {
        if (dict.TryGetValue(key, out var data)) {
            DictCache<K2, V>.Consign(data);
            dict.Remove(key);
        }
    }

    public static void TryRemoveAndCacheAll<K, K2, V>(this Dictionary<K, Dictionary<K2, V>> dict) {
        foreach (var k in dict.Keys.ToArray()) dict.TryRemoveAndCache(k);
    }

    public static void TryRemoveAndCacheAllExcept<K, K2, V>(this Dictionary<K, Dictionary<K2, V>> dict,
        HashSet<K> exceptions) {
        foreach (var k in dict.Keys.ToArray()) {
            if (!exceptions.Contains(k)) dict.TryRemoveAndCache(k);
        }
    }

    public static void ClearExcept<K, V>(this Dictionary<K, V> dict, HashSet<K> exceptions) {
        foreach (var k in dict.Keys.ToArray()) {
            if (!exceptions.Contains(k)) dict.Remove(k);
        }
    }

    public static void ClearExcept<K>(this HashSet<K> dict, HashSet<K> exceptions) {
        foreach (var k in dict.ToArray()) {
            if (!exceptions.Contains(k)) dict.Remove(k);
        }
    }

    public static V? GetOrDefault<K, V>(this Dictionary<K, V> dict, K key) where V : struct {
        if (dict.TryGetValue(key, out var res)) return res;
        return default(V?);
    }
    
    public static V GetOrDefault<K, V>(this Dictionary<K, V> dict, K key, V deflt) {
        if (dict.TryGetValue(key, out var res)) return res;
        return deflt;
    }

    public static V GetOrDefault2<K1, K2, V>(this Dictionary<K1, Dictionary<K2, V>> dict, K1 key, K2 key2) {
        if (dict.TryGetValue(key, out var res) && res.TryGetValue(key2, out var res2)) return res2;
        return default!;
    }

    public static V GetOrDefault3<K1, K2, K3, V>(this Dictionary<K1, Dictionary<K2, Dictionary<K3, V>>> dict, K1 key,
        K2 key2, K3 key3) {
        if (dict.TryGetValue(key, out var res) && res.TryGetValue(key2, out var res2)
                                               && res2.TryGetValue(key3, out var res3)) return res3;
        return default!;
    }

    public static void CopyInto<K, V>(this Dictionary<K, V> src, Dictionary<K, V> target) {
        foreach (var kv in src) target[kv.Key] = kv.Value;
    }

    public static V SearchByType<V>(this Dictionary<Type, V> src, object obj, bool searchInterfaces) {
        var t = obj.GetType();
        if (src.TryGetValue(t, out var v) && v != null)
            return v;
        //Search interfaces first so UINodeLR<T> matches interface before matching UINode
        if (searchInterfaces) {
            foreach (var it in obj.GetType().GetInterfaces()) {
                if (src.TryGetValue(it, out v) && v != null)
                    return v;
            }
        }
        while ((t = t.BaseType) != null) {
            if (src.TryGetValue(t, out v) && v != null)
                return v;
        }
        throw new Exception($"Couldn't find type {obj.GetType()} in dictionary");
    }

    public static void Push<K, V>(this Dictionary<K, Stack<V>> dict, K key, V value) {
        if (!dict.TryGetValue(key, out var s)) s = dict[key] = new Stack<V>();
        s.Push(value);
    }

    public static void Pop<K, V>(this Dictionary<K, Stack<V>> dict, K key) {
        var s = dict[key];
        s.Pop();
        if (s.Count == 0) dict.Remove(key);
    }
}

public static class FormattingExtensions {
    public static string PadRight(this int x, int by) => x.ToString().PadRight(by);
    public static string PadLZero(this int x, int by) => x.ToString().PadLeft(by, '0');

    public static string SimpleDate(this DateTime d) =>
        $"{d.Year}/{d.Month.PadLZero(2)}/{d.Day.PadLZero(2)}";
    public static string SimpleTime(this DateTime d) =>
        $"{d.SimpleDate()} " +
        $"{d.Hour.PadLZero(2)}:{d.Minute.PadLZero(2)}:{d.Second.PadLZero(2)}";

    public static string FileableTime(this DateTime d) =>
        $"{d.Year} {d.Month.PadLZero(2)} {d.Day.PadLZero(2)} " +
        $"{d.Hour.PadLZero(2)} {d.Minute.PadLZero(2)} {d.Second.PadLZero(2)}";
}

public static class NumExtensions {

    public static char ToABC(this int x) => (char) ('A' + x);

    public static LString FramesToTime(this int f) {
        int s = (int) (f / ETime.ENGINEFPS_F);
        int hours = s / 3600;
        s %= 3600;
        int minutes = s / 60;
        s %= 60;
        int seconds = s;
        return hours > 0 ? 
            LocalizedStrings.Generic.render_hoursminssecs_ls(hours, minutes, seconds) : 
            LocalizedStrings.Generic.render_minssecs_ls(minutes, seconds);
    }
}

public static class UnityExtensions {
    public static T Elvis<T>(this T? x, T y) where T : UnityEngine.Object
        => (x == null) ? y : x;

    public static void SetAlpha(this SpriteRenderer sr, float a) {
        var c = sr.color;
        c.a = a;
        sr.color = c;
    }

    /// <summary>
    /// Return the pivot of a sprite in (0, 1) coordinates.
    /// </summary>
    public static Vector2 Pivot(this Sprite s) {
        var p = s.pivot;
        p.x /= s.rect.width;
        p.y /= s.rect.height;
        return p;
    }

    /// <summary>
    /// Return the dimensions of a sprite in Unity coordinates.
    /// </summary>
    public static Vector2 Dims(this Sprite s) {
        return new Vector2(s.rect.width / s.pixelsPerUnit, s.rect.height / s.pixelsPerUnit);
    }
}

}