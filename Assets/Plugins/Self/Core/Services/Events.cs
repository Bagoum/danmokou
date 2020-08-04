using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq.Expressions;
using JetBrains.Annotations;

namespace Core {
/// <summary>
/// Module for managing events.
/// </summary>
public static class Events {
    /// <summary>
    /// Request this type in reflection to create event objects.
    /// </summary>
    /// <typeparam name="E"></typeparam>
    public readonly struct EventDeclaration<E> {
        public readonly E ev;
        private EventDeclaration(E ev) => this.ev = ev;
        public static implicit operator EventDeclaration<E>(E e) => new EventDeclaration<E>(e);
    }

    /// <summary>
    /// Events that take zero parameters.
    /// </summary>
    public class Event0 {
        private static readonly Dictionary<string, List<Event0>> waitingToResolve =
            new Dictionary<string, List<Event0>>();
        private static readonly Dictionary<string, Event0> storedEvents = new Dictionary<string, Event0>();

        private readonly DMCompactingArray<Action> callbacks = new DMCompactingArray<Action>();
        [CanBeNull] private DeletionMarker<Action> refractor = null;
        private readonly bool useRefractoryPeriod = false;
        private bool inRefractoryPeriod = false;
        public Event0() : this(false) { }

        private Event0(bool useRefractoryPeriod) {
            this.useRefractoryPeriod = useRefractoryPeriod;
        }

        private void ListenForReactivation(string reactivator) {
            if (storedEvents.TryGetValue(reactivator, out Event0 refEvent)) {
                refractor = refEvent.Listen(() => inRefractoryPeriod = false);
            } else {
                if (!waitingToResolve.TryGetValue(reactivator, out List<Event0> waiters)) {
                    waitingToResolve[reactivator] = waiters = new List<Event0>();
                }
                waiters.Add(this);
            }
        }

        /// <summary>
        /// An event that may trigger repeatedly.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static EventDeclaration<Event0> Continuous(string name) {
            if (storedEvents.ContainsKey(name)) throw new Exception($"Event already declared: {name}");
            return storedEvents[name] = new Event0(false);
        }

        /// <summary>
        /// An event that will only trigger once.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static EventDeclaration<Event0> Once(string name) {
            if (storedEvents.ContainsKey(name)) throw new Exception($"Event already declared: {name}");
            return storedEvents[name] = new Event0(true);
        }

        /// <summary>
        /// An event that will trigger once, but may be reset when another event is triggered.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="reactivator"></param>
        /// <returns></returns>
        public static EventDeclaration<Event0> Refract(string name, string reactivator) {
            if (storedEvents.ContainsKey(name)) throw new Exception($"Event already declared: {name}");
            var ev = storedEvents[name] = new Event0(true);
            ev.ListenForReactivation(reactivator);
            if (waitingToResolve.TryGetValue(name, out var waiters)) {
                for (int ii = 0; ii < waiters.Count; ++ii) waiters[ii].ListenForReactivation(name);
                waitingToResolve.Remove(name);
            }
            return ev;
        }

        public static Event0 Find(string name) {
            if (!storedEvents.TryGetValue(name, out var ev)) throw new Exception($"Event not declared: {name}");
            return ev;
        }

        public static Maybe<Event0> FindOrNull(string name) {
            if (!storedEvents.TryGetValue(name, out var ev)) return null;
            return ev;
        }

        private void Invoke() {
            int temp_last = callbacks.Count;
            for (int ii = 0; ii < temp_last; ++ii) {
                DeletionMarker<Action> listener = callbacks.arr[ii];
                if (!listener.markedForDeletion) listener.obj();
            }
            callbacks.Compact();
        }

        public void InvokeIfNotRefractory() {
            if (!inRefractoryPeriod) Invoke();
            if (useRefractoryPeriod) inRefractoryPeriod = true;
        }

        private static readonly ExFunction invokeIfNotRefractory = ExUtils.Wrap<Event0>("InvokeIfNotRefractory");
        public Expression ExInvokeIfNotRefractory() => invokeIfNotRefractory.InstanceOf(Expression.Constant(this));

        public static void Reset() {
            foreach (var ev in storedEvents.Values) {
                ev.inRefractoryPeriod = false;
            }
        }

        public static void Reset(string[] names) {
            for (int ii = 0; ii < names.Length; ++ii) {
                storedEvents[names[ii]].inRefractoryPeriod = false;
            }
        }

        public static void DestroyAll() {
            foreach (var ev in storedEvents.Values) {
                ev.Destroy();
            }
            storedEvents.Clear();
        }

        private void Destroy() {
            callbacks.Empty();
            refractor?.MarkForDeletion();
        }

        public DeletionMarker<Action> Listen(Action cb) => callbacks.Add(cb);
    }

    public class Event1<T> {
        private readonly DMCompactingArray<Action<T>> callbacks = new DMCompactingArray<Action<T>>();

        public void Invoke(T arg1) {
            int temp_last = callbacks.Count;
            for (int ii = 0; ii < temp_last; ++ii) {
                DeletionMarker<Action<T>> listener = callbacks.arr[ii];
                if (!listener.markedForDeletion) listener.obj(arg1);
            }
            callbacks.Compact();
        }

        public DeletionMarker<Action<T>> Listen(Action<T> cb) => callbacks.Add(cb);
    }
    public class Event2<T,R> {
        private readonly DMCompactingArray<Action<T,R>> callbacks = new DMCompactingArray<Action<T,R>>();

        public void Invoke(T arg1, R arg2) {
            int temp_last = callbacks.Count;
            for (int ii = 0; ii < temp_last; ++ii) {
                DeletionMarker<Action<T,R>> listener = callbacks.arr[ii];
                if (!listener.markedForDeletion) listener.obj(arg1, arg2);
            }
            callbacks.Compact();
        }

        public DeletionMarker<Action<T,R>> Listen(Action<T,R> cb) => callbacks.Add(cb);
    }
    
    
    //Events with "Noun Has Verbed" are messages that go out after the action has occured.
    //Events with "Verb Noun" are messages that are sent to request invoking an action.
    public static readonly Event1<Danmaku.CampaignMode> PlayerHasDied = new Event1<Danmaku.CampaignMode>();
    /// <summary>
    /// Nothing will occur if the player is in an invulnerable state.
    /// Argument 1: damage number.
    /// </summary>
    public static readonly Event1<int> TryHitPlayer = new Event1<int>();
    /// <summary>
    /// Argument 1: number of invulnerability frames.
    /// Argument 2: Whether or not to show effect.
    /// </summary>
    public static readonly Event2<int, bool> MakePlayerInvincible = new Event2<int, bool>();
    public static readonly Event1<GameState> GameStateHasChanged = new Event1<GameState>();
}
}