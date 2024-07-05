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
        var tcs = new TaskCompletionSource<Unit>();
        Exec.RunAppendRIEnumerator(WaitFor(time, cT, tcs));
        await tcs.Task;
        //I do want this throw here, which is why I don't 'return t'
        cT.ThrowIfCancelled();
    }
    /// <summary>
    /// Task style. Will return as soon as the time is up or cancellation is triggered.
    /// You must check cT.IsCancelled after awaiting this.
    /// </summary>
    public static Task WaitForUnchecked(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity) {
        cT.ThrowIfCancelled();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        if (time < float.Epsilon) return Task.CompletedTask;
        var tcs = new TaskCompletionSource<Unit>();
        Exec.RunAppendRIEnumerator(WaitFor(time, cT, tcs));
        return tcs.Task;
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
    /// Wait for `wait_time` seconds or until the token is cancelled.
    /// Will invoke `done` only if the token is *not* cancelled.
    /// </summary>
    public static void WaitThenCB(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity,
        Action? done) {
        cT.ThrowIfCancelled();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        Exec.RunAppendRIEnumerator(WaitFor(time, cT, () => {
            if (!cT.Cancelled) done?.Invoke();
        }));
    }
    
    /// <summary>
    /// Wait for `wait_time` seconds or until the token is cancelled.
    /// Will invoke `done` regardless of cancellation.
    /// </summary>
    public static void WaitThenCBEvenIfCancelled(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity,
        Action done) {
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        Exec.RunAppendRIEnumerator(WaitFor(time, cT, done));
    }
    
    /// <summary>
    /// Wait until the condition is true AND the time is up, or the the token is cancelled.
    /// Will invoke `done` regardless of cancellation.
    /// </summary>
    public static void WaitThenCBEvenIfCancelled(CoroutineRegularUpdater Exec, ICancellee cT, float time, Func<bool> condition, Action done) {
        cT.ThrowIfCancelled();
        Exec.RunAppendRIEnumerator(WaitForBoth(time, condition, cT, done));
    }

    /// <summary>
    /// Wait for `wait_time` seconds or until the token is cancelled. Will invoke `done` regardless of cancellation.
    /// </summary>
    public static IEnumerator WaitFor(float wait_time, ICancellee cT, Action done) {
        for (; wait_time > ETime.FRAME_YIELD && !cT.Cancelled; 
            wait_time -= ETime.FRAME_TIME) yield return null;
        done();
    }
    
    /// <summary>
    /// Wait for `wait_time` seconds or until the token is cancelled. Will invoke `done` regardless of cancellation.
    /// </summary>
    private static IEnumerator WaitFor(float wait_time, ICancellee cT, TaskCompletionSource<Unit> done) {
        for (; wait_time > ETime.FRAME_YIELD && !cT.Cancelled; 
             wait_time -= ETime.FRAME_TIME) yield return null;
        done.SetResult(default);
    }
    
    /// <summary>
    /// Wait until the condition is true or the token is cancelled. Will invoke `done` regardless of cancellation.
    /// </summary>
    private static IEnumerator WaitFor(Func<bool> condition, ICancellee cT, Action done) {
        while (!condition() && !cT.Cancelled) yield return null;
        done();
    }
    
    /// <summary>
    /// Wait until the condition is true or the token is cancelled. Will invoke `done` regardless of cancellation.
    /// </summary>
    private static IEnumerator WaitFor(Func<bool> condition, ICancellee cT, TaskCompletionSource<Unit> done) {
        while (!condition() && !cT.Cancelled) yield return null;
        done.SetResult(default);
    }
    
    /// <summary>
    /// Wait until the condition is true AND the time is up, or the the token is cancelled.
    /// Will invoke `done` regardless of cancellation.
    /// </summary>
    private static IEnumerator WaitForBoth(float wait_time, Func<bool> condition, ICancellee cT, Action done) {
        for (; (wait_time > ETime.FRAME_YIELD || !condition()) && !cT.Cancelled; 
            wait_time -= ETime.FRAME_TIME) yield return null;
        done();
    }
    
}

}