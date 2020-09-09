using Danmaku;
using Ex = System.Linq.Expressions.Expression;
using static DMath.ExMHelpers;
using tfloat = TEx<float>;
using static Danmaku.Enums;
using static GameManagement;

namespace DMath {
/// <summary>
/// See <see cref="DMath.ExM"/>. This class contains functions related to difficulty control.
/// </summary>
public static class ExMDifficulty {
    /// <summary>
    /// Get the difficulty multiplier. 1 is easy, ~2.3 is lunatic. POSITIVE values outside this range are possible.
    /// </summary>
    public static tfloat D() => Ex.Constant(DifficultyValue);
    /// <summary>
    /// Get the difficulty counter. 1 is easy, 4 is lunatic.
    /// </summary>
    public static tfloat Dc() => Ex.Constant(DifficultyCounter);
    
    /// <summary>
    /// Get the difficulty multiplier centered on normal.
    /// </summary>
    public static tfloat DN() => Ex.Constant(DifficultyValue / DifficultySet.Normal.Value());
    /// <summary>
    /// Get the difficulty multiplier centered on hard.
    /// </summary>
    public static tfloat DH() => Ex.Constant(DifficultyValue / DifficultySet.Hard.Value());
    /// <summary>
    /// Get the difficulty multiplier centered on lunatic.
    /// </summary>
    public static tfloat DL() => Ex.Constant(DifficultyValue / DifficultySet.Lunatic.Value());
    /// <summary>
    /// Get the difficulty multiplier centered on ultra.
    /// </summary>
    public static tfloat DU() => Ex.Constant(DifficultyValue / DifficultySet.Ultra.Value());

    /// <summary>
    /// 1 / DL
    /// </summary>
    public static tfloat iDL() => Ex.Constant(DifficultySet.Lunatic.Value() / DifficultyValue);

    private static tfloat ResolveD3(tfloat n, tfloat h, tfloat u) =>
        DifficultyValue < DifficultySet.Hard.Value() ? n :
        DifficultyValue < DifficultySet.Ultra.Value() ? h :
        u;

    /// <summary>
    /// Return -2 if the difficulty is less than Hard,
    /// else 0 if less than Ultra,
    /// else 2.
    /// </summary>
    /// <returns></returns>
    public static tfloat D3d2() => ResolveD3(EN2, E0, E2);
    /// <summary>
    /// Return -1 if the difficulty is less than Hard,
    /// else 0 if less than Ultra,
    /// else 1.
    /// </summary>
    /// <returns></returns>
    public static tfloat D3d1() => ResolveD3(EN1, E0, E1);

}
}