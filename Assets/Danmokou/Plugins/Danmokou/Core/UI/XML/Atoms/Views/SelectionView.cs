using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

/// <inheritdoc cref="Selector{T}"/>
public abstract class Selector {
    public LString? Description { get; set; }
    public bool[] Selected { get; }
    public bool Multiselect { get; init; }
    /// <inheritdoc cref="Selector{T}"/>
    public Selector(int Length, bool Multiselect = false, bool[]? Selected = null) {
        this.Multiselect = Multiselect;
        this.Selected = Selected ?? new bool[Length];
    }

    public bool IsAnySelected {
        get {
            for (int ii = 0; ii < Selected.Length; ++ii)
                if (Selected[ii])
                    return true;
            return false;
        }
    }

    public (int count, int firstIdx) SelectedCount() {
        var firstIdx = -1;
        var total = 0;
        for (int ii = 0; ii < Selected.Length; ++ii)
            if (Selected[ii]) {
                ++total;
                if (firstIdx < 0)
                    firstIdx = ii;
            }
        return (total, firstIdx);
    }

    public abstract IEnumerable<UINode?> MakeNodes(UINode returnTo);
    public abstract LString DescribeAt(int index);

    public bool TrySelect(int index, bool enable) {
        if (enable && !Multiselect)
            Array.Clear(Selected, 0, Selected.Length);
        Selected[index] = enable;
        return true;
    }
}

/// <summary>
/// Helper model class for selecting options via <see cref="SelectionViewModel{T}"/>.
/// </summary>
public class Selector<F> : Selector {
    public IReadOnlyList<F> Values { get; init; }
    public Func<F, LString> Describe { get; init; }
    public Func<F, UINode?>? MakeNode { get; init; }
    public F this[int index] => Values[index];

    public Maybe<F> FirstSelected {
        get {
            for (int ii = 0; ii < Selected.Length; ++ii)
                if (Selected[ii])
                    return Values[ii];
            return Maybe<F>.None;
        }
    }

    public IEnumerable<F> AllSelected() {
        for (int ii = 0; ii < Selected.Length; ++ii)
            if (Selected[ii])
                yield return Values[ii];
    }
    
    /// <inheritdoc cref="Selector{T}"/>
    public Selector(IReadOnlyList<F> Values, Func<F, LString>? Describe = null, Func<F, UINode?>? MakeNode = null, bool Multiselect = false, bool[]? Selected = null) : base(Values.Count, Multiselect, Selected) {
        this.Values = Values;
        this.Describe = Describe ?? (x => (LString)(x?.ToString() ?? "null"));
    }

    public override IEnumerable<UINode?> MakeNodes(UINode returnTo) => 
        Values.Select((x, i) => {
            var node = MakeNode != null ?
                MakeNode(x) :
                new UINode(Describe(x));
            return node?.Bind(MakeView(i, returnTo));
        });

    public override LString DescribeAt(int index) => Describe(this[index]);

    public SelectionView<F> MakeView(int index, UINode returnTo) => new(new(Values[index],
        () => Selected[index], enable => {
            var success = TrySelect(index, enable);
            if (success && enable && !Multiselect)
                return returnTo.ReturnToGroup;
            return new UIResult.StayOnNode(!success);
        }));
    
    /// <summary>
    /// Take the selected values of this selector as a lookup filter.
    /// </summary>
    public Lookup<T>.Filter AsFilter<T>(Func<F[], T, bool> matcher, bool exclude, LString feature,
        Func<F, LString>? printer = null) where T: class {
        var vals = AllSelected().ToArray();
        var valsPrint = string.Join(", ", vals.Select(v => printer?.Invoke(v) ?? v?.ToString() ?? "null"));
        return new Lookup<T>.Filter(x => matcher(vals, x), feature, valsPrint, exclude);
    }
    
    /// <summary>
    /// Eventually take the selected values of this selector as a lookup filter.
    /// </summary>
    public Continuation<Selector, Lookup<T>.Filter> AsFilterContinuation<T>(Func<F[], T, bool> matcher, 
        ICObservable<bool> exclude, LString feature, Func<F, LString>? printer = null) where T : class {
        Description ??= feature;
        return Continuation<Selector, Lookup<T>.Filter>.Of(this, s => s.AsFilter(matcher, exclude.Value, feature, printer));
    }
}

/// <summary>
/// View/model for selecting nodes.
/// </summary>
public class SelectionViewModel<T> : UIViewModel, IUIViewModel {
    public T Value { get; }
    public Func<bool> Selected { get; }
    private Func<bool, UIResult> TrySet { get; }
    
    public SelectionViewModel(T value, Func<bool> selected, Func<bool, UIResult> trySet) {
        Value = value;
        Selected = selected;
        this.TrySet = trySet;
    }

    public UIResult? OnConfirm(UINode node, ICursorState cs) {
        return TrySet(!Selected());
    }

    public override long GetViewHash() => Selected().GetHashCode();
}

/// <inheritdoc cref="SelectionViewModel{T}"/>
public class SelectionView<T>: UIView<SelectionViewModel<T>> {
    public SelectionView(SelectionViewModel<T> viewModel) : base(viewModel) { }
    
    public override void UpdateHTML() {
        var pass = VM.Selected();
        HTML.EnableInClassList(XMLUtils.dropdownSelect, pass);
        HTML.EnableInClassList(XMLUtils.dropdownUnselect, !pass);
    }
}



}