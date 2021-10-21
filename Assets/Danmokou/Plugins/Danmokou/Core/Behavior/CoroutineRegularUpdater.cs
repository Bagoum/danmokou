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
            Logs.UnityError($"{gameObject.name} has {coroutines.Count} leftover coroutines." +
                           $" This should only occur on hard shutdowns.");
            //coroutines.Close(); //For debugging
        }
    }

    protected override void OnDisable() {
        ForceClosingFrame();
        base.OnDisable();
    }

    public void RunRIEnumerator(IEnumerator ienum) => 
        coroutines.Run(ienum, new CoroutineOptions(execType: CoroutineType.AppendToEnd));
    public void RunTryPrependRIEnumerator(IEnumerator ienum) => 
        coroutines.Run(ienum, new CoroutineOptions(execType: CoroutineType.TryStepPrepend));
    public void RunPrependRIEnumerator(IEnumerator ienum) => 
        coroutines.Run(ienum, new CoroutineOptions(execType: CoroutineType.StepPrepend));

    public void RunDroppableRIEnumerator(IEnumerator ienum) => 
        coroutines.Run(ienum, new CoroutineOptions(true, CoroutineType.AppendToEnd));

    public void Run(IEnumerator ienum, CoroutineOptions? flags = null) => coroutines.Run(ienum, flags);

#if UNITY_EDITOR
    public int NumRunningCoroutines => coroutines.Count;
#endif
}
}