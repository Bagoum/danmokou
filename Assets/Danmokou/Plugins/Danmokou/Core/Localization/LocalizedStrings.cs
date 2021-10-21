using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Culture;
using BagoumLib.Reflection;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Core {
[LocalizationStringsRepo]
public static partial class LocalizedStrings {
    static LocalizedStrings() {
        if (!Application.isPlaying) return;
        //Load strings from all classes labeled [LocalizationStringsRepo] into the runtime data map
        foreach (var t in ReflectorUtils.ReflectableAssemblyTypes
            .Where(a => a.GetCustomAttributes(false).Any(c => c is LocalizationStringsRepoAttribute))) {
            if (t == typeof(LocalizedStrings)) continue;
            var map = t._StaticField<Dictionary<string, LString>>(nameof(_allDataMap));
            foreach (var k in map.Keys) {
                _allDataMap[k] = map[k];
            }
        }
    }
    
    public static bool IsLocalizedStringReference(string key) =>
        key.Length > 0 && key[0] == ':';
    private static string Sanitize(string reference_key) {
        if (IsLocalizedStringReference(reference_key)) reference_key = reference_key.Substring(1);
        return reference_key;
    }
    public static LString? TryFindReference(string reference_key) =>
        TryFindReference(reference_key, out var ls) ? ls : null;

    public static LString FindReference(string reference_key) =>
        TryFindReference(reference_key, out var ls) ?
            ls :
            throw new Exception($"Could not find LocalizedString reference for {reference_key}");

    public static bool TryFindReference(string reference_key, out LString ls) =>
        _allDataMap.TryGetValue(Sanitize(reference_key), out ls);
}
}