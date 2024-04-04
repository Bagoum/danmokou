using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.Reflection2;
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
    public readonly MemberExpression locV2;
    public readonly MemberExpression locV3;
    public readonly MemberExpression locx;
    public readonly MemberExpression locy;
    public readonly MemberExpression locz;
    /// <summary>
    /// While index points to an integer, it can be passed into FXY, as FXY will cast it.
    /// However, it cannot be used for parametric float operations like protate.
    /// </summary>
    public readonly MemberExpression index;
    /// <summary>
    /// A float-cast of the index which can be used for parametric float operations like protate.
    /// </summary>
    public readonly UnaryExpression findex;
    public Ex FiringCtx => ex.Field(nameof(ParametricInfo.ctx));
    public Ex EnvFrame => FiringCtx.Field(nameof(PIData.envFrame));
    private static readonly ExFunction rehash = ExFunction.Wrap<ParametricInfo, int>("Rehash", 0);
    private static readonly ExFunction copyWithT = ExFunction.Wrap<ParametricInfo, float>("CopyWithT", 1);
    private static readonly ExFunction flipSimple =
        ExFunction.Wrap<ParametricInfo>("FlipSimple", new[] {typeof(bool), tfloat});

    public TExPI() : this((string?)null) { }
    public TExPI(string? name) : this(ExMode.Parameter, name) { }

    protected TExPI(ExMode m, string? name) : base(m, name) {
        id = Ex.Field(ex, "id");
        t = Ex.Field(ex, "t");
        locV2 = Ex.Property(ex, "LocV2");
        locV3 = Ex.Field(ex, "loc");
        locx = Ex.Field(locV3, "x");
        locy = Ex.Field(locV3, "y");
        locz = Ex.Field(locV3, "z");
        index = Ex.Field(ex, "index");
        findex = Ex.Convert(index, ExUtils.tfloat);
    }
    public TExPI(Expression ex) : base(ex) {
        id = Ex.Field(ex, "id");
        t = Ex.Field(ex, "t");
        locV2 = Ex.Property(ex, "LocV2");
        locV3 = Ex.Field(ex, "loc");
        locx = Ex.Field(locV3, "x");
        locy = Ex.Field(locV3, "y");
        locz = Ex.Field(locV3, "z");
        index = Ex.Field(ex, "index");
        findex = Ex.Convert(index, ExUtils.tfloat);
    }

    public TExPI Rehash() => new(rehash.InstanceOf(this));
    public TExPI CopyWithT(Ex newT) => new(copyWithT.InstanceOf(this, newT));

    public new static TExPI Box(Ex ex) => new(ex);

    public Ex FlipSimpleY(Ex wall) => flipSimple.InstanceOf(this, Ex.Constant(true), wall);

    public Ex FlipSimpleX(Ex wall) => flipSimple.InstanceOf(this, Ex.Constant(false), wall);
}

public class TExV2 : TEx<Vector2> {
    public readonly MemberExpression x;
    public readonly MemberExpression y;

    public TExV2(string? name = null) : this(ExMode.Parameter, name) { }

    public TExV2(ExMode m, string? name) : base(m, name) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
    }
    public TExV2(Expression ex) : base(ex) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
    }

    public static TExV2 Variable() {
        return new();
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
        return new();
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
    public MemberExpression bpi => Ex.Property(ex, nameof(GenCtx.AsBPI));
    public MemberExpression exec => Ex.Field(ex, nameof(GenCtx.exec));
    public Expression EnvFrame => ex.Field(nameof(GenCtx.EnvFrame));
    public TExGCX(Expression ex_) : base(ex_) {
    }
}

/// <summary>
/// Dummy class used to compile VTP
/// </summary>
public class VTPExpr { }

/// <summary>
/// Dummy class used to compile LVTP
/// </summary>
public class LVTPExpr { }


}