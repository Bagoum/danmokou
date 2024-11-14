using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.Reflection;
using JetBrains.Annotations;
using Scriptor;
using static BagoumLib.Tasks.WaitingUtils;

namespace Danmokou.SM {

/// <summary>
/// `gtr2`: Like <see cref="GTRepeat"/>, but has specific handling for the WAIT, TIMES, and rpp properties.
/// </summary>
public class GTRepeat2 : GTRepeat {
    /// <param name="wait">Frames to wait between invocations</param>
    /// <param name="times">Number of invocations</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child StateMachines to run</param>
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public GTRepeat2(GCXF<float> wait, GCXF<float> times, GCXF<V2RV2> rpp, GenCtxProperty[] props, StateMachine[] target) :
        base(new GenCtxProperties<StateMachine>(props.Append(GenCtxProperty.Async(wait, times, rpp))), target) { }

}

/// <summary>
/// `gtr`: A generic repeater for StateMachines. Supports the same interface as `gcr`/`gir`.
/// <br/>Note: the constructor takes StateMachine[] and not List&lt;SM&gt;, which means you must explicitly wrap multiple SMs in brackets.
/// </summary>
public class GTRepeat : UniversalSM {
    private class SMExecutionTracker {
        public readonly GTRepeat caller;
        public LoopControl<StateMachine> looper;
        private readonly SMHandoff smh;
        private readonly bool waitChild;
        private readonly bool sequential;
        //Task tracker for when all execution is complete
        public TaskCompletionSource<Unit>? onFinish = null;
        public SMExecutionTracker(GTRepeat caller, GenCtxProperties<StateMachine> props, SMHandoff smh, out bool isClipped) {
            this.caller = caller;
            //Make a derived copy for the canPrepend override
            this.smh = new SMHandoff(smh, smh.ch.Mirror(), null, true);
            //But use the provided smh for its envframe
            looper = new LoopControl<StateMachine>(props, smh.ch, out isClipped);
            waitChild = props.waitChild;
            sequential = props.sequential;
            checkIsChildDone = null;
        }

        /// <summary>
        /// Check if the cancellation token is cancelled. If it is, return true and call <see cref="AllDone"/>.
        /// </summary>
        public bool CleanupIfCancelled() {
            if (smh.Cancelled) {
                AllDone(false, false);
                return true;
            }
            return false;
        }

        public bool RemainsExceptLast => looper.RemainsExceptLast;
        public bool PrepareIteration() => looper.PrepareIteration();

        public bool PrepareLastIteration() => looper.PrepareLastIteration();
        public void FinishIteration() => looper.FinishIteration();

        private Func<bool>? checkIsChildDone;

        //this needs to be pulled into a separate function so it doesn't cause local function->delegate cast overhead
        // or cause DoAIteration to be considered async in the standard case
        private async Task DoAIterationSequentialStep(SMHandoff itrsmh) {
            foreach (var fragment in caller.states) {
                if (looper.Handoff.cT.Cancelled) break;
                await fragment.Start(itrsmh);
            }
        }
        public void DoAIteration(ref float extraFrames) {
            bool done = false;
            //Clone the envframe before each iteration of all children
            var itrsmh = new SMHandoff(smh, looper.Handoff.Copy(), null);
            Action loopDone = () => {
                done = true;
                itrsmh.Dispose();
            };
            if (waitChild) {
                checkIsChildDone = () => done;
                --extraFrames;
            } else checkIsChildDone = null;

            if (sequential) {
                _ = DoAIterationSequentialStep(itrsmh).ContinueWithSync(loopDone);
            } else {
                var loopFragmentDone = GetManyCallback(caller.states.Length, loopDone);
                foreach (var fragment in caller.states)
                    _ = fragment.Start(itrsmh).ContinueWithSync(loopFragmentDone);
            }
        }

        public void DoLastAIteration() {
            //Unlike GIR, which hoists its cleanup code into a callback, GTR awaits its last child
            // and calls its cleanup code in Start.
            //Therefore, this code follows the wait-child pattern.
            bool done = false;
            //Clone the envframe before each iteration of all children
            var itrsmh = new SMHandoff(smh, looper.Handoff.Copy(), null);
            Action loopDone = () => {
                done = true;
                itrsmh.Dispose();
            };
            checkIsChildDone = () => done;
            if (sequential) {
                _ = DoAIterationSequentialStep(itrsmh).ContinueWithSync(loopDone);
            } else {
                var loopFragmentDone = GetManyCallback(caller.states.Length, loopDone);
                foreach (var fragment in caller.states)
                    _ = fragment.Start(itrsmh).ContinueWithSync(loopFragmentDone);
            }
        }

