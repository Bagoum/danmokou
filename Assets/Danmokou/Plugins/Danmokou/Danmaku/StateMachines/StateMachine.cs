using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Reactive;
using System.Reflection;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using BagoumLib.Unification;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Reflection;
using Danmokou.Reflection2;
using Danmokou.Scriptables;
using Danmokou.SM.Parsing;
using Mizuhashi;
using UnityEngine;
using UnityEngine.Profiling;
using static BagoumLib.Tasks.WaitingUtils;
using AST = Danmokou.Reflection.AST;
using Helpers = Danmokou.Reflection2.Helpers;
using IAST = Danmokou.Reflection.IAST;
using Parser = Danmokou.DMath.Parser;

namespace Danmokou.SM {
public class SMContext {
    public virtual List<IDisposable> PhaseObjects { get; } = new();
    public Checkpoint? LoadCheckpoint { get; init; }

    public virtual void CleanupObjects() {
        PhaseObjects.DisposeAll();
    }
}
public class PatternContext : SMContext {
    public PatternSM SM { get; }
    public PatternProperties Props { get; }
    
    public PatternContext(SMContext parent, PatternSM sm) {
        SM = sm;
        Props = sm.Props;
        LoadCheckpoint = parent.LoadCheckpoint;
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
        LoadCheckpoint = pattern?.LoadCheckpoint;
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

    private DerivedSMContext(SMContext parent) {
        this.parent = parent;
    }

    public static DerivedSMContext DeriveFrom(SMContext parent) =>
        parent as DerivedSMContext ?? new DerivedSMContext(parent);

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
    public ICancellee cT => ch.cT;
    public GenCtx GCX => ch.gcx;
    public BehaviorEntity Exec => GCX.exec;
    public bool Cancelled => ch.cT.Cancelled;

    public void ThrowIfCancelled() => ch.cT.ThrowIfCancelled();

    public void PipeResult(TaskCompletionSource<Unit> target) {
        if (Cancelled)
            target.SetCanceled();
        else
            target.SetResult(default);
    }

    /// <summary>
    /// Create a SMHandoff for top-level direct execution.
    /// The GCX will contain an empty EnvFrame.
    /// </summary>
    public SMHandoff(BehaviorEntity exec, ICancellee? cT = null) {
        this.ch = new CommonHandoff(cT ?? Cancellable.Null, null, GenCtx.New(exec), null);
        ch.gcx.index = exec.rBPI.index;
        CanPrepend = false;
        Context = new SMContext();
    }

    /// <summary>
    /// Create an SMHandoff for top-level execution with <see cref="SMRunner"/>.
    /// </summary>
    public SMHandoff(BehaviorEntity exec, SMRunner smr, ICancellee? cT = null, SMContext? context = null) {
        var gcx = smr.NewGCX(exec);
        gcx.OverrideScope(exec, exec.rBPI.index);
        //TODO envframe should this be V2RV2.zero or null?
        this.ch = new CommonHandoff(cT ?? smr.cT, null, gcx, V2RV2.Zero);
        CanPrepend = false;
        Context = context ?? new SMContext();
    }

    /// <summary>
    /// Derive an SMHandoff from a parent for localized execution with a new cancellation token.
    /// <br/>The common handoff is mirrored.
    /// </summary>
    public SMHandoff(SMHandoff parent, ICancellee newCT) {
        this.ch = new CommonHandoff(newCT, parent.ch.bc, parent.ch.gcx.Mirror(), parent.ch.rv2Override);
        CanPrepend = parent.CanPrepend;
        Context = DerivedSMContext.DeriveFrom(parent.Context);
    }

    /// <summary>
    /// Derive an SMHandoff from a parent for localized execution.
    /// <br/>The common handoff is NOT copied.
    /// Since SM usage is async, you should either mirror or copy it before passing it here.
    /// </summary>
    public SMHandoff(SMHandoff parent, CommonHandoff ch, SMContext? context, bool? canPrepend = null) {
        this.ch = ch;
        CanPrepend = canPrepend ?? parent.CanPrepend;
        Context = context ?? DerivedSMContext.DeriveFrom(parent.Context);
    }

    /// <summary>
    /// Derive an SMHandoff with an override env frame. The env frame is mirrored or copied and this instance
    ///  should be disposed.
    /// </summary>
    public SMHandoff OverrideEnvFrame(EnvFrame? ef) {
        return new(this, ch.OverrideFrame(ef), null);
    }

    /// <summary>
    /// Derive a joint-token SMHandoff. Mirrors the GCX.
    /// </summary>
    private SMHandoff(SMHandoff parent, SMContext? context, out ICancellable cts) {
        this.ch = new CommonHandoff(cts = new JointCancellable(parent.cT), parent.ch.bc, parent.ch.gcx.Mirror(), null);
        CanPrepend = parent.CanPrepend;
        Context = context ?? DerivedSMContext.DeriveFrom(parent.Context);
    }

    public SMHandoff CreateJointCancellee(out ICancellable cts, SMContext? innerContext) => 
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

    public void SetAllVulnerable(IReadOnlyList<Enemy> subbosses, Vulnerability v) {
        if (Exec.isEnemy)
            Exec.Enemy.SetVulnerable(v);
        for (int ii = 0; ii < subbosses.Count; ++ii) {
            subbosses[ii].SetVulnerable(v);
        }
    }
}

// WARNING: StateMachines must NOT store any state. As in, you must be able to call the same SM twice concurrently,
// and it should run twice without interfering.
public abstract class StateMachine {
    #region InitStuff

