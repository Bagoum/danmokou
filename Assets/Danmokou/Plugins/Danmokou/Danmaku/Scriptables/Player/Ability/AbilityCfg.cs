using Danmokou.Core;
using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {
public interface IAbilityCfg {
    string Key { get; }
    Ability Value { get; }
}
public abstract class AbilityCfg : ScriptableObject, IAbilityCfg {
    public abstract string Key { get; }
    public abstract Ability Value { get; }

    public LocalizedStringReference title = null!;
    public LocalizedStringReference shortTitle = null!;
}

public abstract class BombCfg<T> : AbilityCfg where T : Ability.Bomb, new() {
    public GameObject? cutin;
    public GameObject? spellTitle;
    public Color spellColor1;
    public Color spellColor2;
    public TextAsset? sm;
    
    public override Ability Value => new T {
        Title = title.Value,
        ShortTitle = shortTitle.Value,
        SMFile = sm,
        Cutin = cutin,
        SpellTitle = spellTitle,
        SpellColor1 = spellColor1,
        SpellColor2 = spellColor2
    };
}
}