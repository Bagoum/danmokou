namespace Danmokou.Player {
public class PlayerMovementGridDisplay : MovementGridDisplay {
    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
    }
}
}