using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Events;

namespace Danmokou.UI.XML {

/// <summary>
/// Handling for filtering and sorting model objects.
/// </summary>
public abstract record Lookup<T>(LString Feature) : IModelObject where T : class {
    private Evented<bool> Destroyed { get; } = new(false);
    Evented<bool> IModelObject._destroyed => Destroyed;
    public bool Enabled { get; set; } = true;
    public bool Hidden { get; init; } = false;
    public abstract string Descr { get; }
    public abstract CompiledLookup<T> Compile(CompiledLookup<T> source);

    protected Lookup(Lookup<T> original) {
        //record copy constructor - don't copy Destroyed or Enabled
        Destroyed = new(false);
        Enabled = true;
        Hidden = original.Hidden;
        Feature = original.Feature;
    }

    public record Filter(Func<T, bool> Predicate, LString Feature, string Vals, bool Exclude) : Lookup<T>(Feature) {
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

    public abstract record Sort(LString Feature, bool? Reverse) : Lookup<T>(Feature) {
        public override string Descr => DescrForReverse(Reverse);
        public string DescrForReverse(bool? rev) {
            var arrow = rev is {} r ? (r ? "\u2193" : "\u2191") : "";
            return $"Sort{arrow} by {Feature}";
        }
    }
    public record Sort<U>(Func<T, U> Key, LString Feature, bool? Reverse = null) : Sort(Feature, Reverse), IComparer<T?> {
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
            return Reverse is true ? -result : result;
        }
    }
}

/// <summary>
/// <see cref="Lookup{T}"/> frozen with a specific version of source data.
/// </summary>
public abstract record CompiledLookup<T>() where T : class {
    public abstract int Length { get; }
    public abstract T? this[int index] { get; }

    public static CompiledLookup<T> From(IReadOnlyList<T?> source, IReadOnlyList<Lookup<T>> layers)
        => From(new SourceData(source), layers);
    
    public static CompiledLookup<T> From(SourceData source, IReadOnlyList<Lookup<T>> layers) {
        CompiledLookup<T> lookup = source;
        for (int ii = layers.Count - 1; ii >= 0; --ii)
            if (layers[ii].Enabled)
                lookup = layers[ii].Compile(lookup);
        return lookup;
    }
    
    public record SourceData(IReadOnlyList<T?> Data) : CompiledLookup<T> {
        public override int Length => Data.Count;
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

/// <summary>
/// Helper class for managing a source data list
///  that can be passed through layered <see cref="Lookup{T}"/> filter/sorts.
/// <br/>Note that layers are executed in reverse order.
/// </summary>
public class LookupHelper<T> where T : class {
    public List<Lookup<T>> Layers { get; }
    public CompiledLookup<T> Compiled { get; private set; } = null!;
    private CompiledLookup<T>.SourceData Source { get; set; }
    /// <summary>
    /// Number of items after filtering/sorting. Some of these items may be null.
    /// </summary>
    public int Length => Compiled.Length;
    
    /// <summary>
    /// The item at the ii'th index after filtering/sorting. This may be null.
    /// If provided an index >= <see cref="Length"/>, will return null.
    /// </summary>
    public T? this[int ii] => 
        ii >= Compiled.Length ? null : Compiled[ii];

    public LookupHelper(IReadOnlyList<T?> source, params Lookup<T>[] layers) {
        Layers = layers.ToList();
        Source = new(source);
        Recompile();
    }
    
    public void Recompile() => Compiled = CompiledLookup<T>.From(Source, Layers);

    public IDisposable AddLayer(Lookup<T> layer) {
        if (Layers.Count > 0 && Layers[^1].Hidden) {
            for (int ii = Layers.Count - 1; ii >= 0; --ii) {
                if (ii == 0 || !Layers[ii - 1].Hidden) {
                    Layers.Insert(ii, layer);
                    goto added;
                }
            }
        } else
            Layers.Add(layer);
        added: ;
        var token = layer.WhenDestroyed(() => {
            Layers.Remove(layer);
            Recompile();
        });
        Recompile();
        return token;
    }

    public void SwapIndices(int a, int b) {
        (Layers[a], Layers[b]) = (Layers[b], Layers[a]);
        Recompile();
    }

}

}