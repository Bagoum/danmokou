using System;
using BagoumLib.Culture;

namespace Danmokou.UI.XML {
/// <summary>
/// Basic implementation of a view/model that supports context menus.
/// <br/>Note that in most cases, you want to implement <see cref="IUIViewModel.OnContextMenu"/>
///  within a more customized view/model instead of using this class.
/// </summary>
public class ContextMenuViewModel : IConstUIViewModel {
    private Func<UINode, ICursorState, UINode[]?> Options { get; }

    public ContextMenuViewModel(Func<UINode, ICursorState, UINode[]?> options) {
        this.Options = options;
    }

    UIResult? IUIViewModel.OnContextMenu(UINode node, ICursorState cs) {
        if (Options(node, cs) is not { } opts)
            return null;
        return PopupUIGroup.CreateContextMenu(node, opts);
    }
}

/// <inheritdoc cref="ContextMenuViewModel"/>
public class ContextMenuView : UIView<ContextMenuViewModel> {
    public ContextMenuView(ContextMenuViewModel viewModel) : base(viewModel) { }
}
}