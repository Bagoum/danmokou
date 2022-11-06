using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.SM.Parsing;
using Mizuhashi;
using UnityEngine;
using UnityEngine.Profiling;
using static BagoumLib.Tasks.WaitingUtils;

namespace Danmokou.SM {
public class SMContext {
    public virtual List<IDisposable> PhaseObjects { get; } = new();

    public virtual void CleanupObjects() {
        foreach (var t in PhaseObjects)
            t.Dispose();
        PhaseObjects.Clear();
    }
}
public class PatternContext : SMContext {
    public PatternSM SM { get; }
    public PatternProperties Props { get; }
    
    public PatternContext(PatternSM sm) {
        SM = sm;
        Props = sm.Props;
    }
}
public class PhaseContext : SMContext {
    public PatternContext? Pattern { get; }
    public int Index { get; }
    public PhaseProperties Props { get; }

    public int? BossPhotoHP => Boss == null ? null : Props.photoHP;
    public BossConfig? Boss => Pattern?.Props.boss;
    public PhaseType? PhaseType => Props.phaseType;
    public GameObject? Background => Props.Background != null ? Props.Background :
        (Props.phaseType.Try(out var pt) && Boss != null) ? Boss.Background(pt) : null;
    public SOBgTransition? BgTransitionIn => Props.BgTransitionIn != null ? Props.BgTransitionIn :
        (Props.phaseType.Try(out var pt) && Boss != null) ? Boss.IntoTransition(pt) : null;
    public SOBgTransition? BgTransitionOut => Props.BgTransitionOut;


    public PhaseContext(PatternContext? pattern, int index, PhaseProperties props) {
        Pattern = pattern;
        Index = index;
        Props = props;
    }
    
    public bool GetSpellCutin(out GameObject go) {
        go = null!;
        if (Boss != null) {
            if ((Props.spellCutinIndex ?? ((Props.phaseType?.IsSpell() == true) ? (int?)0 : null)).Try(out var index)) {
                return Boss.spellCutins.Try(index, out go);
            }
        }
        return false;
    }

}

/// <summary>
/// A SM context that does not cleanup objects when it is disposed.
/// </summary>
public class DerivedSMContext : SMContext {
    private readonly SMContext parent;
    public override List<IDisposable> PhaseObjects => parent.PhaseObjects;

    public DerivedSMContext(SMContext parent) {
        this.parent = parent;
    }
    
    public override void CleanupObjects() { }
}

/// <summary>
/// A struct containing information for StateMachine execution.
/// <br/>The caller is responsible for disposing this.
/// </summary>
public readonly struct SMHandoff : IDisposable {
    /// <summary>
    /// RunTryPrepend should delegate to Append unless within a GTR scope.
    /// </summary>
    public bool CanPrepend { get; }
    public SMContext Context { get; }
    
    public readonly CommonHandoff ch;
    public readonly ICancellee parentCT;
    public ICancellee cT => ch.cT;
    public GenCtx GCX => ch.gcx;
    public BehaviorEntity Exec => GCX.exec;
    public bool Cancelled => ch.cT.Cancelled;

    public void ThrowIfCancelled() => ch.cT.ThrowIfCancelled();

    public SMHandoff(BehaviorEntity exec, ICancellee? cT = null, int? index = null) {
        var gcx = GenCtx.New(exec, V2RV2.Zero);
        this.ch = new CommonHandoff(cT ?? Cancellable.Null, null, gcx);
        gcx.Dispose();
        ch.gcx.index = index.GetValueOrDefault(exec.rBPI.index);
        parentCT = Cancellable.Null;
        CanPrepend = false;
        Context = new SMContext();
    }

    public SMHandoff(BehaviorEntity exec, SMRunner smr, ICancellee? cT = null) {
        var gcx = smr.NewGCX ?? GenCtx.New(exec, V2RV2.Zero);
        gcx.OverrideScope(exec, V2RV2.Zero, exec.rBPI.index);
        this.ch = new CommonHandoff(cT ?? smr.cT, null, gcx);
        gcx.Dispose();
        this.parentCT = smr.cT;
        CanPrepend = false;
        Context = new SMContext();
    }

    /// <summary>
    /// Derive an SMHandoff from a parent for localized execution.
    /// <br/>The common handoff is copied.
    /// </summary>
    public SMHandoff(SMHandoff parent, ICancellee newCT) {
        this.ch = new CommonHandoff(newCT, parent.ch.bc, parent.ch.gcx.Copy());
        parentCT = parent.parentCT;
        CanPrepend = parent.CanPrepend;
        Context = new DerivedSMContext(parent.Context);
    }

    /// <summary>
    /// Derive an SMHandoff from a parent for localized execution.
    /// <br/>The common handoff is copied.
    /// </summary>
    public SMHandoff(SMHandoff parent, CommonHandoff ch, SMContext? context, bool? canPrepend = null) {
        this.ch = ch.Copy();
        parentCT = parent.parentCT;
        CanPrepend = canPrepend ?? parent.CanPrepend;
        Context = context ?? new DerivedSMContext(parent.Context);
    }

    /// <summary>
    /// Derive a joint-token SMHandoff.
    /// </summary>
    private SMHandoff(SMHandoff parent, SMContext? context, out Cancellable cts) {
        this.parentCT = parent.parentCT;
        this.ch = new CommonHandoff(
            new JointCancellee(parent.cT, cts = new Cancellable()), parent.ch.bc, parent.ch.gcx);
        CanPrepend = parent.CanPrepend;
        Context = context ?? new DerivedSMContext(parent.Context);
    }

    public SMHandoff CreateJointCancellee(out Cancellable cts, SMContext? innerContext) => 
        new(this, innerContext, out cts);

    public void Dispose() {
        ch.Dispose();
        Context.CleanupObjects();
    }

    public void RunRIEnumerator(IEnumerator cor) => Exec.RunRIEnumerator(cor);
    public void RunTryPrependRIEnumerator(IEnumerator cor) {
        if (CanPrepend) Exec.RunTryPrependRIEnumerator(cor);
        else RunRIEnumerator(cor);
    }
}
// WARNING: StateMachines must NOT store any state. As in, you must be able to call the same SM twice concurrently,
// and it should run twice without interfering.
public abstract class StateMachine {
    #region InitStuff

