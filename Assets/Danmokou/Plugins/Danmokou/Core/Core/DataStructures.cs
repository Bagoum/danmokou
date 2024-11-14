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

}
