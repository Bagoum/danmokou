using System;
using System.Collections.Generic;
using BagoumLib.Culture;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
/// <summary>
/// A view model providing information about text to be bound in UXML.
/// </summary>
public interface ILabelViewModel : IUIViewModel {
    public string Label { get; }
}
public class LabelViewModel<T> : UIViewModel, ILabelViewModel {
    public Func<T> Value { get; }
    public Func<T, string> Mapper { get; }
    public string Label => Mapper(Value());
    

    public LabelViewModel(Func<T> value, Func<T, string> mapper) {
        this.Value = value;
        this.Mapper = mapper;
    }

    public override long GetViewHash() => EqualityComparer<T>.Default.GetHashCode(Value());
}

/// <summary>
/// A view that binds a text field in UXML.
/// </summary>
public class LabelView : UIView<ILabelViewModel> {
    private Label target = null!;
    private readonly string? labelName;
    public LabelView(Func<string> data, string? labelName = null) : 
        this(new LabelViewModel<string>(data, x => x), labelName) { }

    public LabelView(ILabelViewModel data, string? labelName = null) : base(data) {
        this.labelName = labelName;
    }
    
    public override void NodeBuilt(UINode node) {
        base.NodeBuilt(node);
        target = Node.HTML.Q<Label>(labelName);
        if (target == null)
            throw new Exception($"No label found by name `{labelName}`");
    }

    protected override BindingResult Update(in BindingContext context) {
        target.text = ViewModel.Label;
        return base.Update(in context);
    }
}
/// <inheritdoc cref="LabelView"/>
public class LabelView<T> : LabelView {
    public LabelView(LabelViewModel<T> viewModel, string? labelName = null) : base(viewModel, labelName) { }
    public LabelView(Func<string> data, Func<long>? viewHash, string? labelName = null) : 
        base(new LabelViewModel<string>(data, x=>x) { OverrideHashHandler = viewHash }, labelName) { }
}

/// <summary>
/// A view model providing information about a boolean value that maps to one of two strings
///  that can be displayed in a UXML text field.
/// </summary>
public class FlagViewModel : LabelViewModel<bool> {
    public FlagViewModel(Func<bool> flag, LString whenTrue, LString whenFalse) : 
        base(flag, b => b ? whenTrue.Value : whenFalse.Value) { }
}

/// <summary>
/// A view that binds a text field based on the boolean output of <see cref="FlagViewModel"/>.
/// </summary>
public class FlagLabelView : LabelView<bool> {
    public FlagLabelView(FlagViewModel viewModel) : base(viewModel) { }
}

}