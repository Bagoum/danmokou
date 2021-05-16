namespace Danmokou.Player {
public class Bomb : SupportAbility {
    public override bool UsesBomb => bomb.BombsRequired() != null;

    public readonly PlayerBombType bomb;
    
    public Bomb(PlayerBombType bomb) {
        this.bomb = bomb;
    }
}
}