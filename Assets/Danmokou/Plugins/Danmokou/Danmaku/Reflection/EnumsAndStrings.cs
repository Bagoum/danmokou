using System;
using System.Linq;
using Danmokou.DMath;
using Danmokou.Expressions;
using UnityEngine;

namespace Danmokou.Reflection {
public static partial class Reflector {
    public enum ExType {
        Float,
        V2,
        V3,
        RV2
    }

    public static FiringCtx.DataType AsFCtxType(this ExType t) => t switch {
        ExType.RV2 => FiringCtx.DataType.RV2,
        ExType.V3 => FiringCtx.DataType.V3,
        ExType.V2 => FiringCtx.DataType.V2,
        _ => FiringCtx.DataType.Float
    };
    public static ExType AsExType<T>() => AsExType(typeof(T));

    public static ExType AsExType(Type t) {
        if (t == ExUtils.tv2) return ExType.V2;
        if (t == ExUtils.tv3) return ExType.V3;
        if (t == ExUtils.tvrv2) return ExType.RV2;
        return ExType.Float;
    }

    private static Type AsWeakTExType(ExType ext) {
        if (ext == ExType.V2) return typeof(TEx<Vector2>);
        if (ext == ExType.V3) return typeof(TEx<Vector3>);
        if (ext == ExType.RV2) return typeof(TEx<V2RV2>);
        return typeof(TEx<float>);
    }

    private static Type AsType(ExType ext) {
        if (ext == ExType.V2) return typeof(Vector2);
        if (ext == ExType.V3) return typeof(Vector3);
        if (ext == ExType.RV2) return typeof(V2RV2);
        return typeof(float);
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
        for (int ii = 1; ii < len; ++ii) {
            if (raw_name[ii] != '-') temp[ti++] = Lower(raw_name[ii]);
        }
        return new string(temp, 0, ti);
    }

}
}