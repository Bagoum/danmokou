using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection {
[Reflect]
public static class ReflectConstructors {
    /// <summary>
    /// Access a variable in public (shared) data.
    /// </summary>
    public static ReflectEx.Hoist<T> H<T>(string name) => new(name);

    /// <summary>
    /// Create a rectangle object.
    /// </summary>
    public static CRect Rect(float x, float y, float halfW, float halfH, float ang) =>
        new CRect(x, y, halfW, halfH, ang);
    
    /// <summary>
    /// Create a circle object.
    /// </summary>
    public static CCircle Circle(float x, float y, float radius) =>
        new CCircle(x, y, radius);

    /// <summary>
    /// Create a named timer. Calling this function multiple times with the same name will return the same timer.
    /// </summary>
    public static ETime.Timer NamedTimer(string name) => ETime.Timer.GetTimer(name);
    
    /// <summary>
    /// Create a timer. Calling this function multiple times will return different timers.
    /// </summary>
    [NonConstable]
    public static ETime.Timer NewTimer() => ETime.Timer.GetUnnamedTimer();
}
}