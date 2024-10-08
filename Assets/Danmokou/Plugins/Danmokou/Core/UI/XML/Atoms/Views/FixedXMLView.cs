using System;
using System.Linq;
using System.Reactive;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

/// <summary>
/// View/model for positioning HTML using <see cref="IFixedXMLObject"/> and <see cref="IFixedXMLReceiver"/>
///  (primarily used by Suzunoya's ADV handling).
/// </summary>
public class BaseFixedXMLView : UIView<BaseFixedXMLView.BModel> {
    /// <inheritdoc cref="BaseFixedXMLView"/>
    public class BModel : IConstUIViewModel {
        public IFixedXMLObject Descr { get; }
    
        public BModel(IFixedXMLObject descr) {
            Descr = descr;
        }
    }
    
    public bool IsAbsPositioned { get; init; } = true;
    
    public BaseFixedXMLView(BModel data) : base(data) { }

    public override void Bind(MVVMManager mvvm, VisualElement ve) {
        base.Bind(mvvm, ve);
        if (IsAbsPositioned)
            HTML.ConfigureAbsolute(VM.Descr.Pivot).ConfigureLeftTopListeners(VM.Descr.Left, VM.Descr.Top);
        HTML.ConfigureWidthHeightListeners(VM.Descr.Width, VM.Descr.Height);
    }

    public override void Unbind() {
        base.Unbind();
        VM.Descr.Cleanup();
    }
}

/// <summary>
/// View/model for positioning nodes using <see cref="IFixedXMLObject"/> and <see cref="IFixedXMLReceiver"/>
///  (primarily used by Suzunoya's ADV handling).
/// </summary>
public class FixedXMLView : BaseFixedXMLView, IUIView {
    /// <inheritdoc cref="FixedXMLView"/>
    public class Model : BModel, IConstUIViewModel {
        public IFixedXMLReceiver? Recv { get; }
    
        public Model(IFixedXMLObject descr, IFixedXMLReceiver? recv = null) : base(descr) {
            Recv = recv;
        }
        public Model(IFixedXMLObject descr, Func<UINode, ICursorState, UICommand, UIResult?> nav) : 
            this(descr, new NavigatorOnlyReceiver(nav)) { }

        UIResult? IUIViewModel.Navigate(UINode node, ICursorState cs, UICommand req) => Recv?.Navigate(node, cs, req);

        TooltipProxy? IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
            if (Recv?.Tooltip is { } tt)
                return node.MakeTooltip(UINode.SimpleTTGroup(tt));
            return null;
        }

        bool IUIViewModel.ShouldBeInteractable(UINode node) => Descr.IsInteractable.Value;
    
        private record NavigatorOnlyReceiver(Func<UINode, ICursorState, UICommand, UIResult?> nav) : IFixedXMLReceiver {
            public UIResult? Navigate(UINode n, ICursorState cs, UICommand req) => nav.Invoke(n, cs, req);
        }
    }
    
    public bool IsKeyboardNavigable { get; init; } = true;
    public new Model VM => (Model)base.VM;
    public FixedXMLView(Model data) : base(data) { }

    public override void OnBuilt(UINode node) {
        base.OnBuilt(node);
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
        VM.Recv?.OnBuilt(node, VM.Descr);
    }

    void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) => VM.Recv?.OnEnter(node, cs);
    void IUIView.OnMouseDown(UINode node, PointerDownEvent ev) => VM.Recv?.OnPointerDown(node, ev);
    void IUIView.OnMouseUp(UINode node, PointerUpEvent ev) => VM.Recv?.OnPointerUp(node, ev);
    void IUIView.OnLeave(UINode node, ICursorState cs, bool animate, PopupUIGroup.Type? popupType) => 
        VM.Recv?.OnLeave(node, cs);
}
}