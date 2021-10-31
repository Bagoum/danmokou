using BagoumLib.Cancellation;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.SM;

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
        SM.WaitingUtils.WaitFor(this, cT ?? Cancellable.Null, time, true).ContinueWithSync(() => {
            token.Dispose();
            tokens.Remove(token);
        });
    }
}
}