using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.Services;
using JetBrains.Annotations;
using Danmokou.SM;
using Scriptor;
using GCP = Danmokou.Danmaku.Options.GenCtxProperty;
using ExBPY = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<float>>;
using ExBPRV2 = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<BagoumLib.Mathematics.V2RV2>>;
using static BagoumLib.Tasks.WaitingUtils;

namespace Danmokou.Danmaku.Patterns {
/// <summary>
/// Functions that describe actions performed over time.
/// The full type is Func{AsyncHandoff, IEnumerator}.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[Reflect]
public static partial class AsyncPatterns {
    private struct APExecutionTracker {
        private LoopControl<AsyncPattern> looper;
        /// <summary>
        /// AsyncHandoff provided by caller. Contains the callback for marking completion.
        /// </summary>
        private readonly AsyncHandoff abh;
        public APExecutionTracker(GenCtxProperties<AsyncPattern> props, AsyncHandoff abh, out bool isClipped) {
            looper = new LoopControl<AsyncPattern>(props, abh.ch, out isClipped);
            this.abh = abh;
            extraFrames = 0f;
            wasPaused = false;
        }

        public bool CleanupIfCancelled() {
            if (abh.Cancelled) {
                AllSDone(false);
                return true;
            }
            return false;
        }

        public bool RemainsExceptLast => looper.RemainsExceptLast;
        public bool PrepareIteration() => looper.PrepareIteration();

        public bool PrepareLastIteration() => looper.PrepareLastIteration();
        public void DoSIteration(SyncPattern[] target) {
            using var itrSBH = new SyncHandoff(looper.Handoff.Copy(), extraFrames * ETime.FRAME_TIME);
            for (int ii = 0; ii < target.Length; ++ii) {
                target[ii].Run(itrSBH);
            }
        }
        public void FinishIteration() => looper.FinishIteration();
        public void WaitStep() {
            looper.WaitStep();
            if (!looper.IsUnpaused) wasPaused = true;
            else {
                if (wasPaused && looper.props.unpause != null) {
                    _ = looper.GCX.exec.RunExternalSM(SMRunner.Run(looper.props.unpause, abh.ch.cT, looper.GCX));
                }
                wasPaused = false;
                ++extraFrames;
            }
        }

        private bool wasPaused;

        private float extraFrames;
        public bool IsWaiting => !looper.IsUnpaused || extraFrames < 0;
        public void StartInitialDelay() => extraFrames -= looper.props.delay(looper.GCX);
        public void StartWait() => extraFrames -= looper.props.wait(looper.GCX);
        public void AllSDone(bool normalFinish) {
            looper.IAmDone(normalFinish);
            abh.Done();
        }
    }
    private class IPExecutionTracker {
        private LoopControl<AsyncPattern> looper;
        private readonly AsyncHandoff abh;
        private readonly bool waitChild;
        private readonly bool sequential;
        public IPExecutionTracker(GenCtxProperties<AsyncPattern> props, AsyncHandoff abh, out bool isClipped) {
            looper = new LoopControl<AsyncPattern>(props, abh.ch, out isClipped);
            this.abh = abh;
            elapsedFrames = 0f;
            waitChild = props.waitChild;
            sequential = props.sequential;
            checkIsChildDone = null;
            wasPaused = false;
        }

        public bool CleanupIfCancelled() {
            if (abh.Cancelled) {
                AllADone(false, false);
                return true;
            }
            return false;
        }
        public bool RemainsExceptLast => looper.RemainsExceptLast;
        public bool PrepareIteration() => looper.PrepareIteration();

        public bool PrepareLastIteration() => looper.PrepareLastIteration();
        public void FinishIteration() => looper.FinishIteration();

        private Func<bool>? checkIsChildDone;

