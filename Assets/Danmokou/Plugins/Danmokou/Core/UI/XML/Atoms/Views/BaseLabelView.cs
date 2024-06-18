using System;
using System.Collections.Generic;
using BagoumLib.Culture;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
/// <summary>
/// A view/model that binds a text field in UXML.
/// </summary>
public interface ILabelViewModel : IUIViewModel {
    public string Label { get; }
}

/// <summary>
/// A view/model that binds a text field in UXML based on a value of type T.
/// </summary>
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

/// <inheritdoc cref="ILabelViewModel"/>
public class BaseLabelView<T> : UIView<T> where T : ILabelViewModel {
    private Label target = null!;
    private readonly string? labelName;
    public BaseLabelView(T data, string? labelName = null) : base(data) {
        this.labelName = labelName;
    }
    
    public override void OnBuilt(UINode node) {
        base.OnBuilt(node);
        target = Node.HTML.Q<Label>(labelName);
        if (target == null)
            throw new Exception($"No label found by name `{labelName}`");
    }

    protected override BindingResult Update(in BindingContext context) {
        target.text = ViewModel.Label;
        return base.Update(in context);
    }
}
/// <inheritdoc cref="LabelViewModel{T}"/>
public class LabelView<T> : BaseLabelView<LabelViewModel<T>> {
    public LabelView(LabelViewModel<T> viewModel, string? labelName = null) : base(viewModel, labelName) { }
}

/// <summary>
/// A view/model that binds a text field in UXML based on a string value.
/// </summary>
public class SimpleLabelView : LabelView<string> {
    public SimpleLabelView(Func<string> data, string? labelName = null) : 
        base(new LabelViewModel<string>(data, x=>x), labelName) { }
}

/// <summary>
/// A view/model that binds a text field in UXML based on a boolean value.
/// </summary>
public class FlagViewModel : LabelViewModel<bool> {
    public FlagViewModel(Func<bool> flag, LString whenTrue, LString whenFalse) : 
        base(flag, b => b ? whenTrue.Value : whenFalse.Value) { }
}

/// <inheritdoc cref="FlagViewModel"/>
public class FlagView : LabelView<bool> {
    public FlagView(FlagViewModel viewModel) : base(viewModel) { }
}

}