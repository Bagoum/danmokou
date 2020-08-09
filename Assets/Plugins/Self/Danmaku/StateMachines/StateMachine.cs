using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Reflection;
using System.Text;
using Danmaku;
using DMath;
using FParser;
using JetBrains.Annotations;
using UnityEngine;
using static Danmaku.Enums;

namespace SM {
public struct SMHandoff : IDisposable {
    public BehaviorEntity Exec {
        get => GCX.exec;
        set => GCX.exec = value;
    }
    public CommonHandoff ch;
    public CancellationToken cT => ch.cT;
    public readonly CancellationToken parentCT;
    public GenCtx GCX => ch.gcx;
    public bool Cancelled => ch.cT.IsCancellationRequested;

    public void ThrowIfCancelled() => ch.cT.ThrowIfCancellationRequested();

    public SMHandoff(BehaviorEntity exec, CancellationToken cT, int? index = null) {
        this.ch = new CommonHandoff(cT, GenCtx.New(exec, V2RV2.Zero));
        ch.gcx.index = index.GetValueOrDefault(exec.rBPI.index);
        parentCT = CancellationToken.None;
    }

    public SMHandoff(BehaviorEntity exec, SMRunner smr, CancellationToken? cT = null) {
        var gcx = smr.NewGCX ?? GenCtx.New(exec, V2RV2.Zero);
        gcx.OverrideScope(exec, V2RV2.Zero, exec.rBPI.index);
        this.ch = new CommonHandoff(cT ?? smr.cT, gcx);
        this.parentCT = smr.cT;
    }

    public void Dispose() => GCX.Dispose();

    public void RunRIEnumerator(IEnumerator cor) => Exec.RunRIEnumerator(cor);
}
// WARNING: StateMachines must NOT store any state. As in, you must be able to call the same SM twice concurrently,
// and it should run twice without interfering.
public abstract class StateMachine {
    #region InitStuff

    private static readonly Dictionary<string, Type> smInitMap = new Dictionary<string, Type>() {
        {"pattern", typeof(PatternSM)},
        {"phase", typeof(PhaseSM)},
        {"phasej", typeof(PhaseJSM)},
        {"finish", typeof(FinishPSM)},
        {"action", typeof(PhaseActionSM)},
        {"saction", typeof(PhaseSequentialActionSM)},
        {"end", typeof(EndPSM)},
        //{"bpat", typeof(BulletPatternLASM)},
        {"event", typeof(EventLASM)},
        {"anim", typeof(AnimatorControllerLASM)},
        {"track", typeof(TrackControlLASM)},
        {"clear", typeof(ClearLASM)},
        {"@", typeof(RetargetUSM)},
        {"~", typeof(NoBlockUSM)},
        {"seq", typeof(SequentialMPSM)},
        {"nbseq", typeof(NBSequentialMPSM)},
        {"break", typeof(BreakSM)},
        {"sprite", typeof(SpriteControlLASM)},
        {"setstate", typeof(SetStateLASM)},
        {"timer", typeof(TimerControllerLASM)},
        //{"debug", typeof(DebugLASM)},
        //{"!err", typeof(ErrorLASM)},
        {"op", typeof(CoroutineLASM)},
        {"gtr", typeof(GTRepeat)},
        {"gtrepeat", typeof(GTRepeat)},
        {"gtr2", typeof(GTRepeat2)},
        {"if", typeof(IfUSM)},
        {"script", typeof(ScriptTSM)},
        {"endcard", typeof(EndcardControllerTSM)},
        {"debugf", typeof(DebugFloat)}
    };
    private static readonly Dictionary<Type, Type[]> smChildMap = new Dictionary<Type, Type[]>() {
        {typeof(PatternSM), new[] { typeof(PhaseSM), typeof(UniversalSM)}}, {
            typeof(PhaseSM), new[] {
                typeof(PhaseActionSM), typeof(PhaseSequentialActionSM), typeof(EndPSM), typeof(FinishPSM),
                typeof(MetaPASM), typeof(UniversalSM)
            }
        },
        {typeof(PhaseActionSM), new[] {typeof(LineActionSM), typeof(UniversalSM)}},
        {typeof(PhaseSequentialActionSM), new[] {typeof(LineActionSM), typeof(UniversalSM)}},
        {typeof(MetaPASM), new[] {typeof(PhaseActionSM), typeof(PhaseSequentialActionSM), typeof(UniversalSM)}},
        {typeof(EndPSM), new[] {typeof(LineActionSM), typeof(UniversalSM)}},
        {typeof(ScriptTSM), new[] {typeof(ScriptLineSM)}}
    };

