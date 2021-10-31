using System.Linq.Expressions;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.SM;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using static Danmokou.Danmaku.BulletManager;

namespace Danmokou.Expressions {
public interface ITexMovement {
    MemberExpression angle { get; }
    MemberExpression cos { get; }
    MemberExpression sin { get; }
    MemberExpression root { get; }
    MemberExpression rootX { get; }
    MemberExpression rootY { get; }
    Expression flipX { get; }
    Expression flipY { get; }
}

public class TExMov : TEx<Movement>, ITexMovement {
    public MemberExpression angle => Ex.Field(ex, "angle");
    public MemberExpression cos => Ex.Field(ex, "cos_rot");
    public MemberExpression sin => Ex.Field(ex, "sin_rot");
    public MemberExpression root => Ex.Field(ex, "rootPos");
    public MemberExpression rootX => Ex.Field(Ex.Field(ex, "rootPos"), "x");
    public MemberExpression rootY => Ex.Field(Ex.Field(ex, "rootPos"), "y");
    public Expression flipX => Ex.Field(ex, "flipX");
    public Expression flipY => Ex.Field(ex, "flipY");
    public TExMov(ExMode m, string? name) : base(m, name) { }
    public TExMov(Expression ex) : base(ex) { }
    public Ex FlipX() => _flipX.InstanceOf(this);
    private static readonly ExFunction _flipX = ExFunction.Wrap<Movement>("FlipX");
    public Ex FlipY() => _flipY.InstanceOf(this);
    private static readonly ExFunction _flipY = ExFunction.Wrap<Movement>("FlipY");
}

public class TExLMov : TEx<LaserMovement>, ITexMovement {
    public MemberExpression angle => Ex.Field(ex, "angle");
    public MemberExpression cos => Ex.Field(ex, "cos_rot");
    public MemberExpression sin => Ex.Field(ex, "sin_rot");
    public MemberExpression root => Ex.Field(ex, "rootPos");
    public MemberExpression rootX => Ex.Field(Ex.Field(ex, "rootPos"), "x");
    public MemberExpression rootY => Ex.Field(Ex.Field(ex, "rootPos"), "y");
    public Expression flipX => Ex.Field(ex, "flipX");
    public Expression flipY => Ex.Field(ex, "flipY");
    public TExLMov(ExMode m, string? name) : base(m, name) { }
    public TExLMov(Expression ex) : base(ex) { }

    public Ex FlipX() => _flipX.InstanceOf(this);
    private static readonly ExFunction _flipX = ExFunction.Wrap<LaserMovement>("FlipX");
    public Ex FlipY() => _flipY.InstanceOf(this);
    private static readonly ExFunction _flipY = ExFunction.Wrap<LaserMovement>("FlipY");
}

public class TExSB : TEx<SimpleBullet> {
    public readonly TExPI bpi;
    public readonly TEx<float> scale;
    public readonly TExV2 direction;
    public readonly TExMov velocity;
    public TExV2 accDelta => new TExV2(Ex.Field(ex, "accDelta"));

    public TExSB(Expression ex) : base(ex) {
        bpi = TExPI.Box(Ex.Field(ex, "bpi"));
        scale = Ex.Field(ex, "scale");
        direction = new TExV2(Ex.Field(ex, "direction"));
        velocity = new TExMov(Ex.Field(ex, "movement"));
    }

    protected TExSB(ExMode m, string? name) : base(m, name) {
        bpi = TExPI.Box(Ex.Field(ex, "bpi"));
        scale = Ex.Field(ex, "scale");
        direction = new TExV2(Ex.Field(ex, "direction"));
        velocity = new TExMov(Ex.Field(ex, "movement"));
    }
}

public class TExSBC : TEx<AbsSimpleBulletCollection> {
    public MemberExpression style => Ex.Property(ex, "Style");
    public MemberExpression data => Ex.Property(ex, "Data");
    private static readonly ExFunction indexer = ExFunction.Wrap<CompactingArray<SimpleBullet>>("ItemAt",
        new[] {typeof(int)});

