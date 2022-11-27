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
            Logs.UnityError($"{this.GetType().Name} ({gameObject.name}) has {coroutines.Count} leftover coroutines." +
                           $" This should only occur on hard shutdowns.");
            //coroutines.Close(); //For debugging
        }
    }

    protected override void OnDisable() {
        ForceClosingFrame();
        base.OnDisable();
    }

    private static readonly CoroutineOptions standardOpts = new(ExecType: CoroutineType.AppendToEnd);
    private static readonly CoroutineOptions tryPrependOpts = new(ExecType: CoroutineType.TryStepPrepend);
    private static readonly CoroutineOptions prependOpts = new(ExecType: CoroutineType.StepPrepend);
    private static readonly CoroutineOptions droppableOpts = new(true, CoroutineType.AppendToEnd);
    public void RunRIEnumerator(IEnumerator ienum) => 
        coroutines.Run(ienum, standardOpts);
    public void RunTryPrependRIEnumerator(IEnumerator ienum) => 
        coroutines.Run(ienum, tryPrependOpts);
    public void RunPrependRIEnumerator(IEnumerator ienum) => 
        coroutines.Run(ienum, prependOpts);

    public void RunDroppableRIEnumerator(IEnumerator ienum) => 
        coroutines.Run(ienum, droppableOpts);

    public void Run(IEnumerator ienum, CoroutineOptions? flags = null) => coroutines.Run(ienum, flags);

#if UNITY_EDITOR
    public int NumRunningCoroutines => coroutines.Count;
#endif
}
}