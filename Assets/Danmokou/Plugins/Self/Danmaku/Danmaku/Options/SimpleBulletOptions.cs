using System;
using System.Collections.Generic;
using DMK.Core;
using DMK.DMath;
using DMK.Reflection;
using DMK.Scriptables;
using DMK.Services;
using static DMK.DMath.Functions.ExM;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExTP = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExSBF = System.Func<DMK.Expressions.RTExSB, DMK.Expressions.TEx<float>>;
using ExSBV2 = System.Func<DMK.Expressions.RTExSB, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using static DMK.Danmaku.Options.SBOption;

namespace DMK.Danmaku.Options {
/// <summary>
/// Properties that modify the behavior of simple bullets.
/// </summary>
public class SBOption {
    /// <summary>
    /// Give the bullet a custom scale.
    /// </summary>
    public static SBOption Scale(GCXU<BPY> scale) => new ScaleProp(scale);
    /// <summary>
    /// Give the bullet a custom rotation function, in degrees.
    /// </summary>
    public static SBOption Dir(ExSBF dir) => new DirProp(Compilers.GCXU(x => CosSinDeg(dir(x))));
    /// <summary>
    /// Give the bullet a custom rotation function, in cos/sin coordinates.
    /// </summary>
    public static SBOption Dir2(GCXU<SBV2> dir) => new DirProp(dir);

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
    
    public class ScaleProp : ValueProp<GCXU<BPY>> {
        public ScaleProp(GCXU<BPY> f) : base(f) { }
    }
    
    public class DirProp : ValueProp<GCXU<SBV2>> {
        public DirProp(GCXU<SBV2> f) : base(f) { }
    }

    public class PlayerProp : ValueProp<(int bossDmg, int stageDmg, string effStrat)> {
        public PlayerProp((int, int, string) data): base(data) { }
    }
    
    #endregion
}

public class SBOptions {
    public readonly GCXU<BPY>? scale = null;
    public readonly GCXU<SBV2>? direction = null;
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