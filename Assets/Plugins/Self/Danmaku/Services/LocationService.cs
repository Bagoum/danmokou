using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using Danmaku;
using LocationService = Danmaku.LocationService;
using static DMath.ExM;
using tfloat = TEx<float>;
using tv2 = TEx<UnityEngine.Vector2>;
using ev2 = DMath.EEx<UnityEngine.Vector2>;

namespace Danmaku {
public static class LocationService {
#if VER_BRUH
    public const float Left = -4f;
    public const float Right = 4f;
#else
    public const float Left = -5f;
    public const float Right = 5f;
#endif
    public const float Bot = -4.5f;
    public const float Top = 4f;
    public const float Width = Right - Left;
    public const float Height = Top - Bot;
    public static readonly Ex left = Ex.Constant(Left);
    public static readonly Ex right = Ex.Constant(Right);
    public static readonly Ex bot = Ex.Constant(Bot);
    public static readonly Ex top = Ex.Constant(Top);
    public static readonly Ex width = Ex.Constant(Width);
    public static readonly Ex height = Ex.Constant(Height);

    public static Vector2 GetEnemyVisiblePlayer() => GameManagement.VisiblePlayerLocation;

    /// <summary>
    /// Assumes that v2 is in bounds. Dir need not be normalized.
    /// </summary>
    public static Vector2 ToWall(Vector2 from, Vector2 dir) {
        float lrintercept = float.MaxValue;
        float udintercept = float.MaxValue;
        if (dir.x > 0) {
            lrintercept = (Right - from.x) / dir.x * dir.y + from.y;
        } else if (dir.x < 0) {
            lrintercept = (Left - from.x) / dir.x * dir.y + from.y;
        }
        if (dir.y > 0) {
            udintercept = (Top - from.y) / dir.y * dir.x + from.x;
        } else if (dir.y < 0) {
            udintercept = (Bot - from.y) / dir.y * dir.x + from.x;
        }
        if (lrintercept > Bot && lrintercept < Top) {
            return new Vector2((dir.x > 0) ? Right : Left, lrintercept);
        }
        if (udintercept > Left && udintercept < Right) {
            return new Vector2(udintercept, (dir.y > 0) ? Top : Bot);
        }
        return Vector2.zero;
    }

    /// <summary>
    /// Assumes that v2 is in bounds. Dir need not be normalized.
    /// </summary>
    public static float DistToWall(Vector2 from, Vector2 dir) {
        return (from - ToWall(from, dir)).magnitude;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OnPlayableScreen(Vector2 loc) {
        return (loc.x >= Left && loc.x <= Right && loc.y >= Bot && loc.y <= Top);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OnPlayableScreenBy(float f, Vector2 loc) {
        return (loc.x >= Left - f && loc.x <= Right + f && loc.y >= Bot - f && loc.y <= Top + f);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OffPlayableScreenBy(float f, Vector2 loc) {
        return (loc.x < Left - f || loc.x > Right + f || loc.y < Bot - f || loc.y > Top + f);
    }

    public static bool OnScreenInDirection(Vector2 loc, Vector2 directedOffset) {
        if (directedOffset.y < 0) return loc.y > Bot + directedOffset.y;
        else if (directedOffset.y > 0) return loc.y < Top + directedOffset.y;
        else if (directedOffset.x < 0) return loc.x > Left + directedOffset.x;
        else if (directedOffset.x > 0) return loc.x < Right + directedOffset.x;
        return false;
    }
}
}

namespace DMath {
public static partial class ExMPred {
//return (loc.x >= left && loc.x <= right && loc.y >= bot && loc.y <= top);
    public static TEx<bool> OnScreen(EEx<Vector2> loc) => EEx.ResolveV2(loc, l =>
            l.x.GT(LocationService.left)
            .And(l.x.LT(LocationService.right))
            .And(l.y.GT(LocationService.bot))
            .And(l.y.LT(LocationService.top)));
    public static TEx<bool> OnScreenBy(EEx<float> by, EEx<Vector2> loc) => EEx.ResolveV2(loc, by, (l, f) =>
            l.x.GT(LocationService.left.Sub(f))
            .And(l.x.LT(LocationService.right.Add(f)))
            .And(l.y.GT(LocationService.bot.Sub(f)))
            .And(l.y.LT(LocationService.top.Add(f))));

    public static TEx<bool> OffScreen(TEx<Vector2> loc) => Not(OnScreen(loc));
    
    public static TEx<bool> OffScreenBy(TEx<float> f, TEx<Vector2> loc) => Not(OnScreenBy(f, loc));

}
public static partial class ExM {
    public static TEx<float> YMin() => LocationService.bot;
    public static TEx<float> YMax() => LocationService.top;
    public static TEx<float> XMin() => LocationService.left;
    public static TEx<float> XMax() => LocationService.right;
    public static TEx<float> XWidth() => LocationService.width;
    public static TEx<float> YHeight() => LocationService.height;
    
    private static readonly ExFunction GetEnemyVisiblePlayer =
        ExUtils.Wrap(typeof(LocationService), "GetEnemyVisiblePlayer");
    
    /// <summary>
    /// Get the location of the player as visible to enemies.
    /// </summary>
    /// <returns></returns>
    public static TEx<Vector2> LPlayer() => GetEnemyVisiblePlayer.Of();

    public static TEx<Vector2> LBEH(BEHPointer beh) => Ex.Constant(beh).Field("beh").Field("bpi").Field("loc");
    
    private static readonly ExFunction distToWall =
        ExUtils.Wrap(typeof(LocationService), "DistToWall", typeof(Vector2), typeof(Vector2));

    public static tfloat DistToWall(tv2 from, tv2 dir) => distToWall.Of(from, dir);
}

public static partial class BPYRepo {
    
    
    /// <summary>
    /// Returns Atan(Player.Loc - this.Loc) in degrees.
    /// </summary>
    public static ExBPY AngleToPlayer() => AngleTo(x => ExM.LPlayer());
    /// <summary>
    /// Returns Atan(loc - this.Loc) in degrees.
    /// </summary>
    public static ExBPY AngleTo(ExTP loc) => bpi => ATan(Sub(loc(bpi), bpi.loc));
    
    /// <summary>
    /// Returns the x-position of the left/right wall that the location is closer to.
    /// </summary>
    public static ExBPY ToLR(ExTP loc) => bpi =>
        Ex.Condition(new TExV2(loc(bpi)).x.LT0(), LocationService.left, LocationService.right);
    /// <summary>
    /// Returns the x-position of the left/right wall that the location is farther from.
    /// </summary>
    public static ExBPY ToRL(ExTP loc) => bpi =>
        Ex.Condition(new TExV2(loc(bpi)).x.LT0(), LocationService.right, LocationService.left);
}
public static partial class Parametrics {

    public static ExTP LNearestEnemy() => b => {
        Ex data = DataHoisting.GetClearableDictInt();
        var eid_in = ExUtils.V<int?>();
        var eid = ExUtils.V<int>();
        var loc = new TExV2();
        return Ex.Block(new[] { eid_in, eid, loc },
            eid_in.Is(Ex.Condition(ExUtils.DictContains<uint, int>(data, b.id),
                    data.DictGet(b.id).As<int?>(),
                    Ex.Constant(null).As<int?>())
            ),
            Ex.IfThenElse(Enemy.findNearest.Of(b.loc, eid_in, eid, loc),
                data.DictSet(b.id, eid),
                loc.Is(Ex.Constant(new Vector2(0f, 50f)))
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
