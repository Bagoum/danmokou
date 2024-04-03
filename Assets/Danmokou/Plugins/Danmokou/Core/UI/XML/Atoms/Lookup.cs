using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;

namespace Danmokou.UI.XML {

/// <summary>
/// Handling for filtering and sorting model objects.
/// </summary>
public abstract record Lookup<T> : IModelObject where T : class {
    Evented<bool> IModelObject._destroyed { get; } = new(false);
    public bool Enabled { get; set; } = true;
    public bool Hidden { get; init; } = false;
    public abstract string Descr { get; }
    public abstract CompiledLookup<T> Compile(CompiledLookup<T> source);
    
    

    public record Filter(Func<T, bool> Predicate, string Feature, string Vals, bool Exclude) : Lookup<T> {
        public override string Descr {
            get {
                var dir = Exclude ? "Exclude" : "Include";
                return $"{dir} {Feature}: {Vals}";
            }
        }

        public bool Matches(T val) => Predicate(val) != Exclude;

        public override CompiledLookup<T> Compile(CompiledLookup<T> source) =>
            new CompiledLookup<T>.Filter(source, this);
    }

    public abstract record Sort(string Feature, bool? Reverse) : Lookup<T> {
        public override string Descr => DescrForReverse(Reverse);
        public string DescrForReverse(bool? rev) {
            var arrow = rev is {} r ? (r ? "\u2193" : "\u2191") : "";
            return $"Sort{arrow} by {Feature}";
        }
    }
    public record Sort<U>(Func<T, U> Key, string Feature, bool? Reverse = null) : Sort(Feature, Reverse), IComparer<T?> {
        public override CompiledLookup<T> Compile(CompiledLookup<T> source) =>
            new CompiledLookup<T>.Sort<U>(source, this);
        
        public int Compare(T? x, T? y) {
            if (x is null) {
                if (y is null) return 0;
                return 1; //nulls at end
            }
            if (y is null)
                return -1;
            var result = Comparer<U>.Default.Compare(Key(x), Key(y));
            return Reverse is true ? result * -1 : result;
        }
    }
}

/// <summary>
/// <see cref="Lookup{T}"/> frozen with a specific version of source data.
/// </summary>
public abstract record CompiledLookup<T>() where T : class {
    public abstract int Length { get; }
    public abstract T? this[int index] { get; }

    public static CompiledLookup<T> From(T?[] source, IReadOnlyList<Lookup<T>> layers) {
        CompiledLookup<T> lookup = new SourceData(source);
        for (int ii = layers.Count - 1; ii >= 0; --ii)
            if (layers[ii].Enabled)
                lookup = layers[ii].Compile(lookup);
        return lookup;
    }
    
    public record SourceData(T?[] Data) : CompiledLookup<T> {
        public override int Length => Data.Length;
        public override T? this[int index] => Data[index];
    }

    public record Filter(CompiledLookup<T> Source, Lookup<T>.Filter Lookup) : CompiledLookup<T> {
        public override int Length => Source.Length;
        public bool[] Allowed { get; private set; } = Source.Length.Range().Select(i => {
            var val = Source[i];
            return val is not null && Lookup.Matches(val);
        }).ToArray();
        public override T? this[int index] => Allowed[index] ? Source[index] : null;
    }

    public record Sort<U>(CompiledLookup<T> Source, Lookup<T>.Sort<U> Lookup) : CompiledLookup<T> {
        public override int Length => Source.Length;
        //orderby is stable
        public int[] Sorted { get; private set; } = Source.Length.Range().OrderBy(i => Source[i], Lookup).ToArray();
        public override T? this[int index] => Source[Sorted[index]];
    }
}

}