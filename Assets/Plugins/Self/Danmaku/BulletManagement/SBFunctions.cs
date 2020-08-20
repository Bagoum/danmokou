using System.Linq.Expressions;
using DMath;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using ExSBF = System.Func<Danmaku.RTExSB, TEx<float>>;
using ExSBV2 = System.Func<Danmaku.RTExSB, TEx<UnityEngine.Vector2>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;

namespace Danmaku {
public interface ITExVelocity {
    MemberExpression angle { get; }
    MemberExpression cos { get; }
    MemberExpression sin { get; }
    MemberExpression root { get; }
    MemberExpression rootX { get; }
    MemberExpression rootY { get; }
    Expression flipX { get; }
    Expression flipY { get; }
}

public class TExVel : TEx<Velocity>, ITExVelocity {
    public MemberExpression angle => Ex.Field(ex, "angle");
    public MemberExpression cos => Ex.Field(ex, "cos_rot");
    public MemberExpression sin => Ex.Field(ex, "sin_rot");
    public MemberExpression root => Ex.Field(ex, "rootPos");
    public MemberExpression rootX => Ex.Field(Ex.Field(ex, "rootPos"), "x");
    public MemberExpression rootY => Ex.Field(Ex.Field(ex, "rootPos"), "y");
    public Expression flipX => Ex.Field(ex, "flipX").As<float>();
    public Expression flipY => Ex.Field(ex, "flipY").As<float>();
    public TExVel(ExMode m) : base(m) { }
    public TExVel(Expression ex) : base(ex) { }
    public Ex FlipX() => _flipX.InstanceOf(this);
    private static readonly ExFunction _flipX = ExUtils.Wrap<Velocity>("FlipX");
    public Ex FlipY() => _flipY.InstanceOf(this);
    private static readonly ExFunction _flipY = ExUtils.Wrap<Velocity>("FlipY");
}

public class RTExVel : TExVel {
    public RTExVel() : base(ExMode.RefParameter) { }
}

public class TExLVel : TEx<LaserVelocity>, ITExVelocity {
    public MemberExpression angle => Ex.Field(ex, "angle");
    public MemberExpression cos => Ex.Field(ex, "cos_rot");
    public MemberExpression sin => Ex.Field(ex, "sin_rot");
    public MemberExpression root => Ex.Field(ex, "rootPos");
    public MemberExpression rootX => Ex.Field(Ex.Field(ex, "rootPos"), "x");
    public MemberExpression rootY => Ex.Field(Ex.Field(ex, "rootPos"), "y");
    public Expression flipX => Ex.Field(ex, "flipX").As<float>();
    public Expression flipY => Ex.Field(ex, "flipY").As<float>();
    public TExLVel(ExMode m) : base(m) { }

    public Ex FlipX() => _flipX.InstanceOf(this);
    private static readonly ExFunction _flipX = ExUtils.Wrap<LaserVelocity>("FlipX");
    public Ex FlipY() => _flipY.InstanceOf(this);
    private static readonly ExFunction _flipY = ExUtils.Wrap<LaserVelocity>("FlipY");
}

public class RTExLVel : TExLVel {
    public RTExLVel() : base(ExMode.RefParameter) { }
}

public class TExSB : TEx<BulletManager.SimpleBullet> {
    public readonly TExPI bpi;
    public readonly TEx<float> scale;
    public readonly TExV2 direction;
    public readonly TExVel velocity;
    public TExV2 accDelta => new TExV2(Ex.Field(ex, "accDelta"));

    public TExSB(Expression ex) : base(ex) {
        bpi = TExPI.Box(Ex.Field(ex, "bpi"));
        scale = Ex.Field(ex, "scale");
        direction = new TExV2(Ex.Field(ex, "direction"));
        velocity = new TExVel(Ex.Field(ex, "velocity"));
    }

    protected TExSB(ExMode m) : base(m) {
        bpi = TExPI.Box(Ex.Field(ex, "bpi"));
        scale = Ex.Field(ex, "scale");
        direction = new TExV2(Ex.Field(ex, "direction"));
        velocity = new TExVel(Ex.Field(ex, "velocity"));
    }
}

public class RTExSB : TExSB {
    public RTExSB() : base(ExMode.RefParameter) { }
    public RTExSB(Expression ex) : base(ex) { }
}

public class TExSBC : TEx<BulletManager.AbsSimpleBulletCollection> {
    private readonly MemberExpression arr;
    public MemberExpression style => Ex.Property(ex, "Style");

    [UsedImplicitly]
    public TExSBC() : this(ExMode.Parameter) { }

    public TExSBC(ExMode m) : base(m) {
        arr = Ex.Field(ex, "arr");
    }

    public RTExSB this[Ex index] => new RTExSB(arr.Index(index));
    private static readonly ExFunction delete = ExUtils.Wrap<BulletManager.AbsSimpleBulletCollection>("Delete",
        new[] {typeof(int), typeof(bool)});
    public Ex Delete(Ex index) => delete.InstanceOf(this, index, Ex.Constant(false));
    public Ex DeleteDestroy(Ex index) => delete.InstanceOf(this, index, Ex.Constant(true));

    private static readonly ExFunction speedup =
        ExUtils.Wrap<BulletManager.AbsSimpleBulletCollection>("Speedup", new[] {typeof(float)});
    public Expression Speedup(Expression ratio) => speedup.InstanceOf(this, ratio);
}
}
namespace DMath {
/// <summary>
/// Functions that get a float value from a simple bullet.
/// </summary>
public static class SBFRepo {
    /// <summary>
    /// Return the time of the bullet.
    /// </summary>
    /// <returns></returns>
    public static ExSBF Time() => sb => sb.bpi.t;

    /// <summary>
    /// Return the time of the bullet.
    /// </summary>
    /// <returns></returns>
    public static ExSBF Scale() => sb => sb.scale;
    /// <summary>
    /// Return the direction, in degrees, of the bullet.
    /// </summary>
    /// <returns></returns>
    public static ExSBF Dir() => sb => ExM.ATan(sb.direction);
    /// <summary>
    /// Return the value of a function executed on the bullet's parametric information.
    /// This function is automatically applied if no other is applicable.
    /// </summary>
    /// <param name="f"></param>
    /// <returns></returns>
    [Fallthrough(50)]
    public static ExSBF BPY(ExBPY f) => sb => f(sb.bpi);
}

/// <summary>
/// Functions that get a vector2 value from a simple bullet.
/// </summary>
public static class SBV2Repo {
    /// <summary>
    /// Return the global location of the bullet.
    /// </summary>
    /// <returns></returns>
    public static ExSBV2 Loc() => sb => sb.bpi.loc;
    /// <summary>
    /// Return the direction of the bullet.
    /// </summary>
    /// <returns></returns>
    public static ExSBV2 Dir() => sb => sb.direction;

    public static ExSBV2 AccDelta() => sb => sb.accDelta;
    /// <summary>
    /// Return the value of a function executed on the bullet's parametric information.
    /// This function is automatically applied if no other is applicable.
    /// </summary>
    /// <param name="f"></param>
    /// <returns></returns>
    [Fallthrough(50)]
    public static ExSBV2 TP(ExTP f) => sb => f(sb.bpi);
}
}