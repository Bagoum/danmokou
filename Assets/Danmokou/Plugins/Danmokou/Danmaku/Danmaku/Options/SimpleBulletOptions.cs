using System;
using System.Collections.Generic;
using BagoumLib;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using Scriptor;
using Scriptor.Expressions;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.Danmaku.Options.SBOption;

namespace Danmokou.Danmaku.Options {
/// <summary>
/// Properties that modify the behavior of simple bullets.
/// </summary>
[Reflect]
public record SBOption {
    /// <summary>
    /// Give the bullet a custom scale.
    /// </summary>
    public static SBOption Scale(BPY scale) => new ScaleProp(scale);
    
    /// <summary>
    /// Give the bullet a custom rotation function, in degrees.
    /// </summary>
    [ExpressionBoundary]
    public static SBOption Dir(Func<TExArgCtx, TEx<float>> dir) => new DirProp(Compilers.SBV2(x => CosSinDeg(dir(x))));
    
    /// <summary>
    /// Give the bullet a custom rotation function, in cos/sin coordinates.
    /// </summary>
    public static SBOption Dir2(SBV2 dir) => new DirProp(dir);

    /// <summary>
    /// Mark the bullet as a player shot.
    /// </summary>
    /// <param name="bossDmg">Damage against boss enemies</param>
    /// <param name="stageDmg">Damage against stage enemies</param>
    /// <param name="onHit">On-hit effect</param>
    public static SBOption Player(int bossDmg, int stageDmg, string onHit) =>
        new PlayerProp(bossDmg, stageDmg, onHit);

    /// <summary>
    /// Add an on-hit effect to the bullet. (For player bullets, use <see cref="Player"/> instead.)
    /// </summary>
    public static SBOption OnHit(string onHit) => new OnHitProp(onHit);

    #region impl

    public record ScaleProp(BPY F) : SBOption;

    public record DirProp(SBV2 F) : SBOption;

    public record PlayerProp(int BossDmg, int StageDmg, string OnHitEff) : SBOption;

    public record OnHitProp(string OnHitEff) : SBOption;

    #endregion
}

/// <summary>
/// A set of properties modifying the behavior of simple bullets.
/// </summary>
public class SBOptions {
    //Note: If adding GCXU objects here, also add them to
    // the GCXU.ShareTypeAndCompile call in AtomicPAtterns
    public readonly BPY? scale = null;
    public readonly SBV2? direction = null;
    public readonly (int boss, int stage)? player = null;
    public readonly EffectStrategy? onHit = null;

    public SBOptions(IEnumerable<SBOption> props) {
        foreach (var prop in props.Unroll()) {
            if (prop is ScaleProp sp) scale = sp.F;
            else if (prop is DirProp dp) direction = dp.F;
            else if (prop is OnHitProp ohp) onHit = ResourceManager.GetEffect(ohp.OnHitEff);
            else if (prop is PlayerProp pp) {
                player = (pp.BossDmg, pp.StageDmg);
                onHit = ResourceManager.GetEffect(pp.OnHitEff);
            } else throw new Exception($"Simple Bullet option {prop.GetType()} not handled.");
        }
    }
}
}