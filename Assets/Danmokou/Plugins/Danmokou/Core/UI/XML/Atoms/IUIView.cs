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
    /// An override prefab that should be used for the construction of the node to which this view is attached.
    /// <br/>Only one view on a node may provide a prefab. If all are null,
    ///  then will fall back to default prefab determination logic based on the node type.
    /// <br/>This is overridden by <see cref="UINode"/>.<see cref="UINode.Prefab"/>.
    /// </summary>
    VisualTreeAsset? Prefab { get; } 
    
    /// <summary>
    /// Attach this view to a VisualElement (called during UI instantiation).
    /// </summary>
    void Bind(VisualElement ve);
    
    /// <summary>
    /// Notify that the node using this view was built.
    /// </summary>
    void NodeBuilt(UINode node);
    
    /// <summary>
    /// Notify that the node using this view was destroyed.
    /// </summary>
    void NodeDestroyed(UINode node);
    
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
    public List<IDisposable> Tokens { get; } = new();
    public UINode Node { get; private set; } = null!;
    private IUIViewModel ViewModel { get; }
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

    public virtual void NodeBuilt(UINode node) {
        Node = node;
    }

    public virtual void NodeDestroyed(UINode node) {
        (this as IDisposable).Dispose();
    }

    public void DirtyOn<T>(IObservable<T> ev) {
        UpdateTrigger = BindingUpdateTrigger.WhenDirty;
        Tokens.Add(ev.Subscribe(_ => MarkDirty()));
    }
}
public abstract class UIView<T> : UIView, IDataSourceProvider where T : IUIViewModel {
    public T ViewModel { get; }
    public T VM => ViewModel;
    public object dataSource => ViewModel!;
    public PropertyPath dataSourcePath => new();
    private bool _isFirstRender = true;
    protected bool IsFirstRender() {
        if (_isFirstRender) {
            _isFirstRender = false;
            return true;
        } else return false;
    }

    public UIView(T viewModel) : base(viewModel) {
        ViewModel = viewModel;
    }
}


}