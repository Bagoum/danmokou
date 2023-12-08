using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Player/Movement/Flappy")]
public class FlappyMovementCfg : MovementCfg {
    public override PlayerMovement Value => new PlayerMovement.Flappy();
}
}