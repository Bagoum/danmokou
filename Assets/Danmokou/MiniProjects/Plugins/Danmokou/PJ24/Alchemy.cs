using System;
using System.Collections.Generic;
using System.Text;

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
    
    public static List<TraitInstance> CombineTraits(this IEnumerable<TraitInstance> source) {
        var outp = new List<TraitInstance>();
        foreach (var trait in source) {
            for (int ii = 0; ii < outp.Count; ++ii) {
                var other = outp[ii];
                //Don't allow nested merges
                if (other.SynthedFrom != null) continue;
                if ((trait.Type.TryMergeWith(other.Type) ?? other.Type.TryMergeWith(trait.Type)) is { } synth) {
                    outp.RemoveAt(ii);
                    outp.Add(new TraitInstance(synth, (other, trait)));
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
                if (outp[ii].GetType() == outp[jj].GetType()) {
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
}