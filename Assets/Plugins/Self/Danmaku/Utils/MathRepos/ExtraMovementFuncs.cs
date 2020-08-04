using Danmaku;
using Ex = System.Linq.Expressions.Expression;
using static ExUtils;
using static DMath.ExMHelpers;
using P = System.Func<Danmaku.ITExVelocity, TEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;
using static DMath.ExM;

namespace DMath {
public static class ExtraMovementFuncs {

    /// <summary>
    /// From 3,5, go down, at y=0 move softly to x=-3, then go down
    /// </summary>
    /// <returns></returns>
    public static P LeftChair1() => "tprot lerp3 1 2.5 2.5 4 t cy -3 cx -4 cy -3".Into<P>();
}
}