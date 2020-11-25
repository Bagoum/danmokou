using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq.Expressions;
using System.Text;
using Danmaku;
using Core;
using Ex = System.Linq.Expressions.Expression;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
using ExSBPred = System.Func<Danmaku.TExSBC, TEx<int>, DMath.TExPI, TEx<bool>>;
using static ExUtils;
using static DMath.ExM;
using static DMath.ExMHelpers;

namespace DMath {

public static partial class SBPredicates {
    [Fallthrough(50)]
    public static ExSBPred BPI(ExPred pred) => (sbc, ii, bpi) => pred(bpi);
}

/// <summary>
/// Functions that take in parametric information and return true or false.
/// </summary>
public static partial class PredicateLogic {

    /// <summary>
    /// Nest a predicate such that it only returns True once for a single bullet.
    /// </summary>
    /// <param name="pred"></param>
    /// <returns></returns>
    public static ExPred OnlyOnce(ExPred pred) {
        var returned = DataHoisting.GetClearableSet();
        var b = V<bool>();
        return bpi => Ex.Condition(SetHas<uint>(returned, bpi.id),
            ExC(false),
            Ex.Block(new[] {b},
                Ex.IfThen(b.Is(pred(bpi)), SetAdd<uint>(returned, bpi.id)),
                b
            )
        );
    }

    /// <summary>
    /// Return true if the object is in the given circle relative to a BehaviorEntity.
    /// </summary>
    /// <param name="beh">Target BehaviorEntity</param>
    /// <param name="circ">Relative circle</param>
    /// <returns></returns>
    public static ExPred RelCirc(BEHPointer beh, ExTP3 circ) {
        return bpi => Collision.pointInCircle.Of(
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
        return bpi => Collision.pointInCircle.Of(
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
        return bpi => Collision.pointInRect.Of(
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
        return bpi => Collision.pointInRect.Of(
            bpi.loc, ExC(rect)
        );
    }

}

}
