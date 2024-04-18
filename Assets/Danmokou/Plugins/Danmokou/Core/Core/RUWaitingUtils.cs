using System;
using System.Collections;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using static BagoumLib.Tasks.WaitingUtils;

namespace Danmokou.Core {
public static class RUWaitingUtils {
    /// <summary>
    /// Task style-- will throw if cancelled. This checks cT.IsCancelled before returning,
    /// so you do not need to check it after awaiting this.
    /// </summary>
    /// <returns></returns>
    public static async Task WaitFor(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity) {
        cT.ThrowIfCancelled();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        if (time < float.Epsilon) return;
        Exec.RunAppendRIEnumerator(WaitFor(time, cT, GetAwaiter(out Task t)));
        await t;
        //I do want this throw here, which is why I don't 'return t'
        cT.ThrowIfCancelled();
    }
    /// <summary>
    /// Task style. Will return as soon as the time is up or cancellation is triggered.
    /// You must check cT.IsCancelled after awaiting this.
    /// </summary>
    /// <returns></returns>
    public static Task WaitForUnchecked(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity) {
        cT.ThrowIfCancelled();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        if (time < float.Epsilon) return Task.CompletedTask;
        Exec.RunAppendRIEnumerator(WaitFor(time, cT, GetAwaiter(out Task t)));
        return t;
    }
    /// <summary>
    /// Task style. Will return as soon as the condition is satisfied or cancellation is triggered.
    /// You must check cT.IsCancelled after awaiting this.
    /// </summary>
    /// <returns></returns>
    public static Task WaitForUnchecked(CoroutineRegularUpdater Exec, ICancellee cT, Func<bool> condition) {
        cT.ThrowIfCancelled();
        var tcs = new TaskCompletionSource<Unit>();
        Exec.RunAppendRIEnumerator(WaitFor(condition, cT, tcs));
        return tcs.Task;
    }

    /// <summary>
    /// Outer waiter-- Will not cb if cancelled
    /// </summary>
    public static void WaitThenCB(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity,
        Action? cb) {
        cT.ThrowIfCancelled();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        Exec.RunAppendRIEnumerator(WaitFor(time, cT, () => {
            if (!cT.Cancelled) cb?.Invoke();
        }));
    }
    /// <summary>
    /// Outer waiter-- Will cb if cancelled
    /// </summary>
    public static void WaitThenCBEvenIfCancelled(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity,
        Action cb) {
        cT.ThrowIfCancelled();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        Exec.RunAppendRIEnumerator(WaitFor(time, cT, cb));
    }
    /// <summary>
    /// Outer waiter-- Will not cb if cancelled
    /// </summary>
    public static void WaitThenCB(CoroutineRegularUpdater Exec, ICancellee cT, float time, Func<bool> condition,
        Action cb) {
        cT.ThrowIfCancelled();
        Exec.RunAppendRIEnumerator(WaitForBoth(time, condition, cT, () => {
            if (!cT.Cancelled) cb();
        }));
    }
    /// <summary>
    /// Outer waiter-- Will cb if cancelled
    /// </summary>
    public static void WaitThenCBEvenIfCancelled(CoroutineRegularUpdater Exec, ICancellee cT, float time, Func<bool> condition, Action cb) {
        cT.ThrowIfCancelled();
        Exec.RunAppendRIEnumerator(WaitForBoth(time, condition, cT, cb));
    }
    
    /// <summary>
    /// Outer waiter-- Will cb if cancelled
    /// </summary>
    public static void WaitThenCBEvenIfCancelled(CoroutineRegularUpdater Exec, ICancellee cT, Func<bool> condition, Action cb) {
        cT.ThrowIfCancelled();
        Exec.RunAppendRIEnumerator(WaitFor(condition, cT, cb));
    }

    /// <summary>
    /// Outer waiter-- Will not cancel if cancelled
    /// </summary>
    public static void WaitThenCancel(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity,
        ICancellable toCancel) {
        cT.ThrowIfCancelled();
        if (zeroToInfinity && time < float.Epsilon) {
            time = float.MaxValue;
        }
        Exec.RunAppendRIEnumerator(WaitFor(time, cT, () => {
            if (!cT.Cancelled) toCancel.Cancel();
        }));
    }

    /// <summary>
    /// Inner waiter-- Will cb if cancelled. This is necessary so awaiters can be informed of errors.
    /// </summary>
    public static IEnumerator WaitFor(float wait_time, ICancellee cT, Action done) {
        for (; wait_time > ETime.FRAME_YIELD && !cT.Cancelled; 
            wait_time -= ETime.FRAME_TIME) yield return null;
        done();
    }
    
    /// <summary>
    /// Returns when the condition is true (or is cancelled)
    /// </summary>
    private static IEnumerator WaitFor(Func<bool> condition, ICancellee cT, Action done) {
        while (!condition() && !cT.Cancelled) yield return null;
        done();
    }
    
    /// <summary>
    /// Returns when the condition is true (or is cancelled)
    /// </summary>
    private static IEnumerator WaitFor(Func<bool> condition, ICancellee cT, TaskCompletionSource<Unit> done) {
        while (!condition() && !cT.Cancelled) yield return null;
        done.SetResult(default);
    }
    
    /// <summary>
    /// Returns when the condition is true AND time is up (or is cancelled)
    /// </summary>
    private static IEnumerator WaitForBoth(float wait_time, Func<bool> condition, ICancellee cT, Action done) {
        for (; (wait_time > ETime.FRAME_YIELD || !condition()) && !cT.Cancelled; 
            wait_time -= ETime.FRAME_TIME) yield return null;
        done();
    }

    public static IEnumerator WaitWhileWithCancellable(Func<bool> amIFinishedWaiting, ICancellable canceller, Func<bool> cancelIf, ICancellee cT, Action done, float delay=0f) {
        while (!amIFinishedWaiting() && !cT.Cancelled) {
            if (delay < ETime.FRAME_YIELD && cancelIf()) {
                canceller.Cancel();
                break;
            }
            yield return null;
            delay -= ETime.FRAME_TIME;
        }
        done();
    }
    
}

}