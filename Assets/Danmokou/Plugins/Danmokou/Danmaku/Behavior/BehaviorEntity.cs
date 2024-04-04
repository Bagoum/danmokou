using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
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

[Serializable]
public struct ItemDrops {
    public double value;
    public int pointPP;
    public int life;
    public int power;
    public int gems;
    public bool autocollect;
    public ItemDrops(double v, int pp, int l, int pow, int gem, bool autoc=false) {
        value = v;
        pointPP = pp;
        life = l;
        power = pow;
        gems = gem;
        autocollect = autoc;
    }

    public ItemDrops Mul(float by) => new((value * by), (int)(pointPP * by), (int)(life * by), 
        (int)(power * by), (int)(gems * by), autocollect);
}

/// <summary>
/// A high-level class that describes all complex danmaku-related objects (primarily bullets and NPCs).
/// </summary>
public partial class BehaviorEntity : Pooled<BehaviorEntity>, ITransformHandler {
    //BEH is only pooled when summoned via the firing API.

    [Tooltip("This must be null for pooled BEH.")]
    public TextAsset? behaviorScript;
    /// <summary>
    /// ID to refer to this entity in behavior scripts.
    /// </summary>
    public string ID = "";
    private static readonly Dictionary<string, HashSet<BehaviorEntity>> idLookup = new();
    //This is automatically disposed by the state machine that generates it
    public ICancellable? PhaseShifter { get; set; }
    private readonly HashSet<Cancellable> behaviorToken = new();
    public int NumRunningSMs => behaviorToken.Count;
    
    /// <summary>
    /// Components such as <see cref="Enemy"/> which have a separate execution loop
    ///  but depend on this for enable/disable calls.
    /// </summary>
    private readonly List<IBehaviorEntityDependent> dependentComponents = new();
    
    public RealizedBehOptions? Options { get; private set; }
    
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
    public float DirectionDeg => M.AtanD(Direction);

    /// <summary>
    /// An optional rotation function provided by <see cref="Danmaku.Options.BehOption.Rotate"/> for
    ///  manually controlling the rotation of the entity. This will affect the euler angles of the entity.
    /// </summary>
    public BPY? Rotator { get; private set; }
    public float? RotatorRotation { get; private set; }
    
    /// <summary>
    /// Only the original firing angle matters for rotational movement velocity
    /// </summary>
    public float original_angle { get; protected set; }

    protected bool dying { get; private set; } = false;
    private Enemy? enemy;
    public EffectStrategy? deathEffect;

    private string NameMe => string.IsNullOrWhiteSpace(ID) ? gameObject.name : ID;
    public Enemy Enemy {
        get {
            if (enemy == null)
                throw new Exception($"BEH {NameMe} is not an Enemy, " +
                                    $"but you are trying to access the Enemy component.");
            else
                return enemy;
        }
    }
    public bool isEnemy => enemy != null;

    public bool TryAsEnemy(out Enemy e) {
        e = enemy!;
        return isEnemy;
    }

    public virtual bool TriggersUITimeout => false;
    public SMPhaseController phaseController;
    //These values are only set for Initialize-based BEH (via SM summon command)
    //All bullets are initialize-based and use these
    protected ParametricInfo bpi; //bpi.index is queried by other scripts, defaults to zero
    /// <summary>
    /// Access to BPI struct.
    /// Note: Do not modify rBPI.t directly, instead use SetTime. This is because entities like Laser have double handling for rBPI.t.
    /// </summary>
    public virtual ref ParametricInfo rBPI => ref bpi;
    public ParametricInfo BPI => rBPI;
    public Vector2 Location => rBPI.loc;

    public virtual void SetTime(float t) {
        rBPI.t = t;
    }

    private Movement movement;
    private bool doVelocity = false;

    
    private const float FIRST_CULLCHECK_TIME = 2;

    [Serializable]
    public struct CullableRadius {
        public bool cullable;
        public SOFloat cullRadius;
    }

    public CullableRadius cullableRadius;
    public float ScreenCullRadius => (cullableRadius.cullRadius == null) ?  4f :
        cullableRadius.cullRadius.value;
    private const int checkCullEvery = 120;
    private int beh_cullCtr = 0;
    private Pred? delete;
    public int DefaultLayer { get; private set; }

    public enum DirectionRelation {
        RUFlipsLD,
        LDFlipsRU,
        RUCopiesLD,
        LDCopiesRU,
        Independent,
        None
    }

