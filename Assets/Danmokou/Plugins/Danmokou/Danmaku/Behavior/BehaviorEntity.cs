using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using JetBrains.Annotations;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Descriptors;
using Danmokou.Danmaku.Options;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Pooling;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
using UnityEditor;
using UnityEngine.Profiling;

namespace Danmokou.Behavior {
/// <summary>
/// A high-level class that describes all complex danmaku-related objects (primarily bullets and NPCs).
/// </summary>
public partial class BehaviorEntity : Pooled<BehaviorEntity>, ITransformHandler {
    //BEH is only pooled when summoned via the firing API.
    private static readonly Dictionary<string, HashSet<BehaviorEntity>> idLookup = new();

    [Tooltip("This must be null for pooled BEH.")]
    public TextAsset? behaviorScript;
    /// <summary>
    /// ID to refer to this entity in behavior scripts.
    /// </summary>
    public string ID = "";
    
    /// <summary>
    /// Components such as <see cref="Enemy"/> which have a separate execution loop
    ///  but depend on this for enable/disable calls.
    /// <br/>Components should call <see cref="LinkDependentUpdater"/> in their Awake functions.
    /// </summary>
    private readonly List<IBehaviorEntityDependent> dependentComponents = new();
    
    public SMPhaseController phaseController;
    //This is automatically disposed by the state machine that generates it
    public ICancellable? PhaseShifter { get; set; }
    private readonly HashSet<Cancellable> behaviorToken = new();
    public int NumRunningSMs => behaviorToken.Count;
    
    /// <summary>
    /// The angle at which this entity was originally fired.
    /// </summary>
    public float OriginalAngle { get; protected set; }
    /// <summary>
    /// Last movement delta of the entity. Zero if no movement occurred.
    /// </summary>
    public Vector2 LastDelta { get; private set; }
    /// <summary>
    /// Last movement delta of the entity, not including effects from collisions. Zero if the entity did not
    ///  try to move.
    /// </summary>
    public Vector2 LastDesiredDelta { get; private set; }
    /// <summary>
    /// The direction in which the entity is currently moving. If the entity is not moving, then
    ///  this contains the most recent direction in which it was moving.
    /// </summary>
    public Vector2 Direction { get; private set; }
    /// <summary>
    /// An optional rotation function provided by <see cref="Danmaku.Options.BehOption.Rotate"/> for
    ///  manually controlling the rotation of the entity. This will affect the euler angles of the entity.
    /// </summary>
    public BPY? Rotator { get; private set; }
    public float? RotatorRotation { get; private set; }

    protected bool Dying { get; private set; } = false;
    private Enemy? enemy;
    public Enemy Enemy => enemy == null ? throw new Exception($"BEH {ID.Or(gameObject.name)} is not an Enemy.") : enemy;
    
    private Movement movement = Movement.None;
    /// <summary>
    /// If parented, we need firing offset to update Velocity's root position with parent.pos + offset every frame.
    /// </summary>
    private Vector2 firedOffset;
    protected ParametricInfo bpi; //bpi.index is queried by other scripts, defaults to zero
    /// <summary>
    /// Access to BPI struct.
    /// Note: Do not modify rBPI.t directly, instead use SetTime. This is because entities like Laser have double handling for rBPI.t.
    /// </summary>
    public virtual ref ParametricInfo rBPI => ref bpi;
    
    /// <summary>
    /// Global position of this entity.
    /// </summary>
    public virtual Vector2 Location => rBPI.loc;
    
    public int DefaultLayer { get; private set; }

    protected override void Awake() {
        base.Awake();
        DefaultLayer = gameObject.layer;
        bpi = new ParametricInfo(PIData.NewUnscoped(), tr.position, 0, RNG.GetUInt(), 0);
        enemy = GetComponent<Enemy>();
        RegisterID();
        UpdateStyle(defaultMeta);
    }
    
    protected override void BindListeners() {
        base.BindListeners();
#if UNITY_EDITOR || ALLOW_RELOAD
        if (SceneIntermediary.IsFirstScene && 
            (this is FireOption || this is BossBEH || this is LevelController || GetComponent<RELOADER>() != null)) {
            Listen(Events.LocalReset, () => {
                HardCancel(false);
                //Allows all cancellations processes to go through before rerunning
                if (behaviorScript != null)
                    ETime.QueueEOFInvoke(() => _ = RunBehaviorSM(SMRunner.RunRoot(behaviorScript, Cancellable.Null)));
            });
        }
#endif
    }

