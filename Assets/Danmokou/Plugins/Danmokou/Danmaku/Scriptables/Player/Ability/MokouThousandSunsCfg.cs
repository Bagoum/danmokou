using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {

[CreateAssetMenu(menuName = "Data/Player/Ability/MokouThousandSuns")]
public class MokouThousandSunsCfg : BombCfg<Ability.MokouThousandSuns> {
    public override string Key => "bomb:MokouThousandSuns";
}
}