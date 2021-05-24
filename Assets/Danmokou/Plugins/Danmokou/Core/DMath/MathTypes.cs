using BagoumLib.Mathematics;

namespace Danmokou.DMath {

/// <summary>
/// A function that converts a float into a float.
/// </summary>
public delegate float FXY(float t);

public static class MathTypeHelpers {
    public static Easer AsEaser(this FXY f) => x => f(x);
}

}