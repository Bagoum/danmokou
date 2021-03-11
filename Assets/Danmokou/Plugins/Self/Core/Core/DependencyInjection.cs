
using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace DMK.Core {
public static class DependencyInjection {
    private interface IService { }

    private class Service<T> : IService {
        public readonly DMCompactingArray<T> providers;
        public readonly bool unique;

        public Service(bool unique) {
            this.providers = new DMCompactingArray<T>();
            this.unique = unique;
        }
    }

    private static readonly Dictionary<Type, IService> services = new Dictionary<Type, IService>();


    /// <summary>
    /// Register a service that can be reached globally via dependency injection.
    /// <br/>Note: It's preferable to use RegularUpdater.RegisterDI instead, as that
    /// handles service deletion.
    /// </summary>
    public static IDeletionMarker Register<T>(T provider) where T : class {
        if (!services.TryGetValue(typeof(T), out var s)) {
            s = services[typeof(T)] = new Service<T>(false);
        }
        var ts = (Service<T>) s;
        ts.providers.Compact();
        return ts.providers.Add(provider);
    }

    public static T? MaybeFind<T>() where T : class {
        if (services.TryGetValue(typeof(T), out var s)) {
            var ts = (Service<T>) s;
            for (int ii = 0; ii < ts.providers.Count; ++ii) {
                if (!ts.providers.arr[ii].markedForDeletion) return ts.providers[ii];
            }
        }
        return null;
    }

    public static T Find<T>() where T : class =>
        MaybeFind<T>() ?? throw new Exception($"Dependency injection: No provider of type {typeof(T)} found");

}
}