        /// <summary>
        /// Call this when the tracker is finished iterating.
        /// <br/>Called via <see cref="CleanupIfCancelled"/> if cancellation was received.
        /// <br/>Sets <see cref="onFinish"/>.
        /// </summary>
        /// <param name="runFinishIteration">True if looper.FinishIteration should be called.</param>
        /// <param name="normalEnd">True if looper ending rules should be called.</param>
        public void AllDone(bool? runFinishIteration = null, bool? normalEnd = null) {
            if (runFinishIteration ?? !looper.Handoff.cT.Cancelled) 
                FinishIteration();
            smh.Dispose();
            //Looper has a separate copied CH to dispose
            looper.IAmDone(normalEnd ?? !looper.Handoff.cT.Cancelled);
            onFinish?.SetResult(default);
        }
        
        private IEnumerator _WaitIEnum(float waitFrames, Action<SMExecutionTracker, float> cb) {
            if (smh.cT.Cancelled) { cb(this, waitFrames); yield break; }
            bool wasPaused = false;
            waitFrames = -waitFrames;
            while (!looper.IsUnpaused || !(checkIsChildDone?.Invoke() ?? true) || waitFrames < 0) {
                yield return null;
                if (smh.cT.Cancelled) { cb(this, waitFrames); yield break; }
                looper.WaitStep();
                if (!looper.IsUnpaused) wasPaused = true;
                else {
                    if (wasPaused && looper.props.unpause != null) {
                        _ = looper.GCX.exec.RunExternalSM(SMRunner.Run(looper.props.unpause, smh.cT, looper.GCX));
                    }
                    wasPaused = false;
                    if (checkIsChildDone?.Invoke() ?? true) ++waitFrames;
                }
            }
            cb(this, waitFrames);
        }
        public void Wait(float waitFrames, Action<SMExecutionTracker, float> continuation) =>
            looper.GCX.exec.RunAppendRIEnumerator(_WaitIEnum(waitFrames, continuation));

        public float InitialDelay() => looper.props.delay(looper.GCX);
        public float WaitFrames() => looper.props.wait(looper.GCX);
    }

    private readonly GenCtxProperties<StateMachine> props;
    
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public GTRepeat(GenCtxProperties<StateMachine> props, StateMachine[] target) : base(target) {
        this.props = props;
    }
    
    public override Task Start(SMHandoff smh) {
        SMExecutionTracker tracker = new(this, props, smh, out bool isClipped);
        if (isClipped) {
            tracker.AllDone(false, false);
            return Task.CompletedTask;
        }
        if (tracker.CleanupIfCancelled()) return Task.CompletedTask;
        tracker.onFinish = new TaskCompletionSource<Unit>();
        //https://github.com/dotnet/roslyn/issues/5835: use lambda to avoid overhead before .NET7
        tracker.Wait(tracker.InitialDelay(), (x, y) => ContinueFromInitialWait(x, y));
        return tracker.onFinish.Task;
    }

    private static void ContinueFromInitialWait(SMExecutionTracker tracker, float extraFrames) {
        if (tracker.CleanupIfCancelled()) return;
        if (tracker.RemainsExceptLast && tracker.PrepareIteration()) {
            tracker.DoAIteration(ref extraFrames);
            tracker.Wait(tracker.WaitFrames() - extraFrames, (x, y) => ContinueFromLoopIteration(x, y));
        } else
            LastLoopIteration(tracker, extraFrames);
    }

    private static void ContinueFromLoopIteration(SMExecutionTracker tracker, float extraFrames) {
        if (tracker.CleanupIfCancelled()) return;
        tracker.FinishIteration();
        if (tracker.RemainsExceptLast && tracker.PrepareIteration()) {
            tracker.DoAIteration(ref extraFrames);
            tracker.Wait(tracker.WaitFrames() - extraFrames, (x, y) => ContinueFromLoopIteration(x, y));
        } else
            LastLoopIteration(tracker, extraFrames);
    }

    private static void LastLoopIteration(SMExecutionTracker tracker, float extraFrames) {
        if (tracker.PrepareLastIteration()) {
            tracker.DoLastAIteration();
            //this only waits for child to finish
            tracker.Wait(-extraFrames, (_, __) => tracker.AllDone(true, true));
        } else
            tracker.AllDone(true, true);
    }





}

/// <summary>
/// `alternate`: Run only one of the provided StateMachines, using the indexer function to determine which.
/// </summary>
public class AlternateUSM : UniversalSM {
    private readonly GCXF<float> indexer;
    public AlternateUSM(GCXF<float> indexer, StateMachine[] target) : base(target) {
        this.indexer = indexer;
    }

    public override Task Start(SMHandoff smh) => states[(int)indexer(smh.GCX) % states.Length].Start(smh);
}

}