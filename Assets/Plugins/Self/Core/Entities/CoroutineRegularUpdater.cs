using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;

public class CoroutineRegularUpdater : RegularUpdater {
    /// <summary>
    /// A global BehaviorEntity that will not be destroyed as long as the game is running.
    /// </summary>
    public static CoroutineRegularUpdater Global;
    /// <summary>
    /// A global BehaviorEntity that will not be destroyed as long as the game is running,
    /// and also updates during pause.
    /// </summary>
    public static CoroutineRegularUpdater GlobalDuringPause;
    private readonly Coroutines coroutines = new Coroutines();
    public override void RegularUpdate() {
        if (coroutines.Count > 0) coroutines.Step();
    }

    protected void ForceClosingFrame() {
        coroutines.Close();
        if (coroutines.Count > 0) {
            Log.UnityError($"{gameObject.name} has {coroutines.Count} leftover coroutines.");
        }
    }
    protected override void OnDisable() {
        ForceClosingFrame();
        base.OnDisable();
    }

    public void RunRIEnumerator(IEnumerator ienum) => coroutines.Run(ienum);
    public void RunPrependRIEnumerator(IEnumerator ienum) => coroutines.RunPrepend(ienum);

    public void RunDroppableRIEnumerator(IEnumerator ienum) => coroutines.RunDroppable(ienum);

#if UNITY_EDITOR
    public int NumRunningCoroutines => coroutines.Count;
    #endif
}