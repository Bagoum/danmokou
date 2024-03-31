using System;
using System.Reactive;
using BagoumLib.Events;

namespace Danmokou.UI.XML {
/// <summary>
/// Helper for binding changes two ways between a view and view model.
/// </summary>
public interface ITwoWayBinder<T> {
    public IUIViewModel ViewModel { get; }
    public T Value { get; set; }
    public IObservable<Unit> ValueUpdatedFromModel { get; }

}

public abstract class TwoWayBinder<T> : ITwoWayBinder<T> {
    public IUIViewModel ViewModel { get; }
    public IObservable<Unit> ValueUpdatedFromModel { get; } 
    T ITwoWayBinder<T>.Value {
        get => GetInner();
        set {
            SetInner(value);
            if (ViewModel is IVersionedUIViewModel vers)
                vers.Publish();
        }
    }

    public TwoWayBinder(IUIViewModel? vm) {
        ViewModel = vm ?? new VersionedUIViewModel();
        if (ViewModel is IVersionedUIViewModel vers)
            ValueUpdatedFromModel = vers.UpdatedFromModel;
        else
            ValueUpdatedFromModel = new Event<Unit>();
    }

    protected abstract T GetInner();
    protected abstract void SetInner(T value);
}

/// <summary>
/// Bind to an <see cref="Evented{T,U}"/> object on a view model.
/// </summary>
public class EventedBinder<T> : TwoWayBinder<T> {
    private readonly Evented<T> ev;
    public EventedBinder(Evented<T> ev, IUIViewModel? vm = null) : base(vm) {
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

    public ManualBinder(Func<T> get, Action<T> set, IUIViewModel? vm) : base(vm) {
        this.get = get;
        this.set = set;
    }

    protected override T GetInner() => get();

    protected override void SetInner(T value) => set(value);
}

}