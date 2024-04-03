using System;
using BagoumLib.Culture;

namespace Danmokou.UI.XML {
/// <summary>
/// Basic implementation of a view/model that supports tooltips.
/// <br/>Note that in most cases, you want to implement <see cref="IUIViewModel.Tooltip"/>
///  within a more customized view/model instead of using this class.
/// </summary>
public class TooltipViewModel : IConstUIViewModel {
    private Func<LString> Text { get; }

    public TooltipViewModel(Func<LString> text) {
        Text = text;
    }
    public TooltipViewModel(LString text) : this(() => text) { }
    
    UIGroup? IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
        return node.MakeTooltip(node.SimpleTTGroup(Text()));
    }
}

/// <inheritdoc cref="TooltipViewModel"/>
public class TooltipView : UIView<TooltipViewModel> {
    public TooltipView(TooltipViewModel viewModel) : base(viewModel) { }
}
}