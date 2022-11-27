using System;
using System.Linq;
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

    private static char Lower(char c) {
        if (c >= 'A' && c <= 'Z') return (char) (c + 32);
        return c;
    }

    private static char[] temp = new char[256];

    private static string Sanitize(string raw_name) {
        //return $"{raw_name[0].ToString().ToLower()}{raw_name.Substring(1).ToLower().Replace("-", "")}";
        int len = raw_name.Length;
        if (len == 0) return raw_name;
        while (len > temp.Length) 
            temp = new char[temp.Length * 2];
        int ti = 0;
        temp[ti++] = Lower(raw_name[0]);
        bool requiresChange = temp[0] != raw_name[0];
        for (int ii = 1; ii < len; ++ii) {
            if (raw_name[ii] == '-') {
                requiresChange = true;
            } else {
                temp[ti++] = Lower(raw_name[ii]);
                requiresChange |= temp[ti - 1] != raw_name[ii];
            }
        }
        Profiler.BeginSample("Sanitize");
        var output = requiresChange ? 
            temp.MakeString(0, ti)
            : raw_name;
        Profiler.EndSample();
        return output;
    }

}
}