    #endregion
    private static bool CheckCreatableChild(Type myType, Type childType) {
        while (!smChildMap.ContainsKey(myType)) {
            myType = myType.BaseType;
            if (myType == null) throw new Exception($"Could not verify SM type for {childType.RName()}");
        }
        Type[] allowedTypes = smChildMap[myType];
        for (int ii = 0; ii < allowedTypes.Length; ++ii) {
            if (childType == allowedTypes[ii] || childType.IsSubclassOf(allowedTypes[ii])) return true;
        }
        return false;
    }

    public enum SMConstruction {
        ILLEGAL,
        CONSTRUCTOR,
        ANY,
        AS_REFLECTABLE,
        AS_TREFLECTABLE
    }
    private static SMConstruction CheckCreatableChild(Type myType, string cname, ParsingQueue errP) {
        if (!smInitMap.TryGetValue(cname, out var childType)) {
            if (CheckCreatableChild(myType, typeof(ReflectableSLSM)) &&
                Reflector.LazyLoadAndCheckIfCanReflectExternalSourceType<TaskPattern>(typeof(TSMReflection), cname))
                return SMConstruction.AS_TREFLECTABLE;
            if (CheckCreatableChild(myType, typeof(ReflectableLASM)) &&
                Reflector.LazyLoadAndCheckIfCanReflectExternalSourceType<TaskPattern>(typeof(SMReflection), cname))
                return SMConstruction.AS_REFLECTABLE;
            return SMConstruction.ILLEGAL;
        } else {
            while (!smChildMap.ContainsKey(myType)) {
                myType = myType.BaseType;
                if (myType == null) throw new Exception($"Could not verify SM type for {cname}");
            }
            Type[] allowedTypes = smChildMap[myType];
            for (int ii = 0; ii < allowedTypes.Length; ++ii) {
                if (childType == allowedTypes[ii] || childType.IsSubclassOf(allowedTypes[ii])) return SMConstruction.CONSTRUCTOR;
            }
            return SMConstruction.ILLEGAL;
        }
    }
    private static List<StateMachine> CreateChildren(Type myType, ParsingQueue p, int childCt = -1) {
        var children = new List<StateMachine>();
        SMConstruction childType = SMConstruction.ILLEGAL;
        while (childCt-- != 0 && !p.Empty() && 
               (childType = CheckCreatableChild(myType, p.ScanNonProperty(), p)) != SMConstruction.ILLEGAL) {
            StateMachine newsm = Create(p, childType);
            if (!p.IsNewlineOrEmpty()) throw new Exception(
                $"Line {p.GetLastLine()}: Expected a newline, but found \"{p.PrintLine(p.Index, true)}\".");
            children.Add(newsm);
            if (newsm is BreakSM) {
                break;
            }
        }
        return children;
    }

    private static readonly Type statesTyp = typeof(List<StateMachine>);
    private static readonly Type statesArrTyp = typeof(StateMachine[]);
    private static readonly Type stateTyp = typeof(StateMachine);
    private static readonly Type reflectStartTyp = typeof(TaskPattern);
    private static readonly ISet<Type> specialTypes = new HashSet<Type>() { reflectStartTyp };
    private static readonly Dictionary<Type, Type[]> constructorSigs = new Dictionary<Type, Type[]>();