    public DisplayController? displayer;
    public DisplayController DisplayerOrThrow => (displayer != null) ? displayer : throw new Exception($"BEH {ID} does not have a displayer");
    private bool isSummoned = false;
    protected virtual int Findex => 0;
    protected virtual PIData DefaultFCTX() => PIData.NewUnscoped();
    protected override void Awake() {
        base.Awake();
        DefaultLayer = gameObject.layer;
        bpi = new ParametricInfo(DefaultFCTX(), tr.position, Findex, RNG.GetUInt(), 0);
        enemy = GetComponent<Enemy>();
        RegisterID();
        UpdateStyle(defaultMeta);
    }
    
    protected override void BindListeners() {
        base.BindListeners();
#if UNITY_EDITOR || ALLOW_RELOAD
        if (SceneIntermediary.IsFirstScene && this is not Bullet && 
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

    public void Initialize(Movement mov, ParametricInfo pi, SMRunner? sm, string behName = "") =>
        Initialize(null, mov, pi, sm, null, behName);

    /// <summary>
    /// If parented, we need firing offset to update Velocity's root position with parent.pos + offset every frame.
    /// </summary>
    private Vector2 firedOffset;

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
    public void Initialize(BEHStyleMetadata? style, Movement mov, ParametricInfo pi, SMRunner? smr,
        BehaviorEntity? parent=null, string behName="", RealizedBehOptions? options=null) {
        if (parent != null) TakeParent(parent);
        isSummoned = true;
        Options = options;
        bpi = pi;
        tr.localPosition = firedOffset = mov.rootPos;
        mov.rootPos = bpi.loc = tr.position;
        original_angle = mov.angle;
        movement = mov;
        doVelocity = !movement.IsEmpty();
        if (doVelocity) {
            SetMovementDelta(movement.UpdateZero(ref bpi));
            tr.position = bpi.loc;
        }
        if (options?.rotator != null) {
            var rot = (Rotator = options?.rotator!)(bpi);
            RotatorRotation = rot;
            tr.localEulerAngles = new Vector3(0, 0, rot);
        }
        
        if (IsNontrivialID(behName)) ID = behName;
        if (smr is {} runner)
            _ = RunBehaviorSM(runner);
        //This comes after so SMs run due to ~@ commands are not destroyed by BeginBehaviorSM
        RegisterID();
        UpdateStyle(style ?? defaultMeta);
        deathDrops = options?.drops;
        delete = options?.delete;
        for (int ii = 0; ii < dependentComponents.Count; ++ii)
            dependentComponents[ii].Initialized(options);
    }

    protected override void ResetValues() {
        base.ResetValues();
        phaseController = SMPhaseController.Normal(0);
        dying = false;
        Direction = Vector2.right;
        if (enemy != null) enemy.LinkAndReset(this);
        if (displayer != null) displayer.LinkAndReset(this);
        for (int ii = 0; ii < dependentComponents.Count; ++ii)
            dependentComponents[ii].Alive();
    }
    
    public override void FirstFrame() {
        UpdateRendering(true);
        
        //Note: This pathway is used for player shots and minor summoned effects (such as cutins).
        //It is not used for bosses/stages, which call directly into RunBehaviorSM.
        if (behaviorScript != null) {
            try {
                //TODO should this be bound by InstTracker?
                _ = RunBehaviorSM(SMRunner.RunRoot(behaviorScript, Cancellable.Null));
            } catch (Exception e) {
                Logs.UnityError("Failed to load attached SM on startup!");
                Logs.LogException(e);
            }
        }
    }

    public void LinkDependentUpdater(IBehaviorEntityDependent ru) {
        if (!dependentComponents.Contains(ru)) 
            dependentComponents.Add(ru);
    }

    private static bool IsNontrivialID(string? id) => !string.IsNullOrWhiteSpace(id) && id != "_";

    /// <summary>
    /// Safe to call twice.
    /// </summary>
    private void RegisterID() {
        if (!idLookup.ContainsKey(ID)) { idLookup[ID] = new HashSet<BehaviorEntity>(); }
        idLookup[ID].Add(this);
        if (IsNontrivialID(ID)) {
            if (pointers.TryGetValue(ID, out BEHPointer behp) && !behp.Attached) {
                behp.Attach(this);
            }
        }
    }
    
    /// <summary>
    /// Safe to call twice.
    /// </summary>
    private void UnregisterID() {
        if (idLookup.TryGetValue(ID, out var dct)) dct.Remove(this);
        if (pointers.TryGetValue(ID, out var behp) && behp.beh == this) {
            behp.Detach();
        }
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
        if (parented) tr.position = p;
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
        Movement vel = new Movement(ltv.vtp, GlobalPosition(), V2RV2.Angle(original_angle));
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


    #region Death

    /// <summary>
    /// Call this from hp-management scripts when you are out of HP.
    /// </summary>
    public void OutOfHP() {
        if (PhaseShifter != null) {
            ShiftPhase();
        } else {
            Poof(true);
        }
    }

    private void DestroyInitial(bool allowFinalize, bool allowDrops=false) {
        if (dying) return;
        dying = true;
        if (allowDrops) DropItemsOnDeath();
        UnregisterID();
        HardCancel(allowFinalize);
        for (int ii = 0; ii < dependentComponents.Count; ++ii)
            dependentComponents[ii].Died();
    }

    private void DestroyFinal() {
        if (displayer != null) displayer.Hide();
        bpi.Dispose();
        if (isPooled) {
            PooledDone();
        } else {
            Destroy(gameObject);
        }
    }

    private void TryDeathEffect() {
        if (deathEffect != null && !SceneIntermediary.LOADING) deathEffect.Proc(GlobalPosition(), GlobalPosition(), 1f);
    }
    
    /// <summary>
    /// Call instead of InvokeCull when you need to show death animations and trigger finalize effects.
    /// </summary>
    public void Poof(bool? drops=null) {
        if (SceneIntermediary.LOADING) InvokeCull();
        else if (!dying) {
            myStyle.IterateDestroyControls(this);
            DestroyInitial(true, drops ?? AmIOutOfHP);
            if (enemy != null) {
                enemy.DoSuicideFire();
                GameManagement.Instance.NormalEnemyDestroyed();
            }
            TryDeathEffect();
            if (displayer == null) DestroyFinal();
            else displayer.Animate(AnimationType.Death, false, DestroyFinal);
        }
    }
    
    [ContextMenu("Destroy Direct")]
    public virtual void InvokeCull() {
        if (dying) return; //Possible when @ cull is used on cullOnFinish entity
        DestroyInitial(false);
        DestroyFinal();
    }

    protected override void ExternalDestroy() => InvokeCull();

    #endregion

    public static void DestroyAllSummons() {
        foreach (var id in idLookup) {
            foreach (var beh in id.Value.ToList()) {
                if (beh.isSummoned) beh.InvokeCull();
            }
        }
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
        if (doVelocity) {
            if (parented) {
                movement.rootPos = GetParentPosition() + firedOffset;
                bpi.loc = tr.position;
            }
            VelocityStepAndLook(ref movement, ref bpi);
        } else {
            bpi.t += ETime.FRAME_TIME;
            if (parented) bpi.loc = tr.position;
        }
        if (Rotator != null) {
            var rot = Rotator(bpi);
            RotatorRotation = rot;
            tr.localEulerAngles = new Vector3(0, 0, rot);
        }
    }
    
    private void RegularUpdateControl()  {
        //thisStyleControls may change during iteration. Don't respect changes
        myStyle.IterateControls(this);
    }

    /// <summary>
    /// Check if this object needs to be culled automatically.
    /// </summary>
    /// <returns>True iff the object was culled.</returns>
    protected virtual bool RegularUpdateCullCheck() {
        if (delete?.Invoke(rBPI) == true) {
            InvokeCull();
            return true;
        }
        else if (beh_cullCtr == 0 && cullableRadius.cullable && myStyle.CameraCullable.Value 
                 && bpi.t > FIRST_CULLCHECK_TIME && LocationHelpers.OffPlayableScreenBy(ScreenCullRadius, bpi.loc)) {
            InvokeCull();
            return true;
        } else {
            beh_cullCtr = (beh_cullCtr + 1) % checkCullEvery;
            return false;
        }
    }

    public override void RegularUpdate() {
        Profiler.BeginSample("BehaviorEntity Update");
        if (dying) {
            base.RegularUpdate();
        } else {
            if (nextUpdateAllowed) {
                RegularUpdateMove();
                base.RegularUpdate();
            } else nextUpdateAllowed = true;
            RegularUpdateControl();
            if (!dying) RegularUpdateCullCheck();
        }
        Profiler.EndSample();
    }

    public override void RegularUpdateFinalize() {
        UpdateRendering(false);
    }

    protected void UpdateRendering(bool isFirstFrame) {
        Profiler.BeginSample("BehaviorEntity displayer update");
        if (displayer != null) {
            displayer.FaceInDirection(LastDesiredDelta);
            UpdateDisplayerRender(isFirstFrame);
            displayer.UpdateRender(isFirstFrame);
        }
        Profiler.EndSample();
    }

    protected virtual void UpdateDisplayerRender(bool isFirstFrame) { }

    protected bool nextUpdateAllowed = true;

    public override int UpdatePriority => UpdatePriorities.BEH;

    #region Interfaces

    public Vector2 LocalPosition() {
        if (parented) return tr.localPosition;
        return bpi.loc; 
    }

    /// <summary>
    /// For external consumption
    /// </summary>
    /// <returns></returns>
    public virtual Vector2 GlobalPosition() => bpi.loc;

    public bool HasParent() {
        return parented;
    }

    protected virtual void FlipVelX() {
        movement.FlipX();
    }
    protected virtual void FlipVelY() {
        movement.FlipY();
    }
    
    #endregion

    /// <summary>
    /// Destroy all other SMs and run an SM.
    /// Call this with any high-priority SMs-- they are not required to be pattern-type.
    /// While you can pass null here, that will still allocate some Task garbage.
    /// </summary>
    public async Task RunBehaviorSM(SMRunner sm, SMContext? context = null) {
        if (sm.sm == null) 
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
                if (sm.cullOnFinish) {
                    if (PoofOnPhaseEnd) Poof();
                    else {
                        if (DeathEffectOnParentCull && sm.cT.Root.Cancelled) TryDeathEffect();
                        InvokeCull();
                    }
                }
            }
            //It is possible for tasks to still be running at this point (most critically if
            // using ~), so we cancel to make sure they get destroyed
            cT.Cancel();
            behaviorToken.Remove(cT);
        }
    }

    private bool AmIOutOfHP => enemy != null && enemy.HP <= 0;
    private bool PoofOnPhaseEnd => AmIOutOfHP && this is not BossBEH;
    private bool DeathEffectOnParentCull => true;

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

    [ContextMenu("Finish SMs")]
    public void FinishAllSMs() => HardCancel(false);
    
    public bool AllowFinishCalls { get; private set; } = true;
    /// <summary>
    /// Safe to call twice. (which is done in InvokeCull -> OnDisable)
    /// </summary>
    public void HardCancel(bool allowFinishCB) {
        if (behaviorToken.Count == 0) return;
        AllowFinishCalls = allowFinishCB && !SceneIntermediary.LOADING;
        foreach (var cT in behaviorToken.ToArray()) {
            cT.Cancel();
        }
        behaviorToken.Clear();
        ForceClosingFrame();
        AllowFinishCalls = true;
        PhaseShifter = null;
    }
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
        var mov = new Movement(rBPI.loc, V2RV2.Angle(original_angle));
        return BEHPooler.INode(mov, new ParametricInfo(in mov, rBPI.index, bpiid), Direction, behName);
    }


#if UNITY_EDITOR
    [ContextMenu("Debug all BEHIDs")]
    public void DebugBEHID() {
        int total = 0;
        foreach (var p in idLookup.Values) {
            total += p.Count;
        }
        Debug.LogFormat("Found {0} BEH", total);
    }
    
    
#endif

