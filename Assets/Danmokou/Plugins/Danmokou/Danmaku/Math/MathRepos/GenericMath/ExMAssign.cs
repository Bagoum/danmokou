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
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> Assign<T>(TEx<T> x, TEx<T> y) => Ex.Assign(x, y);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> AddAssign<T>(TEx<T> x, TEx<T> y) => Ex.AddAssign(x, y);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> SubAssign<T>(TEx<T> x, TEx<T> y) => Ex.SubtractAssign(x, y);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> MulAssign<T>(TEx<T> x, TEx<T> y) => Ex.MultiplyAssign(x, y);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> DivAssign<T>(TEx<T> x, TEx<T> y) => Ex.DivideAssign(x, y);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> ModAssign<T>(TEx<T> x, TEx<T> y) => Ex.ModuloAssign(x, y);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> AndAssign<T>(TEx<T> x, TEx<T> y) => Ex.AndAssign(x, y);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> OrAssign<T>(TEx<T> x, TEx<T> y) => Ex.OrAssign(x, y);
    
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PostIncrement<T>(TEx<T> x) => Ex.PostIncrementAssign(x);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PreIncrement<T>(TEx<T> x) => Ex.PreIncrementAssign(x);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PostDecrement<T>(TEx<T> x) => Ex.PostDecrementAssign(x);
    
    [Assigns(0)] [BDSL2Operator]
    public static TEx<T> PreDecrement<T>(TEx<T> x) => Ex.PreDecrementAssign(x);
}
}