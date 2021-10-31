using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq.Expressions;
using System.Reactive;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Expressions;
using JetBrains.Annotations;

namespace Danmokou.Core {
/// <summary>
/// Module for managing events.
/// </summary>
public static class Events {
    public enum RuntimeEventType {
        Normal,
        Trigger
    }
    private static readonly Dictionary<Type, Dictionary<string, RuntimeEvent>> events = 
        new Dictionary<Type, Dictionary<string, RuntimeEvent>>();
    private static readonly Dictionary<string, RuntimeEvent> eventsFlat = 
        new Dictionary<string, RuntimeEvent>();

    public static Func<IDisposable> CreateRuntimeEventCreator<T>(string name, RuntimeEventType typ) => 
        () => CreateRuntimeEvent<T>(name, typ, out _);

    public static IDisposable CreateRuntimeEvent<T>(string name, RuntimeEventType typ, out RuntimeEvent<T> ev) {
        return ev = new RuntimeEvent<T>(name, typ switch {
            RuntimeEventType.Trigger => new TriggerEvent<T>(),
            _ => new Event<T>()
        });
    }

    public abstract class RuntimeEvent : IDisposable {
        /// <summary>
        /// For Event&lt;T&gt;, the type of value should be T.
        /// </summary>
        public abstract void TryOnNext(object value);
        /// <summary>
        /// For Event&lt;T&gt;, the type of value should be Action&lt;T&gt;.
        /// </summary>
        public abstract IDisposable TrySubscribe(object listener);

        public abstract IDisposable SubscribeAny(Action listener);

        public abstract void Dispose();

        /// <summary>
        /// If this event is a trigger event, then link the given resetter. Otherwise throw.
        /// </summary>
        public abstract void TriggerResetWith<T>(IObservable<T> resetter);
        public abstract void TriggerResetWith(RuntimeEvent resetter);
    }

    public class RuntimeEvent<T> : RuntimeEvent {
        public static RuntimeEvent<T> Null = new RuntimeEvent<T>("_", NullEvent<T>.Default);
        public string EvName { get; }
        public Event<T> Ev { get; }

        public RuntimeEvent(string evName, Event<T> ev) {
            EvName = evName;
            Ev = ev;
            events.Add2(typeof(T), EvName, this);
            eventsFlat[EvName] = this;
            Logs.Log($"Created runtime event {EvName}", false, LogLevel.DEBUG3);
        }

        public override string ToString() => $"{EvName}<{typeof(T).RName()}>";

        public override void TryOnNext(object value) {
            if (value is T v)
                Ev.OnNext(v);
            else
                throw new Exception(
                    $"Runtime event {this} was provided with an object of type {value.GetType().RName()}");
        }

        public override IDisposable TrySubscribe(object listener) =>
            listener is Action<T> act ?
                Ev.Subscribe(act) :
                throw new Exception(
                    $"Runtime event {this} was provided with a listener of type {listener.GetType().RName()}");

        public override IDisposable SubscribeAny(Action listener) => Ev.Subscribe(_ => listener());

        public override void Dispose() {
            if (eventsFlat.TryGetValue(EvName, out var ev) && ev == this)
                eventsFlat.Remove(EvName);
            if (events.TryGetValue(typeof(T), out var dct))
                if (dct.TryGetValue(EvName, out ev) && ev == this) {
                    dct.Remove(EvName);
                }
            Logs.Log($"Disposed runtime event {EvName}", false, LogLevel.DEBUG3);
        }

        public override void TriggerResetWith<T1>(IObservable<T1> resetter) {
            (Ev as TriggerEvent<T> ?? 
             throw new Exception($"Runtime event {this} is not a TriggerEvent")).ResetOn(resetter);
        }

        public override void TriggerResetWith(RuntimeEvent resetter) {
            var t = (Ev as TriggerEvent<T> ??
                     throw new Exception($"Runtime event {this} is not a TriggerEvent"));
            resetter.SubscribeAny(t.Reset);
        }
    }

    public static RuntimeEvent FindAnyRuntimeEvent(string name) =>
        eventsFlat.TryGetValue(name, out var ev) ?
            ev :
            throw new Exception($"No runtime event of any type by name {name}");
    public static RuntimeEvent<T> FindRuntimeEvent<T>(string name) =>
        events.TryGet2(typeof(T), name, out var ev) ? 
            ((RuntimeEvent<T>)ev) : 
            throw new Exception($"No runtime event for type {typeof(T).RName()} by name {name}");

    public static void ProcRuntimeEvent<T>(string name, T value) => FindRuntimeEvent<T>(name).Ev.OnNext(value);
    

    private static readonly Dictionary<Type, ExFunction> exProcRuntimeEventCache = new Dictionary<Type, ExFunction>(); 
    public static ExFunction exProcRuntimeEvent<T>() => 
        exProcRuntimeEventCache.TryGetValue(typeof(T), out var v) ? v :
            exProcRuntimeEventCache[typeof(T)] = 
                new ExFunction(typeof(Events).GetMethod("ProcRuntimeEvent")!.MakeGenericMethod(typeof(T)));
    
    public static readonly Event<Unit> SceneCleared = new Event<Unit>();
#if UNITY_EDITOR || ALLOW_RELOAD
    public static readonly Event<Unit> LocalReset = new Event<Unit>();
#endif
}
}