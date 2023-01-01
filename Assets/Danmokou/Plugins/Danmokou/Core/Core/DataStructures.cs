using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;


namespace Danmokou.Core {
public static class BitCompression {
    public static byte FromBools(bool b0, bool b1 = false, bool b2 = false, bool b3 = false, bool b4 = false,
        bool b5 = false, bool b6 = false, bool b7 = false) {
        byte data = 0;
        if (b0) data |= 1;
        if (b1) data |= 1 << 1;
        if (b2) data |= 1 << 2;
        if (b3) data |= 1 << 3;
        if (b4) data |= 1 << 4;
        if (b5) data |= 1 << 5;
        if (b6) data |= 1 << 6;
        if (b7) data |= 1 << 7;
        return data;
    }

    public static bool NthBool(this byte b, int n) => (b & (1 << n)) > 0;
}

public readonly struct FreezableArray<T> {
    public readonly T[] Data;
    public FreezableArray(T[] data) {
        this.Data = data;
    }

    //Call this when using as a persistent key so elements don't get modified later
    public FreezableArray<T> Freeze() => 
        new(Data.ToArray());

    public override bool Equals(object obj) =>
        obj is FreezableArray<T> td && Data.AreSame(td.Data);

    public override int GetHashCode() => Data.ElementWiseHashCode();

    public static readonly FreezableArray<T> Empty = new(Array.Empty<T>());
}

public class N2Triangle<T> {
    private readonly T[] arr;
    public readonly int rows;

    public N2Triangle(T[][] data) {
        rows = data.Length;
        arr = new T[rows * rows];
        int ii = 0;
        for (int ri = 0; ri < rows; ++ri) {
            if (data[ri].Length != 2 * ri + 1)
                throw new ArgumentException($"Row {ri} of an N2Triangle should have {2 * ri + 1} elements");
            for (int rj = 0; rj < data[ri].Length; ++rj) {
                arr[ii++] = data[ri][rj];
            }
        }
    }

    public readonly struct Row {
        private readonly N2Triangle<T> parent;
        public readonly int index;

        public Row(N2Triangle<T> n2, int ind) {
            parent = n2;
            index = ind;
        }

        public T this[int j] => Math.Abs(j) <= index ?
            parent.GetRawIndex(index * index + index + j) :
            throw new IndexOutOfRangeException($"Cannot index row {index} of an N2Triangle with column {j}");
    }

    private T GetRawIndex(int i) => arr[i];

    public Row this[int i] => new Row(this, i);

}

public static class DictCache<K, V> {
    private static readonly Stack<Dictionary<K, V>> cached = new();

    public static void Consign(Dictionary<K, V> cacheMe) {
        cacheMe.Clear();
        cached.Push(cacheMe);
    }

    public static Dictionary<K, V> Get() => cached.Count > 0 ? cached.Pop() : new Dictionary<K, V>();
}

public static class ListCache<T> {
    private static readonly Stack<List<T>> cached = new();

    public static void Consign(List<T> cacheMe) {
        cacheMe.Clear();
        cached.Push(cacheMe);
    }

    public static List<T> Get() => cached.Count > 0 ? cached.Pop() : new List<T>();
}

/// <summary>
/// Cache for fixed-size arrays.
/// </summary>
/// <typeparam name="T"></typeparam>
public static class ArrayCache<T> {
    private static readonly Dictionary<int, Stack<T[]>> cached = new();
    
    public static void Consign(T[] cacheMe) {
        Array.Clear(cacheMe, 0, cacheMe.Length);
        if (!cached.TryGetValue(cacheMe.Length, out var l))
            l = cached[cacheMe.Length] = new();
        l.Push(cacheMe);
    }

    public static T[] Get(int size) =>
        cached.TryGetValue(size, out var l) ?
            l.Count > 0 ?
                l.Pop() :
                new T[size] :
            new T[size];
}

}
