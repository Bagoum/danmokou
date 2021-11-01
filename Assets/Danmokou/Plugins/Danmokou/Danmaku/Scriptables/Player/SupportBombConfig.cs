using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using Danmokou.Player;
using Danmokou.SM;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {

public interface ISupportAbilityConfig {
    string Key { get; }
    SupportAbility Value { get; }
}
public abstract class SupportAbilityConfig : ScriptableObject, ISupportAbilityConfig {
    public abstract string Key { get; }
    public abstract SupportAbility Value { get; }

    public GameObject? cutin;
    public GameObject? spellTitle;
    public Color spellColor1;
    public Color spellColor2;

    public LocalizedStringReference title = null!;
    public LocalizedStringReference shortTitle = null!;

}

[CreateAssetMenu(menuName = "Data/Player/Support/Bomb")]
public class SupportBombConfig : SupportAbilityConfig {
    public PlayerBombType type;
    public TextAsset? sm;
    
    public override string Key => $"bomb:{type}";
    public override SupportAbility Value => new Bomb(type, sm) {
        title = title.Value,
        shortTitle = shortTitle.Value,
        cutin = cutin,
        spellTitle = spellTitle,
        spellColor1 = spellColor1,
        spellColor2 = spellColor2
    };
}

}