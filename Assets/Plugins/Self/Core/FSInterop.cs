using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using UnityEngine;
using UnityEngine.Assertions;
using Common;
using DMath;

public static class FSInterop {
    public static T[] ToNullableArray<T>(IEnumerable<FSharpOption<T>> lt) where T : class => lt.Select(x => x.Nullable()).ToArray();

    public static FSharpMap<T, V> NewMap<T, V>() => new FSharpMap<T, V>(new Tuple<T, V>[] {});

    public static FSharpMap<T, V> AsMap<T, V>(Dictionary<T, V> dict) => new FSharpMap<T, V>(dict.Keys.Select(x => new Tuple<T, V>(x, dict[x])));

    public static Func<T, FSharpOption<V>> Try<T, V>(Func<T, V> maybeException) {
        return t => {
            try {
                return FSharpOption<V>.Some(maybeException(t));
            } catch (Exception) {
                return FSharpOption<V>.None;
            }
        };
    }

    public static FSharpFunc<T, FSharpOption<V>> FTry<T, V>(Func<T, V> maybeException) =>
        FuncConvert.FromFunc(Try(maybeException));

    public static FSharpFunc<T, V> F<T, V>(Func<T, V> func) => FuncConvert.FromFunc(func);

}
static class FSExtensions  {
    public static bool IsSome<T>(this FSharpOption<T> option) => FSharpOption<T>.get_IsSome(option);
    public static bool IsNone<T>(this FSharpOption<T> option) => FSharpOption<T>.get_IsNone(option);
    [CanBeNull]
    public static T Nullable<T>(this FSharpOption<T> maybe) where T : class {
        return maybe.IsSome() ? maybe.Value : null;
    }
}