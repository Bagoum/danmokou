using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;

namespace Danmokou.Services {
public class FreezeFrameHelper : CoroutineRegularUpdater {
    public override EngineState UpdateDuring => EngineState.EFFECT_PAUSE;
    protected override void BindListeners() {
        base.BindListeners();
        
        RegisterService(this);
    }

    public void CreateFreezeFrame(float time, ICancellee? cT = null) {
        var token = EngineStateManager.RequestState(EngineState.EFFECT_PAUSE);
        tokens.Add(token);
        RUWaitingUtils.WaitThenCBEvenIfCancelled(this, cT ?? Cancellable.Null, time, true, () => {
            token.Dispose();
            tokens.Remove(token);
        });
    }
}
}