using Danmokou.SM;
using UnityEngine;

namespace Danmokou.Player {
public class Bomb : SupportAbility {
    public override bool UsesBomb => bomb.BombsRequired() != null;

    public readonly PlayerBombType bomb;
    private readonly TextAsset? sm;
    public StateMachine? SM => StateMachineManager.FromText(sm);
    
    public Bomb(PlayerBombType bomb, TextAsset? sm) {
        this.bomb = bomb;
        this.sm = sm;
    }
}
}