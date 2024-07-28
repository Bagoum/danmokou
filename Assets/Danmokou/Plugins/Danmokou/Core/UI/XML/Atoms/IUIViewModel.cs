using System;
using System.Reactive;
using System.Reactive.Subjects;
using BagoumLib.Events;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
//NB: we do not use records for implementing IUIViewModel because Unity internally uses equality/hash checks on 
// view.dataSource, which points to the view model. We need class-based equality/hashing on the view model
// in order to ensure proper behavior

/// <summary>
/// A set of data that will be rendered to screen by <see cref="IUIView"/>.
/// <br/>Contains information about data changes via <see cref="IDataSourceViewHashProvider.GetViewHashCode"/>.
/// </summary>
public interface IUIViewModel {
    Func<long>? OverrideViewHash => null;

    /// <summary>
    /// Update any <see cref="LazyEvented{T}"/> that may lead to event-driven CSS updates.
    /// </summary>
    void UpdateEvents() { }

    /// <summary>
    /// Get a hash code that changes whenever the view needs to be redrawn.
    /// <br/>If <see cref="OverrideViewHash"/> is present, it will be used instead, and this will not be called.
    /// </summary>
    long GetViewHash();

    /// <inheritdoc cref="ICursorState.CustomEventHandling"/>
    UIResult? CustomEventHandling(UINode node) => null;

    /// <summary>
    /// Overrides <see cref="UINode"/>.<see cref="UINode.Navigate"/> when the event is OpenContextMenu.
    /// </summary>
    UIResult? OnContextMenu(UINode node, ICursorState cs) => null;

    /// <summary>
    /// Overrides <see cref="UINode"/>.<see cref="UINode.Navigate"/> when the event is Confirm.
    /// </summary>
    UIResult? OnConfirm(UINode node, ICursorState cs) => null;

    /// <summary>
    /// Overrides <see cref="UINode"/>.<see cref="UINode.NavigateInternal"/> for all events.
    /// <br/>Lower priority than <see cref="OnConfirm"/> or <see cref="OnContextMenu"/>.
    /// </summary>
    UIResult? Navigate(UINode node, ICursorState cs, UICommand req) => null;

    /// <summary>
    /// Called when the node is entered in order to determine a tooltip to show next to the node.
    /// </summary>
    UIGroup? Tooltip(UINode node, ICursorState cs, bool prevExists) => null;

    /// <summary>
    /// Returns whether or not the node should be visible.
    /// <br/>If ANY view model returns `false`, or if the node's containing group is not visible,
    ///  then the node will not be visible.
    /// </summary>
    bool ShouldBeVisible(UINode node) => true;
    
    /// <summary>
    /// Returns whether or not the group should be targetable from navigation/interaction events.
    /// <br/>If ANY view model returns `false`, or if the node is not visible, then the node will not be interactable.
    /// </summary>
    bool ShouldBeInteractable(UINode node) => true;
    
    /// <summary>
    /// Returns whether or not the node is enabled for confirm/edit operations.
    /// <br/>Disabled nodes are generally grayed out (they receive the 'node-disabled' CSS class).
    /// <br/>Disabled nodes can still be navigated.
    /// <br/>If ANY view model returns `false`, then the node will not be enabled.
    /// </summary>
    bool ShouldBeEnabled(UINode node) => true;
}

/// <summary>
/// Basic base class for an implementation of <see cref="IUIViewModel"/>.
/// </summary>
public abstract class UIViewModel : IUIViewModel {
    public Func<long>? OverrideViewHash { get; init; }
    
    public virtual void UpdateEvents() { }
    public abstract long GetViewHash();
}

/// <summary>
/// A view model that never requires a redraw.
/// </summary>
public interface IConstUIViewModel : IUIViewModel {
    long IUIViewModel.GetViewHash() => 0;
}

/// <summary>
/// A view model that explicitly tracks changes via <see cref="ViewUpdated"/> and <see cref="ModelUpdated"/>.
/// </summary>
public interface IVersionedUIViewModel : IUIViewModel {
    /// <summary>
    /// Current version of the view model. This is incremented whenever a change is made.
    /// </summary>
    Evented<long> EvViewVersion { get; }

    /// <summary>
    /// Observable for when <see cref="ModelUpdated"/> is called.
    /// </summary>
    IObservable<Unit> EvModelUpdated => _evModelUpdated;
    protected ISubject<Unit> _evModelUpdated { get; }
    
    //Don't allow recursive calls of ModelChanged and Publish
    //This can occur in normal usage when modifying one value from the view
    // results in the options for another value being reset from the model.
    //eg. on the "player select" screen, changing the Player from the view
    // changes the available Shots from the model.
    protected bool IsModelUpdating { get; set; }

    /// <summary>
    /// Notify that a field on the view model was changed due to a model-side change,
    ///  which may require remapping the view.
    /// <br/>Bumps <see cref="EvViewVersion"/>.
    /// </summary>
    void ModelUpdated() => ModelUpdated(this);
    
    protected static void ModelUpdated(IVersionedUIViewModel me) {
        if (!me.IsModelUpdating) {
            me.IsModelUpdating = true;
            me._evModelUpdated.OnNext(default);
            ++me.EvViewVersion.Value;
            me.IsModelUpdating = false;
        }
    }

    /// <summary>
    /// Notify that a field on the view model was changed due to a view-side change.
    /// <br/>Bumps <see cref="EvViewVersion"/>.
    /// </summary>
    void ViewUpdated() {
        if (!IsModelUpdating)
            ++EvViewVersion.Value;
    }

    long IUIViewModel.GetViewHash() => EvViewVersion;
}

/// <inheritdoc cref="IVersionedUIViewModel"/>
public class VersionedUIViewModel : IVersionedUIViewModel {
    public Evented<long> EvViewVersion { get; } = new(0);
    
    /// <inheritdoc cref="EvViewVersion"/>
    public long ViewVersion => EvViewVersion;
    ISubject<Unit> IVersionedUIViewModel._evModelUpdated { get; } = new Event<Unit>();
    bool IVersionedUIViewModel.IsModelUpdating { get; set; }
    public void ModelUpdated() => 
        IVersionedUIViewModel.ModelUpdated(this);
}

/// <summary>
/// <see cref="VersionedUIViewModel"/> for a single value.
/// </summary>
public class VersionedUIViewModel<T> : VersionedUIViewModel {
    public T Value { get; private set; }
    public ManualBinder<T> Binder { get; }
    
    public VersionedUIViewModel(T value) {
        Value = value;
        Binder = new(() => Value, x => Value = x, this);
    }
}

/// <summary>
/// A view model that passes through a source view model (<see cref="Delegator"/>), potentially
///  with some extra information for binding.
/// </summary>
public interface IDerivativeViewModel : IUIViewModel {
    public IUIViewModel Delegator { get; }
    Func<long>? IUIViewModel.OverrideViewHash => Delegator.OverrideViewHash;
    long IUIViewModel.GetViewHash() => Delegator.GetViewHash();
}

}