using System;
using System.Linq;
using System.Reactive;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

/// <summary>
/// View/model for positioning objects using <see cref="IFixedXMLObject"/> and <see cref="IFixedXMLReceiver"/>
///  (primarily used by Suzunoya's ADV handling).
/// </summary>
public class FixedXMLViewModel : IConstUIViewModel {
    public IFixedXMLObject Descr { get; }
    public IFixedXMLReceiver? Recv { get; }
    public Func<UINode,ICursorState,UIResult?>? OnConfirmer { get; init; }

    public FixedXMLViewModel(IFixedXMLObject descr, IFixedXMLReceiver? recv = null) {
        Descr = descr;
        Recv = recv;
    }

    UIResult? IUIViewModel.OnConfirm(UINode node, ICursorState cs) => 
        OnConfirmer?.Invoke(node, cs) ?? Recv?.OnConfirm(node, cs);

    UIGroup? IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
        if (Recv?.Tooltip is { } tt)
            return node.MakeTooltip(node.SimpleTTGroup(tt));
        return null;
    }
}

/// <inheritdoc cref="FixedXMLViewModel"/>
public class FixedXMLView : UIView<FixedXMLViewModel>, IUIView {
    public bool AsEmpty { get; init; }
    public FixedXMLView(FixedXMLViewModel data) : base(data) { }

    public override void OnBuilt(UINode node) {
        base.OnBuilt(node);
        if (AsEmpty)
            Node.HTML.ConfigureEmpty();
        //Since FixedXMLObject is event-based, we don't need an update method, just event bindings.
        Node.AddToken(VM.Descr.IsInteractable.Subscribe(b => Node.UpdatePassthrough(!b)))
            .AddToken(VM.Descr.IsVisible.Subscribe(b => {
            //Allows opacity fade-out
            Node.HTML.pickingMode = b ? PickingMode.Position : PickingMode.Ignore;
            Node.HTML.style.opacity = b ? 1 : 0;
        }));
        Node.HTML.ConfigureAbsolute(VM.Descr.Pivot).ConfigureFixedXMLPositions(VM.Descr);

        VM.Recv?.OnBuilt((Node as EmptyNode)!);
    }

    public override void OnDestroyed(UINode node) {
        base.OnDestroyed(node);
        VM.Descr.Cleanup();
    }

    void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) => VM.Recv?.OnEnter(node, cs);
    void IUIView.OnMouseDown(UINode node, PointerDownEvent ev) => VM.Recv?.OnPointerDown(node, ev);
    void IUIView.OnMouseUp(UINode node, PointerUpEvent ev) => VM.Recv?.OnPointerUp(node, ev);
    void IUIView.OnLeave(UINode node, ICursorState cs, bool animate, bool isEnteringPopup) => 
        VM.Recv?.OnLeave(node, cs);
}
}