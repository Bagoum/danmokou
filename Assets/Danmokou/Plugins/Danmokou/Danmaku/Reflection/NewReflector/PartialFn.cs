using System;
using BagoumLib.Reflection;

namespace Danmokou.Reflection2 {
public abstract record PartialFn<R>(object[]? prms = null) {
    public abstract R Execute(object[]? nprms);
    protected abstract int MaxArgs { get; }

    public PartialFn<R> Apply(object[] nprms) => this with {
        prms = ConcatParams(prms, nprms)
    };

    protected object[] ConcatParams(object[]? prms1, object[]? prms2) {
        if (prms1 == null || prms1.Length == 0) return prms2!;
        if (prms2 == null) return prms1;
        if (prms1.Length + prms2.Length > MaxArgs) {
            throw new Exception($"Too many arguments ({prms1.Length+prms2.Length}/{MaxArgs}) " +
                                $"provided to partial function execution");
        }
        var nprms = new object[prms1.Length + prms2.Length];
        for (int ii = 0; ii < prms1.Length; ++ii)
            nprms[ii] = prms1![ii];
        for (int ii = 0; ii < prms2.Length; ++ii)
            nprms[ii] = prms2[prms1.Length + ii];
        return nprms;
    }
    

    public record PartialFn0(Func<R> fn) : PartialFn<R> {
        public override R Execute(object[]? nprms) => fn();
        protected override int MaxArgs => 0;
    }
    public record PartialFn1<T>(Func<T,R> fn) : PartialFn<R> {
        public override R Execute(object[]? nprms) {
            var p = ConcatParams(prms, nprms);
            return fn((T)p[0]);
        }

        protected override int MaxArgs => 1;
    }
    public record PartialFn2<T0,T1>(Func<T0,T1,R> fn) : PartialFn<R> {
        public override R Execute(object[]? nprms) {
            var p = ConcatParams(prms, nprms);
            return fn((T0)p![0], (T1)p![1]);
        }

        protected override int MaxArgs => 2;
    }
    public record PartialFn3<T0,T1,T2>(Func<T0,T1,T2,R> fn) : PartialFn<R> {
        public override R Execute(object[]? nprms) {
            var p = ConcatParams(prms, nprms);
            return fn((T0)p[0], (T1)p[1], (T2)p[2]);
        }

        protected override int MaxArgs => 3;
    }
    public record PartialFn4<T0,T1,T2,T3>(Func<T0,T1,T2,T3,R> fn) : PartialFn<R> {
        public override R Execute(object[]? nprms) {
            var p = ConcatParams(prms, nprms);
            return fn((T0)p[0], (T1)p[1], (T2)p[2], (T3)p[3]);
        }

        protected override int MaxArgs => 4;
    }
    public record PartialFn5<T0,T1,T2,T3,T4>(Func<T0,T1,T2,T3,T4,R> fn) : PartialFn<R> {
        public override R Execute(object[]? nprms) {
            var p = ConcatParams(prms, nprms);
            return fn((T0)p[0], (T1)p[1], (T2)p[2], (T3)p[3], (T4)p[4]);
        }

        protected override int MaxArgs => 5;
    }
    public record PartialFn6<T0,T1,T2,T3,T4,T5>(Func<T0,T1,T2,T3,T4,T5,R> fn) : PartialFn<R> {
        public override R Execute(object[]? nprms) {
            var p = ConcatParams(prms, nprms);
            return fn((T0)p[0], (T1)p[1], (T2)p[2], (T3)p[3], (T4)p[4], (T5)p[5]);
        }

        protected override int MaxArgs => 6;
    }
    public record PartialFn7<T0,T1,T2,T3,T4,T5,T6>(Func<T0,T1,T2,T3,T4,T5,T6,R> fn) : PartialFn<R> {
        public override R Execute(object[]? nprms) {
            var p = ConcatParams(prms, nprms);
            return fn((T0)p[0], (T1)p[1], (T2)p[2], (T3)p[3], (T4)p[4], (T5)p[5], (T6)p[6]);
        }

        protected override int MaxArgs => 7;
    }
    public record PartialFn7<T0,T1,T2,T3,T4,T5,T6,T7>(Func<T0,T1,T2,T3,T4,T5,T6,T7,R> fn) : PartialFn<R> {
        public override R Execute(object[]? nprms) {
            var p = ConcatParams(prms, nprms);
            return fn((T0)p[0], (T1)p[1], (T2)p[2], (T3)p[3], (T4)p[4], (T5)p[5], (T6)p[6], (T7)p[7]);
        }

        protected override int MaxArgs => 8;
    }
}
}