using System.Collections;
using DMK.Core;

namespace DMK.Behavior {
public class CoroutineRegularUpdater : RegularUpdater {
    private readonly Coroutines coroutines = new Coroutines();

    public override void RegularUpdate() {
        if (coroutines.Count > 0) coroutines.Step();
    }

    protected void ForceClosingFrame() {
        coroutines.Close();
        if (coroutines.Count > 0) {
            Log.UnityError($"{gameObject.name} has {coroutines.Count} leftover coroutines.");
            coroutines.Close();
        }
    }

    protected override void OnDisable() {
        ForceClosingFrame();
        base.OnDisable();
    }

    public void RunRIEnumerator(IEnumerator ienum) => coroutines.Run(ienum);
    public void RunTryPrependRIEnumerator(IEnumerator ienum) => coroutines.RunTryPrepend(ienum);
    public void RunPrependRIEnumerator(IEnumerator ienum) => coroutines.RunPrepend(ienum);

    public void RunDroppableRIEnumerator(IEnumerator ienum) => coroutines.RunDroppable(ienum);

#if UNITY_EDITOR
    public int NumRunningCoroutines => coroutines.Count;
#endif
}
}