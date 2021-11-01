using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
/// `gtr2`: Like GTRepeat, but has specific handling for the WAIT, TIMES, and rpp properties.
/// </summary>
public class GTRepeat2 : GTRepeat {
    /// <param name="wait">Frames to wait between invocations</param>
    /// <param name="times">Number of invocations</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child StateMachines to run</param>
    public GTRepeat2(GCXF<float> wait, GCXF<float> times, GCXF<V2RV2> rpp, GenCtxProperty[] props, StateMachine[] target) :
        base(new GenCtxProperties<StateMachine>(props.Append(GenCtxProperty.Async(wait, times, rpp))), target) { }

}

/// <summary>
/// `gtr`: A generic repeater for StateMachines. Supports the same interface as `gcr`/`gir`.
/// <br/>Note: the constructor takes StateMachine[] and not List{SM}, which means you must explicitly wrap multiple SMs in brackets.
/// </summary>
public class GTRepeat : UniversalSM {
    private class SMExecutionTracker {
        public LoopControl<StateMachine> looper;
        private readonly SMHandoff smh;
        private readonly bool waitChild;
        private readonly bool sequential;
        public SMExecutionTracker(GenCtxProperties<StateMachine> props, SMHandoff smh, out bool isClipped) {
            //Make a derived copy for the canPrepend override
            this.smh = new SMHandoff(smh, smh.ch, null, true);
            looper = new LoopControl<StateMachine>(props, this.smh.ch, out isClipped);
            waitChild = props.waitChild;
            sequential = props.sequential;
            checkIsChildDone = null;
        }

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

        public void DoAIteration(ref float elapsedFrames, IReadOnlyList<StateMachine> target) {
            bool done = false;
            Action loop_done = () => {
                done = true;
            };
            if (waitChild) {
                checkIsChildDone = () => done;
                --elapsedFrames;
            } else checkIsChildDone = null;
            
            if (looper.props.childSelect == null) {
                if (sequential) {
                    void DoNext(int ii) {
                        if (ii >= target.Count || looper.Handoff.cT.Cancelled) {
                            loop_done();
                        } else 
                            DoAIteration(target[ii], () => DoNext(ii + 1));
                    }
                    DoNext(0);
                } else {
                    var loop_fragment_done = GetManyCallback(target.Count, loop_done);
                    for (int ii = 0; ii < target.Count; ++ii) {
                        DoAIteration(target[ii], loop_fragment_done);
                    }
                }
            } else DoAIteration(target[(int) looper.props.childSelect(looper.GCX) % target.Count], loop_done);
        }

        private void DoAIteration(StateMachine target, Action childDone) {
            var itrSMH = new SMHandoff(smh, looper.Handoff, null);
            target.Start(itrSMH).ContinueWithSync(() => {
                itrSMH.Dispose();
                childDone();
            });
        }

        public void DoLastAIteration(IReadOnlyList<StateMachine> target) {
            //Unlike GIR, which hoists its cleanup code into a callback, GTR awaits its last child
            // and calls its cleanup code in Start.
            //Therefore, this code follows the wait-child pattern.
            bool done = false;
            checkIsChildDone = () => done;
            Action loop_done = () => {
                done = true;
                //AllDone called by Start code
            };
            if (looper.props.childSelect == null) {
                if (sequential) {
                    void DoNext(int ii) {
                        if (ii >= target.Count || looper.Handoff.cT.Cancelled)
                            loop_done();
                        else 
                            DoAIteration(target[ii], () => DoNext(ii + 1));
                    }
                    DoNext(0);
                } else {
                    var loop_fragment_done = GetManyCallback(target.Count, loop_done);
                    for (int ii = 0; ii < target.Count; ++ii) {
                        DoAIteration(target[ii], loop_fragment_done);
                    }
                }
            } else DoAIteration(target[(int) looper.props.childSelect(looper.GCX) % target.Count], loop_done);
        }

        public void AllDone(bool? runFinishIteration = null, bool? normalEnd = null) {
            if (runFinishIteration ?? !looper.Handoff.cT.Cancelled) 
                FinishIteration();
            smh.Dispose();
            //Looper has a separate copied CH to dispose
            looper.IAmDone(normalEnd ?? !looper.Handoff.cT.Cancelled);
        }
        
        public static IEnumerator Wait(float elapsedFrames, float waitFrames, LoopControl<StateMachine> looper, Func<bool>? childAwaiter, Action<(float, LoopControl<StateMachine>)> cb, ICancellee cT) {
            elapsedFrames -= waitFrames;
            if (cT.Cancelled) { cb((elapsedFrames, looper)); yield break; }
            bool wasPaused = false;
            while (!looper.IsUnpaused || !(childAwaiter?.Invoke() ?? true) || elapsedFrames < 0) {
                yield return null;
                if (cT.Cancelled) { cb((elapsedFrames, looper)); yield break; }
                looper.WaitStep();
                if (!looper.IsUnpaused) wasPaused = true;
                else {
                    if (wasPaused && looper.props.unpause != null) {
                        _ = looper.GCX.exec.RunExternalSM(SMRunner.Run(looper.props.unpause, cT, looper.GCX));
                    }
                    wasPaused = false;
                    if (childAwaiter?.Invoke() ?? true) ++elapsedFrames;
                }
            }
            cb((elapsedFrames, looper));
        }

        public Task<(float, LoopControl<StateMachine>)> Wait(float elapsedFrames, float waitFrames) {
            looper.GCX.exec.RunRIEnumerator(Wait(elapsedFrames, waitFrames, looper, checkIsChildDone, 
                GetAwaiter(out Task<(float, LoopControl<StateMachine>)> t), smh.cT));
            return t;
        }

        public float InitialDelay() => looper.props.delay(looper.GCX);
        public float WaitFrames() => looper.props.wait(looper.GCX);
    }

    private readonly GenCtxProperties<StateMachine> props;
    
    public GTRepeat(GenCtxProperties<StateMachine> props, StateMachine[] target) : base(target.ToList()) {
        this.props = props;
    }

    public override async Task Start(SMHandoff smh) {
        SMExecutionTracker tracker = new SMExecutionTracker(props, smh, out bool isClipped);
        if (isClipped) {
            tracker.AllDone(false, false);
            return;
        }
        if (tracker.CleanupIfCancelled()) return;
        float elapsedFrames;
        (elapsedFrames, tracker.looper) = await tracker.Wait(0f, tracker.InitialDelay());
        if (tracker.CleanupIfCancelled()) return;
        while (tracker.RemainsExceptLast && tracker.PrepareIteration()) {
            tracker.DoAIteration(ref elapsedFrames, states);
            (elapsedFrames, tracker.looper) = await tracker.Wait(elapsedFrames, tracker.WaitFrames());
            if (tracker.CleanupIfCancelled()) return;
            tracker.FinishIteration();
        }
        if (tracker.PrepareLastIteration()) {
            tracker.DoLastAIteration(states);
            await tracker.Wait(elapsedFrames, 0f); //this only waits for child to finish
        }
        tracker.AllDone(true, true);
    }
}

}