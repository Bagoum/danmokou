using System;
using System.Collections.Generic;
using System.Linq;
using LanguageExt;

namespace ParserCS {
public static class Helpers {
    /// <summary>
    /// Skip the first SKIP elements of the enumerable.
    /// If there are fewer than SKIP elements, then return an empty enumerable instead of throwing.
    /// </summary>
    public static IEnumerable<T> SoftSkip<T>(this IEnumerable<T> arr, int skip) {
        foreach (var x in arr) {
            if (skip-- <= 0) yield return x;
        }
    }

    public static IEnumerable<T> FilterNone<T>(this IEnumerable<T?> arr) where T : class {
        foreach (var x in arr) {
            if (x != null) yield return x;
        }
    }
    public static IEnumerable<T> FilterNone<T>(this IEnumerable<T?> arr) where T : struct {
        foreach (var x in arr) {
            if (x.HasValue) yield return x.Value;
        }
    }

    public static IEnumerable<T> Join<T>(this IEnumerable<IEnumerable<T>> arrs) {
        foreach (var arr in arrs) {
            foreach (var x in arr) {
                yield return x;
            }
        }
    }

    public static Dictionary<K, V> ToDict<K, V>(this IEnumerable<(K, V)> arr) {
        var dict = new Dictionary<K, V>();
        foreach (var (k, v) in arr) dict[k] = v;
        return dict;
    }
    
    public readonly struct Errorable<T> {
        public readonly string[] errors;
        public string JoinedErrors => string.Join("\n", errors);
        public readonly bool isValid;
        public readonly T value;
        public T GetOrThrow => isValid ? value : throw new Exception(JoinedErrors);
        private Errorable(string[]? errors, T value) {
            this.isValid = errors == null;
            this.errors = errors ?? noStrs;
            this.value = value;
        }
        public static Errorable<T> Fail(string[] errs) => new Errorable<T>(errs, default!);
        public static Errorable<T> Fail(string err) => new Errorable<T>(new[] {err}, default!);
        public static Errorable<T> OK(T value) => new Errorable<T>(null, value);

        public static implicit operator Errorable<T>(T value) => OK(value);

        public Errorable<U> Map<U>(Func<T, U> f) => isValid ? Errorable<U>.OK(f(value)) : Errorable<U>.Fail(errors);
        public Errorable<U> Bind<U>(Func<T, Errorable<U>> f) => isValid ? f(value) : Errorable<U>.Fail(errors);
    }

    public static readonly string[] noStrs = { };
    public static Errorable<List<T>> Acc<T>(IEnumerable<Errorable<T>> errbs) {
        var ret = new List<T>();
        var errs = new List<string[]>();
        foreach (var x in errbs) {
            if (errs.Count == 0 && x.isValid) ret.Add(x.value);
            else if (x.errors.Length > 0) errs.Add(x.errors);
        }
        return errs.Count > 0 ? 
            Errorable<List<T>>.Fail(errs.Join().ToArray()) : 
            ret;
    }

    public static Errorable<List<T>> ReplaceEntries<T>(bool allowFewer, List<T> replaceIn, List<T> replaceFrom, Func<T, bool> replaceFilter) {
        replaceIn = replaceIn.ToList(); //nondestructive
        int jj = 0;
        for (int ii = 0; ii < replaceIn.Count; ++ii) {
            if (replaceFilter(replaceIn[ii])) {
                if (jj < replaceFrom.Count) {
                    replaceIn[ii] = replaceFrom[jj++];
                } else {
                    if (!allowFewer) return Errorable<List<T>>.Fail("Not enough replacements provided");
                }
            }
        }
        if (jj < replaceFrom.Count) return Errorable<List<T>>.Fail("Too many replacements provided");
        return Errorable<List<T>>.OK(replaceIn);
    }

    public static IEnumerable<T> SeparateBy<T>(this IEnumerable<IEnumerable<T>> arrs, T sep) {
        bool first = true;
        foreach (var arr in arrs) {
            if (!first) yield return sep;
            first = false;
            foreach (var x in arr) yield return x;
        }
    }

    public static int MaxConsecutive<T>(T obj, IList<T> arr) where T:IEquatable<T> {
        int max = 0;
        int curr = 0;
        for (int ii = 0; ii < arr.Count; ++ii) {
            if (arr[ii].Equals(obj)) {
                if (++curr > max) max = curr;
            } else {
                curr = 0;
            }
        }
        return max;
    }
}
}