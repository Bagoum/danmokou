using System;
using System.Collections.Generic;
using System.Reactive;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Behavior {
public abstract class RegularUpdater : MonoBehaviour, IRegularUpdater {
    protected readonly List<IDisposable> tokens = new List<IDisposable>();
    protected bool Enabled { get; private set; } = false;

    /// <summary>
    /// Safe to call twice.
    /// </summary>
    protected void EnableUpdates() {
        if (!Enabled) {
            tokens.Add(ETime.RegisterRegularUpdater(this).AllowPooling());
            BindListeners();
            Enabled = true;
        }
    }

    public void AddToken(IDisposable t) => tokens.Add(t);

    protected virtual void BindListeners() { }

    protected void Listen<T, E>(IObservable<T> obj, Func<T, IObservable<E>?> ev, Action<E> sub) {
        tokens.Add(obj.BindSubscribe(ev, sub));
    }
    protected void Listen<T>(IObservable<T> obj, Func<T, IObservable<Unit>> ev, Action sub) {
        tokens.Add(obj.BindSubscribe(ev, _ => sub()));
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

    public virtual void RegularUpdateParallel() { }
    public abstract void RegularUpdate();
    public virtual void RegularUpdateCollision() { }
    public virtual void RegularUpdateFinalize() { }
    public virtual void FirstFrame() { }
    public virtual int UpdatePriority => UpdatePriorities.DEFAULT;
    public virtual bool HasNontrivialParallelUpdate => false;

    public virtual EngineState UpdateDuring => EngineState.RUN;


    /// <summary>
    /// Safe to call twice.
    /// </summary>
    protected void DisableUpdates() {
        if (Enabled) {
            tokens.DisposeAll();
            Enabled = false;
        }
    }

    protected virtual void OnDisable() => DisableUpdates();

    protected void DisableDestroy() {
        DisableUpdates();
        Destroy(gameObject);
    }
}
}

