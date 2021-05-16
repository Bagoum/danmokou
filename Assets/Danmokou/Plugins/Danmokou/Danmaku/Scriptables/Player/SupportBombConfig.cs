using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using Danmokou.Player;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {

public interface ISupportAbilityConfig {
    string Key { get; }
    SupportAbility Value { get; }
    string Title { get; }
}
public abstract class SupportAbilityConfig : ScriptableObject, ISupportAbilityConfig {
    public abstract string Key { get; }
    public abstract SupportAbility Value { get; }

    public string Title => title.Value;

    public LocalizedStringReference title = null!;
}

[CreateAssetMenu(menuName = "Data/Player/Support/Bomb")]
public class SupportBombConfig : SupportAbilityConfig {
    public PlayerBombType type;
    
    public override string Key => $"bomb:{type}";
    public override SupportAbility Value => new Bomb(type);
}

}