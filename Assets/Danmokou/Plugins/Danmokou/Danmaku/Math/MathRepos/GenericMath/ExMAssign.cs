using Danmokou.Core;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.DMath.Functions {

/// <summary>
/// See <see cref="ExM"/>. This class contains functions related to assignment.
/// It is not reflected as it is only for use with BDSL2, which calls them explicitly.
/// </summary>
[DontReflect]
public static class ExMAssign {
    public static TEx<T> Is<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, y);
    public static TEx<T> IsAdd<T>(TEx<T> x, TEx<T> y) => Ex.AddAssign(x, y);
    public static TEx<T> IsSub<T>(TEx<T> x, TEx<T> y) => Ex.SubtractAssign(x, y);
    public static TEx<T> IsMul<T>(TEx<T> x, TEx<T> y) => Ex.MultiplyAssign(x, y);
    public static TEx<T> IsDiv<T>(TEx<T> x, TEx<T> y) => Ex.DivideAssign(x, y);
    public static TEx<T> IsMod<T>(TEx<T> x, TEx<T> y) => Ex.ModuloAssign(x, y);
    public static TEx<T> IsAnd<T>(TEx<T> x, TEx<T> y) => Ex.AndAssign(x, y);
    public static TEx<T> IsOr<T>(TEx<T> x, TEx<T> y) => Ex.OrAssign(x, y);

    public static TEx<T> PostIncr<T>(TEx<T> x) => Ex.PostIncrementAssign(x);
    public static TEx<T> PreIncr<T>(TEx<T> x) => Ex.PreIncrementAssign(x);
    public static TEx<T> PostDecr<T>(TEx<T> x) => Ex.PostDecrementAssign(x);
    public static TEx<T> PreDecr<T>(TEx<T> x) => Ex.PreDecrementAssign(x);
}
}