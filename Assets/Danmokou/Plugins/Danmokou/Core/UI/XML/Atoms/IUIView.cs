using System;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using BagoumLib.Reflection;
using Danmokou.Core;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
/// <summary>
/// A set of instructions for rendering a <see cref="IUIViewModel"/> to screen.
/// </summary>
public interface IUIView {
    /// <summary>
    /// When to update this view.
    /// </summary>
    BindingUpdateTrigger UpdateTrigger { get; }

    /// <summary>
    /// Returns true if the view is dirty, and set the dirty state to false.
    /// </summary>
    bool TryConsumeDirty();

    /// <summary>
    /// Update the view's HTML bindings.
    /// </summary>
    void UpdateHTML();
    
    /// <summary>
    /// The view model bound to this view.
    /// </summary>
    IUIViewModel ViewModel { get; }
    
    /// <summary>
    /// An override prefab that should be used for the construction of the node to which this view is attached.
    /// <br/>Only one view on a node may provide a prefab. If all are null,
    ///  then will fall back to default prefab determination logic based on the node type.
    /// <br/>This is overridden by <see cref="UINode"/>.<see cref="UINode.Prefab"/>.
    /// </summary>
    VisualTreeAsset? Prefab { get; } 
    
    /// <summary>
    /// An override set of instructions for constructing the node to which this view is attached,
    ///  provided the HTML of the build target.
    /// <br/>Overrides <see cref="Prefab"/>. Does not require *actually* building HTML; it is safe
    ///  to simply query and return some part of the build target.
    /// </summary>
    Func<VisualElement, VisualElement>? Builder { get; }
    
    /// <summary>
    /// Attach this view to a VisualElement and register it for updates.
    /// <br/>Called during UI instantiation, before <see cref="OnBuilt"/>.
    /// All views attached to a VE receive <see cref="Bind"/>,
    /// and then if the VE is part of a UINode, all of them receive <see cref="OnBuilt"/>.
    /// </summary>
    void Bind(MVVMManager mvvm, VisualElement ve);

    /// <summary>
    /// Unbind this view from its VisualElement.
    /// <br/>Called when the node or HTML using this view was destroyed.
    /// </summary>
    void Unbind();
    
    /// <summary>
    /// Called when the node using this view was built.
    /// <br/>If this view is free-floating and not attached to a node, this method will not be called.
    /// </summary>
    void OnBuilt(UINode node);
    

    /// <summary>
    /// Called when the display language or other global display setting changes.
    /// </summary>
    void ReprocessForLanguageChange();
    
    /// <summary>
    /// Called when navigation entered this node.
    /// </summary>
    void OnEnter(UINode node, ICursorState cs, bool animate) { }

    /// <summary>
    /// Called when navigation exited this node.
    /// </summary>
    void OnLeave(UINode node, ICursorState cs, bool animate, bool isEnteringPopup) { }
    
    /// <inheritdoc cref="UINode.AddedToNavHierarchy"/>
    void OnAddedToNavHierarchy(UINode node) { }
    
    /// <inheritdoc cref="UINode.RemovedFromNavHierarchy"/>
    void OnRemovedFromNavHierarchy(UINode node) { }

    /// <summary>
    /// Called when the mouse is pressed over this node.
    /// <br/>Note that this may not be followed by OnMouseUp (if the mouse moves outside the bounds
    ///  before being released).
    /// <br/>OnMouseEnter and OnMouseLeave are not provided as it is preferred to use OnEnter and OnLeave,
    ///  which are tied more closely to layout handling.
    /// </summary>
    void OnMouseDown(UINode node, PointerDownEvent ev) { }
    
    /// <summary>
    /// Called when the mouse is released over this node.
    /// <br/>Note that this may not be preceded by OnMouseDown.
    /// </summary>
    void OnMouseUp(UINode node, PointerUpEvent ev) { }
    
    /// <summary>
    /// Mark that this view should use the provided event to determine when it is dirty,
    ///  rather than using a hash code or rendering every frame.
    /// <br/>Can be called multiple times if there are multiple events that might require reprocessing.
    /// </summary>
    IDisposable DirtyOn<T>(IObservable<T> ev);
}


public abstract class UIView : IUIView {
    private static readonly Dictionary<Type, BindingId> typeBindings = new();
    public virtual VisualTreeAsset? Prefab => null;
    public virtual Func<VisualElement, VisualElement>? Builder => null;

    public UINode Node { get; private set; } = null!;
    public VisualElement HTML { get; private set; } = null!;
    public BindingUpdateTrigger UpdateTrigger { get; protected set; } = BindingUpdateTrigger.OnSourceChanged;
    public IUIViewModel ViewModel { get; }
    private IDisposable? updateToken;
    private bool dirty;
    
    public UIView(IUIViewModel viewModel) {
        this.ViewModel = viewModel;
    }

    public virtual void Bind(MVVMManager mvvm, VisualElement ve) {
        HTML = ve;
        updateToken = mvvm.RegisterView(this);
    }

    public virtual void Unbind() {
        updateToken?.Dispose();
        updateToken = null;
    }

    public virtual void OnBuilt(UINode node) {
        Node = node;
    }

    public virtual void UpdateHTML() { }
    
    public virtual void ReprocessForLanguageChange() => UpdateHTML();

    bool IUIView.TryConsumeDirty() {
        var x = dirty;
        dirty = false;
        return x;
    }

    public void MarkDirty() => dirty = true;

    public IDisposable DirtyOn<T>(IObservable<T> ev) {
        UpdateTrigger = BindingUpdateTrigger.WhenDirty;
        return ev.Subscribe(_ => dirty = true);
    }

    /// <summary>
    /// Create a UINode with only this view.
    /// </summary>
    public UINode MakeNode() => new UINode(this);
}
public abstract class UIView<T> : UIView where T : IUIViewModel {
    public new T ViewModel { get; }
    public T VM => ViewModel;
    private bool _isFirstRender = true;
    private bool _isFirstVisibleRender = true;
    protected bool IsFirstRender() {
        if (_isFirstRender) {
            _isFirstRender = false;
            return true;
        } else return false;
    }
    protected bool IsFirstVisibleRender() {
        if (_isFirstVisibleRender) {
            _isFirstVisibleRender = false;
            return true;
        } else return false;
    }

    public UIView(T viewModel) : base(viewModel) {
        ViewModel = viewModel;
    }
}


}