    public static readonly Dictionary<string, Type> SMInitMap = new() {
        {"pattern", typeof(PatternSM)},
        {"phase", typeof(PhaseSM)},
        {"phased", typeof(DialoguePhaseSM)},
        {"phasej", typeof(PhaseJSM)},
        {"finish", typeof(FinishPSM)},
        {"paction", typeof(PhaseParallelActionSM)},
        {"saction", typeof(PhaseSequentialActionSM)},
        {"end", typeof(EndPSM)},
        //{"bpat", typeof(BulletPatternLASM)},
        {"event", typeof(EventLASM)},
        {"anim", typeof(AnimatorControllerLASM)},
        {"@", typeof(RetargetUSM)},
        {"~", typeof(NoBlockUSM)},
        {"break", typeof(BreakSM)},
        {"timer", typeof(TimerControllerLASM)},
        {"gtr", typeof(GTRepeat)},
        {"gtrepeat", typeof(GTRepeat)},
        {"gtr2", typeof(GTRepeat2)},
        {"if", typeof(IfUSM)},
        {"script", typeof(ScriptTSM)},
        {"debugf", typeof(DebugFloat)}
    };
    public static readonly Dictionary<Type, Type[]> SMChildMap = new() {
        {typeof(PatternSM), new[] { typeof(PhaseSM)}}, {
            typeof(PhaseSM), new[] {
                typeof(PhaseParallelActionSM), typeof(PhaseSequentialActionSM), typeof(EndPSM), typeof(FinishPSM),
                typeof(UniversalSM)
            }
        },
        {typeof(PhaseParallelActionSM), new[] {typeof(LineActionSM), typeof(UniversalSM)}},
        {typeof(PhaseSequentialActionSM), new[] {typeof(LineActionSM), typeof(UniversalSM)}},
        {typeof(EndPSM), new[] {typeof(LineActionSM), typeof(UniversalSM)}},
        {typeof(ScriptTSM), new[] {typeof(ScriptLineSM)}}
    };

    #endregion
    public static bool CheckCreatableChild(Type myType, Type childType) {
        var childMapper = myType;
        while (!SMChildMap.ContainsKey(childMapper)) {
            if (childMapper.BaseType is not {} bt)
                throw new StaticException($"Could not verify base type for {myType.RName()}");
            childMapper = bt;
        }
        Type[] allowedTypes = SMChildMap[childMapper];
        for (int ii = 0; ii < allowedTypes.Length; ++ii) {
            if (childType == allowedTypes[ii] || childType.IsSubclassOf(allowedTypes[ii])) return true;
        }
        return false;
    }

