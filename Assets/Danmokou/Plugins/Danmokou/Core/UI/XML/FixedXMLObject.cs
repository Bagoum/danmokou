using System;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;
using UnityEngine;

namespace Danmokou.UI.XML {
/// <summary>
/// A data source for the configuration of a <see cref="EmptyNode"/> created within a freeform <see cref="IFixedXMLObjectContainer"/>.
/// </summary>
public interface IFixedXMLObject {
    string Descriptor { get; }
    Vector2 Pivot => XMLUtils.Pivot.Center;
    ICObservable<float> Left { get; }
    ICObservable<float> Top { get; }
    ICObservable<float?> Width { get; }
    ICObservable<float?> Height { get; }
    ICObservable<bool> IsVisible { get; }
    ICObservable<bool> IsInteractable { get; }

    public void Cleanup() { }
    
    public ICObservable<float> CreateCenterOffsetChildX(ICObservable<float> childX) =>
        new LazyEvented<float>(() => childX.Value + (Width.Value ?? throw new Exception()) / 2f,
            childX.Erase(), Width.Erase());
    
    public ICObservable<float> CreateCenterOffsetChildY(ICObservable<float> childY) =>
        new LazyEvented<float>(() => childY.Value + (Height.Value ?? throw new Exception()) / 2f,
            childY.Erase(), Height.Erase());
}

/// <summary>
/// A freeform UITK menu/group that contains dynamically added/removed nodes.
/// </summary>
public interface IFixedXMLObjectContainer {
    void AddNodeDynamic(UINode n);
    UIScreen Screen { get; }
} 

/// <inheritdoc cref="IFixedXMLObject"/>
public record FixedXMLObject : IFixedXMLObject {
    public string Descriptor { get; init; } = "";
    public Vector2 Pivot { get; init; } = XMLUtils.Pivot.Center;
    ICObservable<float> IFixedXMLObject.Left => Left;
    public Evented<float> Left { get; }
    ICObservable<float> IFixedXMLObject.Top => Top;
    public Evented<float> Top { get; }
    ICObservable<float?> IFixedXMLObject.Width => Width;
    public Evented<float?> Width { get; }
    ICObservable<float?> IFixedXMLObject.Height => Height;
    public Evented<float?> Height { get; }
    public Evented<bool> IsVisible { get; } = new(true);
    ICObservable<bool> IFixedXMLObject.IsVisible => IsVisible;
    public DisturbedAnd IsInteractable { get; } = new(true);
    ICObservable<bool> IFixedXMLObject.IsInteractable => IsInteractable;
    
    /// <inheritdoc cref="IFixedXMLObject"/>
    public FixedXMLObject(float l, float t, float? w, float? h) {
        Left = new(l);
        Top = new(t);
        Width = new(w);
        Height = new(h);
        IsInteractable.AddDisturbance(IsVisible);
    }

    public FixedXMLObject MakeUninteractable(out IDisposable token) {
        token = IsInteractable.AddConst(false);
        return this;
    }

    public void Cleanup() {
        Left.OnCompleted();
        Top.OnCompleted();
        Width.OnCompleted();
        Height.OnCompleted();
        IsInteractable.OnCompleted();
        IsVisible.OnCompleted();
    }
}
}