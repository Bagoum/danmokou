using System;
using System.Collections.Generic;

namespace Danmokou.Expressions {
public static class GeneratedExpressions {
    public static List<object> RetrieveBakedOrEmpty(string key) =>
        GeneratedExpressions_CG._allDataMap.TryGetValue(key, out var res) ?
            res :
            new List<object>(0);
    
}

internal static partial class GeneratedExpressions_CG {
    public static readonly Dictionary<string, List<object>> _allDataMap =
        new Dictionary<string, List<object>>();
    
}
}