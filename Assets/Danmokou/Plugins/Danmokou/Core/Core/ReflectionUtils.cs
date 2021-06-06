using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Danmokou.Core {
public static class ReflectorUtils {
    private static Type[]? _reflectableAssemblyTypes;
    public static Type[] ReflectableAssemblyTypes => _reflectableAssemblyTypes ??=
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a => a.GetCustomAttributes(false).Any(c => c is ReflectAttribute))
            .SelectMany(a => a.GetTypes())
            .ToArray();
    
}
}