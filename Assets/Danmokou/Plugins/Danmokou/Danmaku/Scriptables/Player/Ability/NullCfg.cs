using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {

[CreateAssetMenu(menuName = "Data/Player/Ability/Null")]
public class NullCfg : AbilityCfg {
    public override string Key => "null";
    public override Ability Value => Ability.Null.Default;
}
}