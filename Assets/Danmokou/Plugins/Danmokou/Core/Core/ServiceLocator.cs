
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using Danmokou.Expressions;
using Danmokou.Services;
using JetBrains.Annotations;

namespace Danmokou.Core {
/// <summary>
/// A store that allows registering and locating services at runtime.
/// </summary>
public static class ServiceLocator {
    private interface IService { }

    public class ServiceOptions {
        public bool Unique { get; set; } = false;
    }
    private class Service<T> : IService where T: class {
        private readonly DMCompactingArray<T> providers;
        private readonly ServiceOptions options;

        public Service(ServiceOptions? options) {
            this.providers = new DMCompactingArray<T>();
            this.options = options ?? new ServiceOptions();
        }

        public IDisposable Add(T service) {
            providers.Compact();
            if (options.Unique && providers.Count > 0)
                throw new Exception($"An instance of unique service {typeof(T)} already exists.");
            return providers.Add(service);
        }

        public T? MaybeFind() => providers.FirstOrNull();

        public List<T> FindAll()  {
            var results = new List<T>();
            providers.CopyIntoList(results);
            return results;
        }
    }

    private static readonly Dictionary<Type, IService> services = new Dictionary<Type, IService>();

    public static ISFXService SFXService => Find<ISFXService>();
    public static Expression SFXRequest(Expression style) => 
        sfxrequest.InstanceOf(Expression.Property(null, typeof(ServiceLocator), "SFXService"), style);

    private static readonly ExFunction sfxrequest = ExFunction.Wrap<ISFXService>("Request", new[] {typeof(string)});


    /// <summary>
    /// Register a service that can be reached globally via service location.
    /// <br/>The caller must dispose the disposable when the registered service is no longer available
    ///  (eg. due to object deletion).
    /// <br/>TODO send alerts to consumers when the service is disposed
    /// </summary>
    public static IDisposable Register<T>(T provider, ServiceOptions? options = null) where T : class {
        if (!services.TryGetValue(typeof(T), out var s))
            s = services[typeof(T)] = new Service<T>(options);
        return ((Service<T>) s).Add(provider);
    }
    
    /// <summary>
    /// Find a service of type T, or return null.
    /// </summary>
    public static T? MaybeFind<T>() where T : class =>
        services.TryGetValue(typeof(T), out var s) ? 
            ((Service<T>) s).MaybeFind() : 
            null;

    /// <summary>
    /// Find all services of type T. The returned list may be empty.
    /// </summary>
    public static List<T> FindAll<T>() where T : class =>
        services.TryGetValue(typeof(T), out var s) ? 
            ((Service<T>) s).FindAll() : 
            new List<T>();

    /// <summary>
    /// Find a service of type T, or throw an exception.
    /// </summary>
    public static T Find<T>() where T : class =>
        MaybeFind<T>() ?? throw new Exception($"Service locator: No provider of type {typeof(T)} found");

}
}
