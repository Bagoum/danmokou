using System;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection2;

namespace Danmokou.DMath.Functions {
[Reflect]
public static class ExMTesting {
    public static TEx<T> TestLookup2<T>(Func<TEx<T>, TEx<T>, TEx<T>> generic, TEx<T> subject1, TEx<T> subject2) 
        => generic(subject1, subject2);
    public static TEx<T> TestLookup1<T>(TEx<Func<T, T>> generic, TEx<T> subject1) 
        =>  PartialFn.Execute(generic, subject1);
    public static TEx<T> AddTest<T>(TEx<T> x, TEx<T> y) => x.Add(y);
    public static TEx<T> MulTest<T>(TEx<T> x, TEx<T> y) => x.Mul(y);
}
}