    public bool TryDependent<T>(out T value) where T : IBehaviorEntityDependent {
        for (int ii = 0; ii < dependentComponents.Count; ++ii)
            if (dependentComponents[ii] is T typed) {
                value = typed;
                return true;
            }
        value = default!;
        return false;
    }
    
    public T Dependent<T>() where T : IBehaviorEntityDependent {
        for (int ii = 0; ii < dependentComponents.Count; ++ii)
            if (dependentComponents[ii] is T typed)
                return typed;
        throw new Exception($"BEH {ID.Or(gameObject.name)} is not an {typeof(T).RName()}, " +
                            $"but you are trying to access this component.");
    }
    
    public virtual void SetTime(float t) => rBPI.t = t;

    /// <summary>
    /// Initialize a BEH. You are not required to call this, but all BEH that are generated in code should use this.
    /// </summary>
    /// <param name="style"></param>
    /// <param name="mov">Velocity struct</param>
    /// <param name="smr">SM to execute. Set null if no SM needs to be run.</param>
    /// <param name="pi">ParametricInfo to bind to this object.</param>
    /// <param name="parent">Transform parent of this BEH. Use sparingly</param>
    /// <param name="behName"></param>
    /// <param name="options"></param>
    public void Initialize(BEHStyleMetadata? style, in Movement mov, ParametricInfo pi, SMRunner? smr,
        BehaviorEntity? parent=null, string behName="", RealizedBehOptions? options=null) {
        if (parent != null) TakeParent(parent);
        bpi = pi;
        movement = mov;
        tr.localPosition = firedOffset = movement.rootPos;
        movement.rootPos = bpi.loc = tr.position;
        OriginalAngle = movement.angle;
        if (!movement.IsEmpty()) {
            SetMovementDelta(movement.UpdateZero(ref bpi));
            tr.position = bpi.loc;
        }
        Rotator = options?.rotator;
        var rot = Rotator?.Invoke(bpi) ?? 0;
        RotatorRotation = rot;
        tr.localEulerAngles = new Vector3(0, 0, rot);
        if (IsNontrivialID(behName)) ID = behName;
        if (smr is {} runner)
            _ = RunBehaviorSM(runner);
        //This comes after so SMs run due to ~@ commands are not destroyed by BeginBehaviorSM
        RegisterID();
        UpdateStyle(style ?? defaultMeta);
        for (int ii = 0; ii < dependentComponents.Count; ++ii)
            dependentComponents[ii].Initialized(options);
    }

    protected override void ResetValues(bool isFirst) {
        base.ResetValues(isFirst);
        phaseController = SMPhaseController.Normal(0);
        Dying = false;
        Direction = Vector2.right;
        if (!isFirst) 
            for (int ii = 0; ii < dependentComponents.Count; ++ii)
                dependentComponents[ii].OnLinkOrResetValues(false);
    }
    
    public override void FirstFrame() {
        base.FirstFrame();
        UpdateRendering(true);
        //Note: This pathway is used for player shots and minor summoned effects (such as cutins).
        //It is not used for bosses/stages, which call directly into RunBehaviorSM.
        if (behaviorScript != null) {
            if (behaviorToken.Count > 0)
                Logs.UnityError($"BEH {gameObject.name} has a behaviorScript attached, but is already executing" +
                                $" another script. This is incorrect; you must either set up a script through code" +
                                $" (eg. for bosses) or attach a behaviorScript (eg. for cutins), but not both.");
            try {
                //TODO should this be bound by InstTracker?
                _ = RunBehaviorSM(SMRunner.RunRoot(behaviorScript, Cancellable.Null));
            } catch (Exception e) {
                Logs.UnityError("Failed to load attached SM on startup!");
                Logs.LogException(e);
            }
        }
    }

    /// <summary>
    /// Call this in Awake from any <see cref="IBehaviorEntityDependent"/>.
    /// </summary>
    public void LinkDependentUpdater(IBehaviorEntityDependent ru) {
        if (dependentComponents.Contains(ru)) return;
        dependentComponents.Add(ru);
        ru.OnLinkOrResetValues(true);
    }

    private static bool IsNontrivialID(string? id) => !string.IsNullOrWhiteSpace(id) && id != "_";

