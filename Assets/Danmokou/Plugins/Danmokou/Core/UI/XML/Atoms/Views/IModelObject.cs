using System;
using BagoumLib;
using BagoumLib.Events;

namespace Danmokou.UI.XML {
/// <summary>
/// Basic interface for a model object which can be destroyed.
/// </summary>
public interface IModelObject {
    protected Evented<bool> _destroyed { get; }
    
    /// <summary>
    /// Event that the model/view model/view can listen to for when the model object is destroyed.
    /// </summary>
    ICObservable<bool> Destroyed => _destroyed;
    
    /// <summary>
    /// Destroy the model object.
    /// </summary>
    public static void Destroy(IModelObject obj) => obj._destroyed.Finalize(true);
}
}