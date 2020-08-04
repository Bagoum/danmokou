using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Danmaku;
using DMath;
using Ex = System.Linq.Expressions.Expression;
using UnityEngine;
using static ExUtils;
using static DMath.ExMHelpers;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using static DMath.ExM;

/// <summary>
/// Base class for TEx{T} used for type constraints.
/// </summary>
public class TEx {
    protected readonly Expression ex;
    public readonly Type type;
    protected TEx(Expression ex) {
        this.ex = ex;
        this.type = ex.Type;
    }
    private static readonly IReadOnlyDictionary<Type, Type> TExBoxMap = new Dictionary<Type, Type> {
        { typeof(Vector2), typeof(TExV2) },
        { typeof(Vector3), typeof(TExV3) },
        { typeof(ParametricInfo), typeof(TExPI) },
        { typeof(float), typeof(TEx<float>) },
        { typeof(V2RV2), typeof(TExRV2) },
    };
    private static readonly Type TypeTExT = typeof(TEx<>);
    public static TEx Box(Expression ex) {
        var ext = ex.Type;
        if (!TExBoxMap.TryGetValue(ext, out var tt)) throw new Exception($"Cannot box expression of type {ext}");
        return Activator.CreateInstance(tt, ex) as TEx;
    }

    public virtual TEx Member(string name) => 
        throw new Exception($"Box {ex} of type {Reflector.NameType(this.GetType())} has no member \"{name}\"");

    protected TEx(ExMode mode, Type t) {
        if (mode == ExMode.RefParameter) {
            t = t.MakeByRefType();
        }
        ex = Ex.Parameter(t);
        this.type = ex.Type;
    }
    public static implicit operator TEx(Expression ex) {
        return new TEx(ex);
    }
    public static implicit operator Expression(TEx me) {
        return me.ex;
    }
    public static implicit operator ParameterExpression(TEx me) {
        return (ParameterExpression)me.ex;
    }
}
/// <summary>
/// A typed expression.
/// This typing is syntactic sugar: any expression, regardless of type, can be cast as eg. TEx{float}.
/// However, constructing a parameter expression via TEx{T} will type the expression appropriately.
/// By default, creates a ParameterExpression.
/// </summary>
/// <typeparam name="T">Type of expression.</typeparam>
public class TEx<T> : TEx {

    public TEx() : this(ExMode.Parameter) {}

    public TEx(Expression ex) : base(ex) { }

    public TEx(ExMode m) : base(m, typeof(T)) {}
    
    public static implicit operator TEx<T>(Expression ex) {
        return new TEx<T>(ex);
    }

    public static implicit operator EEx(TEx<T> tex) => tex.ex;

    public static implicit operator TEx<T>(T obj) => Ex.Constant(obj);

    public Ex GetExprDontUseThisGenerally() {
        return ex;
    }
}
public class RTEx<T> : TEx<T> {
    public RTEx() : base(ExMode.RefParameter) { }
}

namespace DMath {
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
    private static readonly ExFunction rehash = ExUtils.Wrap<ParametricInfo, int>("Rehash", 0);
    private static readonly ExFunction copyWithP = ExUtils.Wrap<ParametricInfo, int>("CopyWithP", 1);
    private static readonly ExFunction copyWithT = ExUtils.Wrap<ParametricInfo, float>("CopyWithT", 1);
    private static readonly ExFunction flipSimple =
        ExUtils.Wrap<ParametricInfo>("FlipSimple", new[] {typeof(bool), tfloat});

    public TExPI() : this(ExMode.Parameter, true) { }

    protected TExPI(ExMode m, bool computeFields) : base(m) {
        if (computeFields) {
            id = Ex.Field(ex, "id");
            t = Ex.Field(ex, "t");
            loc = Ex.Field(ex, "loc");
            locx = Ex.Field(loc, "x");
            locy = Ex.Field(loc, "y");
            index = Ex.Field(ex, "index");
            findex = Ex.Convert(index, ExUtils.tfloat);
        }
    }
    private TExPI(Expression ex) : base(ex) {
        id = Ex.Field(ex, "id");
        t = Ex.Field(ex, "t");
        loc = Ex.Field(ex, "loc");
        locx = Ex.Field(loc, "x");
        locy = Ex.Field(loc, "y");
        index = Ex.Field(ex, "index");
        findex = Ex.Convert(index, ExUtils.tfloat);
    }

    public TExPI Rehash() => new TExPI(rehash.InstanceOf(this));
    public TExPI CopyWithP(Ex newP) => new TExPI(copyWithP.InstanceOf(this, newP));
    public TExPI CopyWithT(Ex newT) => new TExPI(copyWithT.InstanceOf(this, newT));

