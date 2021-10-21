using System;
using System.Collections.Generic;
using System.Reactive;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Behavior {
public abstract class RegularUpdater : MonoBehaviour, IRegularUpdater {
    protected readonly List<IDisposable> tokens = new List<IDisposable>();
    protected bool Enabled => tokens.Count > 0;

    /// <summary>
    /// Safe to call twice.
    /// </summary>
    protected void EnableUpdates() {
        if (!Enabled) {
            tokens.Add(ETime.RegisterRegularUpdater(this));
            BindListeners();
        }
    }

    protected virtual void BindListeners() { }

    protected void Listen<T, E>(EventProxy<T> obj, Func<T, IObservable<E>> ev, Action<E> sub) {
        tokens.Add(obj.Subscribe(ev, sub));
    }
    protected void Listen<T>(EventProxy<T> obj, Func<T, IObservable<Unit>> ev, Action sub) {
        tokens.Add(obj.Subscribe(ev, _ => sub()));
    }
    protected void Listen<T>(IObservable<T> ev, Action<T> sub) {
        tokens.Add(ev.Subscribe(sub));
    }
    protected void Listen(IObservable<Unit> ev, Action sub) {
        tokens.Add(ev.Subscribe(_ => sub()));
    }

    protected void RegisterService<T>(T me, ServiceLocator.ServiceOptions? options = null) where T : class => 
        tokens.Add(ServiceLocator.Register(me, options));

    protected virtual void OnEnable() => EnableUpdates();

    public abstract void RegularUpdate();
    public virtual void RegularUpdateParallel() { }
    public virtual void FirstFrame() { }
    public virtual int UpdatePriority => UpdatePriorities.DEFAULT;

    public virtual EngineState UpdateDuring => EngineState.RUN;


    /// <summary>
    /// Safe to call twice.
    /// </summary>
    protected void DisableUpdates() {
        for (int ii = 0; ii < tokens.Count; ++ii) {
            tokens[ii].Dispose();
        }
        tokens.Clear();
    }

    protected virtual void OnDisable() => DisableUpdates();

    protected void DisableDestroy() {
        DisableUpdates();
        Destroy(gameObject);
    }
}
}

