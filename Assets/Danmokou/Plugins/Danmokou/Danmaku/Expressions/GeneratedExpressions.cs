using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib.Culture;
using BagoumLib.Reflection;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Expressions {
public static class GeneratedExpressions {
    private static readonly Dictionary<string, List<object>> _allDataMap = new();

    static GeneratedExpressions() {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
#endif
        //Load strings from all classes labeled [LocalizationStringsRepo] into the runtime data map
        foreach (var t in ReflectorUtils.ReflectableAssemblyTypes
                     .Where(a => a.GetCustomAttribute<GeneratedExpressionsAttribute>() != null)) {
            var map = t._StaticField<Dictionary<string, List<object>>>(nameof(_allDataMap));
            foreach (var k in map.Keys)
                _allDataMap[k] = map[k];
            map.Clear();
        }
    }
    
    public static List<object> RetrieveBakedOrEmpty(string key) =>
        _allDataMap.TryGetValue(key, out var res) ?
            res :
            new List<object>(0);
}
}