using Danmokou.Behavior;
using Danmokou.Core;
using SuzunoyaUnity;

namespace Danmokou.VN {
/// <summary>
/// Small project-specific class that issues update calls to VNWrapper.
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
            InputManager.DialogueToEnd, 
            InputManager.DialogueSkipAll);
    }
}
}