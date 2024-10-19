using System;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.UI.XML {
/// <summary>
/// A data source for the configuration of a <see cref="EmptyNode"/> created within a freeform <see cref="IFreeformContainer"/>.
/// </summary>
public interface IFixedXMLObject {
    Vector2 Pivot => XMLUtils.Pivot.Center;
    ICObservable<float> Left { get; }
    ICObservable<float> Top { get; }
    ICObservable<float?> Width { get; }
    ICObservable<float?> Height { get; }
    ICObservable<bool> IsVisible { get; }
    ICObservable<bool> IsInteractable { get; }

    public void Cleanup() { }
    
    //NB: normally to center an absolute child you would use left/top:50% with transform:-50%, but in these cases
    // we want to add an additional pixel-based X/Y-offset, so we need to manually calculate half the parent width
    public ICObservable<float> CreateCenterOffsetChildX(ICObservable<float> childX) =>
        //Width is null ? throw new Exception($"{nameof(CreateCenterOffsetChildX)} requires Width parameter") :
        new LazyEvented<float>(() => childX.Value + (Width.Value ?? throw new Exception()) / 2f,
            childX.Erase(), Width.Erase());
    
    public ICObservable<float> CreateCenterOffsetChildY(ICObservable<float> childY) =>
        //Height is null ? throw new Exception($"{nameof(CreateCenterOffsetChildX)} requires Width parameter") :
        new LazyEvented<float>(() => childY.Value + (Height.Value ?? throw new Exception()) / 2f,
            childY.Erase(), Height.Erase());
}

/// <summary>
/// A freeform UITK menu/group that contains dynamically added/removed nodes.
/// </summary>
public interface IFreeformContainer {
    void AddNodeDynamic(UINode n);
    UIScreen Screen { get; }
} 

/// <inheritdoc cref="IFixedXMLObject"/>
public record FixedXMLObject : IFixedXMLObject {
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
    
    public FixedXMLObject(Vector2 lt, Vector2? wh) : this(lt.x, lt.y, wh?.x, wh?.y) { }

    public FixedXMLObject MakeUninteractable(out IDisposable token) {
        token = IsInteractable.AddConst(false);
        return this;
    }

    public virtual void Cleanup() {
        Left.OnCompleted();
        Top.OnCompleted();
        Width.OnCompleted();
        Height.OnCompleted();
        IsInteractable.OnCompleted();
        IsVisible.OnCompleted();
    }
}

/// <summary>
/// A <see cref="FixedXMLObject"/> that updates its positions so that it renders at a target position
///  relative to a given camera. Can be used to track objects in the world.
/// </summary>
public record WorldTrackingXML : FixedXMLObject, IRegularUpdater {
    private readonly IDisposable updToken;
    private readonly Func<Vector3> worldPos;
    private readonly Func<Vector3?>? worldSize;
    public CameraInfo TargetCam { get; set; }
    public Vector3 WorldPos { get; private set; }
    public Vector2 ScreenPoint { get; private set; }

    public WorldTrackingXML(CameraInfo targetCam, Func<Vector3> worldPos, Func<Vector3?>? worldSize) : base(0, 0, null, null) {
        updToken = ETime.RegisterRegularUpdater(this);
        this.TargetCam = targetCam;
        this.worldPos = worldPos;
        this.worldSize = worldSize;
        UpdatePositions();
    }

    public void RegularUpdate() => UpdatePositions();

    private void UpdatePositions() {
        WorldPos = worldPos();
        ScreenPoint = TargetCam.WorldToScreen(WorldPos);
        var l = UIBuilderRenderer.ScreenToXML(ScreenPoint);
        Left.PublishIfNotSame(l.x);
        Top.PublishIfNotSame(l.y);
        var _size = worldSize?.Invoke();
        if (_size is { } size) {
            var s = TargetCam.ToXMLDims(WorldPos, size);
            Width.PublishIfNotSame(s.x);
            Height.PublishIfNotSame(s.y);
        } else {
            Width.PublishIfNotSame(null);
            Height.PublishIfNotSame(null);
        }
    }

    public override void Cleanup() {
        base.Cleanup();
        updToken.Dispose();
    }
}

}