    public static StateMachine Create(ParsingQueue p, SMConstruction method = SMConstruction.ANY) {
        if (method == SMConstruction.ILLEGAL)
            throw new Exception("Somehow received a Create(ILLEGAL) call in SM. This should not occur.");
        MaybeQueueProperties(p);
        string first = p.Next();
        MethodInfo autoReflectMI = null;
        Type[] prms = null;
        if (!smInitMap.TryGetValue(first, out var myType)) {
            if (method == SMConstruction.AS_TREFLECTABLE || method == SMConstruction.ANY)
                prms = Reflector.LazyLoadAndGetSignature<TaskPattern>(typeof(TSMReflection), first, out autoReflectMI);
            if (prms != null) method = SMConstruction.AS_TREFLECTABLE;
            else if (method == SMConstruction.AS_REFLECTABLE || method == SMConstruction.ANY)
                prms = Reflector.LazyLoadAndGetSignature<TaskPattern>(typeof(SMReflection), first, out autoReflectMI);
            if (prms != null) method = SMConstruction.AS_REFLECTABLE;
            prms = prms ?? throw new Exception($"Line {p.GetLastLine()}: {first} is not a StateMachine or applicable auto-reflectable.");
        } else prms = Reflector.GetConstructorSignature(myType);
        object[] reflect_args = new object[prms.Length];
        if (prms.Length > 0) {
            bool requires_children = prms[0] == statesTyp;
            bool requires_arr_children = prms[0] == statesArrTyp;
            int extra_child_i = (requires_children || requires_arr_children) ? 1 : 0;
            int final_child_i = extra_child_i;
            for (; final_child_i < prms.Length && prms[final_child_i] == stateTyp; ++final_child_i) { }
            int special_args_i = final_child_i;
            for (; special_args_i < prms.Length && specialTypes.Contains(prms[special_args_i]); ++special_args_i) {
                if (prms[special_args_i] == reflectStartTyp) {
                    reflect_args[special_args_i] = Reflector.LazyLoadAndReflectExternalSourceType<TaskPattern>(myType, p);
                } else {
                    throw new Exception($"Line {p.GetLastLine()}: cannot resolve constructor type {prms[special_args_i].Name}");
                }
            }
            Reflector.FillInvokeArray(reflect_args, special_args_i, prms, p, myType ?? typeof(ReflectableLASM), first);
            if (p.queuedProps.Count > 0)
                throw new Exception($"Line {p.GetLastLine()}: StateMachine {first} is not allowed to have properties.");
            int childCt = -1;
            if (!p.IsNewlineOrEmpty()) {
                if (IsChildCountMarker(p.Scan(), out int ct)) {
                    p.Next();
                    childCt = ct;
                } else if (p.Prev() != "}" && !allowSameLine.Contains(first)) { 
                    throw new Exception($"Line {p.GetLastLine()} is missing a newline after the inline arguments.");
                }
            }
            if (requires_children) reflect_args[0] = CreateChildren(myType, p, childCt);
            else if (requires_arr_children) {
                reflect_args[0] = Reflector.ResolveAsArray((pq, _) => Create(pq), typeof(StateMachine), p);
            }
            for (int ii = extra_child_i; ii < final_child_i; ++ii) {
                reflect_args[ii] = Create(p);
                if (!p.IsNewlineOrEmpty()) throw new Exception($"Line {p.GetLastLine()} is missing a newline at the end of the StateMachine.");
            }
        }
        if (autoReflectMI != null) {
            var rs = (TaskPattern)autoReflectMI.Invoke(null, reflect_args);
            if (method == SMConstruction.AS_REFLECTABLE) return new ReflectableLASM(rs);
            return new ReflectableSLSM(rs);
        }
        return (StateMachine) Activator.CreateInstance(myType, reflect_args);
    }

    private static readonly HashSet<string> allowSameLine = new HashSet<string>() { "~", ">>", "_" };

    private static bool IsChildCountMarker(string s, out int ct) {
        if (s[0] == ':') {
            if (Parser.TryFloat(s, 1, s.Length, out float f)) {
                if (Math.Abs(f - Mathf.Floor(f)) < float.Epsilon) {
                    ct = Mathf.FloorToInt(f);
                    return true;
                }
            }
        }
        ct = 0;
        return false;
    }

