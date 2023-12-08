using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Player/Movement/Standard")]
public class StandardMovementCfg : MovementCfg {
    public override PlayerMovement Value => new PlayerMovement.Standard();
}
}