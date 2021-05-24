using System;
using System.Collections.Generic;
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

    protected void Listen<T>(IBSubject<T> ev, Action<T> sub) {
        tokens.Add(ev.Subscribe(sub));
    }
    
    /// <summary>
    /// Invoke the function with the last published value, and then listen to future changes.
    /// </summary>
    protected void ListenInv<T>(IBSubject<T> ev, Action<T> sub) {
        if (ev.LastPublished.Valid)
            sub(ev.LastPublished.Value);
        Listen(ev, sub);
    }
    protected void ListenInv<T>(IBSubject<T> ev, Action sub) {
        sub();
        Listen(ev, _ => sub());
    }

    protected void Listen(Events.Event0 ev, Action sub) {
        tokens.Add(ev.Subscribe(sub));
    }
    
    /// <summary>
    /// Invoke the function, and then listen to future changes.
    /// </summary>
    protected void ListenInv(Events.Event0 ev, Action sub) {
        sub();
        Listen(ev, sub);
    }
    
    protected void RegisterDI<T>(T me) where T : class => tokens.Add(DependencyInjection.Register(me));

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

