using Danmokou.Behavior;
using Danmokou.Core;

namespace Danmokou.Player {
/// <summary>
/// A helper component that runs coroutines for AyaCamera while the screen is frozen.
/// </summary>
public class AyaCameraFreezeHelper : CoroutineRegularUpdater {
    public override EngineState UpdateDuring => EngineState.EFFECT_PAUSE;
}
}