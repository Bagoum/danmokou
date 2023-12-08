using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using JetBrains.Annotations;
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
    [CreatesInternalScope(3)]
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
            this.smh = new SMHandoff(smh, smh.ch, null, true);
            looper = new LoopControl<StateMachine>(props, this.smh.ch, out isClipped);
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
        // in the standard case
        private async Task DoAIterationSequentialStep(Action? loopDone) {
            for (int ii = 0; ii < caller.states.Length; ++ii) {
                if (looper.Handoff.cT.Cancelled) break;
                await DoAIteration(ii, null);
            }
            loopDone?.Invoke();
            /*
            void DoNext(int ii) {
                ++ii;
                if (ii >= caller.states.Length || looper.Handoff.cT.Cancelled) {
                    loopDone?.Invoke();
                } else 
                    DoAIteration(ii, ni => DoNext(ni));
            }
            DoNext(-1);*/
        }
        public void DoAIteration(ref float extraFrames) {
            Action? loopDone = null;
            if (waitChild) {
                bool done = false;
                loopDone = () => done = true;
                checkIsChildDone = () => done;
                --extraFrames;
            } else checkIsChildDone = null;

            if (looper.props.childSelect == null) {
                if (sequential) {
                    _ = DoAIterationSequentialStep(loopDone).ContinueWithSync();
                } else {
                    var loopFragmentDone = loopDone == null ? null : GetManyCallback(caller.states.Length, loopDone);
                    for (int ii = 0; ii < caller.states.Length; ++ii) {
                        _ = DoAIteration(caller.states[ii], loopFragmentDone).ContinueWithSync();
                    }
                }
            } else
                _ = DoAIteration(caller.states[(int)looper.props.childSelect(looper.GCX) % caller.states.Length],
                    loopDone).ContinueWithSync();
        }

        private async Task DoAIteration(int index, Action<int>? childDone) {
            using var itrSMH = new SMHandoff(smh, looper.Handoff, null);
            try {
                await caller.states[index].Start(itrSMH);
            } finally {
                childDone?.Invoke(index);
            }
        }
        private async Task DoAIteration(StateMachine target, Action? childDone) {
            using var itrSMH = new SMHandoff(smh, looper.Handoff, null);
            try {
                await target.Start(itrSMH);
            } finally {
                childDone?.Invoke();
            }
        }

        public void DoLastAIteration() {
            //Unlike GIR, which hoists its cleanup code into a callback, GTR awaits its last child
            // and calls its cleanup code in Start.
            //Therefore, this code follows the wait-child pattern.
            bool done = false;
            Action loopDone = () => done = true;
            checkIsChildDone = () => done;
            if (looper.props.childSelect == null) {
                if (sequential) {
                    _ = DoAIterationSequentialStep(loopDone).ContinueWithSync();
                } else {
                    var loopFragmentDone = GetManyCallback(caller.states.Length, loopDone);
                    for (int ii = 0; ii < caller.states.Length; ++ii) {
                        _ = DoAIteration(caller.states[ii], loopFragmentDone).ContinueWithSync();
                    }
                }
            } else 
                _ = DoAIteration(caller.states[(int) looper.props.childSelect(looper.GCX) % caller.states.Length], loopDone).ContinueWithSync();
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
            looper.GCX.exec.RunRIEnumerator(_WaitIEnum(waitFrames, continuation));

        public float InitialDelay() => looper.props.delay(looper.GCX);
        public float WaitFrames() => looper.props.wait(looper.GCX);
    }

    private readonly GenCtxProperties<StateMachine> props;
    
    [CreatesInternalScope(0)]
    public GTRepeat(GenCtxProperties<StateMachine> props, StateMachine[] target) : base(target) {
        this.props = props;
    }

    //Old task-based implementation, replaced with callback-based implementation for garbage efficiency
    /*
    public override async Task Start(SMHandoff smh) {
        SMExecutionTracker tracker = new(this, props, smh, out bool isClipped);
        if (isClipped) {
            tracker.AllDone(false, false);
            return;
        }
        if (tracker.CleanupIfCancelled()) return;
        //a number [0, inf) which is the number of extra frames we waited for from the previous loop
        float extraFrames = await tracker.Wait(tracker.InitialDelay());
        if (tracker.CleanupIfCancelled()) return;
        while (tracker.RemainsExceptLast && tracker.PrepareIteration()) {
            tracker.DoAIteration(ref extraFrames, states);
            extraFrames = await tracker.Wait(tracker.WaitFrames() - extraFrames);
            if (tracker.CleanupIfCancelled()) return;
            tracker.FinishIteration();
        }
        if (tracker.PrepareLastIteration()) {
            tracker.DoLastAIteration(states);
            await tracker.Wait(-extraFrames); //this only waits for child to finish
        }
        tracker.AllDone(true, true);
    }*/
    
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

}