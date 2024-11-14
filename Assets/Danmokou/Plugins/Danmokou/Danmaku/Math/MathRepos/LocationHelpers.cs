using System.Linq.Expressions;
using BagoumLib.Expressions;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DataHoist;
using Danmokou.Expressions;
using Scriptor;
using Scriptor.Expressions;
using static Danmokou.DMath.Functions.ExM;
using static Scriptor.Math.ExMOperators;
using tfloat = Scriptor.Expressions.TEx<float>;
using tv2 = Scriptor.Expressions.TEx<UnityEngine.Vector2>;
using ExBPY = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<float>>;
using ExTP = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<UnityEngine.Vector2>>;


namespace Danmokou.DMath.Functions {
public static partial class ExMPred {
    /// <summary>
    /// Return true iff the location is within the playing field.
    /// </summary>
    /// <param name="loc"></param>
    /// <returns></returns>
    public static TEx<bool> OnScreen(TEx<Vector2> loc) => TExHelpers.ResolveV2AsXY(loc, (x, y) =>
            x.GT(LocationHelpers.left)
            .And(x.LT(LocationHelpers.right))
            .And(y.GT(LocationHelpers.bot))
            .And(y.LT(LocationHelpers.top)));
    
    /// <summary>
    /// Return true if the location is within BY units (square expansion) of the edge of the playing field.
    /// </summary>
    public static TEx<bool> OnScreenBy(TEx<float> by, TEx<Vector2> loc) => 
        TEx.ResolveF(by, f => TExHelpers.ResolveV2AsXY(loc, (x, y) =>
            x.GT(LocationHelpers.left.Sub(f))
            .And(x.LT(LocationHelpers.right.Add(f)))
            .And(y.GT(LocationHelpers.bot.Sub(f)))
            .And(y.LT(LocationHelpers.top.Add(f)))));

    /// <summary>
    /// Return true if the location not within the playing field.
    /// </summary>
    public static TEx<bool> OffScreen(TEx<Vector2> loc) => Not(OnScreen(loc));
    
    /// <summary>
    /// Return true if the location is more than BY units (square expansion) outside of the playing field.
    /// </summary>
    public static TEx<bool> OffScreenBy(TEx<float> f, TEx<Vector2> loc) => Not(OnScreenBy(f, loc));

}
public static partial class ExM {
    /// <summary>
    /// Y-coordinate of the lowest section of the playing field. (Default value: -4.5)
    /// </summary>
    /// <returns></returns>
    public static TEx<float> YMin() => LocationHelpers.bot;
    
    /// <summary>
    /// Y-coordinate of the highest section of the playing field. (Default value: 4.1)
    /// </summary>
    public static TEx<float> YMax() => LocationHelpers.top;
    
    /// <summary>
    /// X-coordinate of the leftmost section of the playing field. (Default value: -3.6)
    /// </summary>
    /// <returns></returns>
    public static TEx<float> XMin() => LocationHelpers.left;
    
    /// <summary>
    /// X-coordinate of the rightmost section of the playing field. (Default value: 3.6)
    /// </summary>
    public static TEx<float> XMax() => LocationHelpers.right;
    
    /// <summary>
    /// YMin - 1
    /// </summary>
    /// <returns></returns>
    [Alias("ymin_")]
    public static TEx<float> YMinMinus1() => LocationHelpers.bot.Sub(1);
    
    /// <summary>
    /// YMax + 1
    /// </summary>
    [Alias("ymax+")]
    public static TEx<float> YMaxPlus1() => LocationHelpers.top.Add(1);
    
    /// <summary>
    /// XMin - 1
    /// </summary>
    [Alias("xmin_")]
    public static TEx<float> XMinMinus1() => LocationHelpers.left.Sub(1);
    
    /// <summary>
    /// XMax + 1
    /// </summary>
    /// <returns></returns>
    [Alias("xmax+")]
    public static TEx<float> XMaxPlus1() => LocationHelpers.right.Add(1);
    
    /// <summary>
    /// Width of the playing field (XMax - XMin)
    /// </summary>
    public static TEx<float> XWidth() => LocationHelpers.width;
    
    /// <summary>
    /// Height of the playing field (YMax - YMin)
    /// </summary>
    public static TEx<float> YHeight() => LocationHelpers.height;

    /// <summary>
    /// Get the location of the player as visible to enemies.
    /// </summary>
    /// <returns></returns>
    public static TEx<Vector2> LPlayer() =>
        Ex.Property(null, typeof(LocationHelpers), nameof(LocationHelpers.VisiblePlayerLocation));
    
    /// <summary>
    /// Get the true location of the player. Use this for positioning player shots.
    /// </summary>
    /// <returns></returns>
    public static TEx<Vector2> LPlayerTrue() =>
        Ex.Property(null, typeof(LocationHelpers), nameof(LocationHelpers.TruePlayerLocation));

    /// <summary>
    /// Get the location of the BehaviorEntity.
    /// </summary>
    public static TEx<Vector2> LBEH(TEx<BehaviorEntity> beh) => beh.Field(nameof(BehaviorEntity.Location));
    
