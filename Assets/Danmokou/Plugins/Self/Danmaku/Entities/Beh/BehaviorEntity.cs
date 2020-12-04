using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DMath;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using UnityEngine.Serialization;
using Ex = System.Linq.Expressions.Expression;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine.Profiling;
using Collision = DMath.Collision;

namespace Danmaku {

[Serializable]
public struct ItemDrops {
    public int value;
    public int pointPP;
    public int life;
    public int power;
    public int gems;
    public bool autocollect;
    public ItemDrops(int v, int pp, int l, int pow, int gem, bool autoc=false) {
        value = v;
        pointPP = pp;
        life = l;
        power = pow;
        gems = gem;
        autocollect = autoc;
    }

    public ItemDrops Mul(float by) => new ItemDrops((int)(value * by), (int)(pointPP * by), (int)(life * by), 
        (int)(power * by), (int)(gems * by), autocollect);
}

/// <summary>
/// A high-level class that describes all complex danmaku-related objects (primarily bullets and NPCs).
/// </summary>
public partial class BehaviorEntity : Pooled<BehaviorEntity>, ITransformHandler {
    //BEH is only pooled when summoned via the firing API.

    [Tooltip("This must be null for pooled BEH.")]
    public TextAsset behaviorScript;
    /// <summary>
    /// ID to refer to this entity in behavior scripts.
    /// </summary>
    public string ID;
    private static readonly Dictionary<string, HashSet<BehaviorEntity>> idLookup = new Dictionary<string, HashSet<BehaviorEntity>>();
    //This is automatically disposed by the state machine that generates it
    [CanBeNull] public Cancellable PhaseShifter { get; set; }
    private readonly HashSet<Cancellable> behaviorToken = new HashSet<Cancellable>();
    public int NumRunningSMs => behaviorToken.Count;
    public Vector2 LastDelta { get; private set; }

    public void SetDirection(Vector2 delta) {
        LastDelta = delta;
        var mag = delta.x * delta.x + delta.y * delta.y;
        if (mag > M.MAG_ERR) {
            Direction = new Vector2(delta.x / mag, delta.y / mag);
        }
    }
    //Normalized lastDelta that does not update when the delta is zero.
    public Vector2 Direction { get; private set; }
    public float DirectionDeg => M.AtanD(Direction);
    /// <summary>
    /// Only the original firing angle matters for rotational movement velocity
    /// </summary>
    public float original_angle { get; protected set; }

    protected bool dying { get; private set; } = false;
    [CanBeNull] private Enemy enemy;
    [CanBeNull] public EffectStrategy deathEffect;

    private string NameMe => string.IsNullOrWhiteSpace(ID) ? gameObject.name : ID;
    public Enemy Enemy {
        get {
            if (enemy == null) {
                throw new Exception($"BEH {NameMe} is not an Enemy, but you are trying to access the Enemy component.");
            }
            return enemy;
        }
    }
    public bool isEnemy => enemy != null;

