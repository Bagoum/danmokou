using System;
using System.Collections.Generic;

namespace DMK.Expressions {
public static class GeneratedExpressions {
    public static List<Func<object>> RetrieveBakedOrEmpty(string key) =>
        GeneratedExpressions_CG._allDataMap.TryGetValue(key, out var res) ?
            res :
            new List<Func<object>>(0);
    
}

internal static partial class GeneratedExpressions_CG {
    public static readonly Dictionary<string, List<Func<object>>> _allDataMap =
        new Dictionary<string, List<Func<object>>>();
    
}
}