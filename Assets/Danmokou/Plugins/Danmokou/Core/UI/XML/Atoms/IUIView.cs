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
    /// Attach this view to a VisualElement (called during UI instantiation).
    /// </summary>
    void Bind(VisualElement ve);
    
    /// <summary>
    /// Called when the node using this view was built.
    /// </summary>
    void OnBuilt(UINode node);
    
    /// <summary>
    /// Called when navigation entered this node.
    /// </summary>
    void OnEnter(UINode node, ICursorState cs, bool animate) { }

    /// <summary>
    /// Called when navigation exited this node.
    /// </summary>
    void OnLeave(UINode node, ICursorState cs, bool animate, bool isEnteringPopup) { }
    
    /// <summary>
    /// Called when this node is being removed from the group call stack.
    /// </summary>
    void OnCloseDependencies(UINode node) { }

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
    /// Called when the node using this view was destroyed.
    /// </summary>
    void OnDestroyed(UINode node);
    
    /// <summary>
    /// Mark that this view should use the provided event to determine when it is dirty,
    ///  rather than using a hash code or rendering every frame.
    /// <br/>Can be called multiple times if there are multiple events that might require reprocessing.
    /// </summary>
    void DirtyOn<T>(IObservable<T> ev);
}


public abstract class UIView : CustomBinding, IUIView, ITokenized {
    private static readonly Dictionary<Type, BindingId> typeBindings = new();
    public virtual VisualTreeAsset? Prefab => null;
    public virtual Func<VisualElement, VisualElement>? Builder => null;
    public List<IDisposable> Tokens { get; } = new();
    public UINode Node { get; private set; } = null!;
    public IUIViewModel ViewModel { get; }
    public BindingUpdateTrigger UpdateTrigger {
        get => updateTrigger;
        set => ViewModel.UpdateTrigger = updateTrigger = value;
    }
    public BindingId BindingId =>
        typeBindings.TryGetValue(this.GetType(), out var bdg) ?
            bdg :
            typeBindings[this.GetType()] = new(this.GetType().RName());
    
    public UIView(IUIViewModel viewModel) {
        this.ViewModel = viewModel;
        UpdateTrigger = BindingUpdateTrigger.OnSourceChanged;
    }

    public void Bind(VisualElement ve) => ve.SetBinding(BindingId, this);

    public virtual void OnBuilt(UINode node) {
        Node = node;
    }

    public virtual void OnDestroyed(UINode node) {
        (this as IDisposable).Dispose();
    }

    public void DirtyOn<T>(IObservable<T> ev) {
        UpdateTrigger = BindingUpdateTrigger.WhenDirty;
        Tokens.Add(ev.Subscribe(_ => MarkDirty()));
    }
}
public abstract class UIView<T> : UIView, IDataSourceProvider where T : IUIViewModel {
    public new T ViewModel { get; }
    public T VM => ViewModel;
    public object dataSource => ViewModel!;
    public PropertyPath dataSourcePath => new();
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