    private static void MaybeQueueProperties(ParsingQueue p) {
        while (p.Scan() == SMParser.PROP_KW) {
            p.Next();
            p.queuedProps.Add(p.Into<PhaseProperty>());
            if (!p.IsNewline()) throw new Exception($"Line {p.GetLastLine()} is missing a newline at the end of the the property declaration. Instead, it found \"{p.Scan()}\".");
        }
    }

    public static StateMachine CreateFromDump(string dump) {
        using (ParsingQueue p = ParsingQueue.Lex(dump)) {
            return Create(p);
        }
    }

    protected StateMachine(List<StateMachine> states) {
        this.states = states;
    }

    protected StateMachine(params StateMachine[] states) : this(new List<StateMachine>(states)) { }

    protected StateMachine() : this(new List<StateMachine>()) { }
    protected readonly List<StateMachine> states;

    public abstract Task Start(SMHandoff smh);
}

public static class WaitingUtils {
    public static Task WaitFor(SMHandoff smh, float time, bool zeroToInfinity) =>
        WaitFor(smh.Exec, smh.cT, time, zeroToInfinity);
    /// <summary>
    /// Task style-- will throw if cancelled. This checks cT.IsCancelled before returning,
    /// so you do not need to check it after awaiting this.
    /// </summary>
    /// <returns></returns>
    public static async Task WaitFor(BehaviorEntity Exec, CancellationToken cT, float time, bool zeroToInfinity) {
        cT.ThrowIfCancellationRequested();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        if (time < float.Epsilon) return;
        Exec.RunRIEnumerator(WaitFor(time, cT, GetAwaiter(out Task t)));
        await t;
        //I do want this throw here, which is why I don't 'return t'
        cT.ThrowIfCancellationRequested();
    }
    /// <summary>
    /// Task style. Will return as soon as the time is up or cancellation is triggered.
    /// You must check cT.IsCancelled after awaiting this.
    /// </summary>
    /// <returns></returns>
    public static Task WaitForUnchecked(CoroutineRegularUpdater Exec, CancellationToken cT, float time, bool zeroToInfinity) {
        cT.ThrowIfCancellationRequested();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        if (time < float.Epsilon) return Task.CompletedTask;
        Exec.RunRIEnumerator(WaitFor(time, cT, GetAwaiter(out Task t)));
        return t;
    }
    /// <summary>
    /// Task style. Will return as soon as the condition is satisfied or cancellation is triggered.
    /// You must check cT.IsCancelled after awaiting this.
    /// </summary>
    /// <returns></returns>
    public static Task WaitForUnchecked(CoroutineRegularUpdater Exec, CancellationToken cT, Func<bool> condition) {
        cT.ThrowIfCancellationRequested();
        Exec.RunRIEnumerator(WaitFor(condition, cT, GetAwaiter(out Task t)));
        return t;
    }

    /// <summary>
    /// Outer waiter-- Will not cb if cancelled
    /// </summary>
    public static void WaitThenCB(CoroutineRegularUpdater Exec, CancellationToken cT, float time, bool zeroToInfinity,
        Action cb) {
        cT.ThrowIfCancellationRequested();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        Exec.RunRIEnumerator(WaitFor(time, cT, () => {
            if (!cT.IsCancellationRequested) cb();
        }));
    }
    /// <summary>
    /// Outer waiter-- Will cb if cancelled
    /// </summary>
    public static void WaitThenCBEvenIfCancelled(CoroutineRegularUpdater Exec, CancellationToken cT, float time, bool zeroToInfinity,
        Action cb) {
        cT.ThrowIfCancellationRequested();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        Exec.RunRIEnumerator(WaitFor(time, cT, cb));
    }
    /// <summary>
    /// Outer waiter-- Will not cb if cancelled
    /// </summary>
    public static void WaitThenCB(CoroutineRegularUpdater Exec, CancellationToken cT, float time, Func<bool> condition,
        Action cb) {
        cT.ThrowIfCancellationRequested();
        Exec.RunRIEnumerator(WaitForBoth(time, condition, cT, () => {
            if (!cT.IsCancellationRequested) cb();
        }));
    }
    /// <summary>
    /// Outer waiter-- Will cb if cancelled
    /// </summary>
    public static void WaitThenCBEvenIfCancelled(CoroutineRegularUpdater Exec, CancellationToken cT, float time, Func<bool> condition, Action cb) {
        cT.ThrowIfCancellationRequested();
        Exec.RunRIEnumerator(WaitForBoth(time, condition, cT, cb));
    }