    public bool TryAsEnemy(out Enemy e) {
        e = enemy;
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

    public virtual void SetTime(float t) {
        rBPI.t = t;
    }

    private Velocity velocity;
    private bool doVelocity = false;

    protected void AssignVelocity(Velocity newVel) {
        velocity = newVel;
        doVelocity = !velocity.IsEmpty();
    }
    
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
    
    protected bool collisionActive = false;
    protected SOPlayerHitbox collisionTarget;
    protected int Damage => 1;

    [Serializable]
    public struct CollisionInfo {
        [Tooltip("Only used for default BEH circle collision")]
        public float collisionRadius;
        public bool CollisionActiveOnInit;
        public bool destructible;
        public ushort grazeEveryFrames;
    }

    public CollisionInfo collisionInfo;
    private int grazeFrameCounter = 0;
    [CanBeNull] private Pred delete;

    /// <summary>
    /// Sets the transform position iff `doMovement` is enabled.
    /// This is an public function. BPI is not updated.
    /// </summary>
    /// <param name="p">Target global position</param>
    private void SetTransformGlobalPosition(Vector2 p) {
        if (parented) tr.position = p;
        else tr.localPosition = p; // Slightly faster pathway
    }
    
    /// <summary>
    /// Sets the transform position iff `doMovement` is enabled.
    /// This is an external function. BPI is updated.
    /// </summary>
    /// <param name="p">Target local position</param>
    public void ExternalSetLocalPosition(Vector2 p) {
        tr.localPosition = p;
        bpi.loc = tr.position;
    }

    public enum DirectionRelation {
        RUFlipsLD,
        LDFlipsRU,
        RUCopiesLD,
        LDCopiesRU,
        Independent,
        None
    }

    public DisplayController displayer;
    private bool isSummoned = false;
    protected virtual int Findex => 0;
    protected override void Awake() {
        base.Awake();
        bpi = ParametricInfo.WithRandomId(tr.position, Findex);
        enemy = GetComponent<Enemy>();
        ResetV();
        RegisterID();
        UpdateStyle(defaultMeta);
        try {
            RunAttachedSM();
        } catch (Exception e) {
            Log.UnityError("Failed to load attached SM on startup!");
            Log.Print(e);
        }
#if UNITY_EDITOR || ALLOW_RELOAD
        if (SceneIntermediary.IsFirstScene && this is FireOption || this is BossBEH || this is LevelController) {
            Core.Events.LocalReset.Subscribe(() => {
                HardCancel(false);
                //Allows all cancellations processes to go through before rerunning
                ETime.QueueEOFInvoke(RunAttachedSM);
            });
        }
#endif
    }

    public Task Initialize(SMRunner smr) {
        if (smr.sm != null) {
            behaviorScript = null;
            return BeginBehaviorSM(smr, 0);
        }
        return Task.CompletedTask;
    }
    public void Initialize(Vector2 parentLoc, V2RV2 position,
        SMRunner sm, int firingIndex, uint? bpiid, string behName = "") =>
        Initialize(null, new Velocity(parentLoc, position), sm, firingIndex, bpiid, null, behName);

    /// <summary>
    /// If parented, we need firing offset to update Velocity's root position with parent.pos + offset every frame.
    /// </summary>
    private Vector2 firedOffset;

    /// <summary>
    /// Initialize a BEH. You are not required to call this, but all BEH that are generated in code should use this.
    /// </summary>
    /// <param name="style"></param>
    /// <param name="_velocity">Velocity struct</param>
    /// <param name="smr">SM to execute. Set null if no SM needs to be run.</param>
    /// <param name="firingIndex">Firing index of BPI that will be created.</param>
    /// <param name="bpiid">ID of BPI that will be created.</param>
    /// <param name="parent">Transform parent of this BEH. Use sparingly</param>
    /// <param name="behName"></param>
    /// <param name="options"></param>
    public void Initialize([CanBeNull] BEHStyleMetadata style, Velocity _velocity, SMRunner smr, int firingIndex=0, 
        uint? bpiid=null, [CanBeNull] BehaviorEntity parent=null, string behName="", RealizedBehOptions? options=null) {
        if (parent != null) TakeParent(parent);
        isSummoned = true;
        tr.localPosition = firedOffset = _velocity.rootPos;
        _velocity.rootPos = bpi.loc = tr.position;
        original_angle = _velocity.angle;
        bpi = new ParametricInfo(_velocity.rootPos, firingIndex, bpiid ?? RNG.GetUInt());
        AssignVelocity(_velocity);
        if (doVelocity) {
            SetDirection(velocity.UpdateZero(ref bpi, 0f));
            tr.position = bpi.loc;
        }
        if (IsNontrivialID(behName)) ID = behName;
        Initialize(smr);
        //This comes after so SMs run due to ~@ commands are not destroyed by BeginBehaviorSM
        RegisterID();
        UpdateStyle(style ?? defaultMeta);
        deathDrops = options?.drops;
        delete = options?.delete;
        if (options.HasValue) {
            var o = options.Value;
            if (o.hp.HasValue) Enemy.SetHP(o.hp.Value, o.hp.Value);
        }
    }

    public override void ResetV() {
        base.ResetV();
        collisionTarget = BulletManager.PlayerTarget;
        collisionActive = collisionInfo.CollisionActiveOnInit;
        phaseController = SMPhaseController.Normal(0);
        dying = false;
        if (enemy != null) enemy.Initialize(this);
        if (displayer != null) displayer.ResetV(this);
        //Pooled objects should not be running SMs from the inspector, only via Initialize,
        //so there is no RunImmediateSM in ResetV.
    }
    
    /// <summary>
    /// Run the attached behaviorScript.
    /// </summary>
    public void RunAttachedSM() {
        if (behaviorScript != null) {
            RunPatternSM(StateMachineManager.FromText(behaviorScript));
        }
    }

    public void RunPatternSM(StateMachine sm) => _ = 
        BeginBehaviorSM(SMRunner.RunNoCancelRoot(sm), phaseController.WhatIsNextPhase(0));

    private static bool IsNontrivialID([CanBeNull] string id) => !string.IsNullOrWhiteSpace(id) && id != "_";

    /// <summary>
    /// Safe to call twice.
    /// </summary>
    private void RegisterID() {
        if (!idLookup.ContainsKey(ID)) { idLookup[ID] = new HashSet<BehaviorEntity>(); }
        idLookup[ID].Add(this);
        if (IsNontrivialID(ID)) {
            if (pointersToResolve.TryGetValue(ID, out BEHPointer behp)) {
                behp.Attach(this);
                pointersToResolve.Remove(ID);
                attachedPointers[ID] = behp;
            }
        }
    }
    /// <summary>
    /// Safe to call twice.
    /// </summary>
    private void UnregisterID() {
        if (idLookup.TryGetValue(ID, out var dct)) dct.Remove(this);
        if (attachedPointers.TryGetValue(ID, out var behp)) {
            behp.Detach();
            pointersToResolve[ID] = behp;
            attachedPointers.Remove(ID);
        }
    }

    /// <summary>
    /// You can technically overlay this on a BEH that is initialized with non-empty velocity,
    /// but the results are not well-defined.
    /// </summary>
    /// <returns></returns>
    public IEnumerator ExecuteVelocity(LimitedTimeVelocity ltv, uint newId) {
        if (ltv.cT.Cancelled) { ltv.done(); yield break; }
        Velocity vel = new Velocity(ltv.VTP2, GlobalPosition(), V2RV2.Angle(original_angle));
        float doTime = (ltv.enabledFor < float.Epsilon) ? float.MaxValue : ltv.enabledFor;
        ParametricInfo tbpi = new ParametricInfo(bpi.loc, ltv.firingIndex, newId);
        //Sets initial position correctly for offset-based velocity
        _ = vel.UpdateZero(ref tbpi, 0f);
        SetTransformGlobalPosition(tbpi.loc);
        if (ltv.ThisCannotContinue(tbpi)) { ltv.done(); yield break; }
        for (; tbpi.t < doTime - ETime.FRAME_TIME;) {
            tbpi.loc = bpi.loc;
            vel.UpdateDeltaAssignAcc(ref tbpi, out Vector2 delta, ETime.FRAME_TIME);
            //Checking the canceller before committing position allows using eg. successive onscreen checks.
            //This is a core use case for move-while. So we split up velocitystep to allow it
            if (ltv.ThisCannotContinue(tbpi)) {
                SetDirection(Vector2.zero);
                ltv.done(); yield break;
            }
            SetDirection(delta);
            SetTransformGlobalPosition(bpi.loc = tbpi.loc);
            yield return null;
            if (ltv.cT.Cancelled) {
                SetDirection(Vector2.zero);
                ltv.done(); yield break;
            }
        }
        tbpi.loc = bpi.loc;
        VelocityStepAndLook(ref vel, ref tbpi, doTime - tbpi.t);
        bpi.loc = tbpi.loc;
        SetDirection(Vector2.zero);
        ltv.done();
    }


    #region Death

    /// <summary>
    /// Call this from hp-management scripts when you are out of HP.
    /// Returns True iff the BEH is now going to cull.
    /// </summary>
    public virtual bool OutOfHP() {
        if (PhaseShifter != null) {
            ShiftPhase();
            return false;
        } else {
            Poof(true);
            return true;
        }
    }

    private void DestroyInitial(bool allowFinalize, bool allowDrops=false) {
        if (dying) return;
        dying = true;
        collisionActive = false;
        if (allowDrops) DropItemsOnDeath();
        if (isSummoned) DataHoisting.Destroy(bpi.id);
        UnregisterID();
        if (enemy != null) enemy.IAmDead();
        HardCancel(allowFinalize);
    }

    private void DestroyFinal() {
        if (displayer != null) displayer.Hide();
        //Flip(false, false);
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
            DestroyInitial(true, drops ?? AmIOutOfHP);
            if (enemy != null) enemy.DoSuicideFire();
            GameManagement.campaign.DestroyNormalEnemy();
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

    private void VelocityStepAndLook(ref Velocity vel, ref ParametricInfo pi, float dT=ETime.FRAME_TIME) {
        vel.UpdateDeltaAssignAcc(ref pi, out Vector2 delta, dT);
        SetDirection(delta);
        SetTransformGlobalPosition(pi.loc);
    }

    protected virtual void RegularUpdateMove() {
        if (doVelocity) {
            if (parented) {
                velocity.rootPos = GetParentPosition() + firedOffset;
                bpi.loc = tr.position;
            }
            VelocityStepAndLook(ref velocity, ref bpi);
        } else {
            bpi.t += ETime.FRAME_TIME;
            if (parented) bpi.loc = tr.position;
        }
    }
    
    private void RegularUpdateControl()  {
        //thisStyleControls may change during iteration. Don't respect changes
        myStyle.IterateControls(this);
    }

    private void RegularUpdateCollide() {
        if (collisionActive) {
            var cr = CollisionCheck();
            if (grazeFrameCounter-- == 0) {
                grazeFrameCounter = 0;
                if (cr.graze) {
                    grazeFrameCounter = collisionInfo.grazeEveryFrames - 1;
                    collisionTarget.Player.Graze(1);
                }
            }
            if (cr.collide) {
                collisionTarget.Player.Hit(Damage);
                if (collisionInfo.destructible) InvokeCull();
            }
        }
        beh_cullCtr = (beh_cullCtr + 1) % checkCullEvery;
        if (delete?.Invoke(rBPI) == true) InvokeCull();
        else if (beh_cullCtr == 0 && cullableRadius.cullable && myStyle.CameraCullable 
            && bpi.t > FIRST_CULLCHECK_TIME && LocationService.OffPlayableScreenBy(ScreenCullRadius, bpi.loc)) {
            InvokeCull();
        }
    }
    
    protected virtual CollisionResult CollisionCheck() {
        return new CollisionResult(Collision.CircleOnCircle(collisionTarget.Hitbox, 
            rBPI.loc, collisionInfo.collisionRadius), false);
    }

    protected virtual void RegularUpdateRender() {
        if (displayer != null) {
            displayer.FaceInDirection(LastDelta);
            displayer.UpdateRender();
        }
    }

    public override void RegularUpdate() {
        if (dying) {
            base.RegularUpdate();
            RegularUpdateRender();
        } else {
            if (nextUpdateAllowed) {
                RegularUpdateMove();
                base.RegularUpdate();
            } else nextUpdateAllowed = true;
            RegularUpdateControl();
            if (dying) return; //controls may cause destruction
            RegularUpdateCollide();
            RegularUpdateRender();
        }
    }

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
        velocity.FlipX();
    }
    protected virtual void FlipVelY() {
        velocity.FlipY();
    }
    
    #endregion

    /// <summary>
    /// Destroy all other SMs and run an SM.
    /// Call this with any high-priority SMs-- they are not required to be pattern-type.
    /// While you can pass null here, that will still allocate some Task garbage.
    /// </summary>
    protected async Task BeginBehaviorSM(SMRunner sm, int startAtPatternId) {
        if (sm.sm == null || sm.cT.Cancelled) return;
        HardCancel(false);
        phaseController.SetDesiredNext(startAtPatternId);
        var cT = new Cancellable();
        var joint = sm.MakeNested(cT);
        using (var smh = new SMHandoff(this, sm, joint)) {
            behaviorToken.Add(cT);
            try {
                await sm.sm.Start(smh);
            } catch (Exception e) {
                if (!(e is OperationCanceledException)) {
                    Log.UnityError(Log.StackInnerException(e)
                        .Message); //This is only here for the vaguest of debugging purposes.
                }
            } finally {
                //It is possible for tasks to still be running at this point (most critically if
                // using ~), so we cancel to make sure they get destroyed
                cT.Cancel();
                behaviorToken.Remove(cT);
            }
            phaseController.RunEndingCallback();
            if (IsNontrivialID(ID)) {
                Log.Unity(
                    $"BehaviorEntity {ID} finished running its SM{(sm.cullOnFinish ? " and will destroy itself." : ".")}",
                    level: Log.Level.DEBUG2);
            }
            if (sm.cullOnFinish) {
                if (PoofOnPhaseEnd) Poof();
                else {
                    if (DeathEffectOnParentCull && sm.cT.Cancelled) TryDeathEffect();
                    InvokeCull();
                }
            }
        }
        
    }

    private bool AmIOutOfHP => enemy != null && enemy.HP <= 0;
    private bool PoofOnPhaseEnd => AmIOutOfHP && !(this is BossBEH);
    private bool DeathEffectOnParentCull => true;

    /// <summary>
    /// Run an SM in parallel to any currently running SMs.
    /// Do not call with pattern SMs.
    /// </summary>
    public async Task RunExternalSM(SMRunner sm) {
        if (sm.sm == null || sm.cT.Cancelled) return;
        var cT = new Cancellable();
        var joint = sm.MakeNested(cT);
        using (var smh = new SMHandoff(this, sm, joint)) {
            behaviorToken.Add(cT);
            try {
                await sm.sm.Start(smh);
            } catch (Exception e) {
                if (!(e is OperationCanceledException)) {
                    Log.UnityError(Log.StackInnerException(e)
                        .Message); //This is only here for the vaguest of debugging purposes.
                }
                //When ending a level, the order of OnDisable is random, so a node running a sub-SM may
                //be cancelled before its caller, so the caller cannot rely on this line throwing.
                //This is OK under the standard design pattern which is "check cT after awaiting".
                if (sm.cT.Cancelled) throw;
                //When running external SM, "local cancel" (due to death) is a valid output, and we should not throw.
                //Same as how Phase does not throw if OpCanceled is raised via shiftphasetoken.
            } finally {
                cT.Cancel();
                behaviorToken.Remove(cT);
            }
            if (sm.cullOnFinish) InvokeCull();
        }
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
        try {
            foreach (var cT in behaviorToken.ToArray()) {
                cT.Cancel();
            } //Cancelling cT will lead to removal from behaviorToken in the run methods.
        } catch (ObjectDisposedException) {
            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            Debug.LogWarning($"BehaviorEntity {ID} tried to cancel a disposed token. This should NOT occur outside of development.");
        }
        ForceClosingFrame();
        AllowFinishCalls = true;
        PhaseShifter = null;
    }

    public override void PreSceneClose() {
        HardCancel(false);
        base.PreSceneClose();
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
    
    public BehaviorEntity GetINode(string behName, uint? bpiid) => BEHPooler.INode(rBPI.loc, 
        V2RV2.Angle(original_angle), Direction, rBPI.index, bpiid, behName);


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

    private static readonly Dictionary<string, BEHPointer> pointersToResolve = new Dictionary<string, BEHPointer>();
    private static readonly Dictionary<string, BEHPointer> attachedPointers = new Dictionary<string, BEHPointer>();

    public static void ClearPointers() {
        pointersToResolve.Clear();
        attachedPointers.Clear();
    }
    public static BEHPointer GetPointerForID(string id) {
        if (attachedPointers.ContainsKey(id)) return attachedPointers[id];
        if (idLookup.ContainsKey(id)) {
            foreach (BehaviorEntity beh in idLookup[id]) {
                return new BEHPointer(beh);
            }
        }
        if (!pointersToResolve.ContainsKey(id)) pointersToResolve[id] = new BEHPointer();
        return pointersToResolve[id];
    }

    public static readonly ExFunction hpRatio =
        ExUtils.Wrap<BehaviorEntity, BEHPointer>("HPRatio");
    [UsedImplicitly]
    public static float HPRatio(BEHPointer behp) => behp.beh.Enemy.EffectiveBarRatio;

    public static readonly ExFunction photosTaken =
        ExUtils.Wrap<BehaviorEntity, BEHPointer>("PhotosTaken");
    [UsedImplicitly]
    public static float PhotosTaken(BEHPointer behp) => behp.beh.Enemy.PhotosTaken;

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

    private void OnDrawGizmos() {
        var loc = transform.position;
        Handles.color = Color.cyan;
        if (collisionInfo.collisionRadius > 0) 
            Handles.DrawWireDisc(loc, Vector3.forward, collisionInfo.collisionRadius);
    }

#endif
}

public class BEHPointer {
    public BehaviorEntity beh;
    private bool found;

    public BEHPointer(BehaviorEntity beh = null) {
        this.beh = beh;
        found = beh != null;
    }

    public void Attach(BehaviorEntity new_beh) {
        if (found) throw new Exception("Cannot attach BEHPointer twice");
        beh = new_beh;
        found = true;
    }

    public void Detach() {
        found = false;
        beh = null;
    }
}

}