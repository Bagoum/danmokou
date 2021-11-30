using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {

[CreateAssetMenu(menuName = "Data/Player/Ability/ReimuFantasySeal")]
public class ReimuFantasySealCfg : BombCfg<Ability.ReimuFantasySeal> {
    public override string Key => "bomb:ReimuFantasySeal";
}
}