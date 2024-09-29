using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib.Reflection;

namespace Danmokou.Reflection {
/// <summary>
/// Helper for getting parameter attributes from TypeMember.Method/TypeMember.Constructor, which are
///  required in BDSL1.
/// <br/>This uses an inoptimal and roundabout method. It should NOT be used for any new use cases.
/// </summary>
public static class TypeMemberFeatures {
    private static readonly Dictionary<TypeMember, Reflector.ParamFeatures[]> cache = new();

    public static Reflector.ParamFeatures[]? Features(TypeMember mem) {
        if (cache.TryGetValue(mem, out var feats))
            return feats;
        if (mem.BaseMi is MethodBase mb) {
            var args = mb.GetParameters().Select(x => (Reflector.ParamFeatures)x);
            return cache[mem] = (mb is not MethodInfo {IsStatic:false} ? args : 
                args.Prepend(new Reflector.ParamFeatures())).ToArray();
        }
        return null;
    }
}

}