    private static readonly ExFunction distToWall =
        ExFunction.Wrap(typeof(LocationHelpers), "DistToWall", typeof(Vector2), typeof(Vector2));
    private static readonly ExFunction toWall =
        ExFunction.Wrap(typeof(LocationHelpers), "ToWall", typeof(Vector2), typeof(Vector2));

    public static tfloat DistToWall(tv2 from, tv2 dir) => distToWall.Of(from, dir);
    public static tv2 ToWall(tv2 from, tv2 dir) => toWall.Of(from, dir);
}

public static partial class BPYRepo {
    
    
    /// <summary>
    /// Returns Atan(Player.Loc - this.Loc) in degrees.
    /// </summary>
    public static ExBPY AngleToPlayer() => AngleTo(x => ExM.LPlayer());
    
    /// <summary>
    /// Returns Atan(loc - this.Loc) in degrees.
    /// </summary>
    public static ExBPY AngleTo(ExTP loc) => bpi => ATan(Sub(loc(bpi), bpi.LocV2()));
    
    /// <summary>
    /// Returns the x-position of the left/right wall that the location is closer to.
    /// </summary>
    public static ExBPY ToLR(ExTP loc) => bpi =>
        Ex.Condition(new TExV2(loc(bpi)).x.LT0(), LocationHelpers.left, LocationHelpers.right);
    
    /// <summary>
    /// Returns the x-position of the left/right wall that the location is farther from.
    /// </summary>
    public static ExBPY ToRL(ExTP loc) => bpi =>
        Ex.Condition(new TExV2(loc(bpi)).x.LT0(), LocationHelpers.right, LocationHelpers.left);
}
public static partial class Parametrics {

    /// <summary>
    /// Get the location of the closest enemy. If no enemies exist, default to (0, 50).
    /// <br/>WARNING: It is inoptimal to use this in <see cref="LaserOption"/>.<see cref="LaserOption.Dynamic"/>.
    /// Instead, use <see cref="LaserOption.BeforeDraw"/> with this function, and use the saved value in Dynamic.
    /// </summary>
    public static ExTP LNearestEnemy() => LNearestEnemyDefault(Parametrics.CXY(0, 50));
    
    /// <summary>
    /// Get the location of the closest enemy. If no enemies exist, default to the provided location.
    /// <br/>WARNING: It is inoptimal to use this in <see cref="LaserOption"/>.<see cref="LaserOption.Dynamic"/>.
    /// Instead, use <see cref="LaserOption.BeforeDraw"/> with this function, and use the saved value in Dynamic.
    /// </summary>
    public static ExTP LNearestEnemyDefault(ExTP deflt) => b => {
        var loc = new TExV2();
        return Ex.Block(new ParameterExpression[] { loc },
            Ex.IfThen(Ex.Not(Enemy.findNearest.Of(b.LocV3(), loc)),
                loc.Is(deflt(b))
            ),
            loc
        );
    };


    /// <summary>
    /// Get the location of the closest enemy, preferring the one that was chosen in the previous frame.
    /// If no enemies exist, default to (0, 50).
    /// </summary>
    public static ExTP LSaveNearestEnemy() => LSaveNearestEnemyDefault(Parametrics.CXY(0, 50));
    

    /// <summary>
    /// Get the location of the closest enemy, preferring the one that was chosen in the previous frame.
    /// If no enemies exist, default to the provided location.
    /// </summary>
    public static ExTP LSaveNearestEnemyDefault(ExTP deflt) => b => {
        var key = b.Ctx.NameWithSuffix("_LSaveNearestEnemyKey");
        var eid_in = ExUtils.V<int?>("preferred_enemy");
        var eid = ExUtils.V<int>("enemy");
        var loc = new TExV2("position");
        return Ex.Block(new[] { eid_in, eid, loc },
            eid_in.Is(Ex.Condition(b.DynamicHas<int>(key),
                b.DynamicGet<int>(key).Cast<int?>(),
                Ex.Constant(null).Cast<int?>())
            ),
            Ex.IfThenElse(Enemy.findNearestSave.Of(b.LocV3(), eid_in, eid, loc),
                b.DynamicSet<int>(key, eid),
                loc.Is(deflt(b))
            ),
            loc
        );
    };

    /*
    /// <summary>
    /// Find the location such that a ray fired from the source would bounce
    /// against a horizontal wall at {x, y} and hit the target.
    /// </summary>
    /// <param name="y"></param>
    /// <param name="source"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public static ExTP BounceY(ExBPY y, ExTP source, ExTP target) {
        var src = TExV2.Variable();
        var v2 = TExV2.Variable();
        var yw = ExUtils.VFloat();
        return bpi => Ex.Block(new[] {src, v2, yw},
            Ex.Assign(src, source(bpi)),
            Ex.Assign(yw, Ex.Subtract(y(bpi), src.y)),
            Ex.Assign(v2, Ex.Subtract(target(bpi), src)),
            ExUtils.AddAssign(src.x, v2.x.Mul(yw).Div(yw.Add(yw.Sub(v2.y)))),
            ExUtils.AddAssign(src.y, yw),
            src
        );
    }*/
}
}