    public static readonly Dictionary<string, Type> SMInitMap = new(StringComparer.OrdinalIgnoreCase) {
        {"pattern", typeof(PatternSM)},
        {"phase", typeof(PhaseSM)},
        {"phased", typeof(DialoguePhaseSM)},
        {"phasej", typeof(PhaseJSM)},
        {"paction", typeof(PhaseParallelActionSM)},
        {"saction", typeof(PhaseSequentialActionSM)},
        {"end", typeof(EndPSM)},
        //{"bpat", typeof(BulletPatternLASM)},
        {"event", typeof(EventLASM)},
        {"anim", typeof(AnimatorControllerLASM)},
        {"collide", typeof(BxBCollideLASM)},
        {"@", typeof(RetargetUSM)},
        {"~", typeof(NoBlockUSM)},
        {"nb", typeof(NoBlockUSM)},
        {"break", typeof(BreakSM)},
        {"timer", typeof(TimerControllerLASM)},
        {"gtr", typeof(GTRepeat)},
        {"gtrepeat", typeof(GTRepeat)},
        {"gtr2", typeof(GTRepeat2)},
        {"alternate", typeof(AlternateUSM)},
        {"if", typeof(IfUSM)},
        {"script", typeof(ScriptTSM)},
        {"debugf", typeof(DebugFloat)}
    };
    public static readonly Dictionary<string, List<MethodSignature>> SMInitMethodMap;
    public static readonly Dictionary<Type, Type[]> SMChildMap = new() {
        {typeof(PatternSM), new[] { typeof(PhaseSM)}}, {
            typeof(PhaseSM), new[] {
                typeof(PhaseParallelActionSM), typeof(PhaseSequentialActionSM), typeof(EndPSM),
                typeof(UniversalSM)
            }
        },
        {typeof(PhaseParallelActionSM), new[] {typeof(LineActionSM), typeof(UniversalSM)}},
        {typeof(PhaseSequentialActionSM), new[] {typeof(LineActionSM), typeof(UniversalSM)}},
        {typeof(EndPSM), new[] {typeof(LineActionSM), typeof(UniversalSM)}},
        {typeof(ScriptTSM), new[] {typeof(ScriptLineSM)}}
    };

    static StateMachine() {
        SMInitMethodMap = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, t) in SMInitMap) {
            SMInitMethodMap[k] = new() { Reflector.GetConstructorSignature(t) };
        }
    }

