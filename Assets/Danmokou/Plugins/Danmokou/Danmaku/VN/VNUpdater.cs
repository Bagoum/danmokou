using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.UI.XML;
using SuzunoyaUnity;

namespace Danmokou.VN {
/// <summary>
/// Small project-specific class that issues update calls to VNWrapper in accordance with the project's time handling.
/// </summary>
public class VNUpdater : RegularUpdater {
    private VNWrapper wrapper = null!;
    private bool nextFrameIsConfirm = false;

    protected override void BindListeners() {
        wrapper = GetComponent<VNWrapper>();
        RegisterService<IVNWrapper>(wrapper);
    }

    public override void FirstFrame() {
        AddToken(ServiceLocator.Find<XMLDynamicMenu>().HandleDefaultUnselectConfirm.AddConst((n, cs) => {
            nextFrameIsConfirm = true;
            return null;
        }));
    }

    public override void RegularUpdate() {
        wrapper.DoUpdate(ETime.FRAME_TIME, 
            //If a UIConfirm cascades to unselector, then nextFrameIsConfirm is set
            nextFrameIsConfirm ||
            //DialogueConfirm can be shared with UIConfirm, or it can originate from InCodeInputSource
            //In the case where DialogueConfirm is the same key as UIConfirm,
            // we don't want to call VNState.UserConfirm() when the UIConfirm input is "consumed" by a UINode
            (InputManager.DialogueConfirm && !InputManager.UIConfirm), 
            InputManager.DialogueSkipAll);
        nextFrameIsConfirm = false;
    }
}
}