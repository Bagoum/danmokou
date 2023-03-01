using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using UnityEngine;
using UnityEngine.Profiling;

namespace Danmokou.Reflection {
public static partial class Reflector {
    public enum ExType {
        Float,
        V2,
        V3,
        RV2
    }
/*
    public static FiringCtx.DataType AsFCtxType(this ExType t) => t switch {
        ExType.RV2 => FiringCtx.DataType.RV2,
        ExType.V3 => FiringCtx.DataType.V3,
        ExType.V2 => FiringCtx.DataType.V2,
        _ => FiringCtx.DataType.Float
    };*/
    public static ExType AsExType<T>() => AsExType(typeof(T));

    public static ExType AsExType(Type t) {
        if (t == ExUtils.tv2) return ExType.V2;
        if (t == ExUtils.tv3) return ExType.V3;
        if (t == ExUtils.tv2rv2) return ExType.RV2;
        return ExType.Float;
    }

    private static Type AsWeakTExType(ExType ext) {
        if (ext == ExType.V2) return typeof(TEx<Vector2>);
        if (ext == ExType.V3) return typeof(TEx<Vector3>);
        if (ext == ExType.RV2) return typeof(TEx<V2RV2>);
        return typeof(TEx<float>);
    }

    public static Type AsType(this ExType ext) {
        if (ext == ExType.V2) return ExUtils.tv2;
        if (ext == ExType.V3) return ExUtils.tv3;
        if (ext == ExType.RV2) return ExUtils.tv2rv2;
        return ExUtils.tfloat;
    }

    private static Type AsTExType(ExType ext) {
        if (ext == ExType.V2) return typeof(TExV2);
        if (ext == ExType.V3) return typeof(TExV3);
        if (ext == ExType.RV2) return typeof(TExRV2);
        return typeof(TEx<float>);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char Lower(char c) {
        if (c >= 'A' && c <= 'Z') return (char) (c + 32);
        return c;
    }

    /// <summary>
    /// String comparer that is case-insensitive and ignores '-' when preceded by a letter.
    /// </summary>
    public class SanitizedStringComparer : IEqualityComparer<string> {
        public static IEqualityComparer<string> Singleton { get; } = new SanitizedStringComparer();
        public bool Equals(string x, string y) {
            var xi = 0;
            var yi = 0;
            var ignoreNextXDash = false;
            var ignoreNextYDash = false;
            while (xi < x.Length && yi < y.Length) {
                var xc = x[xi];
                while (xc == '-' && ignoreNextXDash) {
                    xc = x[++xi];
                }
                var yc = y[yi];
                while (yc == '-' && ignoreNextYDash) {
                    yc = y[++yi];
                }
                if (xc >= 'A' && xc <= 'Z') xc = (char)(xc + 32);
                if (yc >= 'A' && yc <= 'Z') yc = (char)(yc + 32);
                if (xc != yc)
                    return false;
                ignoreNextXDash = (xc >= 'a' && xc <= 'z');
                ignoreNextYDash = (yc >= 'a' && yc <= 'z');
                ++xi;
                ++yi;
            }
            while (xi < x.Length && x[xi] == '-' && ignoreNextXDash)
                ++xi;
            while (yi < y.Length && y[yi] == '-' && ignoreNextYDash)
                ++yi;
            return xi == x.Length && yi == y.Length;
        }

        public int GetHashCode(string s) {
            int num1 = 352654597;
            int num2 = num1;
            var l = s.Length;
            var ct = 0;
            var ignoreNextDash = false;
            for (int i = 0; i < l; ++i) {
                var c = s[i];
                if (c == '-' && ignoreNextDash)
                    continue;
                if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                if (ct % 2 == 0)
                    num1 = (num1 << 5) + num1 + (num1 >> 27) ^ c;
                else
                    num2 = (num2 << 5) + num2 + (num2 >> 27) ^ c;
                ignoreNextDash = (c >= 'a' && c <= 'z');
                ++ct;
            }
            return num1 + num2 * 1566083941;
        }
    }

}
}