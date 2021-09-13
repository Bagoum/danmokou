
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BagoumLib.DataStructures;
using Danmokou.Expressions;
using Danmokou.Services;
using JetBrains.Annotations;

namespace Danmokou.Core {
public static class ServiceLocator {
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

    public static ISFXService SFXService => Find<ISFXService>();
    public static Expression SFXRequest(Expression style) => 
        sfxrequest.InstanceOf(Expression.Property(null, typeof(ServiceLocator), "SFXService"), style);

    private static readonly ExFunction sfxrequest = ExUtils.Wrap<ISFXService>("Request", new[] {typeof(string)});


    /// <summary>
    /// Register a service that can be reached globally via service location.
    /// <br/>The caller must dispose the disposable when the registered service is no longer available
    ///  (eg. due to object deletion).
    /// </summary>
    public static IDisposable Register<T>(T provider) where T : class {
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
                if (!ts.providers.Data[ii].MarkedForDeletion) 
                    return ts.providers[ii];
            }
        }
        return null;
    }

    public static List<T> FindAll<T>() where T : class {
        var results = new List<T>();
        if (services.TryGetValue(typeof(T), out var s)) {
            var ts = (Service<T>) s;
            for (int ii = 0; ii < ts.providers.Count; ++ii) {
                if (!ts.providers.Data[ii].MarkedForDeletion)
                    results.Add(ts.providers[ii]);
            }
        }
        return results;
    }

    public static T Find<T>() where T : class =>
        MaybeFind<T>() ?? throw new Exception($"Service locator: No provider of type {typeof(T)} found");

}
}
