using DMK.Core;
using DMK.Reflection;
using Ex = System.Linq.Expressions.Expression;
using P = System.Func<DMK.Expressions.ITexMovement, DMK.Expressions.TEx<float>, DMK.Expressions.TExPI, DMK.Expressions.TExV2, DMK.Expressions.TEx<UnityEngine.Vector2>>;

namespace DMK.DMath.Functions {
[Reflect]
public static class ExtraMovementFuncs {

    /// <summary>
    /// From 3,5, go down, at y=0 move softly to x=-3, then go down
    /// </summary>
    /// <returns></returns>
    public static P LeftChair1() => "tprot lerp3 1 2.5 2.5 4 t cy -3 cx -4 cy -3".Into<P>();
}
}