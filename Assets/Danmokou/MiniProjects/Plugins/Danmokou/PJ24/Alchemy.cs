using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using BagoumLib.Events;
using Danmokou.UI.XML;

namespace MiniProjects.PJ24 {
public static class Alchemy {
    public static Category[] Categories { get; } = {
        Category.CLOTH,
        Category.OIL
    };
    
    public static string Print(this Category c) => c switch {
        Category.CLOTH => "(Cloth)",
        Category.OIL => "(Oil)",
        _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
    };
    
    /// <summary>
    /// Determine the traits that will be applied to the final result.
    /// <br/>Can be called with incomplete inputs.
    /// </summary>
    public static List<TraitInstance> CombineTraits(List<ItemInstance>[] inputs) =>
        inputs.SelectMany(x => x).SelectMany(x => x.Traits).CombineTraits();
    
    /// <inheritdoc cref="CombineTraits(System.Collections.Generic.List{MiniProjects.PJ24.ItemInstance}[])"/>
    public static List<TraitInstance> CombineTraits(this IEnumerable<TraitInstance> source) {
        var outp = new List<TraitInstance>();
        foreach (var trait in source) {
            for (int ii = 0; ii < outp.Count; ++ii) {
                var other = outp[ii];
                //Don't allow nested merges
                if (other.SynthedFrom != null) continue;
                if ((trait.Type.TryMergeWith(other.Type) ?? other.Type.TryMergeWith(trait.Type)) is { } synth) {
                    outp.RemoveAt(ii);
                    outp.Insert(ii, new TraitInstance(synth, (other, trait)));
                    goto nxt;
                }
            }
            //Wipe existing SynthedFrom info
            outp.Add(trait with { SynthedFrom = null });
            nxt: ;
        }
        //Remove duplicate traits
        for (int ii = 0; ii < outp.Count; ++ii) {
            for (int jj = 0; jj < ii; ++jj) {
                if (outp[ii].Type == outp[jj].Type) {
                    outp.RemoveAt(ii--);
                    break;
                }
            }
        }
        return outp;
    }

    public static string DefaultName(Type t) {
        var typName = t.Name;
        var sb = new StringBuilder();
        for (int ii = 0; ii < typName.Length; ++ii) {
            var c = typName[ii];
            if (ii > 0 && (char.IsUpper(c) || char.IsDigit(c) && !char.IsDigit(typName[ii-1])))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}

public record CurrentSynth(Recipe Recipe, int Count) : IDisposable {
    public int Version { get; private set; } = 0;
    public Event<Unit> SelectionChanged { get; } = new();

    private void DidUpdate() {
        ++Version;
        SelectionChanged.OnNext(default);
    }
    public List<ItemInstance>[] Selected { get; set; } = 
        Recipe.Components
            .Select(x => new List<ItemInstance>())
            .ToArray();
    public int? CurrentSelection { get; private set; }
    public int CurrentSelectionOrThrow =>
        CurrentSelection ?? throw new Exception("Not currently selecting ingredients for a recipe component");

    public bool CurrentComponentSatisfied =>
        CurrentSelection is {} sel && ComponentSatisfied(sel);
    public (int selected, int required) ComponentReq(int index) => 
        (Selected[index].Count, Recipe.Components[index].Count * Count);
    public bool ComponentSatisfied(int index) {
        var (sel, req) = ComponentReq(index);
        return sel == req;
    }
    public bool AllComponentsSatisfied() {
        for (int ii = 0; ii < Selected.Length; ++ii)
            if (!ComponentSatisfied(ii))
                return false;
        return true;
    }

    public int? FirstUnsatisfiedIndex {
        get {
            for (int ii = 0; ii < Selected.Length; ++ii)
                if (!ComponentSatisfied(ii))
                    return ii;
            return null;
        }
    }

    /// <summary>
    /// Returns true if the item is selected for a component index other than the one currently being edited.
    /// </summary>
    public bool IsSelectedForOther(ItemInstance inst) {
        for (int ii = 0; ii < Selected.Length; ++ii) {
            if (ii != CurrentSelection && Selected[ii] is { } lis) {
                for (int jj = 0; jj < lis.Count; ++jj)
                    if (lis[jj] == inst)
                        return true;
            }
        }
        return false;
    }

    public bool IsSelectedForCurrent(ItemInstance inst) =>
        CurrentSelection is { } sel && Selected[sel].Contains(inst);
    
    public void StartSelecting(int index) {
        Selected[index] = new();
        CurrentSelection = index;
        DidUpdate();
    }
    
    /// <summary>
    /// Select the item if it is currently unselected,
    /// or unselect it if it is currently selected.
    /// </summary>
    /// <returns>True if the item was selected, false if it was unselected,
    /// or null if the item could not be selected because enough items have already been selected.</returns>
    public bool? ChangeSelectionForCurrent(ItemInstance inst) {
        var lis = Selected[CurrentSelectionOrThrow];
        if (lis.Remove(inst)) {
            SelectionChanged.OnNext(default);
            return false;
        }
        if (CurrentComponentSatisfied)
            return null;
        lis.Add(inst);
        DidUpdate();
        return true;
    }
    
    
    public void CancelSelection() {
        if (CurrentSelection is not { } sel) return;
        Selected[sel] = new();
        CurrentSelection = null;
        DidUpdate();
    }

    public void CommitSelection() {
        CurrentSelection = null;
        DidUpdate();
    }

    public (ItemInstance result, IEnumerable<ItemInstance> consumed) ExecuteSynthesis() {
        var result = Recipe.Synthesize(Selected);
        return (result, Selected.SelectMany(x => x));
    }

    public void Dispose() => SelectionChanged.OnCompleted();
}

public record Date(int Month, int Day) {
    public Date Add(int days) {
        var d = Day + days;
        if (Month == 5 && d > 31)
            return new Date(6, 1).Add(d - 32);
        if (Month == 6 && d > 42)
            return new(7, 1);
        return this with { Day = d };
    }

    /// <summary>
    /// Return -1 if this date is before `other`, 0 if they are the same, or 1 if this date is after `other`.
    /// </summary>
    public int Cmp(Date other) {
        if (this == other) 
            return 0;
        if (this.Month < other.Month || this.Month == other.Month && this.Day < other.Day)
            return -1;
        return 1;
    }

    public override string ToString() => $"{Month}月{Day:00}日";

    public string AsMDDate => $"{Month}/{Day:00}";
    
    public static Date operator+(Date d, int days) => d.Add(days);
}

}