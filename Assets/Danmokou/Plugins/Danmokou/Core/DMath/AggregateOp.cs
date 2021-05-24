using System;
using BagoumLib.DataStructures;
using Danmokou.Core;

namespace Danmokou.DMath {
public abstract class AggregateOp<X, T> : MultiOp {
    public abstract T Aggregate { get; }
    protected readonly DMCompactingArray<X> values = new DMCompactingArray<X>();

    public DeletionMarker<X> AddValue(X x) {
        return values.Add(x);
    }
}

public class MaxOp<T> : AggregateOp<T, T> {
    public override T Aggregate {
        get {
            var max = deflt;
            for (int ii = 0; ii < values.Count; ++ii) {
                if (!values.Data[ii].MarkedForDeletion && XGtY(values[ii], max))
                    max = values[ii];
            }
            values.Compact();
            return max;
        }
    }

    private readonly T deflt;
    private readonly Func<T, T, bool> XGtY;

    public MaxOp(T deflt, Func<T, T, bool> XGtY) {
        this.deflt = deflt;
        this.XGtY = XGtY;
    }
}
}