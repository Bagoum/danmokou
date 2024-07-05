using System;
using System.Reactive;
using BagoumLib.Events;
using SuzunoyaUnity;

namespace Danmokou.UI.XML {
/// <summary>
/// Helper for binding changes two ways between a view and view model.
/// <br/>When <see cref="Value"/> is set, the default logic will
///  re-publish the linked view model (if it is <see cref="IVersionedUIViewModel"/>).
/// </summary>
public interface ITwoWayBinder<T> {
    /// <summary>
    /// The view model providing the underlying data for the binding.
    /// </summary>
    public IVersionedUIViewModel ViewModel { get; }
    public T Value {
        get => GetInner();
        set {
            SetInner(value);
            ViewModel.ViewUpdated();
        }
    }
    
    protected T GetInner();
    protected void SetInner(T value);

}

/// <summary>
/// Base class for binding values two ways between a view and model/view model.
/// <br/>When <see cref="ITwoWayBinder{T}.Value"/> is set, this class will
///  re-publish the linked view model (if it is <see cref="IVersionedUIViewModel"/>).
/// </summary>
public abstract class TwoWayBinder<T> : ITwoWayBinder<T> {
    public IVersionedUIViewModel ViewModel { get; }

    public TwoWayBinder(IVersionedUIViewModel? vm) {
        ViewModel = vm ?? new VersionedUIViewModel();
    }

    T ITwoWayBinder<T>.GetInner() => GetInner();
    protected abstract T GetInner();
    void ITwoWayBinder<T>.SetInner(T value) => SetInner(value);
    protected abstract void SetInner(T value);
}

/// <summary>
/// Bind to an <see cref="Evented{T,U}"/> object on a view model.
/// <br/>Note: the view will NOT automatically be rebound if you modify the Evented other than through the view,
///  unless you manually call (EventedBinder.ViewModel as IVersionedUIViewModel).ModelChanged().
/// </summary>
public class EventedBinder<T> : TwoWayBinder<T> {
    private readonly Evented<T> ev;
    public EventedBinder(Evented<T> ev, IVersionedUIViewModel? vm = null) : base(vm) {
        this.ev = ev;
    }

    protected override T GetInner() => ev.Value;

    protected override void SetInner(T value) => ev.PublishIfNotSame(value);
}

/// <summary>
/// Bind using manually-provided getters and setters.
/// </summary>
public class ManualBinder<T> : TwoWayBinder<T> {
    private readonly Func<T> get;
    private readonly Action<T> set;

    public ManualBinder(Func<T> get, Action<T> set, IVersionedUIViewModel? vm) : base(vm) {
        this.get = get;
        this.set = set;
    }

    protected override T GetInner() => get();

    protected override void SetInner(T value) => set(value);
}

}