    private enum SMConstruction {
        CONSTRUCTOR,
        ANY,
        AS_REFLECTABLE,
        AS_TREFLECTABLE
    }
    private static Either<SMConstruction, ReflectionException> CheckCreatableChild(Type myType, SMParser.ParsedUnit.Str unit) {
        var cname = unit.Item;
        if (!SMInitMap.TryGetValue(cname, out var childType)) {
            ReflectionException? defltErr = null;
            if (Reflector.TryGetSignature<TTaskPattern>(cname) is {} mscr) {
                if (CheckCreatableChild(myType, typeof(ReflectableSLSM))) 
                    return SMConstruction.AS_TREFLECTABLE;
                defltErr = new ReflectionException(unit.Position,
                    $"State machine {mscr.SimpleName} is a dialogue-script {nameof(TTaskPattern)}," +
                    $" which is not allowed to be a child of {myType.SimpRName()}.");
            }
            if (Reflector.TryGetSignature<TaskPattern>(cname) is {} mrefl) {
                 if (CheckCreatableChild(myType, typeof(ReflectableLASM)))
                     return SMConstruction.AS_REFLECTABLE;
                 defltErr = new ReflectionException(unit.Position,
                     $"State machine {mrefl.SimpleName} is a {nameof(TaskPattern)}," +
                     $" which is not allowed to be a child of {myType.SimpRName()}.");
            }
            return defltErr ??
                   new ReflectionException(unit.Position, $"No state machine function found by name '{cname}'.");
        } else if (CheckCreatableChild(myType, childType)) {
            return SMConstruction.CONSTRUCTOR;
        } else {
            return new ReflectionException(unit.Position,
                $"State machine {childType.SimpRName()}/{cname} is not allowed to be a child of {myType.SimpRName()}.");
        }
    }
    /*
    private static List<StateMachine> CreateChildren(Type? myType, IParseQueue q, int childCt = -1) {
        var children = new List<StateMachine>();
        SMConstruction childType;
        while (childCt-- != 0 && !q.Empty && 
               (childType = CheckCreatableChild(myType, q.ScanNonProperty())) != SMConstruction.ILLEGAL) {
            StateMachine newsm = Create(q.NextChild(), childType);
            if (!q.IsNewlineOrEmpty) throw new Exception(
                $"{q.GetLastPosition()}: Expected a newline, but found \"{q.Print()}\".");
            children.Add(newsm);
            if (newsm is BreakSM) {
                break;
            }
        }
        return children;
    }*/

    public static readonly Type SMChildStatesType = typeof(List<StateMachine>);
    private static readonly Type stateTyp = typeof(StateMachine);
    private static readonly Dictionary<Type, Type[]> constructorSigs = new();

    public static IAST<StateMachine> Create(string name, object?[] args) {
        var method = SMConstruction.ANY;
        var sig = GetSignature(default, name, ref method, out _);
        return Create(default, default, method, sig, 
            args.Select(x => (IAST)new AST.Preconstructed<object?>(default, x)).ToArray());
    }

    private static Reflector.MethodSignature GetSignature(PositionRange loc, string name, ref SMConstruction method, out Type myType) {
        if (!SMInitMap.TryGetValue(name, out myType)) {
            Reflector.MethodSignature? prms;
            if (method == SMConstruction.AS_TREFLECTABLE || method == SMConstruction.ANY) {
                if ((prms = Reflector.TryGetSignature<TTaskPattern>(name)) != null) {
                    method = SMConstruction.AS_TREFLECTABLE;
                    myType = typeof(ReflectableSLSM);
                    return prms;
                }
            }
            if (method == SMConstruction.AS_REFLECTABLE || method == SMConstruction.ANY) {
                if ((prms = Reflector.TryGetSignature<TaskPattern>(name)) != null) {
                    method = SMConstruction.AS_REFLECTABLE;
                    myType = typeof(ReflectableLASM);
                    return prms;
                }
            }
        } else 
             return Reflector.GetConstructorSignature(myType) with { CalledAs = name };
        throw new ReflectionException(loc, $"{name} is not a StateMachine or applicable auto-reflectable.");
    }

    
    private static IAST<StateMachine> Create(PositionRange loc, PositionRange callLoc, SMConstruction method, Reflector.MethodSignature sig, IAST[] args, bool parenthesized = false) => method switch {
            SMConstruction.AS_REFLECTABLE =>
            new ASTFmap<TaskPattern, StateMachine>(x => new ReflectableLASM(x),
                new AST.MethodInvoke<TaskPattern>(loc, callLoc, sig, args) 
                    {Type = AST.MethodInvoke.InvokeType.SM, Parenthesized = parenthesized}),
            SMConstruction.AS_TREFLECTABLE =>
            new ASTFmap<TTaskPattern, StateMachine>(x => new ReflectableSLSM(x),
                new AST.MethodInvoke<TTaskPattern>(loc, callLoc, sig, args) 
                    {Type = AST.MethodInvoke.InvokeType.SM, Parenthesized = parenthesized}),
            _ => new AST.MethodInvoke<StateMachine>(loc, callLoc, sig, args) 
                {Type = AST.MethodInvoke.InvokeType.SM, Parenthesized = parenthesized},
        };

