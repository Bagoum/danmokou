using System;

namespace Danmokou.Core {
public readonly struct DU<T0, T1> {
    private readonly T0 obj0;
    private readonly T1 obj1;
    private readonly bool which;

    public DU(T0 o1) {
        obj0 = o1;
        obj1 = default!;
        which = true;
    }

    public DU(T1 o1) {
        obj0 = default!;
        obj1 = o1;
        which = false;
    }

    public T Resolve<T>(Func<T0, T> f0, Func<T1, T> f1) => which ? f0(obj0) : f1(obj1);
}

public readonly struct DU<T0, T1, T2> {
    private readonly T0 obj0;
    private readonly T1 obj1;
    private readonly T2 obj2;
    private readonly short which;

    public (T0, T1, T2, short) Tuple => (obj0, obj1, obj2, which);

    public DU(T0 o0, T1 o1, T2 o2, short which) {
        obj0 = o0;
        obj1 = o1;
        obj2 = o2;
        this.which = which;
    }

    public DU(T0 o1) {
        obj0 = o1;
        obj1 = default!;
        obj2 = default!;
        which = 0;
    }

    public DU(T1 o1) {
        obj0 = default!;
        obj1 = o1;
        obj2 = default!;
        which = 1;
    }

    public DU(T2 o1) {
        obj0 = default!;
        obj1 = default!;
        obj2 = o1;
        which = 2;
    }

    public T Resolve<T>(Func<T0, T> f0, Func<T1, T> f1, Func<T2, T> f2) {
        if (which == 0) return f0(obj0);
        else if (which == 1) return f1(obj1);
        else return f2(obj2);
    }
    public void Resolve(Action<T0> f0, Action<T1> f1, Action<T2> f2) {
        if (which == 0) f0(obj0);
        else if (which == 1) f1(obj1);
        else f2(obj2);
    }

    public static DU<U0, U1, U2>? FromNullable<U0, U1, U2>(U0? o1, U1? o2, U2? o3)
        where U0 : struct where U1 : struct where U2 : struct {
        if (o1.HasValue) return new DU<U0, U1, U2>(o1.Value);
        else if (o2.HasValue) return new DU<U0, U1, U2>(o2.Value);
        else if (o3.HasValue) return new DU<U0, U1, U2>(o3.Value);
        return null;
    }
}

public readonly struct DU<T0, T1, T2, T3> {
    private readonly T0 obj0;
    private readonly T1 obj1;
    private readonly T2 obj2;
    private readonly T3 obj3;
    private readonly short which;

    public (T0, T1, T2, T3, short) Tuple => (obj0, obj1, obj2, obj3, which);

    public DU(short which, T0 o0 = default, T1 o1 = default, T2 o2 = default, T3 o3 = default) {
        this.which = which;
        obj0 = o0;
        obj1 = o1;
        obj2 = o2;
        obj3 = o3;
    }

    public DU(T0 ob) : this(0, o0: ob) { }
    public DU(T1 ob) : this(1, o1: ob) { }
    public DU(T2 ob) : this(2, o2: ob) { }
    public DU(T3 ob) : this(3, o3: ob) { }


    public T Resolve<T>(Func<T0, T> f0, Func<T1, T> f1, Func<T2, T> f2, Func<T3, T> f3) =>
        which switch {
            0 => f0(obj0),
            1 => f1(obj1),
            2 => f2(obj2),
            _ => f3(obj3)
        };

    public static DU<U0, U1, U2, U3>? FromNullable<U0, U1, U2, U3>(U0? o0, U1? o1, U2? o2, U3? o3)
        where U0 : struct where U1 : struct where U2 : struct where U3 : struct {
        if (o0.HasValue) return new DU<U0, U1, U2, U3>(o0.Value);
        else if (o1.HasValue) return new DU<U0, U1, U2, U3>(o1.Value);
        else if (o2.HasValue) return new DU<U0, U1, U2, U3>(o2.Value);
        else if (o3.HasValue) return new DU<U0, U1, U2, U3>(o3.Value);
        return null;
    }
}

public static class DUHelpers {
    public static bool Tuple4Eq<T0, T1, T2, T3>(this (T0, T1, T2, T3, short which) tup1,
        (T0, T1, T2, T3, short which) tup2) {
        if (tup1.which == tup2.which) {
            return tup1.which switch {
                0 => tup1.Item1!.Equals(tup2.Item1),
                1 => tup1.Item2!.Equals(tup2.Item2),
                2 => tup1.Item3!.Equals(tup2.Item3),
                _ => tup1.Item4!.Equals(tup2.Item4)
            };
        } else return false;
    }
}
}