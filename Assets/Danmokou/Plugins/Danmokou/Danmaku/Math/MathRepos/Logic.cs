using BagoumLib.Expressions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.Expressions.ExMHelpers;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP3 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector3>>;

namespace Danmokou.DMath.Functions {


/// <summary>
/// Functions that take in parametric information and return true or false.
/// </summary>
[Reflect]
public static partial class PredicateLogic {

    /// <summary>
    /// Nest a predicate such that it only returns True once for a single bullet.
    /// </summary>
    /// <param name="pred"></param>
    /// <returns></returns>
    public static ExPred OnlyOnce(ExPred pred) =>
        bpi => {
            var b = V<bool>();
            var key = bpi.Ctx.NameWithSuffix("_OnlyOnce_Set");
            return Ex.Condition(FiringCtx.Contains<int>(bpi, key),
                ExC(false),
                Ex.Block(new[] {b},
                    Ex.IfThen(b.Is(pred(bpi)), FiringCtx.SetValue<int>(bpi, key, ExC(1))),
                    b
                )
            );
        };

    /// <summary>
    /// Return true if the object is in the given circle relative to a BehaviorEntity.
    /// </summary>
    /// <param name="beh">Target BehaviorEntity</param>
    /// <param name="circ">Relative circle</param>
    /// <returns></returns>
    public static ExPred RelCirc(BEHPointer beh, ExTP3 circ) {
        return bpi => CollisionMath.pointInCircle.Of(
            Ex.Subtract(bpi.loc, LBEH(beh)),
            Ex.Convert(circ(bpi), typeof(CCircle))
        );
    }
    /// <summary>
    /// Return true if the object is in the given circle.
    /// </summary>
    /// <param name="circ">Circle in world-space</param>
    /// <returns></returns>
    public static ExPred Circ(ExTP3 circ) {
        return bpi => CollisionMath.pointInCircle.Of(
            bpi.loc, Ex.Convert(circ(bpi), typeof(CCircle))
        );
    }
    /// <summary>
    /// Return true if the object is in the given rectangle relative to a BehaviorEntity.
    /// </summary>
    /// <param name="beh">Target BehaviorEntity</param>
    /// <param name="rect">Relative rectangle</param>
    /// <returns></returns>
    public static ExPred RelRect(BEHPointer beh, CRect rect) {
        return bpi => CollisionMath.pointInRect.Of(
            Ex.Subtract(bpi.loc, LBEH(beh)),
            ExC(rect)
        );
    }
    /// <summary>
    /// Return true if the object is in the given rectangle.
    /// </summary>
    /// <param name="rect">Rectangle in world-space</param>
    /// <returns></returns>
    public static ExPred Rect(CRect rect) {
        return bpi => CollisionMath.pointInRect.Of(
            bpi.loc, ExC(rect)
        );
    }

}

}
