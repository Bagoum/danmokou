using BagoumLib.Cancellation;
using Danmokou.Core;

namespace Danmokou.Behavior.Functions {
/// <summary>
/// Graphical only. Not on RU loop.
/// </summary>
public abstract class TimeBoundAnimator : CoroutineRegularUpdater {
    public bool destroyOnDone;

    public abstract void Initialize(ICancellee cT, float time);


    /// <summary>
    /// Share allocated time among the fade-in time, the wait time, and the fade-out time.
    /// The wait time will be modified freely.
    /// </summary>
    /// <param name="tin"></param>
    /// <param name="twait"></param>
    /// <param name="tout"></param>
    /// <param name="total"></param>
    protected static void Share(ref float tin, out float twait, ref float tout, float total) {
        if (tin + tout < total) {
            twait = total - tin - tout;
            return;
        }
        twait = 0;
        tin = total * tin / (tin + tout);
        tout = total - tin;

    }
}
}