    /// <summary>
    /// Safe to call twice.
    /// </summary>
    private void RegisterID() {
        if (!idLookup.ContainsKey(ID)) { idLookup[ID] = new HashSet<BehaviorEntity>(); }
        idLookup[ID].Add(this);
        if (IsNontrivialID(ID) && pointers.TryGetValue(ID, out BEHPointer behp) && !behp.Attached)
            behp.Attach(this);
    }
    
    /// <summary>
    /// Safe to call twice.
    /// </summary>
    private void UnregisterID() {
        if (idLookup.TryGetValue(ID, out var set)) set.Remove(this);
        if (pointers.TryGetValue(ID, out var behp) && behp.beh == this)
            behp.Detach();
    }
    
    /// <summary>
    /// Given the last movement delta, update LastDelta as well as Direction (if the movement delta is nonzero).
    /// </summary>
    public void SetMovementDelta(Vector2 actual, Vector2? desired = null) {
        LastDelta = actual;
        LastDesiredDelta = desired ?? actual;
        var mag = actual.x * actual.x + actual.y * actual.y;
        if (mag > M.MAG_ERR) {
            mag = (float)Math.Sqrt(mag);
            Direction = new Vector2(actual.x / mag, actual.y / mag);
        }
    }
    
    /// <summary>
    /// Sets the transform position. This is an private function. BPI is not updated.
    /// </summary>
    /// <param name="p">Target global position</param>
    private void SetTransformGlobalPosition(Vector2 p) {
        if (Parented) tr.position = p;
        else tr.localPosition = p; // Slightly faster pathway
    }
    
    /// <summary>
    /// Sets the transform position. This is an external function. BPI is updated.
    /// </summary>
    /// <param name="p">Target local position</param>
    public void ExternalSetLocalPosition(Vector2 p) {
        tr.localPosition = p;
        bpi.loc = tr.position;
    }

    /// <summary>
    /// You can technically overlay this on a BEH that is initialized with non-empty velocity,
    /// but the results are not well-defined.
    /// </summary>
    /// <returns></returns>
    public IEnumerator ExecuteVelocity(LimitedTimeMovement ltv) {
        if (ltv.cT.Cancelled) { ltv.done(); yield break; }
        Movement vel = new Movement(ltv.vtp, Location, V2RV2.Angle(OriginalAngle));
        float doTime = (ltv.enabledFor < float.Epsilon) ? float.MaxValue : ltv.enabledFor;
        ParametricInfo tbpi = ltv.pi;
        tbpi.loc = bpi.loc;
        //Sets initial position correctly for offset-based velocity
        _ = vel.UpdateZero(ref tbpi);
        SetTransformGlobalPosition(tbpi.loc);
        if (ltv.ThisCannotContinue(tbpi)) { ltv.done(); yield break; }
        for (; tbpi.t < doTime - ETime.FRAME_TIME;) {
            tbpi.loc = bpi.loc;
            Vector3 delta = default;
            vel.UpdateDeltaAssignDelta(ref tbpi, ref delta, ETime.FRAME_TIME);
            //Checking the canceller before committing position allows using eg. successive onscreen checks.
            //This is a core use case for move-while. So we split up velocitystep to allow it
            if (ltv.ThisCannotContinue(tbpi)) {
                SetMovementDelta(Vector2.zero);
                ltv.done(); yield break;
            }
            SetMovementDelta(delta);
            SetTransformGlobalPosition(bpi.loc = tbpi.loc);
            yield return null;
            if (ltv.cT.Cancelled) {
                SetMovementDelta(Vector2.zero);
                ltv.done(); yield break;
            }
        }
        tbpi.loc = bpi.loc;
        VelocityStepAndLook(ref vel, ref tbpi, doTime - tbpi.t);
        bpi.loc = tbpi.loc;
        SetMovementDelta(Vector2.zero);
        ltv.done();
    }
    
    protected virtual void SpawnSimple(string styleName) {
        BulletManager.RequestNullSimple(styleName, bpi.loc, Direction);
    }

    private void VelocityStepAndLook(ref Movement vel, ref ParametricInfo pi, float dT=ETime.FRAME_TIME) {
        Vector3 delta = default;
        vel.UpdateDeltaAssignDelta(ref pi, ref delta, dT);
        SetMovementDelta(delta);
        SetTransformGlobalPosition(pi.loc);
    }