    #endregion
    public static bool CheckCreatableChild(Type myType, Type childType) {
        var childMapper = myType;
        while (!SMChildMap.ContainsKey(childMapper)) {
            if (childMapper.BaseType is not {} bt)
                throw new StaticException($"Could not verify base type for {myType.SimpRName()}");
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
            if (Reflector.TryGetSignature<ReflectableLASM>(cname) is {} mrefl) {
                 if (CheckCreatableChild(myType, typeof(ReflectableLASM)))
                     return SMConstruction.AS_REFLECTABLE;
                 defltErr = new ReflectionException(unit.Position,
                     $"State machine {mrefl.SimpleName} is a {nameof(ReflectableLASM)}," +
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

    private static readonly Dictionary<Type, Type[]> constructorSigs = new();

    public static IAST<StateMachine> Create(string name, object?[] args) {
        var method = SMConstruction.ANY;
        var sig = GetSignature(default, name, ref method, out _);
        return Create(default, default, method, sig, 
            args.Select(x => (IAST)new AST.Preconstructed<object?>(default, x)).ToArray());
    }

    private static Reflector.InvokedMethod GetSignature(PositionRange loc, string name, ref SMConstruction method, out Type myType) {
        if (!SMInitMap.TryGetValue(name, out myType)) {
            Reflector.InvokedMethod? prms;
            if (method == SMConstruction.AS_TREFLECTABLE || method == SMConstruction.ANY) {
                if ((prms = Reflector.TryGetSignature<TTaskPattern>(name)) != null) {
                    method = SMConstruction.AS_TREFLECTABLE;
                    myType = typeof(ReflectableSLSM);
                    return prms;
                }
            }
            if (method == SMConstruction.AS_REFLECTABLE || method == SMConstruction.ANY) {
                if ((prms = Reflector.TryGetSignature<ReflectableLASM>(name)) != null) {
                    method = SMConstruction.AS_REFLECTABLE;
                    myType = typeof(ReflectableLASM);
                    return prms;
                }
            }
        } else 
             return Reflector.GetConstructorSignature(myType).Call(name);
        throw new ReflectionException(loc, $"{name} is not a StateMachine or applicable auto-reflectable.");
    }

    
    private static IAST<StateMachine> Create(PositionRange loc, PositionRange callLoc, SMConstruction method, Reflector.InvokedMethod sig, IAST[] args, bool parenthesized = false) => method switch {
            SMConstruction.AS_REFLECTABLE =>
                new AST.MethodInvoke<StateMachine>(loc, callLoc, sig, args) 
                    {Type = AST.MethodInvoke.InvokeType.SM, Parenthesized = parenthesized},
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
                var requires_children = prms[^1].BDSL1ImplicitSMList;
                var fill = Reflector.FillASTArray(args, 0, args.Length - (requires_children ? 1 : 0), sig, q);
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
                    var children = new List<IAST>();
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
                        if (newsm is AST.MethodInvoke miAst && miAst.BaseMethod.Mi.ReturnType == typeof(BreakSM))
                            break;
                    }
                    nchildren = children.Count;
                    args[^1] = new AST.SequenceArray(children.Count > 0 ?
                        children[0].Position.Merge(children[^1].Position) :
                        loc, typeof(StateMachine), children.ToArray());
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

    public static StateMachine CreateFromDump(string dump) => CreateFromDump(dump, out _);
    public static StateMachine CreateFromDump(string dump, out EnvFrame scriptFrame) {
        using var _ = BakeCodeGenerator.OpenContext(BakeCodeGenerator.CookingContext.KeyType.SM, dump);
        if (!dump.TrimStart().StartsWith("<#>")) {
            Profiler.BeginSample("SM AST (BDSL2) Parsing/Compilation");
            var (sm, ef) = Helpers.ParseAndCompileValue<StateMachine>(dump);
            Profiler.EndSample();
            scriptFrame = ef;
            return sm;
        } else {
            var p = IParseQueue.Lex(dump);
            Profiler.BeginSample("SM AST construction");
            var ast = Create(p);
            Profiler.EndSample();
            foreach (var d in ast.WarnUsage(p.Ctx))
                d.Log();
            if (p.Ctx.ParseEndFailure(p, ast) is { } exc)
                throw exc;
            var rootScope = LexicalScope.NewTopLevelScope();
            using var __ = new LexicalScope.ParsingScope(rootScope);
            ast.AttachLexicalScope(rootScope);
            if (rootScope.FinalizeVariableTypes(Unifier.Empty).TryR(out var err))
                throw Reflection2.IAST.EnrichError(err);
            Profiler.BeginSample("SM AST realization");
            var result = ast.Evaluate();
            result = EnvFrameAttacher.AttachSM(result, scriptFrame = EnvFrame.Create(rootScope, null));
            Profiler.EndSample();
            return result;
        }
    }
    

    public static List<PhaseProperties> ParsePhases(string dump) {
        using var _ = BakeCodeGenerator.OpenContext(BakeCodeGenerator.CookingContext.KeyType.SM, "phase_" + dump);
        var ps = new List<PhaseProperties>();
        var p = IParseQueue.Lex(dump);
        while (!p.Empty) {
            MaybeQueueProperties(p);
            if (p.Ctx.QueuedProps.Count > 0) {
                ps.Add(new PhaseProperties(p.Ctx.QueuedProps.Select(pp => pp.Evaluate()).ToList()));
                p.Ctx.QueuedProps.Clear();
            }
            while (!p.Empty && p.MaybeScan() != SMParser.PROP_KW)
                p.Advance();
        }
        return ps;
    }

    protected StateMachine(params StateMachine[] states) {
        this.states = states;
    }
    
    protected readonly StateMachine[] states;
    public LexicalScope Scope { get; set; } = DMKScope.Singleton;

    public abstract Task Start(SMHandoff smh);
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
    public SequentialSM(StateMachine[] states) : base(states) {}

    public override async Task Start(SMHandoff smh) {
        for (int ii = 0; ii < states.Length; ++ii) {
            await states[ii].Start(smh);
            smh.ThrowIfCancelled();
        }
    }
}

public class ParallelSM : StateMachine {
    public ParallelSM(StateMachine[] states) : base(states) { }

    public override Task Start(SMHandoff smh) {
        //Minor garbage optimization
        if (states.Length == 1) return states[0].Start(smh);
        var tasks = new Task[states.Length];
        for (int ii = 0; ii < states.Length; ++ii)
            tasks[ii] = states[ii].Start(smh);
        //WARNING: Due to how WhenAll works, any child exceptions will only be thrown at the end of execution.
        return TaskHelpers.TaskWhenAll(tasks);
    }
}

public abstract class UniversalSM : StateMachine {
    protected UniversalSM() { }
    public UniversalSM(params StateMachine[] states) : base(states) { }
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
    public IfUSM(GCXF<bool> pred, StateMachine iftrue, StateMachine iffalse) : base(iftrue, iffalse) {
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
        _ = ExecuteBlocking(smh).ContinueWithSync();
        return Task.CompletedTask;
    }

    private async Task ExecuteBlocking(SMHandoff smh) {
        //The GCX may be disposed by the parent before the child has run (due to early return), so we copy it
        using var smh2 = new SMHandoff(smh, smh.ch.Mirror(), null);
        await states[0].Start(smh2);
    }
}
}