        public void DoAIteration(AsyncPattern[] target) {
            //To prevent secondary sequential children from trying to copy this object's GCX
            // which will have already changed when the next loop starts.
            bool done = false;
            //iteration abh is disabled done call because it is shared between all children,
            // and will only be cleaned up manually via loop_done
            AsyncHandoff itrABH = new AsyncHandoff(abh, looper.Handoff, null);
            Action loop_done = () => {
                done = true;
                itrABH.Cleanup();
            };
            if (waitChild) {
                checkIsChildDone = () => done;
                //On the frame that the child finishes, the waitstep will increment elapsedFrames
                //even though it should not. However, it is difficult to tell the waitstep whether
                //the child was finished or not finished before that frame. This is the easiest solution.
                --elapsedFrames;
            } else checkIsChildDone = null;
            
            if (sequential) {
                void DoNext(int ii) {
                    if (ii >= target.Length || looper.Handoff.cT.Cancelled) {
                        loop_done();
                    } else 
                        DoAIteration(target[ii], itrABH, () => DoNext(ii + 1));
                }
                DoNext(0);
            } else {
                var loop_fragment_done = GetManyCallback(target.Length, loop_done);
                for (int ii = 0; ii < target.Length; ++ii) {
                    DoAIteration(target[ii], itrABH, loop_fragment_done);
                }
            }
        }

        private void DoAIteration(AsyncPattern target, AsyncHandoff itrABH, Action done) {
            //RunPrepend steps the coroutine and places it before the current one,
            //so we can continue running on the same frame that the child finishes (if using waitchild). 
            itrABH.callback = done;
            itrABH.RunPrependRIEnumerator(target.Run(itrABH));
        }

        public void AllADone(bool? runFinishIteration = null, bool? normalEnd = null) {
            if (runFinishIteration ?? !looper.Handoff.cT.Cancelled) 
                FinishIteration();
            looper.IAmDone(normalEnd ?? !looper.Handoff.cT.Cancelled);
            abh.Done();
        }
        
        public void DoLastAIteration(AsyncPattern[] target) {
            //iteration abh is disabled done call because it is shared between all children,
            // and will only be cleaned up manually via loop_done
            AsyncHandoff itrABH = new AsyncHandoff(abh, looper.Handoff, null);
            Action loop_done = () => {
                AllADone();
                itrABH.Cleanup();
            };
            if (sequential) {
                void DoNext(int ii) {
                    if (ii >= target.Length || looper.Handoff.cT.Cancelled)
                        loop_done();
                    else
                        DoAIteration(target[ii], itrABH, () => DoNext(ii + 1));
                }
                DoNext(0);
            } else {
                var loop_fragment_done = GetManyCallback(target.Length, loop_done);
                for (int ii = 0; ii < target.Length; ++ii) {
                    DoAIteration(target[ii], itrABH, loop_fragment_done);
                }
            }
        }
        
        public void WaitStep() {
            looper.WaitStep();
            if (!looper.IsUnpaused) wasPaused = true;
            else {
                if (wasPaused && looper.props.unpause != null) {
                    _ = looper.GCX.exec.RunExternalSM(SMRunner.Run(looper.props.unpause, abh.ch.cT, looper.GCX));
                }
                wasPaused = false;
                if (checkIsChildDone?.Invoke() ?? true) ++elapsedFrames;
            }
        }

        private bool wasPaused;

        private float elapsedFrames;
        public bool IsWaiting => !looper.IsUnpaused || !(checkIsChildDone?.Invoke() ?? true) || elapsedFrames < 0;
        public void StartInitialDelay() => elapsedFrames -= looper.props.delay(looper.GCX);
        public void StartWait() => elapsedFrames -= looper.props.wait(looper.GCX);
    }
    /// <summary>
    /// The generic C-level repeater function.
    /// Takes any number of functionality-modifying properties as an array.
    /// </summary>
    /// <param name="props">Array of properties</param>
    /// <param name="target">Child SyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GCR")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GCRepeat(GenCtxProperties<AsyncPattern> props, SyncPattern[] target) {
        IEnumerator Inner(AsyncHandoff abh) {
            APExecutionTracker tracker = new APExecutionTracker(props, abh, out bool isClipped);
            if (isClipped) {
                tracker.AllSDone(false);
                yield break;
            }
            if (tracker.CleanupIfCancelled()) yield break;
            for (tracker.StartInitialDelay(); tracker.IsWaiting; tracker.WaitStep()) {
                yield return null;
                if (tracker.CleanupIfCancelled()) yield break;
            }
            while (tracker.RemainsExceptLast && tracker.PrepareIteration()) { 
                tracker.DoSIteration(target);
                for (tracker.StartWait(); tracker.IsWaiting; tracker.WaitStep()) {
                    yield return null;
                    if (tracker.CleanupIfCancelled()) yield break;
                }
                tracker.FinishIteration();
            }
            if (tracker.PrepareLastIteration()) {
                tracker.DoSIteration(target);
                tracker.FinishIteration();
            }
            tracker.AllSDone(true);
        }
        return new(Inner);
    }
    