    public new static TExPI Box(Ex ex) => new TExPI(ex);

    public Ex When(ExPred pred, Ex then) => Ex.IfThen(pred(this), then);

    public Ex FlipSimpleY(Ex wall) => flipSimple.InstanceOf(this, Ex.Constant(true), wall);

    public Ex FlipSimpleX(Ex wall) => flipSimple.InstanceOf(this, Ex.Constant(false), wall);
}

public class RTExPI : TExPI {
    public RTExPI() : base(ExMode.RefParameter, true) { }
}

public class TExV2 : TEx<Vector2> {
    public readonly MemberExpression x;
    public readonly MemberExpression y;

    public TExV2() : this(ExMode.Parameter) { }

    protected TExV2(ExMode m) : base(m) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
    }
    public TExV2(Expression ex) : base(ex) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
    }

    public override TEx Member(string name) {
        if (name == "x") return (TEx<float>) x;
        if (name == "y") return (TEx<float>) y;
        return base.Member(name);
    }

    /// <summary>
    /// Creates a new Vector2 which is the normalized of this.
    /// Zero if zero magnitude.
    /// </summary>
    /// <returns></returns>
    public Ex Normalize() {
        var mag = ExUtils.VFloat();
        return Ex.Block(new[] {mag},
            Ex.Assign(mag, SqrMag(this)),
            Ex.Condition(Ex.GreaterThan(mag, Ex.Constant(M.MAG_ERR)), 
                Ex.Block(
                    Ex.Assign(mag, Ex.Divide(Ex.Constant(1f), Sqrt(mag))),
                    ExUtils.V2(x.Mul(mag), y.Mul(mag))),
                this
            )
        );
    }

    public static TExV2 Variable() {
        return new TExV2(ExMode.Parameter);
    }
}

public class RTExV2 : TExV2 {
    public RTExV2() : base(ExMode.RefParameter) { }
}

public class TExV3 : TEx<Vector3> {
    public readonly MemberExpression x;
    public readonly MemberExpression y;
    public readonly MemberExpression z;

    public TExV3() : this(ExMode.Parameter) { }

    public TExV3(ExMode m) : base(m) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
        z = Ex.Field(ex, "z");
    }
    public TExV3(Expression ex) : base(ex) {
        x = Ex.Field(ex, "x");
        y = Ex.Field(ex, "y");
        z = Ex.Field(ex, "z");
    }

    public override TEx Member(string name) {
        if (name == "x") return new TEx<float>(x);
        if (name == "y") return new TEx<float>(y);
        if (name == "z") return new TEx<float>(z);
        if (name == "xy") return new TExV2(ExUtils.V2(x, y));
        if (name == "yz") return new TExV2(ExUtils.V2(y, z));
        if (name == "xz") return new TExV2(ExUtils.V2(x, z));
        return base.Member(name);
    }

    public static TExV3 Variable() {
        return new TExV3(ExMode.Parameter);
    }
}

public class TExRV2 : TEx<V2RV2> {
    public readonly MemberExpression nx;
    public readonly MemberExpression ny;
    public readonly MemberExpression rx;
    public readonly MemberExpression ry;
    public readonly MemberExpression angle;

    public TExRV2() : this(ExMode.Parameter) { }

    public TExRV2(ExMode m) : base(m) {
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
    
    public override TEx Member(string name) {
        if (name == "nx") return new TEx<float>(nx);
        if (name == "ny") return new TEx<float>(ny);
        if (name == "rx") return new TEx<float>(rx);
        if (name == "ry") return new TEx<float>(ry);
        if (name == "angle") return new TEx<float>(angle);
        if (name == "nv") return new TExV2(ExUtils.V2(nx, ny));
        if (name == "rv") return new TExV2(ExUtils.V2(rx, ry));
        if (name == "rva") return new TExV2(ExUtils.V3(rx, ry, angle));
        return base.Member(name);
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
    public TExGCX() : base() {
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
            if (name == "pi") return pi_float;
            return fs.DictSafeGet<string, float>(ExC(name), $"No float exists by name {name}.");
        }
        if (t == tv2) return v2s.DictSafeGet<string, Vector2>(ExC(name), $"No v2 exists by name {name}.");
        if (t == tv3) return v3s.DictSafeGet<string, Vector3>(ExC(name), $"No v3 exists by name {name}.");
        if (t == tvrv2) return rv2s.DictSafeGet<string, V2RV2>(ExC(name), $"No V2RV2 exists by name {name}.");
        throw new Exception($"No handling in GenCtx for type {Reflector.NameType(t)}");
    }
}
}