    protected virtual void RegularUpdateMove() {
        if (!movement.IsEmpty()) {
            if (Parented) {
                movement.rootPos = GetParentPosition() + firedOffset;
                bpi.loc = tr.position;
            }
            VelocityStepAndLook(ref movement, ref bpi);
        } else {
            bpi.t += ETime.FRAME_TIME;
            if (Parented) bpi.loc = tr.position;
        }
        if (Rotator != null) {
            var rot = Rotator(bpi);
            RotatorRotation = rot;
            tr.localEulerAngles = new Vector3(0, 0, rot);
        }
    }

    protected bool nextUpdateAllowed = true;
    public override void RegularUpdate() {
        Profiler.BeginSample("BehaviorEntity Update");
        if (Dying) {
            base.RegularUpdate();
        } else {
            if (nextUpdateAllowed) {
                RegularUpdateMove();
                base.RegularUpdate();
            } else nextUpdateAllowed = true;
            Style.IterateControls(this);
        }
        Profiler.EndSample();
    }

    public override void RegularUpdateFinalize() => UpdateRendering(false);

    protected virtual void UpdateRendering(bool isFirstFrame) {
        foreach (var cmp in dependentComponents)
            cmp.OnRender(isFirstFrame, LastDesiredDelta);
    }

    public override int UpdatePriority => UpdatePriorities.BEH;
    
    protected virtual void FlipVelX() => movement.FlipX();
    protected virtual void FlipVelY() => movement.FlipY();

    /// <summary>
    /// Destroy all other SMs and run an SM.
    /// Call this with any high-priority SMs-- they are not required to be pattern-type.
    /// While you can pass null here, that will still allocate some Task garbage.
    /// </summary>
    public async Task RunBehaviorSM(SMRunner sm, SMContext? context = null) {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (sm.sm == null) //check this just to be safe
            return;
        if (sm.cT.Cancelled)
            throw new OperationCanceledException();
        HardCancel(false);
        phaseController.SetDesiredNext(0);
        var cT = new Cancellable();
        var joint = sm.MakeNested(cT);
        using var smh = new SMHandoff(this, sm, joint, context);
        behaviorToken.Add(cT);
        try {
            await sm.sm.Start(smh);
        } catch (Exception e) {
            if (!(e is OperationCanceledException)) {
                Logs.UnityError(Exceptions.PrintNestedException(e)); //This is only here for the vaguest of debugging purposes.
            }
            throw;
        } finally {
            if (GameManagement.Instance.mode == InstanceMode.NULL ||
                GameManagement.Instance.Request?.InstTracker.Cancelled is not true) {
                if (IsNontrivialID(ID)) {
                    Logs.Log(
                        $"BehaviorEntity {ID} finished running its SM{(sm.cullOnFinish ? " and will destroy itself." : ".")}",
                        level: LogLevel.DEBUG1);
                }
                if (sm.cullOnFinish)
                    CullMe(true);
            }
            //It is possible for tasks to still be running at this point (most critically if
            // using ~), so we cancel to make sure they get destroyed
            cT.Cancel();
            behaviorToken.Remove(cT);
        }
    }

    /// <summary>
    /// Run an SM in parallel to any currently running SMs.
    /// Do not call with pattern SMs.
    /// <param name="cancelOnFinish">If true, will cancel the local Cancellable upon exit, which is the behavior for BeginBehaviorSM. This is set to false for Retarget functions, which are generally run many times on persistent objects. Even if set to false, the Cancellable will still be cancelled by HardCancel.</param>
    /// </summary>
    public async Task RunExternalSM(SMRunner? smr, bool cancelOnFinish = true) {
        if (smr is not {} sm || sm.cT.Cancelled) return;
        var cT = new Cancellable();
        var joint = sm.MakeNested(cT);
        using var smh = new SMHandoff(this, sm, joint);
        behaviorToken.Add(cT);
        try {
            await sm.sm.Start(smh);
        } catch (Exception e) {
            if (!(e is OperationCanceledException)) {
                Logs.UnityError(Exceptions.PrintNestedException(e)); //This is only here for the vaguest of debugging purposes.
            }
            //When ending a level, the order of OnDisable is random, so a node running a sub-SM may
            //be cancelled before its caller, so the caller cannot rely on this line throwing.
            //This is OK under the standard design pattern which is "check cT after awaiting".
            if (sm.cT.Cancelled) throw;
            //When running external SM, "local cancel" (due to death) is a valid output, and we should not throw.
            //Same as how Phase does not throw if OpCanceled is raised via shiftphasetoken.
        } finally {
            if (cancelOnFinish) {
                cT.Cancel();
                behaviorToken.Remove(cT);
            }
        }
        if (sm.cullOnFinish) InvokeCull();
    }
    
