using Danmokou.Behavior;

namespace Danmokou.Player {
/// <summary>
/// A helper component that runs coroutines for AyaCamera while the screen is frozen.
/// </summary>
public class AyaCameraFreezeHelper : CoroutineRegularUpdater {
    public override bool UpdateDuringPause => true;
}
}