    /// <summary>
    /// Outer waiter-- Will not cancel if cancelled
    /// </summary>
    public static void WaitThenCancel(CoroutineRegularUpdater Exec, CancellationToken cT, float time, bool zeroToInfinity,
        CancellationTokenSource toCancel) {
        cT.ThrowIfCancellationRequested();
        if (zeroToInfinity && time < float.Epsilon) {
            time = float.MaxValue;
        }
        Exec.RunRIEnumerator(WaitFor(time, cT, () => {
            if (!cT.IsCancellationRequested) toCancel.Cancel();
        }));
    }

    public static Action GetAwaiter(out Task t) {
        var tcs = new TaskCompletionSource<bool>();
        t = tcs.Task;
        return () => tcs.SetResult(true);
    }
    public static Action GetAwaiter(out Func<bool> t) {
        bool done = false;
        t = () => done;
        return () => done = true;
    }
    public static Action<T> GetAwaiter<T>(out Task<T> t) {
        var tcs = new TaskCompletionSource<T>();
        t = tcs.Task;
        return f => tcs.SetResult(f);
    }
    public static Action GetCondition(out Func<bool> t) {
        bool completed = false;
        t = () => completed;
        return () => completed = true;
    }
    public static Action<T> GetFree1Condition<T>(out Func<bool> t) {
        bool completed = false;
        t = () => completed;
        return _ => completed = true;
    }
    public static Action<T> GetFree1ManyCondition<T>(int ct, out Func<bool> t) {
        int acc = 0;
        t = () => acc == ct;
        return _ => ++acc;
    }
    public static Action GetManyCondition(int ct, out Func<bool> t) {
        int acc = 0;
        t = () => acc == ct;
        return () => ++acc;
    }
    public static Action GetManyCallback(int ct, Action whenAll) {
        int acc = 0;
        return () => {
            if (++acc == ct) whenAll();
        };
    }

    /// <summary>
    /// This must be run on RegularCoroutine.
    /// Inner waiter-- Will cb if cancelled. This is necessary so awaiters can be informed of errors,
    /// specifically the task-style waiter, which would otherwise spin infinitely.
    /// </summary>
    private static IEnumerator WaitFor(float wait_time, CancellationToken cT, Action done) {
        for (; wait_time > ETime.FRAME_YIELD && !cT.IsCancellationRequested; 
            wait_time -= ETime.FRAME_TIME) yield return null;
        done();
    }
    /// <summary>
    /// Returns when the condition is true
    /// </summary>
    private static IEnumerator WaitFor(Func<bool> condition, CancellationToken cT, Action done) {
        while (!condition() && !cT.IsCancellationRequested) yield return null;
        done();
    }
    /// <summary>
    /// Returns when the condition is true AND time is up
    /// </summary>
    private static IEnumerator WaitForBoth(float wait_time, Func<bool> condition, CancellationToken cT, Action done) {
        for (; (wait_time > ETime.FRAME_YIELD || !condition()) && !cT.IsCancellationRequested; 
            wait_time -= ETime.FRAME_TIME) yield return null;
        done();
    }
    public static IEnumerator WaitForConfirm(CancellationToken cT, Action done) {
        while (!cT.IsCancellationRequested) {
            if (InputManager.UIConfirm.Active) break;
            yield return null;
        }
        done();
    }

    public static IEnumerator WaitWhileWithCancellable(Func<bool> amIFinishedWaiting, CancellationTokenSource canceller, Func<bool> cancelIf, CancellationToken cT, Action done) {
        while (!amIFinishedWaiting() && !cT.IsCancellationRequested) {
            if (cancelIf()) {
                canceller.Cancel();
                break;
            }
            yield return null;
        }
        done();
    }
    
}

/// <summary>
/// `break`: Indicates that its parent should take no more children.
/// </summary>
public class BreakSM : UniversalSM {
    public override Task Start(SMHandoff smh) {
        return Task.CompletedTask;
    }
}

public abstract class SequentialSM : StateMachine {
    public SequentialSM(List<StateMachine> states) : base(states) {}

