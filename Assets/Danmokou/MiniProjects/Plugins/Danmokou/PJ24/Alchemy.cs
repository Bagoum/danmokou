using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using BagoumLib.Events;
using Danmokou.UI.XML;
using Newtonsoft.Json;

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

public record Date(int Month, int Day) {
    public Date Add(int days) {
        var d = Day + days;
        if (Month == 5 && d > 31)
            return new Date(6, 1).Add(d - 32);
        if (Month == 6 && d > 43)
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

    [JsonIgnore] public string AsMDDate => $"{Month}/{Day:00}";
    
    public static Date operator+(Date d, int days) => d.Add(days);
}

}