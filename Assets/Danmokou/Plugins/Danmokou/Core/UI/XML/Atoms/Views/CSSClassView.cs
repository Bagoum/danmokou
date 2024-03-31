using System;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
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
public class CssClassView : UIView<CSSClassViewModel> {
    public CssClassView(CSSClassViewModel viewModel) : base(viewModel) { }

    protected override BindingResult Update(in BindingContext context) {
        var pass = ViewModel.Switch();
        if (ViewModel.WhenTrue is { } trueClass)
            Node.HTML.EnableInClassList(trueClass, pass);
        if (ViewModel.WhenFalse is { } falseClass)
            Node.HTML.EnableInClassList(falseClass, !pass);
        return base.Update(in context);
    }
}
}