    public override async Task Start(SMHandoff smh) {
        for (int ii = 0; ii < states.Count; ++ii) {
            await states[ii].Start(smh);
            smh.ThrowIfCancelled();
        }
    }
}

public class ParallelSM : StateMachine {
    private readonly Blocking blocking;

    public ParallelSM(List<StateMachine> states, Blocking blocking) : base(states) {
        this.blocking = blocking;
    }

    public override Task Start(SMHandoff smh) {
        if (blocking == Blocking.BLOCKING) {
            //WARNING: Due to how WhenAll works, any child exceptions will only be thrown at the end of execution.
            return Task.WhenAll(states.Select(s => s.Start(smh)));
        } else {
            for (int ii = 0; ii < states.Count; ++ii) {
                _ = states[ii].Start(smh);
            }
            return Task.CompletedTask;
        }
    }
}

public abstract class UniversalSM : StateMachine {
    protected UniversalSM() { }
    protected UniversalSM(StateMachine state) : base(new List<StateMachine>() {state}) { }
    public UniversalSM(List<StateMachine> states) : base(states) { }
}

/// <summary>
/// `@`: Run an SM on another BEH.
/// </summary>
public class RetargetUSM : UniversalSM {
    private readonly string[] targets;

    public RetargetUSM(StateMachine state, string[] targets) : base(state) {
        this.targets = targets;
    }

    public static RetargetUSM Retarget(StateMachine state, params string[] targets) => new RetargetUSM(state, targets);
    public static RetargetUSM Retarget(TaskPattern state, params string[] targets) => Retarget(new ReflectableLASM(state), targets);

    public override Task Start(SMHandoff smh) {
        var behs = BehaviorEntity.GetExecsForIDs(targets);
        if (behs.Length == 0) {
            Log.Unity($"Retarget operation with targets {string.Join(", ", targets)} found no BEH", level: Log.Level.WARNING);
            return Task.CompletedTask;
        } else if (behs.Length == 1) {
            return behs[0].RunExternalSM(SMRunner.Run(states[0], smh.cT, smh.GCX));
        } else {
            return Task.WhenAll(behs.Select(x => x.RunExternalSM(SMRunner.Run(states[0], smh.cT, smh.GCX))));
        }
    }
}

/// <summary>
/// `if`: Choose between two SMs based on the predicate.
/// </summary>
public class IfUSM : UniversalSM {
    private readonly GCXF<bool> pred;
    public IfUSM(StateMachine iftrue, StateMachine iffalse, GCXF<bool> pred) : base(new List<StateMachine>() {iftrue, iffalse}) {
        this.pred = pred;
    }
    public override Task Start(SMHandoff smh) {
        return pred(smh.GCX) ? states[0].Start(smh) : states[1].Start(smh);
    }
}

/// <summary>
/// `seq`: Run a list of `action` or `saction` blocks in sequence.
/// <br/>Note: while I'd like to deprecate this, it's still quite useful for GTR children.
/// </summary>
public class SequentialMPSM : MetaPASM {
    public SequentialMPSM(List<StateMachine> states) : base(states) { }
}

/// <summary>
/// `nbseq`: Run a list of `action` or `saction` blocks in sequence, without blocking.
/// </summary>
public class NBSequentialMPSM : MetaPASM {
    public NBSequentialMPSM(List<StateMachine> states) : base(states) { }

    public override Task Start(SMHandoff smh) {
        _ = base.Start(smh);
        return Task.CompletedTask;
    }
}

/// <summary>
/// `~`: Run the child SM without blocking.
/// </summary>
public class NoBlockUSM : UniversalSM {
    public NoBlockUSM(StateMachine state) : base(state) { }

    public override Task Start(SMHandoff smh) {
        states[0].Start(smh);
        return Task.CompletedTask;
    }
}
}