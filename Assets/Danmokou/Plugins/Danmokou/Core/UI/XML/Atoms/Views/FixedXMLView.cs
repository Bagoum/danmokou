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
    
    public FixedXMLViewModel(IFixedXMLObject descr, IFixedXMLReceiver? recv = null) {
        Descr = descr;
        Recv = recv;
    }
    public FixedXMLViewModel(IFixedXMLObject descr, Func<UINode, ICursorState, UICommand, UIResult?> nav) : 
        this(descr, new NavigatorOnlyReceiver(nav)) { }

    UIResult? IUIViewModel.Navigate(UINode node, ICursorState cs, UICommand req) => Recv?.Navigate(node, cs, req);

    UIGroup? IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
        if (Recv?.Tooltip is { } tt)
            return node.MakeTooltip(UINode.SimpleTTGroup(tt));
        return null;
    }

    bool IUIViewModel.ShouldBeInteractable(UINode node) => Descr.IsInteractable.Value;
    
    private record NavigatorOnlyReceiver(Func<UINode, ICursorState, UICommand, UIResult?> nav) : IFixedXMLReceiver {
        public UIResult? Navigate(UINode n, ICursorState cs, UICommand req) => nav.Invoke(n, cs, req);
    }
}

/// <inheritdoc cref="FixedXMLViewModel"/>
public class FixedXMLView : UIView<FixedXMLViewModel>, IUIView {
    public bool AsEmpty { get; init; }
    public bool IsKeyboardNavigable { get; init; } = true;
    public FixedXMLView(FixedXMLViewModel data) : base(data) { }

    public override void OnBuilt(UINode node) {
        base.OnBuilt(node);
        if (AsEmpty)
            HTML.ConfigureEmpty();
        if (!IsKeyboardNavigable)
            node.AllowKBInteraction = false;
        //Since FixedXMLObject is event-based, we don't need an update method, just event bindings.
        node.AddToken(VM.Descr.IsInteractable.Subscribe(b => {
                if (!b)
                    node.Controller.MoveCursorAwayFromNode(node);
            }))
            .AddToken(VM.Descr.IsVisible.Subscribe(b => {
                //Allows opacity fade-out
                HTML.pickingMode = b ? PickingMode.Position : PickingMode.Ignore;
                HTML.style.opacity = b ? new StyleFloat(StyleKeyword.Null) : 0;
            }));
        HTML.ConfigureAbsolute(VM.Descr.Pivot).ConfigureFixedXMLPositions(VM.Descr);

        VM.Recv?.OnBuilt(node, VM.Descr);
    }

    public override void Unbind() {
        base.Unbind();
        VM.Descr.Cleanup();
    }

    void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) => VM.Recv?.OnEnter(node, cs);
    void IUIView.OnMouseDown(UINode node, PointerDownEvent ev) => VM.Recv?.OnPointerDown(node, ev);
    void IUIView.OnMouseUp(UINode node, PointerUpEvent ev) => VM.Recv?.OnPointerUp(node, ev);
    void IUIView.OnLeave(UINode node, ICursorState cs, bool animate, PopupUIGroup.Type? popupType) => 
        VM.Recv?.OnLeave(node, cs);
}
}