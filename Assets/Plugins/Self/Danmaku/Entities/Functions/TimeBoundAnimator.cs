using Core;
using UnityEngine;

public abstract class TimeBoundAnimator : MonoBehaviour {
    public bool destroyOnDone;
    protected ICancellee cT = Cancellable.Null;

    public void Initialize(ICancellee canceller, float time) {
        cT = canceller;
        AssignTime(time);
    }
    public abstract void AssignTime(float total);

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