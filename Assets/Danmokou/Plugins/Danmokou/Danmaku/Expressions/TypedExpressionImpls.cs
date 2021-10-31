using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Reflection;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using UnityEngine;
using static Danmokou.Expressions.ExUtils;
using static Danmokou.Expressions.ExMHelpers;
using static Danmokou.DMath.Functions.ExM;


namespace Danmokou.Expressions {
public class TExPI : TEx<ParametricInfo> {
    public readonly MemberExpression id;
    public readonly MemberExpression t;
    public readonly MemberExpression loc;
    public readonly MemberExpression locx;
    public readonly MemberExpression locy;
    /// <summary>
    /// While index points to an integer, it can be passed into FXY, as FXY will cast it.
    /// However, it cannot be used for parametric float operations like protate.
    /// </summary>
    public readonly MemberExpression index;
    /// <summary>
    /// A float-cast of the index which can be used for parametric float operations like protate.
    /// </summary>
    public readonly UnaryExpression findex;
    public Ex FiringCtx => ex.Field("ctx");
    private static readonly ExFunction rehash = ExFunction.Wrap<ParametricInfo, int>("Rehash", 0);
    private static readonly ExFunction copyWithT = ExFunction.Wrap<ParametricInfo, float>("CopyWithT", 1);
    private static readonly ExFunction flipSimple =
        ExFunction.Wrap<ParametricInfo>("FlipSimple", new[] {typeof(bool), tfloat});

    public TExPI() : this((string?)null) { }
    public TExPI(string? name) : this(ExMode.Parameter, name) { }

    protected TExPI(ExMode m, string? name) : base(m, name) {
        id = Ex.Field(ex, "id");
        t = Ex.Field(ex, "t");
        loc = Ex.Field(ex, "loc");
        locx = Ex.Field(loc, "x");
        locy = Ex.Field(loc, "y");
        index = Ex.Field(ex, "index");
        findex = Ex.Convert(index, ExUtils.tfloat);
    }
    public TExPI(Expression ex) : base(ex) {
        id = Ex.Field(ex, "id");
        t = Ex.Field(ex, "t");
        loc = Ex.Field(ex, "loc");
        locx = Ex.Field(loc, "x");
        locy = Ex.Field(loc, "y");
        index = Ex.Field(ex, "index");
        findex = Ex.Convert(index, ExUtils.tfloat);
    }

    public TExPI Rehash() => new TExPI(rehash.InstanceOf(this));
    public TExPI CopyWithT(Ex newT) => new TExPI(copyWithT.InstanceOf(this, newT));

    public new static TExPI Box(Ex ex) => new TExPI(ex);

    public Ex FlipSimpleY(Ex wall) => flipSimple.InstanceOf(this, Ex.Constant(true), wall);

    public Ex FlipSimpleX(Ex wall) => flipSimple.InstanceOf(this, Ex.Constant(false), wall);
}

public class TExV2 : TEx<Vector2> {
    public readonly MemberExpression x;
    public readonly MemberExpression y;

    public TExV2() : this(ExMode.Parameter, null) { }

    public TExV2(ExMode m, string? name) : base(m, name) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
    }
    public TExV2(Expression ex) : base(ex) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
    }

    public static TExV2 Variable() {
        return new TExV2();
    }
}

public class TExV3 : TEx<Vector3> {
    public readonly MemberExpression x;
    public readonly MemberExpression y;
    public readonly MemberExpression z;

    public TExV3() : this(ExMode.Parameter, null) { }

    public TExV3(ExMode m, string? name) : base(m, name) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
        z = Ex.Field(ex, "z");
    }
    public TExV3(Expression ex) : base(ex) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
        z = Ex.Field(ex, "z");
    }

    public static TExV3 Variable() {
        return new TExV3();
    }
}

public class TExRV2 : TEx<V2RV2> {
    public readonly MemberExpression nx;
    public readonly MemberExpression ny;
    public readonly MemberExpression rx;
    public readonly MemberExpression ry;
    public readonly MemberExpression angle;

    public TExRV2() : this(ExMode.Parameter, null) { }

    public TExRV2(ExMode m, string? name) : base(m, name) {
        nx = Ex.Field(ex, "nx");
        ny = Ex.Field(ex, "ny");
        rx = Ex.Field(ex, "rx");
        ry = Ex.Field(ex, "ry");
        angle = Ex.Field(ex, "angle");
    }
    public TExRV2(Expression ex) : base(ex) {
        nx = Ex.Field(ex, "nx");
        ny = Ex.Field(ex, "ny");
        rx = Ex.Field(ex, "rx");
        ry = Ex.Field(ex, "ry");
        angle = Ex.Field(ex, "angle");
    }
}
public class TExGCX : TEx<GenCtx> {
    private readonly MemberExpression fs;
    private readonly MemberExpression v2s;
    private readonly MemberExpression v3s;
    private readonly MemberExpression rv2s;
    public readonly MemberExpression index;
    public readonly Expression i_float;
    public readonly Expression pi_float;
    public readonly MemberExpression beh_loc;
    public readonly MemberExpression bpi;
    public TExGCX(Expression ex_) : base(ex_) {
        fs = Ex.Field(ex, "fs");
        v2s = Ex.Field(ex, "v2s");
        v3s = Ex.Field(ex, "v3s");
        rv2s = Ex.Field(ex, "rv2s");
        index = Ex.Field(ex, "index");
        i_float = Ex.Field(ex, "i").As<float>();
        pi_float = Ex.Field(ex, "pi").As<float>();
        beh_loc = Ex.Property(ex, "Loc");
        bpi = Ex.Property(ex, "AsBPI");
    }
    public Ex FindReference<T>(string name) {
        var t = typeof(T);
        if (t == tfloat) {
            if (name == "i") return i_float;
            else if (name == "pi") return pi_float;
            else return fs.DictGetOrThrow(ExC(name), $"No float exists by name {name}.");
        }
        if (t == tv2) return v2s.DictGetOrThrow(ExC(name), $"No v2 exists by name {name}.");
        if (t == tv3) return v3s.DictGetOrThrow(ExC(name), $"No v3 exists by name {name}.");
        if (t == tvrv2) return rv2s.DictGetOrThrow(ExC(name), $"No V2RV2 exists by name {name}.");
        throw new Exception($"No handling in GenCtx for type {Reflector.NameType(t)}");
    }
}
}