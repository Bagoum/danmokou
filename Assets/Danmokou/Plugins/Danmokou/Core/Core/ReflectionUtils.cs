using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Scriptor;

namespace Danmokou.Core {
public static class ReflectorUtils {
    private static Type[]? _reflectableAssemblyTypes;
    public static Type[] ReflectableAssemblyTypes => _reflectableAssemblyTypes ??=
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a => a.GetCustomAttribute<ReflectAttribute>() != null)
            .SelectMany(a => a.GetTypes())
            .ToArray();
    
}
}