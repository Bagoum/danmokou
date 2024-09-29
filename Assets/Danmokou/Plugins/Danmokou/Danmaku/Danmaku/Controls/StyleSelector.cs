using System;
using System.Collections.Generic;
using BagoumLib.DataStructures;
using Danmokou.Core;

namespace Danmokou.Danmaku {
public class StyleSelector {
    private const char wildcard = '*';
    public readonly List<string> enumerated;
    public readonly bool exclude;

    public StyleSelector(string[][] selections, bool exclude) {
        this.enumerated = Resolve(selections);
        this.exclude = exclude;
    }

    public StyleSelector(List<string> enumerated, bool exclude) {
        this.enumerated = enumerated;
        this.exclude = exclude;
    }

    public StyleSelector(string one, bool exclude) : this(new[] {new[] {one}}, exclude) { }
    
    public static implicit operator StyleSelector(string s) => new(s, false);
    
    public static bool RegexMatches(string rgx, string style) {
        var thompson1 = ListCache<int>.Get();
        thompson1.Add(0);
        var thompson2 = ListCache<int>.Get();
        for (int si = 0; si < style.Length; ++si) {
            for (int thi = 0; thi < thompson1.Count; ++thi) {
                var ri = thompson1[thi];
                if (ri >= rgx.Length) continue;
                if (rgx[ri] == '*') {
                    //can repeat or consume
                    thompson2.Add(ri);
                    thompson2.Add(ri + 1);
                } else if (style[si] == rgx[ri]) {
                    //can consume
                    thompson2.Add(ri + 1);
                }
            }
            thompson1.Clear();
            (thompson1, thompson2) = (thompson2, thompson1);
            if (thompson1.Count == 0) break;
        }
        var success = thompson1.Contains(rgx.Length);
        ListCache<int>.Consign(thompson1);
        ListCache<int>.Consign(thompson2);
        return success;
    }

    public void TryMakeSimpleCopies() {
        for (int ii = 0; ii < enumerated.Count; ++ii) {
            var typ = enumerated[ii];
            if (typ.IndexOf('*') == -1)
                BulletManager.NullableGetMaybeCopyPool(typ);
        }
    }
    public void TryMakeComplexCopies() {
        for (int ii = 0; ii < enumerated.Count; ++ii) {
            var typ = enumerated[ii];
            if (typ.IndexOf('*') == -1)
                BulletManager.CheckComplexPool(typ, out _);
        }
    }

    public bool Matches(string style) {
        if (style is null)
            throw new Exception($"Expected non-null style to {nameof(StyleSelector)}.{nameof(Matches)} call");
        for (int ii = 0; ii < enumerated.Count; ++ii) {
            if (RegexMatches(enumerated[ii], style))
                return !exclude;
        }
        return exclude;
    }

    //each string[] is a list of `repeatcolorp`-type styles. 
    //we enumerate the entire selection by enumerating the cartesian product of selections,
    //then merging* the cartesian product, then enumerating from StylesFromKey if there are wildcards.
    //*Merging occurs by folding the cartesian product against the empty string with the following rules:
    // acc, x =>
    //     let ii = acc.indexOf('*')
    //     if (ii == -1) return x;
    //     return $"{acc.Substring(0, ii)}{x}{acc.Substring(ii+1)}";
    // ie. the first * is replaced with the next string.
    // This allows composing in any order. eg:
    // [ circle-*, ellipse-* ] [ */w, */b ] [ red, green ]
    private static string ComputeMerge(string acc, string newStyle) {
        for (int ii = 0; ii < acc.Length; ++ii) {
            if (acc[ii] == wildcard) {
                //optimization for common edge cases
                if (ii == 0) {
                    return acc.Length == 1 ? newStyle : $"{newStyle}{acc.Substring(1)}";
                } else if (ii + 1 == acc.Length) {
                    return $"{acc.Substring(0, ii)}{newStyle}";
                } else
                    return $"{acc.Substring(0, ii)}{newStyle}{acc.Substring(ii + 1)}";
            }
        }
        /*
        for (int ii = 0; ii < newStyle.Length; ++ii) {
            if (newStyle[ii] == wildcard) {
                //optimization for common edge cases
                if (ii == 0) return $"{acc}{newStyle.Substring(1)}";
                if (ii + 1 == newStyle.Length) return $"{newStyle.Substring(0, ii)}{acc}";
                return $"{newStyle.Substring(0, ii)}{acc}{newStyle.Substring(ii + 1)}";
            }
        }*/
        return newStyle;
    }
    public static string MergeStyles(string acc, string newStyle) {
        if (string.IsNullOrEmpty(acc) || acc == "_") return newStyle;
        if (string.IsNullOrEmpty(newStyle) || newStyle == "_") return acc;
        //This may look stupid, but merges in loops (eg. repeatcolorp) are extremely costly, and caching is way cheaper.
        if (!cachedMerges.TryGetValue(acc, out var againstDict)) {
            againstDict = cachedMerges[acc] = new Dictionary<string, string>();
        }
        if (!againstDict.TryGetValue(newStyle, out var merged)) {
            merged = againstDict[newStyle] = ComputeMerge(acc, newStyle);
        }
        return merged;
    }
    private static readonly Dictionary<string, Dictionary<string, string>> cachedMerges = new();

    private static List<string> Resolve(IReadOnlyList<string[]> selections) {
        Stack<int> indices = new();
        Stack<string> partials = new();
        List<string> done = new();
        if (selections.Count == 0 || selections.Count == 1 && selections[0].Length == 0)
            return done;
        partials.Push("");
        int iselection = 0;
        int ichoice = 0;
        while (true) {
            string merged = MergeStyles(partials.Peek(), selections[iselection][ichoice]);
            if (++iselection == selections.Count) {
                done.Add(merged);
                --iselection;
            } else {
                partials.Push(merged);
                indices.Push(ichoice);
                ichoice = -1;
            }
            while (++ichoice == selections[iselection].Length) {
                if (iselection == 0) goto Done;
                --iselection;
                partials.Pop();
                ichoice = indices.Pop();
            }
        }
        Done:
        return done;
    }
}

}