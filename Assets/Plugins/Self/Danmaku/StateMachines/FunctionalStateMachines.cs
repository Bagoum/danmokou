using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Danmaku;
using DMath;
using JetBrains.Annotations;

namespace SM {

/// <summary>
/// `gtr2`: Like GTRepeat, but has specific handling for the WAIT, TIMES, and rpp properties.
/// </summary>
public class GTRepeat2 : GTRepeat {
    /// <param name="wait">Frames to wait between invocations</param>
    /// <param name="times">Number of invocations</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child StateMachines to run</param>
    public GTRepeat2(StateMachine[] target, GCXF<float> wait, GCXF<float> times, GCXF<V2RV2> rpp, GenCtxProperty[] props) :
        base(target, new GenCtxProperties<StateMachine>(props.Append(GenCtxProperty.Async(wait, times, rpp)))) { }

}

/// <summary>
/// `gtr`: A generic repeater for StateMachines. Supports the same interface as `gcr`/`gir`.
/// <br/>Note: the constructor takes StateMachine[] and not List{SM}, which means you must explicitly wrap multiple SMs in brackets.
/// </summary>
public class GTRepeat : UniversalSM {
    private struct SMExecutionTracker {
        public LoopControl<StateMachine> looper;
        /// <summary>
        /// SMHandoff to pass around. Note that this is dirty and the ch/Exec/GCX properties are repeatedly modified.
        /// </summary>
        private SMHandoff smh;
        private readonly bool waitChild;
        public SMExecutionTracker(GenCtxProperties<StateMachine> props, SMHandoff smh, out bool isClipped) {
            looper = new LoopControl<StateMachine>(props, smh.ch, out isClipped);
            this.smh = smh;
            waitChild = props.waitChild;
            checkIsChildDone = null;
            tmp_ret = ListCache<GenCtx>.Get();
        }

        public bool CleanupIfCancelled() {
            if (smh.Cancelled) {
                //Note: since wait-child under gtr is stored in tmp_ret,
                //this is only valid if gtr children cancel under the same conditions as parent. 
                //Note that we only do this because wait-child does not put Dispose in CB.
                DisposeAll();
                ListCache<GenCtx>.Consign(tmp_ret);
                return true;
            }
            return false;
        }

        private void DisposeAll() {
            for (int ii = 0; ii < tmp_ret.Count; ++ii) tmp_ret[ii].Dispose();
            tmp_ret.Clear();
        }

        public bool RemainsExceptLast => looper.RemainsExceptLast;
        public bool PrepareIteration() => looper.PrepareIteration();

        public bool PrepareLastIteration() => looper.PrepareLastIteration();
        private readonly List<GenCtx> tmp_ret;
        public void FinishIteration() => looper.FinishIteration(tmp_ret);

        [CanBeNull] private Func<bool> checkIsChildDone;

        public void DoAIteration(ref float elapsedFrames, IReadOnlyList<StateMachine> target) {
            if (looper.props.childSelect == null) {
                Action<Task> cb = null;
                if (waitChild) {
                    cb = WaitingUtils.GetFree1ManyCondition<Task>(target.Count, out checkIsChildDone);
                    //See IPExecutionTracker for explanation
                    --elapsedFrames; 
                } else checkIsChildDone = null;
                for (int ii = 0; ii < target.Count; ++ii) {
                    DoAIteration(target[ii], cb);
                }
            } else DoAIteration(target[(int) looper.props.childSelect(looper.GCX) % target.Count]);
        }

        private Action<Task> DisposeCB(GenCtx gcx) => _ => gcx.Dispose();
        private void DoAIteration(StateMachine target, [CanBeNull] Action<Task> waitChildDone=null) {
            smh.ch = looper.Handoff.CopyGCX();
            Action<Task> cb;
            if (waitChild) {
                tmp_ret.Add(smh.GCX);
                cb = waitChildDone ?? WaitingUtils.GetFree1Condition<Task>(out checkIsChildDone);
            } else cb = DisposeCB(smh.GCX);
            target.Start(smh).ContinueWithSync(cb);
        }

        public void DoLastAIteration(IReadOnlyList<StateMachine> target) {
            //Always track the done command. Even if we are not waiting-child, a TRepeat is only done
            //when its last invokee is done.
            if (looper.props.childSelect == null) {
                var cb = WaitingUtils.GetFree1ManyCondition<Task>(target.Count, out checkIsChildDone);
                for (int ii = 0; ii < target.Count; ++ii) {
                    DoLastAIteration(target[ii], cb);
                }
            } else DoLastAIteration(target[(int) looper.props.childSelect(looper.GCX) % target.Count]);
        }
        
        private void DoLastAIteration(StateMachine target, [CanBeNull] Action<Task> waitChildDone=null) {
            smh.ch = looper.Handoff.CopyGCX();
            tmp_ret.Add(smh.GCX);
            var cb = waitChildDone ?? WaitingUtils.GetFree1Condition<Task>(out checkIsChildDone);
            target.Start(smh).ContinueWithSync(cb);
        }

        public void AllDone() {
            FinishIteration();
            looper.IAmDone();
        }
        
        public static IEnumerator Wait(float elapsedFrames, float waitFrames, LoopControl<StateMachine> looper, [CanBeNull] Func<bool> childAwaiter, Action<(float, LoopControl<StateMachine>)> cb, CancellationToken cT) {
            elapsedFrames -= waitFrames;
            if (cT.IsCancellationRequested) { cb((elapsedFrames, looper)); yield break; }
            bool wasPaused = false;
            while (!looper.IsUnpaused || !(childAwaiter?.Invoke() ?? true) || elapsedFrames < 0) {
                yield return null;
                if (cT.IsCancellationRequested) { cb((elapsedFrames, looper)); yield break; }
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
                WaitingUtils.GetAwaiter(out Task<(float, LoopControl<StateMachine>)> t), smh.cT));
            return t;
        }

        public float InitialDelay() => looper.props.delay(looper.GCX);
        public float WaitFrames() => looper.props.wait(looper.GCX);
    }

    private readonly GenCtxProperties<StateMachine> props;
    
    public GTRepeat(StateMachine[] target, GenCtxProperties<StateMachine> props) : base(target.ToList()) {
        this.props = props;
    }

    public override async Task Start(SMHandoff smh) {
        //Since we can't guarantee who is calling GTR, we need to copy GCX...
        smh.ch = smh.ch.CopyGCX();
        using (var gcx = smh.GCX) {
            SMExecutionTracker tracker = new SMExecutionTracker(props, smh, out bool isClipped);
            if (isClipped) {
                tracker.AllDone();
                return;
            }
            if (tracker.CleanupIfCancelled()) return;
            float elapsedFrames = 0;
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
            tracker.AllDone();
        }
    }
}

}