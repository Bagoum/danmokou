using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Core.DInput;
using SuzunoyaUnity;

namespace Danmokou.VN {
/// <summary>
/// Small project-specific class that issues update calls to VNWrapper in accordance with the project's time handling.
/// </summary>
public class VNUpdater : RegularUpdater {
    private VNWrapper wrapper = null!;
    
    private void Awake() {
        wrapper = GetComponent<VNWrapper>();
    }

    protected override void BindListeners() {
        RegisterService<IVNWrapper>(wrapper);
    }

    public override void RegularUpdate() {
        wrapper.DoUpdate(ETime.FRAME_TIME, 
            InputManager.DialogueConfirm, 
            InputManager.DialogueSkipAll);
    }
}
}