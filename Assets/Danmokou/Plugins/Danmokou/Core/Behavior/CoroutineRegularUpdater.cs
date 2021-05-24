using System.Collections;
using BagoumLib.DataStructures;
using Danmokou.Core;

namespace Danmokou.Behavior {
public class CoroutineRegularUpdater : RegularUpdater, ICoroutineRunner {
    private readonly Coroutines coroutines = new Coroutines();

    public override void RegularUpdate() {
        if (coroutines.Count > 0) coroutines.Step();
    }

    protected void ForceClosingFrame() {
        coroutines.Close();
        if (coroutines.Count > 0) {
            Log.UnityError($"{gameObject.name} has {coroutines.Count} leftover coroutines." +
                           $" This should only occur on hard shutdowns.");
            //coroutines.Close(); //For debugging
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

    void ICoroutineRunner.Run(IEnumerator ienum) => RunRIEnumerator(ienum);
    void ICoroutineRunner.RunDroppable(IEnumerator ienum) => RunDroppableRIEnumerator(ienum);

#if UNITY_EDITOR
    public int NumRunningCoroutines => coroutines.Count;
#endif
}
}