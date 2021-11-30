using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {

[CreateAssetMenu(menuName = "Data/Player/Ability/MimaBlackHole")]
public class MimaBlackHoleCfg : BombCfg<Ability.MimaBlackHole> {
    public override string Key => "bomb:MimaBlackHole";
}
}