    public bool AllowFinishCalls { get; private set; } = true;
    /// <summary>
    /// Safe to call twice. (which is done in InvokeCull -> OnDisable)
    /// </summary>
    public void HardCancel(bool allowFinishCB) {
        if (behaviorToken.Count == 0) return;
        AllowFinishCalls = allowFinishCB;
        foreach (var cT in behaviorToken.ToArray()) {
            cT.Cancel();
        }
        behaviorToken.Clear();
        ForceClosingFrame();
        AllowFinishCalls = true;
        PhaseShifter = null;
    }

    /// <summary>
    /// Destroy this BehaviorEntity. If `allowFinalize` is true, produce death effects.
    /// </summary>
    public void CullMe(bool allowFinalize) {
        if (Dying) return;
        Dying = true;
        allowFinalize &= !SceneIntermediary.LOADING;
        CullHook(allowFinalize);
        if (allowFinalize)
            Style.IterateDestroyControls(this);
        UnregisterID();
        HardCancel(allowFinalize);
        var (fragDone, cullNow) = allowFinalize && dependentComponents.Count > 0 ?
            (WaitingUtils.GetManyCallback(dependentComponents.Count, FinalizeCull), false) :
            (WaitingUtils.NoOp, true);
        for (int ii = 0; ii < dependentComponents.Count; ++ii)
            dependentComponents[ii].Culled(allowFinalize, fragDone);
        if (cullNow)
            FinalizeCull();
        return;

        void FinalizeCull() {
            bpi.Dispose();
            if (isPooled)
                PooledDone();
            else
                Destroy(gameObject);
        }
    }
    
    protected virtual void CullHook(bool allowFinalize) { }

    [ContextMenu("Destroy Direct")]
    public void InvokeCull() => CullMe(false);

    protected override void ExternalDestroy() => CullMe(false);

    protected override void OnDisable() {
        ExternalDestroy();
        base.OnDisable();
    }

    [ContextMenu("Phase Shift")]
    public void ShiftPhase() {
        var oldSPT = PhaseShifter;
        PhaseShifter = null;
        oldSPT?.Cancel(); 
    }
    
    public BehaviorEntity GetINode(string behName, uint? bpiid) {
        var mov = new Movement(rBPI.loc, V2RV2.Angle(OriginalAngle));
        return BEHPooler.INode(mov, new ParametricInfo(in mov, rBPI.index, bpiid), Direction, behName);
    }

    #region GetExecForID

    public static BehaviorEntity GetExecForID(string id) {
        foreach (BehaviorEntity beh in idLookup[id])
            return beh;
        throw new Exception("Could not find beh for ID: " + id);
    }

    private static readonly Dictionary<string, BEHPointer> pointers = new();

    public static BEHPointer GetPointerForID(string id) {
        if (!pointers.TryGetValue(id, out var p))
            pointers[id] = p = new(id);
        if (!p.Attached && idLookup.TryGetValue(id, out var behs))
            foreach (BehaviorEntity beh in behs) {
                p.Attach(beh);
                break;
            }
        return p;
    }

    public static BehaviorEntity[] GetExecsForIDs(string[] ids) {
        int totalct = 0;
        for (int ii = 0; ii < ids.Length; ++ii) {
            if (idLookup.TryGetValue(ids[ii], out var behs)) {
                totalct += behs.Count;
            }
        }
        BehaviorEntity[] ret = new BehaviorEntity[totalct];
        int acc = 0;
        for (int ii = 0; ii < ids.Length; ++ii) {
            if (idLookup.TryGetValue(ids[ii], out var behs)) {
                behs.CopyTo(ret, acc);
                acc += behs.Count;
            }
        }
        return ret;
    }

    #endregion
}

public class BEHPointer {
    public readonly string id;
    public BehaviorEntity? beh;
    public BehaviorEntity Beh => (beh != null) ? beh : throw new Exception($"BEHPointer {id} has not been bound");
    public bool Attached { get; private set; }

    public BEHPointer(string id, BehaviorEntity? beh = null) {
        this.id = id;
        this.beh = beh;
        Attached = beh != null;
    }

    public void Attach(BehaviorEntity new_beh) {
        if (Attached) throw new Exception("Cannot attach BEHPointer twice");
        beh = new_beh;
        Attached = true;
    }

    public void Detach() {
        Attached = false;
        beh = null;
    }
}

}