    /// <summary>
    /// Like GCRepeat, but has specific handling for the WAIT, TIMES, and rpp properties.
    /// </summary>
    /// <param name="wait">Frames to wait between invocations</param>
    /// <param name="times">Number of invocations</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child SyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GCR2")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GCRepeat2(GCXF<float> wait, GCXF<float> times, GCXF<V2RV2> rpp, GenCtxProperty[] props, SyncPattern[] target) =>
        GCRepeat(new GenCtxProperties<AsyncPattern>(props.Append(GenCtxProperty.Async(wait, times, rpp))), target);

    /// <summary>
    /// Like GCRepeat, but has specific handling for the WAIT, TIMES, and rpp properties,
    /// where WAIT and TIMES are mutated by the difficulty reference (wait / difficulty, times * difficulty)
    /// </summary>
    /// <param name="difficulty">Difficulty multiplier</param>
    /// <param name="wait">Frames to wait between invocations</param>
    /// <param name="times">Number of invocations</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child SyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GCR2d")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GCRepeat2d(ExBPY difficulty, ExBPY wait, ExBPY times, GCXF<V2RV2> rpp, GenCtxProperty[] props, SyncPattern[] target) =>
        GCRepeat(new GenCtxProperties<AsyncPattern>(props.Append(GenCtxProperty.AsyncD(difficulty, wait, times, rpp))), target);
    
