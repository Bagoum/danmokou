using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Danmokou.Core {
public static class ReflectionUtils {
    
    /// <summary>
    /// Check if a realized type (eg. List&lt;List&lt;int&gt;&gt;)
    /// and a generic type (eg. List&lt;List&lt;T&gt;&gt;, where T is a generic method type)
    /// match. If they do, also get a dictionary mapping each generic type to its realized type.
    /// </summary>
    public static bool ConstructedGenericTypeMatch(Type realized, Type generic, out Dictionary<Type, Type> mapper) {
        mapper = new Dictionary<Type, Type>();
        return _ConstructedGenericTypeMatch(realized, generic, mapper);
    }

    public static MethodInfo MakeGeneric(this MethodInfo mi, Dictionary<Type, Type> typeMap) {
        var typeParams = mi.GetGenericArguments()
            .Select(t => typeMap.TryGetValue(t, out var prm) ? prm : 
                throw new Exception($"Method {mi.Name} does not have enough information to construct a generic"));
        return mi.MakeGenericMethod(typeParams.ToArray());
    }

    public static bool _ConstructedGenericTypeMatch(Type realized, Type generic, Dictionary<Type, Type> genericMap) {
        if (generic.IsGenericParameter) {
            if (genericMap.TryGetValue(generic, out var x) && x != realized) return false;
            genericMap[generic] = realized;
            return true;
        }
        if (realized.IsGenericType != generic.IsGenericType) return false;
        if (!realized.IsGenericType) return realized == generic;
        return realized.GetGenericTypeDefinition() == generic.GetGenericTypeDefinition()
               && realized.GetGenericArguments().Length == generic.GetGenericArguments().Length
               && realized.GetGenericArguments()
                   .Zip(generic.GetGenericArguments())
                   .All(t => _ConstructedGenericTypeMatch(t.Item1, t.Item2, genericMap));
    }


    public static T CreateInstance<T>(params object[] args) => (T) Activator.CreateInstance(typeof(T), args);
}
}