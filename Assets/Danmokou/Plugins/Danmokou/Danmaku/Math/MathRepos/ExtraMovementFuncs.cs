using Danmokou.Core;
using Danmokou.Reflection;
using Ex = System.Linq.Expressions.Expression;
using P = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExPI, Danmokou.Expressions.TExV2, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;

namespace Danmokou.DMath.Functions {
[Reflect]
public static class ExtraMovementFuncs {

    /// <summary>
    /// From 3,5, go down, at y=0 move softly to x=-3, then go down
    /// </summary>
    /// <returns></returns>
    public static P LeftChair1() => "tprot lerp3 1 2.5 2.5 4 t cy -3 cx -4 cy -3".Into<P>();
}
}