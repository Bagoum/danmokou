using System;
using System.Reactive;
using BagoumLib.Events;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
/// <summary>
/// A set of data that will be rendered to screen by <see cref="IUIView"/>.
/// <br/>Contains information about data changes via <see cref="IDataSourceViewHashProvider.GetViewHashCode"/>.
/// </summary>
public interface IUIViewModel : IDataSourceViewHashProvider {
    BindingUpdateTrigger UpdateTrigger { get; set; }
    Func<long>? OverrideHashHandler { get; set; }

    //UITK internals will recompute hash code even if the update trigger is WhenDirty.
    //In most cases when we set the update trigger to WhenDirty, we want to avoid allocations
    // that would otherwise occur in hash code computation. As such, we provide this default behavior
    // to prevent calculating the hash if it's not going to be used.
    long IDataSourceViewHashProvider.GetViewHashCode() =>
        UpdateTrigger == BindingUpdateTrigger.WhenDirty ? 0 : 
            OverrideHashHandler?.Invoke() ?? GetViewHash();
        
    /// <summary>
    /// Get a hash code that changes whenever the view needs to be redrawn.
    /// <br/>This will not be called if <see cref="UpdateTrigger"/> is WhenDirty,
    ///  and is overriden by <see cref="OverrideHashHandler"/>.
    /// </summary>
    long GetViewHash();
}

/// <summary>
/// Basic base class for an implementation of <see cref="IUIViewModel"/>.
/// </summary>
public abstract class UIViewModel : IUIViewModel {
    public BindingUpdateTrigger UpdateTrigger { get; set; }
    public Func<long>? OverrideHashHandler { get; set; }
    public abstract long GetViewHash();
}

/// <summary>
/// A view model that explicitly tracks changes via <see cref="Publish"/>.
/// </summary>
public interface IVersionedUIViewModel : IUIViewModel {
    /// <summary>
    /// Current version of the view model. This is incremented whenever a change is made.
    /// </summary>
    Evented<long> ViewVersion { get; }
    
    /// <summary>
    /// Notify that a field on the view model was changed due to a model-side change,
    ///  which may require remapping the view.
    /// </summary>
    IObservable<Unit> UpdatedFromModel { get; }

    /// <summary>
    /// Notify that a field on the view model was changed, either due to a view-side change or a model-side change.
    /// </summary>
    void Publish();

    long IUIViewModel.GetViewHash() => ViewVersion;
}

/// <inheritdoc cref="IVersionedUIViewModel"/>
public class VersionedUIViewModel : IVersionedUIViewModel {
    public BindingUpdateTrigger UpdateTrigger { get; set; }
    public Func<long>? OverrideHashHandler { get; set; }
    public Evented<long> ViewVersion { get; } = new(0);
    public IObservable<Unit> UpdatedFromModel => _updModel;
    private readonly Event<Unit> _updModel = new();
    
    /// <inheritdoc cref="UpdatedFromModel"/>
    public void ModelChanged() {
        if (!isModelUpdating) {
            isModelUpdating = true;
            _updModel.OnNext(default);
            ++ViewVersion.Value;
            isModelUpdating = false;
        }
    }

    //Don't allow recursive calls of ModelChanged and Publish
    //This can occur in normal usage when modifying one value from the view
    // results in the options for another value being reset from the model.
    //eg. on the "player select" screen, changing the Player from the view
    // changes the available Shots from the model.
    private bool isModelUpdating = false;
    
    public void Publish() {
        if (!isModelUpdating)
            ++ViewVersion.Value;
    }
}

/// <summary>
/// A view model that passes through a source view model (<see cref="Delegator"/>), potentially
///  with some extra information for binding.
/// </summary>
public interface IDerivativeViewModel : IUIViewModel {
    public IUIViewModel Delegator { get; }

    BindingUpdateTrigger IUIViewModel.UpdateTrigger {
        get => Delegator.UpdateTrigger; 
        set => Delegator.UpdateTrigger = value;
    }
    Func<long>? IUIViewModel.OverrideHashHandler {
        get => Delegator.OverrideHashHandler; 
        set => Delegator.OverrideHashHandler = value;
    }
    long IUIViewModel.GetViewHash() => Delegator.GetViewHash();
}

}