    /// <summary>
    /// Like GCRepeat, but has specific handling for the WAIT, TIMES, and rpp properties,
    /// where all three are adjusted for difficulty.
    /// </summary>
    [Alias("GCR2dr")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GCRepeat2dr(ExBPY difficulty, ExBPY wait, ExBPY times, ExBPRV2 rpp, GenCtxProperty[] props, SyncPattern[] target) =>
        GCRepeat(new GenCtxProperties<AsyncPattern>(props.Append(GenCtxProperty.AsyncDR(difficulty, wait, times, rpp))), target);

    [Alias("GCRf")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GCRepeatFRV2(GCXF<float> wait, GCXF<float> times, GCXF<V2RV2> frv2, GenCtxProperty[] props, SyncPattern[] target) =>
        GCRepeat(new GenCtxProperties<AsyncPattern>(props.Append(GenCtxProperty.WT(wait, times)).Append(GenCtxProperty.FRV2(frv2))), target);
    
    /// <summary>
    /// Like GCRepeat, but has specific handling for the WAIT, FOR, and rpp properties (times is set to infinity).
    /// </summary>
    /// <param name="wait">Frames to wait between invocations</param>
    /// <param name="forTime">Maximum length of time to run these invocations for</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child SyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GCR3")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GCRepeat3(GCXF<float> wait, GCXF<float> forTime, GCXF<V2RV2> rpp, GenCtxProperty[] props, SyncPattern[] target) =>
        GCRepeat(new GenCtxProperties<AsyncPattern>(props.Append(GenCtxProperty.AsyncFor(wait, forTime, rpp))), target);

    private static AsyncPattern _AsGCR(SyncPattern target, params GenCtxProperty[] props) =>
        _AsGCR(new[] {target}, props);
    private static AsyncPattern _AsGCR(SyncPattern target, GenCtxProperty[] props1, params GenCtxProperty[] props) =>
        _AsGCR(new[] {target}, props1, props);
    private static AsyncPattern _AsGCR(SyncPattern[] target, GenCtxProperty[] props1, params GenCtxProperty[] props) =>
        GCRepeat(new GenCtxProperties<AsyncPattern>(props1.Extend(props)), target);
    private static AsyncPattern _AsGCR(SyncPattern[] target, params GenCtxProperty[] props) =>
        GCRepeat(new GenCtxProperties<AsyncPattern>(props), target);
    // WARNING!!!
    // All non-passthrough async functions should have 
    //     if (abh.Cancelled) { abh.done(); yield break; }
    // as their first line.

    /*
     * COROUTINE FUNCTIONS
     */

    /// <summary>
    /// Run only one of the provided patterns, using the indexer function to determine which.
    /// </summary>
    public static AsyncPattern Alternate(GCXF<float> indexer, AsyncPattern[] aps) => new(abh =>
        aps[(int)indexer(abh.ch.gcx) % aps.Length].Run(abh));

    /// <summary>
    /// Execute the child SyncPattern once.
    /// </summary>
    /// <param name="target">Child SyncPattern to run unchanged</param>
    /// <returns></returns>
    [Fallthrough]
    public static AsyncPattern COnce(SyncPattern target) {
        IEnumerator Inner(AsyncHandoff abh) {
            if (!abh.Cancelled) {
                var itrSBH = new SyncHandoff(abh.ch, 0);
                target.Run(itrSBH);
            }
            abh.Done();
            yield break;
        }
        return new(Inner);
    }


    /// <summary>
    /// Delay a synchronous invokee by a given number of frames.
    /// </summary>
    /// <param name="delay">Frame delay</param>
    /// <param name="next">Synchronous invokee to delay</param>
    /// <returns></returns>
    public static AsyncPattern CDelay(GCXF<float> delay, SyncPattern next) => _AsGCR(next, GenCtxProperty.Delay(delay));
}

//IEnum nesting funcs here
//Please prefix all functions with "I"
//Use yield return where possible
//Cancellation checks at the beginning of nesting patterns may be unnecessary, but is good hygiene.
public static partial class AsyncPatterns {

    /// <summary>
    /// The generic I-level repeater function.
    /// Takes any number of functionality-modifying properties as an array.
    /// </summary>
    /// <param name="props">Array of properties</param>
    /// <param name="target">Child AsyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GIR")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GIRepeat(GenCtxProperties<AsyncPattern> props, AsyncPattern[] target) {
        IEnumerator Inner(AsyncHandoff abh) {
            IPExecutionTracker tracker = new(props, abh, out bool isClipped);
            if (isClipped) {
                tracker.AllADone(false, false);
                yield break;
            }
            if (tracker.CleanupIfCancelled()) yield break;
            for (tracker.StartInitialDelay(); tracker.IsWaiting; tracker.WaitStep()) {
                yield return null;
                if (tracker.CleanupIfCancelled()) yield break;
            }
            while (tracker.RemainsExceptLast && tracker.PrepareIteration()) { 
                tracker.DoAIteration(target);
                for (tracker.StartWait(); tracker.IsWaiting; tracker.WaitStep()) {
                    yield return null;
                    if (tracker.CleanupIfCancelled()) yield break;
                }
                tracker.FinishIteration();
            }
            if (tracker.PrepareLastIteration()) {
                //FinishIteration is hoisted into the callback
                tracker.DoLastAIteration(target);
            } else tracker.AllADone(false, true);
        }
        return new(Inner);
    }
    
    /// <summary>
    /// Like GIRepeat, but has specific handling for the WAIT, TIMES, and rpp properties.
    /// </summary>
    /// <param name="wait">Frames to wait between invocations</param>
    /// <param name="times">Number of invocations</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child AsyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GIR2")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GIRepeat2(GCXF<float> wait, GCXF<float> times, GCXF<V2RV2> rpp, GenCtxProperty[] props, AsyncPattern[] target) =>
        GIRepeat(new GenCtxProperties<AsyncPattern>(props.Append(GenCtxProperty.Async(wait, times, rpp))), target);
    
    /// <summary>
    /// Like GIRepeat, but has specific handling for the WAIT, TIMES, and rpp properties,
    /// where WAIT and TIMES are mutated by the difficulty reference (wait / difficulty, times * difficulty)
    /// </summary>
    /// <param name="difficulty">Difficulty multiplier</param>
    /// <param name="wait">Frames to wait between invocations</param>
    /// <param name="times">Number of invocations</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child AsyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GIR2d")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GIRepeat2d(ExBPY difficulty, ExBPY wait, ExBPY times, GCXF<V2RV2> rpp, GenCtxProperty[] props, AsyncPattern[] target) =>
        GIRepeat(new GenCtxProperties<AsyncPattern>(props.Append(GenCtxProperty.AsyncD(difficulty, wait, times, rpp))), target);
    
    /// <summary>
    /// Like GIRepeat, but has specific handling for the WAIT, FOR, and rpp properties (times is set to infinity).
    /// </summary>
    /// <param name="wait">Frames to wait between invocations</param>
    /// <param name="forTime">Maximum length of time to run these invocations for</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child AsyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GIR3")]
    [CreatesInternalScope((int)AutoVarMethod.GenCtx)]
    public static AsyncPattern GIRepeat3(GCXF<float> wait, GCXF<float> forTime, GCXF<V2RV2> rpp, GenCtxProperty[] props, AsyncPattern[] target) =>
        GIRepeat(new GenCtxProperties<AsyncPattern>(props.Append(GenCtxProperty.AsyncFor(wait, forTime, rpp))), target);
    private static AsyncPattern _AsGIR(AsyncPattern target, params GenCtxProperty[] props) =>
        _AsGIR(new[] {target}, props);
    private static AsyncPattern _AsGIR(AsyncPattern[] target, params GenCtxProperty[] props) =>
        GIRepeat(new GenCtxProperties<AsyncPattern>(props), target);
    
    public static AsyncPattern IColor(string color, AsyncPattern ap) => _AsGIR(ap, GCP.Color(new[] {color}));
    
    public static AsyncPattern ISetP(GCXF<float> p, AsyncPattern ap) => _AsGIR(ap, GCP.SetP(p));

    // The following functions have NOT been ported to _AsGIR. Most of them should be OK as is.
    

    //Pass-through IFunctions don't need inner ienums, and they don't need cancellation checks (unless they perform
    // nontrivial operations.)

    //But this one does. GetExecForID can't be called immediately, in case the beh is created on the same frame.
    //This holds primarily due to EMLaser, which doesn't construct the node until coroutine execution time.
    /// <summary>
    /// Any firees will be assigned the transform parent with the given BehaviorEntity ID.
    /// <br/>Currently, this only works for lasers.
    /// </summary>
    /// <param name="behid">BehaviorEntity ID</param>
    /// <param name="next">Asynchronous invokee to modify</param>
    /// <returns></returns>
    public static AsyncPattern IParent(string behid, AsyncPattern next) {
        IEnumerator Inner(AsyncHandoff abh) {
            if (abh.Cancelled) { abh.Done(); yield break; }
            abh.ch.bc.transformParent = (behid == "this") ? abh.ch.gcx.exec : BehaviorEntity.GetExecForID(behid);
            yield return next.Run(abh);
        }
        return new(Inner);
    }
    
    /// <summary>
    /// Run arbitrary code as an AsyncPattern.
    /// <br/>Note: This is reflected via <see cref="SM.SMReflection.Exec"/>.
    /// </summary>
    [DontReflect]
    public static AsyncPattern Exec(ErasedGCXF code) {
        IEnumerator Inner(AsyncHandoff abh) {
            if (abh.Cancelled) { abh.Done(); yield break; }
            code(abh.ch.gcx);
            abh.Done();
        }
        return new(Inner);
    }

    /// <summary>
    /// Run some code that returns an AsyncPattern, and then execute that AsyncPattern.
    /// </summary>
    [BDSL2Only]
    public static AsyncPattern Wrap(GCXF<AsyncPattern> code) {
        IEnumerator Inner(AsyncHandoff abh) {
            if (abh.Cancelled) { abh.Done(); yield break; }
            var inner = code(abh.ch.gcx);
            yield return inner.Run(abh);
            //The created AP has a mirrored envframe on it; since we are no longer using the AP, we should free the EF.
            //If we were to assign or return the AP, then we'd have to keep the EF alive.
            inner.EnvFrame?.Free();
            inner.EnvFrame = null;
        }
        return new(Inner);
    }


    /// <summary>
    /// Saves the current location of the executing parent so all bullets fired will fire from
    /// the saved position.
    /// </summary>
    /// <param name="next">Asynchronous invokee to modify</param>
    /// <returns></returns>
    public static AsyncPattern ICacheLoc(AsyncPattern next) {
        return new(abh => {
            abh.ch.bc.CacheLoc();
            return next.Run(abh);
        });
    }
}

}