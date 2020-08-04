using System;
using System.Collections.Generic;
using System.Linq;
using DMath;
using UnityEngine;

public static partial class Reflector {
    public enum ExType {
        Float,
        V2,
        V3,
        RV2
    }

    public static ExType AsExType<T>() => AsExType(typeof(T));
    public static ExType AsExType(Type t) {
        if (t == ExUtils.tv2) return ExType.V2;
        if (t == ExUtils.tv3) return ExType.V2;
        if (t == ExUtils.tvrv2) return ExType.RV2;
        return ExType.Float;
    }

    private static Type AsWeakTExType(ExType ext) {
        if (ext == ExType.V2) return typeof(TEx<Vector2>);
        if (ext == ExType.V3) return typeof(TEx<Vector3>);
        if (ext == ExType.RV2) return typeof(TEx<V2RV2>);
        return typeof(TEx<float>);
    }
    private static Type AsTExType(ExType ext) {
        if (ext == ExType.V2) return typeof(TExV2);
        if (ext == ExType.V3) return typeof(TExV3);
        if (ext == ExType.RV2) return typeof(TExRV2);
        return typeof(TEx<float>);
    }
    private enum Reflected {
        SyncPattern,
        AsyncPattern,
        PAsyncPattern,
        TP,
        TP3,
        BPY,
        FXY,
        Pred,
        SBPred,
        Path,
        LaserPath,
        SBCF,
        BehCF,
        LCF,
        SPCF,
        FPCF,
        LPCF,
        FV2,
        FF,
        SBF,
        SBV2,
        BPRV2,
        GCProp
    }
    private static string GetNameForEnum(Reflected rc) {
        if (rc == Reflected.AsyncPattern) {
            return "AsyncPattern";
        } else if (rc == Reflected.SyncPattern) {
            return "SyncPattern";
        } else if (rc == Reflected.PAsyncPattern) {
            return "PremadePattern";
        } else if (rc == Reflected.TP) {
            return "TP";
        } else if (rc == Reflected.TP3) {
            return "TP3";
        } else if (rc == Reflected.Path) {
            return "Path";
        } else if (rc == Reflected.LaserPath) {
            return "Laser Path";
        } else if (rc == Reflected.FXY) {
            return "FXY";
        } else if (rc == Reflected.BPY) {
            return "BPY";
        } else if (rc == Reflected.Pred) {
            return "Predicate";
        } else if (rc == Reflected.SBPred) {
            return "SimpleBullet Predicate";
        } else if (rc == Reflected.SBCF) { 
            return "BulletControl";
        } else if (rc == Reflected.BehCF) {
            return "BEH Control";
        } else if (rc == Reflected.LCF) {
            return "LaserControl";
        } else if (rc == Reflected.SPCF) {
            return "PoolControl";
        } else if (rc == Reflected.FPCF) {
            return "FancyPoolControl";
        } else if (rc == Reflected.LPCF) {
            return "LaserPoolControl";
        } else if (rc == Reflected.FV2) {
            return "FV2";
        } else if (rc == Reflected.FF) {
            return "FF";
        } else if (rc == Reflected.SBF) {
            return "SB>Float Func";
        } else if (rc == Reflected.SBV2) {
            return "SB>V2 Func";
        } else if (rc == Reflected.BPRV2) {
            return "BP>V2RV2 Func";
        } else if (rc == Reflected.GCProp) {
            return "GenCtx Property";
        }
        return "NULL CLASS";
    }
    
    //You can use dashes and capitaliazation to distinguish names in files. They will be removed here.
    //Note that starting dashes are kept, this is for negative numbers.
    private const int A = 'A';
    private const int Z = 'Z';

    private static char Lower(char c) {
        if (c >= 'A' && c <= 'Z') return (char) (c + 32);
        return c;
    }
    private static char[] temp = new char[64];
    private static string Sanitize(string raw_name) {
        //return $"{raw_name[0].ToString().ToLower()}{raw_name.Substring(1).ToLower().Replace("-", "")}";
        int len = raw_name.Length;
        if (len == 0) return raw_name;
        if (len > temp.Length) temp = new char[temp.Length * 2];
        int ti = 0;
        temp[ti++] = Lower(raw_name[0]);
        for (int ii = 1; ii < len; ++ii) {
            if (raw_name[ii] != '-') temp[ti++] = Lower(raw_name[ii]);
        }
        return new string(temp, 0, ti);
    }

    public static string NameType<T>() => NameType(typeof(T));
    public static string NameType(Type t) {
        if (TypeNameMap.TryGetValue(t, out var name)) return name;
        if (t.IsArray) {
            return $"[{NameType(t.GetElementType())}]";
        } if (t.IsConstructedGenericType) {
            return $"{NameType(t.GetGenericTypeDefinition())}<{string.Join(", ", t.GenericTypeArguments.Select(NameType))}>";
        }
        if (t.IsGenericType) {
            var n = t.Name;
            int cutFrom = n.IndexOf('`');
            if (cutFrom > 0) return n.Substring(0, cutFrom);
        }
        return t.Name;
    }

    public static string RName(this Type t) => NameType(t);

    
}