    private static readonly Action noop = () => { };
    [ContextMenu("Animate Attack")]
    public void AnimateAttack() {
        if (displayer != null) displayer.Animate(AnimationType.Attack, false, null);
    }

    #region GetExecForID

    public static BehaviorEntity GetExecForID(string id) {
        foreach (BehaviorEntity beh in idLookup[id]) {
            return beh;
        }
        throw new Exception("Could not find beh for ID: " + id);
    }

    private static readonly Dictionary<string, BEHPointer> pointers = new();

    public static BEHPointer GetPointerForID(string id) {
        if (!pointers.TryGetValue(id, out var p))
            pointers[id] = p = new(id);
        if (!p.Attached && idLookup.ContainsKey(id))
            foreach (BehaviorEntity beh in idLookup[id]) {
                p.Attach(beh);
                break;
            }
        return p;
    }

    public static BehaviorEntity[] GetExecsForIDs(string[] ids) {
        int totalct = 0;
        for (int ii = 0; ii < ids.Length; ++ii) {
            if (idLookup.ContainsKey(ids[ii])) {
                totalct += idLookup[ids[ii]].Count;
            }
        }
        BehaviorEntity[] ret = new BehaviorEntity[totalct];
        int acc = 0;
        for (int ii = 0; ii < ids.Length; ++ii) {
            if (idLookup.ContainsKey(ids[ii])) {
                idLookup[ids[ii]].CopyTo(ret, acc);
                acc += idLookup[ids[ii]].Count;
            }
        }
        return ret;
    }

    #endregion
    
    
#if UNITY_EDITOR
    [ContextMenu("Debug transform")]
    public void DebugTransform() {
        Debug.Log($"${tr.gameObject.name} parented by ${tr.parent.gameObject.name}");
    }

#endif
}

public class BEHPointer {
    public readonly string id;
    public BehaviorEntity? beh;
    public BehaviorEntity Beh => (beh != null) ? beh : throw new Exception($"BEHPointer {id} has not been bound");
    public bool Attached { get; private set; }
    public Vector2 Loc => Beh.Location;

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