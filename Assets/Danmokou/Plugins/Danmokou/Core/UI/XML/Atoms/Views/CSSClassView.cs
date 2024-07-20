using System;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

/// <summary>
/// A view/model that enables and disables two CSS classes based on whether a function returns true or false.
/// </summary>
public class CSSClassViewModel : UIViewModel {
    public Func<bool> Switch { get; }
    public string? WhenTrue { get; }
    public string? WhenFalse { get; }
    
    public CSSClassViewModel(Func<bool> _switch, string? whenTrue, string? whenFalse) {
        Switch = _switch;
        WhenTrue = whenTrue;
        WhenFalse = whenFalse;
    }

    public override long GetViewHash() => Switch().GetHashCode();
}

/// <inheritdoc cref="CSSClassViewModel"/>
public class CssClassView : UIView<CSSClassViewModel> {
    public CssClassView(CSSClassViewModel viewModel) : base(viewModel) { }

    public override void UpdateHTML() {
        var pass = ViewModel.Switch();
        if (ViewModel.WhenTrue is { } trueClass)
            HTML.EnableInClassList(trueClass, pass);
        if (ViewModel.WhenFalse is { } falseClass)
            HTML.EnableInClassList(falseClass, !pass);
    }
}
}