    public TExSBC(string name) : base(ExMode.Parameter, name) { }
    public TExSBC(Ex _ex) : base(_ex) {}

    public TExSB this[Ex index] => new TExSB(data.Index(index));
    private static readonly ExFunction delete = ExFunction.Wrap<AbsSimpleBulletCollection>("DeleteSB",
        new[] {typeof(int)});
    private static readonly ExFunction softcull = ExFunction.Wrap<AbsSimpleBulletCollection>("Softcull",
        new[] {typeof(AbsSimpleBulletCollection), typeof(int), typeof(SoftcullProperties?)});
    private static readonly ExFunction isAlive =
        ExFunction.Wrap<AbsSimpleBulletCollection>("IsAlive", typeof(int));
    public static readonly ExFunction transferFrom = ExFunction.Wrap<AbsSimpleBulletCollection>("TransferFrom", 
        new[] {typeof(AbsSimpleBulletCollection), typeof(int)});
    public static readonly ExFunction copyNullFrom = ExFunction.Wrap<AbsSimpleBulletCollection>("CopyNullFrom", 
        new[] {typeof(AbsSimpleBulletCollection), typeof(int), typeof(SoftcullProperties?)});
    public static readonly ExFunction copyFrom = ExFunction.Wrap<AbsSimpleBulletCollection>("CopyFrom", 
        new[] {typeof(AbsSimpleBulletCollection), typeof(int)});
    public static readonly ExFunction runINodeAt = ExFunction.Wrap<AbsSimpleBulletCollection>("RunINodeAt", 
        new[] {typeof(int), typeof(StateMachine), typeof(ICancellee)});
    public static readonly ExFunction makeCulledCopy = ExFunction.Wrap<AbsSimpleBulletCollection>("MakeCulledCopy",
        new[] {typeof(int)});
    
    public Ex DeleteSB(Ex index) => delete.InstanceOf(this, index);
    public Ex Softcull(Ex target, Ex index) => softcull.InstanceOf(this, target, index, Ex.Constant(null, typeof(SoftcullProperties?)));
    public Ex MakeCulledCopy(Ex index) => makeCulledCopy.InstanceOf(this, index);
    public Ex IsAlive(Ex index) => isAlive.InstanceOf(this, index);

    public Ex RunINodeAt(Ex index, Ex sm, Ex cT) => runINodeAt.InstanceOf(this, index, sm, cT);
    

    private static readonly ExFunction speedup =
        ExFunction.Wrap<AbsSimpleBulletCollection>("Speedup", new[] {typeof(float)});
    public Expression Speedup(Expression ratio) => speedup.InstanceOf(this, ratio);
}
}
namespace Danmokou.DMath.Functions {
/// <summary>
/// Functions that get a float value from a simple bullet.
/// </summary>
[Reflect]
public static class SBFRepo {
    /// <summary>
    /// Return the scale of the bullet.
    /// </summary>
    /// <returns></returns>
    public static ExBPY Scale() => sb => sb.SB.scale;
    /// <summary>
    /// Return the direction, in degrees, of the bullet.
    /// </summary>
    /// <returns></returns>
    public static ExBPY Dir() => sb => ExM.ATan(sb.SB.direction);
}

/// <summary>
/// Functions that get a vector2 value from a simple bullet.
/// </summary>
[Reflect]
public static class SBV2Repo {
    /// <summary>
    /// Return the global location of the bullet.
    /// </summary>
    public static ExTP Loc() => sb => sb.loc;
    /// <summary>
    /// Return the direction of the bullet as a (cos, sin) vector.
    /// </summary>
    public static ExTP Dir() => sb => sb.SB.direction;

    /// <summary>
    /// Return the delta movement of the bullet this frame.
    /// </summary>
    public static ExTP AccDelta() => sb => sb.SB.accDelta;
}
}