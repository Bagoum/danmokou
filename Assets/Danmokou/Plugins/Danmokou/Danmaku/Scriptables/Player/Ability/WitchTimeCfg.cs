using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {

[CreateAssetMenu(menuName = "Data/Player/Ability/WitchTime")]
public class WitchTimeCfg : AbilityCfg {
    public override string Key => "WitchTime";
    public override Ability Value => new Ability.WitchTime {
        Title = title.Value,
        ShortTitle = shortTitle.Value
    };
}
}