    public static IAST<StateMachine> Create(IParseQueue q) => Create(q, SMConstruction.ANY);
    private static IAST<StateMachine> Create(IParseQueue q, SMConstruction method) {
        try {
            MaybeQueueProperties(q);
            var (name, loc) = q.ScanUnit(out int pind);
            var sig = GetSignature(loc, name, ref method, out var myType);
            q.Advance();
            var prms = sig.Params;

            var args = new IAST[prms.Length];
            bool parenthesized = false;
            ReflectionException? argErr = null;
            ReflectionException? extraChildErr = null;
            int? nchildren = 0;
            if (prms.Length > 0) {
                var requires_children = prms[0].Type == SMChildStatesType && !q.Ctx.Props.trueArgumentOrder;
                int special_args_i = (requires_children) ? 1 : 0;
                var fill = Reflector.FillASTArray(args, special_args_i, sig, q);
                argErr = fill.Error;
                parenthesized = fill.Parenthesized;
                if (q.Ctx.QueuedProps.Count > 0)
                    throw q.WrapThrowHighlight(pind,
                        $"StateMachine {q.AsFileLink(sig)} is not allowed to have phase properties.");
                int childCt = -1;
                if (!q.IsNewlineOrEmpty && IsChildCountMarker(q.MaybeScan(), out int ct)) {
                    q.Advance();
                    childCt = ct;
                }
                if (requires_children) {
                    var children = new List<IAST<StateMachine>>();
                    while (childCt-- != 0 && !q.Empty) {
                        var childTypeOrErr = CheckCreatableChild(myType, q.ScanNonProperty());
                        if (childTypeOrErr.TryR(out var err)) {
                            extraChildErr = err;
                            break;
                        }
                        var newsm = Create(q.NextChild(), childTypeOrErr.Left);
                        if (newsm.IsUnsound) {
                            newsm = new AST.Failure<StateMachine>(new(newsm.Position,
                                    $"Failed to construct child #{children.Count + 1} for state machine {sig.SimpleName}."))
                                { Basis = newsm };
                            //error recovery
                            while (!q.IsNewlineOrEmpty)
                                q.Advance();
                        }
                        if (!q.IsNewlineOrEmpty) {
                            q.MaybeGetCurrentUnit(out var i);
                            newsm = new AST.Failure<StateMachine>(q.WrapThrowHighlight(i, 
                                "Expected a newline after constructing a StateMachine.")) { Basis = newsm };
                        }
                        children.Add(newsm);
                        if (newsm is AST.MethodInvoke miAst && miAst.BaseMethod.ReturnType == typeof(BreakSM))
                            break;
                    }
                    nchildren = children.Count;
                    args[0] = new AST.SequenceList<StateMachine>(children.Count > 0 ?
                        children[0].Position.Merge(children[^1].Position) :
                        loc, children);
                }
            }
            //Due to inconsistent ordering of state machine arguments,
            // we have to calculate the bounding position like this
            var smallestStart = loc.Start;
            var largestEnd = loc.End;
            for (int ii = 0; ii < args.Length; ++ii) {
                if (args[ii].Position.Start.Index < smallestStart.Index)
                    smallestStart = args[ii].Position.Start;
                if (args[ii].Position.End.Index > largestEnd.Index)
                    largestEnd = args[ii].Position.End;
            }
            var ast = Create(new PositionRange(smallestStart, largestEnd), loc, method, sig, args, parenthesized);
            if (argErr != null)
                ast = new AST.Failure<StateMachine>(argErr) { Basis = ast };
            else if (q.HasLeftovers(out var qpi)) {
                var childSuffix = nchildren.Try(out var nc) ? $" with {nc} children" : "";
                ast = new AST.Failure<StateMachine>(q.WrapThrowLeftovers(qpi, $"Parsed {myType.SimpRName()}{childSuffix}, but then found extra text (in ≪≫).")) { Basis = ast };
            }
            if (extraChildErr != null) {
                q.Ctx.NonfatalErrors.Add((extraChildErr, typeof(StateMachine)));
            }
            return ast;
        } catch (Exception e) {
            if (e is ReflectionException re)
                return new AST.Failure<StateMachine>(re);
            return new AST.Failure<StateMachine>(new ReflectionException(q.PositionUpToCurrentObject, e.Message, e.InnerException));
        }
    }
    
