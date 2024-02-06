using System;
using System.Collections.Generic;
using BagoumLib;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.Danmaku.Options.SBOption;

namespace Danmokou.Danmaku.Options {
/// <summary>
/// Properties that modify the behavior of simple bullets.
/// </summary>
[Reflect]
public class SBOption {
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
    /// <param name="effStrategy">On-hit effect</param>
    public static SBOption Player(int bossDmg, int stageDmg, string effStrategy) =>
        new PlayerProp((bossDmg, stageDmg, effStrategy));

    #region impl
    public class ValueProp<T> : SBOption {
        public readonly T value;
        protected ValueProp(T value) => this.value = value;
    }
    
    public class ScaleProp : ValueProp<BPY> {
        public ScaleProp(BPY f) : base(f) { }
    }
    
    public class DirProp : ValueProp<SBV2> {
        public DirProp(SBV2 f) : base(f) { }
    }

    public class PlayerProp : ValueProp<(int bossDmg, int stageDmg, string effStrat)> {
        public PlayerProp((int, int, string) data): base(data) { }
    }
    
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
    public readonly (int boss, int stage, EffectStrategy effStrat)? player = null;

    public SBOptions(IEnumerable<SBOption> props) {
        foreach (var prop in props.Unroll()) {
            if (prop is ScaleProp sp) scale = sp.value;
            else if (prop is DirProp dp) direction = dp.value;
            else if (prop is PlayerProp pp) {
                player = (pp.value.bossDmg, pp.value.stageDmg, ResourceManager.GetEffect(pp.value.effStrat));
            } else throw new Exception($"Simple Bullet option {prop.GetType()} not handled.");
        }
    }
}
}