using System;
using System.Linq;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;

namespace Danmokou.UI.XML {
public static class ModelHelpers {
    /// <inheritdoc cref="IModelObject.Destroy"/>
    public static void Destroy(this IModelObject obj) => IModelObject.Destroy(obj);

    /// <summary>
    /// Run a callback when the model object is destroyed.
    /// </summary>
    public static IDisposable WhenDestroyed(this IModelObject obj, Action cb) => 
        obj.Destroyed.Subscribe(dead => {
            if (dead)
                cb();
        });

    /// <summary>
    /// Returns the current value of <see cref="IModelObject.Destroyed"/>.
    /// </summary>
    public static bool IsDestroyed(this IModelObject obj) => obj.Destroyed.Value;
}
}