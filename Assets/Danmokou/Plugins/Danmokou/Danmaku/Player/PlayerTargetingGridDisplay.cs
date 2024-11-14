namespace Danmokou.Player {
public class PlayerTargetingGridDisplay : MovementGridDisplay {
    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
    }
}
}