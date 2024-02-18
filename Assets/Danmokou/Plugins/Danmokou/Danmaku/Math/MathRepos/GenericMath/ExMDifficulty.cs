using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Services;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExMHelpers;
using tfloat = Danmokou.Expressions.TEx<float>;
using static Danmokou.Services.GameManagement;

namespace Danmokou.DMath.Functions {
/// <summary>
/// See <see cref="ExM"/>. This class contains functions related to difficulty control.
/// </summary>
[Reflect] [Atomic]
public static class ExMDifficulty {
    //As of DMK v10.1, difficulty is no longer parsed statically at script compile time.
    // Like rank, it is read dynamically from the instance settings.
    //In most cases, difficulty does not change, but this allows preserving script compilations between executions.
    
    #region Difficulty
    [DontReflect] private static Ex d => ExM.Instance.Field(nameof(InstanceData.Difficulty));

    /// <summary>
    /// Get the difficulty multiplier. 1 is easy, ~2.3 is lunatic. POSITIVE values outside this range are possible.
    /// </summary>
    public static tfloat D() => d.Field(nameof(DifficultySettings.Value));
    
    /// <summary>
    /// Get the difficulty counter. 0 is easy, 3 is lunatic.
    /// </summary>
    public static tfloat Dc() => d.Field(nameof(DifficultySettings.Counter));
    
    /// <summary>
    /// Get the difficulty counter relative to lunatic. -3 is easy, 0 is lunatic.
    /// </summary>
    public static tfloat DcL() => Dc().Sub(FixedDifficulty.Lunatic.Counter());
    
    /// <summary>
    /// Get the difficulty multiplier centered on normal.
    /// </summary>
    public static tfloat DN() => d.Field(nameof(DifficultySettings.ValueRelNormal));
    
    /// <summary>
    /// Get the difficulty multiplier centered on hard.
    /// </summary>
    public static tfloat DH() => d.Field(nameof(DifficultySettings.ValueRelHard));
    
    /// <summary>
    /// Get the difficulty multiplier centered on lunatic.
    /// </summary>
    public static tfloat DL() => d.Field(nameof(DifficultySettings.ValueRelLunatic));

    private static tfloat ResolveD3(tfloat n, tfloat h, tfloat u) =>
        Ex.Condition(D().LT(ExC(FixedDifficulty.Normal.Value())), n,
            Ex.Condition(D().LT(ExC(FixedDifficulty.Lunatic.Value())), h, 
                u));

    /// <summary>
    /// Return -2 if the difficulty is less than Normal,
    /// else 0 if less than Lunatic,
    /// else 2.
    /// </summary>
    /// <returns></returns>
    public static tfloat D3d2() => ResolveD3(EN2, E0, E2);
    
    /// <summary>
    /// Return -1 if the difficulty is less than Normal,
    /// else 0 if less than Lunatic,
    /// else 1.
    /// </summary>
    /// <returns></returns>
    public static tfloat D3d1() => ResolveD3(EN1, E0, E1);
    
    #endregion

    #region Rank

    private static Ex RankFeature => ExM.Instance.Field(nameof(InstanceData.RankF));

    /// <summary>
    /// Get the dynamic difficulty rank, which varies between MinRank and MaxRank.
    /// </summary>
    public static tfloat Rank() => RankFeature.Field(nameof(IRankFeature.RankLevel)).Cast<float>();

    public static tfloat RankRatio() => RankFeature.Field(nameof(IRankFeature.RankRatio)).Cast<float>();


    /// <summary>
    /// Minumum possible rank value (inclusive).
    /// </summary>
    public static tfloat MinRank() =>
        RankFeature.Field(nameof(IRankFeature.MinRankLevel)).As<float>();
    
    /// <summary>
    /// Maximum possible rank value (inclusive).
    /// </summary>
    public static tfloat MaxRank() => 
        RankFeature.Field(nameof(IRankFeature.MaxRankLevel)).As<float>();

    
    #endregion



}
}