    private static bool IsChildCountMarker(string? s, out int ct) {
        if (s != null && s[0] == ':') {
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

    private static void MaybeQueueProperties(IParseQueue p) {
        while (p.MaybeScan() == SMParser.PROP_KW) {
            p.Advance();
            var child = p.NextChild();
            var prop = child.IntoAST<PhaseProperty>();
            p.Ctx.QueuedProps.Add(prop);
            if (prop.IsUnsound)
                //error recovery
                while (!p.IsNewlineOrEmpty)
                    p.Advance();
            //Note that newlines are skipped in scan
            if (!p.IsNewline) {
                p.GetCurrentUnit(out int ind);
                throw child.WrapThrowHighlight(ind, "Missing a newline at the end of the property declaration.");
            }
        }
    }

    public static StateMachine CreateFromDump(string dump) {
        using var __ = BakeCodeGenerator.OpenContext(BakeCodeGenerator.CookingContext.KeyType.SM, dump);
        var p = IParseQueue.Lex(dump);
        Profiler.BeginSample("SM AST construction");
        var ast = Create(p);
        Profiler.EndSample();
        foreach (var d in ast.WarnUsage(p.Ctx))
            d.Log();
        if (p.Ctx.ParseEndFailure(p, ast) is { } exc)
            throw exc;
        Profiler.BeginSample("SM AST realization");
        var result = ast.Evaluate(new());
        Profiler.EndSample();
        return result;
    }

    public static List<PhaseProperties> ParsePhases(string dump) {
        using var _ = BakeCodeGenerator.OpenContext(BakeCodeGenerator.CookingContext.KeyType.SM, "phase_" + dump);
        var ps = new List<PhaseProperties>();
        var p = IParseQueue.Lex(dump);
        while (!p.Empty) {
            MaybeQueueProperties(p);
            if (p.Ctx.QueuedProps.Count > 0) {
                ps.Add(new PhaseProperties(p.Ctx.QueuedProps.Select(pp => pp.Evaluate(new())).ToList()));
                p.Ctx.QueuedProps.Clear();
            }
            while (!p.Empty && p.MaybeScan() != SMParser.PROP_KW)
                p.Advance();
        }
        return ps;
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
    public static async Task WaitFor(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity) {
        cT.ThrowIfCancelled();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        if (time < float.Epsilon) return;
        Exec.RunRIEnumerator(WaitFor(time, cT, GetAwaiter(out Task t)));
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
        Exec.RunRIEnumerator(WaitFor(time, cT, GetAwaiter(out Task t)));
        return t;
    }
    /// <summary>
    /// Task style. Will return as soon as the condition is satisfied or cancellation is triggered.
    /// You must check cT.IsCancelled after awaiting this.
    /// </summary>
    /// <returns></returns>
    public static Task WaitForUnchecked(CoroutineRegularUpdater Exec, ICancellee cT, Func<bool> condition) {
        cT.ThrowIfCancelled();
        Exec.RunRIEnumerator(WaitFor(condition, cT, GetAwaiter(out Task t)));
        return t;
    }

    /// <summary>
    /// Outer waiter-- Will not cb if cancelled
    /// </summary>
    public static void WaitThenCB(CoroutineRegularUpdater Exec, ICancellee cT, float time, bool zeroToInfinity,
        Action? cb) {
        cT.ThrowIfCancelled();
        if (zeroToInfinity && time < float.Epsilon) time = float.MaxValue;
        Exec.RunRIEnumerator(WaitFor(time, cT, () => {
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
        Exec.RunRIEnumerator(WaitFor(time, cT, cb));
    }
    /// <summary>
    /// Outer waiter-- Will not cb if cancelled
    /// </summary>
    public static void WaitThenCB(CoroutineRegularUpdater Exec, ICancellee cT, float time, Func<bool> condition,
        Action cb) {
        cT.ThrowIfCancelled();
        Exec.RunRIEnumerator(WaitForBoth(time, condition, cT, () => {
            if (!cT.Cancelled) cb();
        }));
    }
    /// <summary>
    /// Outer waiter-- Will cb if cancelled
    /// </summary>
    public static void WaitThenCBEvenIfCancelled(CoroutineRegularUpdater Exec, ICancellee cT, float time, Func<bool> condition, Action cb) {
        cT.ThrowIfCancelled();
        Exec.RunRIEnumerator(WaitForBoth(time, condition, cT, cb));
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
        Exec.RunRIEnumerator(WaitFor(time, cT, () => {
            if (!cT.Cancelled) toCancel.Cancel();
        }));
    }

    /// <summary>
    /// This must be run on RegularCoroutine.
    /// Inner waiter-- Will cb if cancelled. This is necessary so awaiters can be informed of errors,
    /// specifically the task-style waiter, which would otherwise spin infinitely.
    /// </summary>
    public static IEnumerator WaitFor(float wait_time, ICancellee cT, Action done) {
        for (; wait_time > ETime.FRAME_YIELD && !cT.Cancelled; 
            wait_time -= ETime.FRAME_TIME) yield return null;
        done();
    }
    /// <summary>
    /// Returns when the condition is true
    /// </summary>
    private static IEnumerator WaitFor(Func<bool> condition, ICancellee cT, Action done) {
        while (!condition() && !cT.Cancelled) yield return null;
        done();
    }
    /// <summary>
    /// Returns when the condition is true AND time is up
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
    public ParallelSM(List<StateMachine> states) : base(states) { }

    public override Task Start(SMHandoff smh) =>
        //Minor garbage optimization
        states.Count == 1 ? 
            states[0].Start(smh) :
            //WARNING: Due to how WhenAll works, any child exceptions will only be thrown at the end of execution.
            Task.WhenAll(states.Select(s => s.Start(smh)));
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

    public RetargetUSM(string[] targets, StateMachine state) : base(state) {
        this.targets = targets;
    }

    public static RetargetUSM Retarget(StateMachine state, params string[] targets) => new(targets, state);
    public static RetargetUSM Retarget(TaskPattern state, params string[] targets) => Retarget(new ReflectableLASM(state), targets);

    public override Task Start(SMHandoff smh) {
        var behs = BehaviorEntity.GetExecsForIDs(targets);
        if (behs.Length == 0) {
            Logs.Log($"Retarget operation with targets {string.Join(", ", targets)} found no BEH", level: LogLevel.WARNING);
            return Task.CompletedTask;
        } else if (behs.Length == 1) {
            return behs[0].RunExternalSM(SMRunner.Run(states[0], smh.cT, smh.GCX), false);
        } else {
            return Task.WhenAll(behs.Select(x => x.RunExternalSM(SMRunner.Run(states[0], smh.cT, smh.GCX), false)));
        }
    }
}

/// <summary>
/// `if`: Choose between two SMs based on the predicate.
/// </summary>
public class IfUSM : UniversalSM {
    private readonly GCXF<bool> pred;
    public IfUSM(GCXF<bool> pred, StateMachine iftrue, StateMachine iffalse) : base(new List<StateMachine>() {iftrue, iffalse}) {
        this.pred = pred;
    }
    public override Task Start(SMHandoff smh) {
        return pred(smh.GCX) ? states[0].Start(smh) : states[1].Start(smh);
    }
}

/// <summary>
/// `~`: Run the child SM without blocking.
/// </summary>
public class NoBlockUSM : UniversalSM {
    public NoBlockUSM(StateMachine state) : base(state) { }

    public override Task Start(SMHandoff smh) {
        //The GCX may be disposed by the parent before the child has run (due to early return), so we copy it
        var smh2 = new SMHandoff(smh, smh.ch, null);
        _ = states[0].Start(smh2).ContinueWithSync(smh2.Dispose);
        return Task.CompletedTask;
    }
}
}