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
using UnityEngine.Profiling;

namespace Danmaku {

[Serializable]
public struct ItemDrops {
    public int value;
    public int pointPP;
    public int life;
    public bool autocollect;
    public ItemDrops(int v, int p, int l, bool autoc=false) {
        value = v;
        pointPP = p;
        life = l;
        autocollect = autoc;
    }

    public ItemDrops Mul(float by) => new ItemDrops((int)(value * by), (int)(pointPP * by), (int)(life * by), autocollect);
}

/// <summary>
/// A high-level class that describes all complex danmaku-related objects (primarily bullets and NPCs).
/// </summary>
public partial class BehaviorEntity : Pooled<BehaviorEntity>, ITransformHandler {
    //BEH is only pooled when summoned via the firing API.
    public enum RotationMethod {
        Manual,
        InVelocityDirection,
        VelocityDirectionPlus90,
        VelocityDirectionMinus90
    }

    [Tooltip("This must be null for pooled BEH.")]
    public TextAsset behaviorScript;
    /// <summary>
    /// ID to refer to this entity in behavior scripts.
    /// </summary>
    public string ID;
    private static readonly Dictionary<string, HashSet<BehaviorEntity>> idLookup = new Dictionary<string, HashSet<BehaviorEntity>>();
    //This is automatically disposed by the state machine that generates it
    [CanBeNull] public CancellationTokenSource PhaseShifter { get; set; }
    private readonly HashSet<CancellationTokenSource> behaviorToken = new HashSet<CancellationTokenSource>();
    public int NumRunningSMs => behaviorToken.Count;
    protected Vector2 lastDelta;
    public Vector2 LastDelta => lastDelta;
    private Vector3 facingVec = Vector3.zero;
    /// <summary>
    /// Do not modify this at runtime.
    /// </summary>
    [FormerlySerializedAs("doMovement")] 
    [Tooltip("If set to false, will calculate movement in cposition but not actually move after Initialize.")]
    public bool displayChangedPosition = true;
    /// <summary>
    /// Only the original firing angle matters for rotational movement velocity
    /// </summary>
    public float original_angle { get; protected set; }
    public RotationMethod rotationMethod;
    /// <summary>
    /// Whether or not rotationMethod affects the transform. In the case of iparent,
    /// entities parented to this will be modified by transform rotation.
    /// </summary>
    public bool rotateTransform;

    protected bool dying { get; private set; } = false;
    [CanBeNull] private Animator anim;
    [CanBeNull] protected SpriteRenderer sr;
    [CanBeNull] protected MaterialPropertyBlock pb;
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
    public bool triggersUITimeout = false;
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
    public bool cullable = true;
    public float ScreenCullRadius = 4;
    private const int checkCullEvery = 120;
    private int beh_cullCtr = 0;
    
    protected bool collisionActive = false;
    public int damage = 1;
    public bool destructible;
    public ushort grazeEveryFrames = 20;
    private int grazeFrameCounter = 0;

    /// <summary>
    /// Sets the transform position iff `doMovement` is enabled.
    /// This is an public function. BPI is not updated.
    /// </summary>
    /// <param name="p">Target global position</param>
    private void SetTransformGlobalPosition(Vector2 p) {
        if (displayChangedPosition) {
            if (parented) tr.position = p;
            else tr.localPosition = p; // Slightly faster pathway
        }
    }
    
    /// <summary>
    /// Sets the transform position iff `doMovement` is enabled.
    /// This is an external function. BPI is updated.
    /// </summary>
    /// <param name="p">Target local position</param>
    public void ExternalSetLocalPosition(Vector2 p) {
        p = movementModifiers.ApplyOver(p);
        if (displayChangedPosition) {
            tr.localPosition = p;
            bpi.loc = tr.position;
        } else {
            bpi.loc = (Vector2)tr.position + p;
        }
    }

    public enum DirectionRelation {
        RUFlipsLD,
        LDFlipsRU,
        RUCopiesLD,
        LDCopiesRU,
        Independent,
        None
    }

    [Serializable]
    public struct Animation {
        [Serializable]
        public struct FrameConfig {
            public Frame[] idleAnim;
            public Frame[] rightAnim;
            public Frame[] leftAnim;
            public Frame[] upAnim;
            public Frame[] downAnim;
            public Frame[] attackAnim;
            public Frame[] deathAnim;
            public FrameRunner runner;

            private Frame[] GetFramesForAnimType(AnimationType typ) {
                if (typ == AnimationType.Attack) return attackAnim;
                if (typ == AnimationType.Right) return rightAnim;
                if (typ == AnimationType.Left) return leftAnim;
                if (typ == AnimationType.Up) return upAnim;
                if (typ == AnimationType.Down) return downAnim;
                if (typ == AnimationType.Death) return deathAnim;
                return idleAnim;
            }
            
            [CanBeNull]
            public Sprite SetAnimationTypeIfPriority(AnimationType typ, bool loop, [CanBeNull] Action onLoopOrFinish) => 
                runner.SetAnimationTypeIfPriority(typ, GetFramesForAnimType(typ), loop, onLoopOrFinish);

            public Sprite ResetToIdle() => 
                runner.SetAnimationType(AnimationType.None, GetFramesForAnimType(AnimationType.None), true, noop);

            [CanBeNull]
            public Sprite Update(float dT) {
                var (resetMe, updSprite) = runner.Update(dT);
                return resetMe ? ResetToIdle() : updSprite;
            }
        }

        public enum AnimationMethod {
            None,
            Frames,
            Animator,
        }
        public AnimationMethod method;
        public DirectionRelation LRRelation;
        public DirectionRelation UDRelation;
        public FrameConfig frames;

        private BehaviorEntity beh;
        public void Initialize(BehaviorEntity setBEH) {
            beh = setBEH;
            if (method == AnimationMethod.Frames) beh.SetSprite(frames.ResetToIdle());
        }
        
        private enum Direction : byte {
            None,
            Left,
            Right,
            Up,
            Down
        }

        private int DirectionToAnimInt(Direction d) {
            if (d == Direction.Right) return 1;
            if (d == Direction.Up) return 2;
            if (d == Direction.Left) return 3;
            if (d == Direction.Down) return 4;
            return 0;
        }

        private bool XFlipped;
        private bool YFlipped;
        //TODO call on end
        private void Flip(bool flipX, bool flipY) {
            if (flipX != XFlipped || flipY != YFlipped) {
                Vector3 trs = beh.tr.localScale;
                if (flipX != XFlipped) trs.x *= -1;
                if (flipY != YFlipped) trs.y *= -1;
                XFlipped = flipX;
                YFlipped = flipY;
                beh.tr.localScale = trs;
            }
        }
        private void SetDirection(Direction d, bool flipX, bool flipY) {
            Flip(flipX, flipY);
            if (method == AnimationMethod.Animator) {
                beh.anim.SetInteger(AnimIDRepo.direction, DirectionToAnimInt(d));
            } else if (method == AnimationMethod.Frames) {
                beh.SetSprite(frames.SetAnimationTypeIfPriority(AsAnimType(d), true, noop));
            }
        }

        private Direction Opposite(Direction d) {
            if (d == Direction.Right) return Direction.Left;
            if (d == Direction.Left) return Direction.Right;
            if (d == Direction.Up) return Direction.Down;
            if (d == Direction.Down) return Direction.Up;
            return Direction.None;
        }

        private AnimationType AsAnimType(Direction d) {
            if (d == Direction.Right) return AnimationType.Right;
            if (d == Direction.Left) return AnimationType.Left;
            if (d == Direction.Up) return AnimationType.Up;
            if (d == Direction.Down) return AnimationType.Down;
            return AnimationType.None;
        }

        private (Direction d, bool flipX, bool flipY) ReduceDirection(Direction primary, Direction secondary) {
            var dfx = ReduceDirection(primary);
            if (dfx.d == Direction.None) dfx = ReduceDirection(secondary);
            return dfx;
        }
        private (Direction d, bool flipX, bool flipY) ReduceDirection(Direction d) {
            bool flipX = false;
            bool flipY = false;
            if (d == Direction.Left) {
                if (LRRelation == DirectionRelation.None) d = Direction.None;
                if (LRRelation == DirectionRelation.LDCopiesRU) d = Direction.Right;
                if (LRRelation == DirectionRelation.LDFlipsRU) {
                    d = Direction.Right;
                    flipX = true;
                }
            } else if (d == Direction.Right) {
                if (LRRelation == DirectionRelation.None) d = Direction.None;
                if (LRRelation == DirectionRelation.RUCopiesLD) d = Direction.Left;
                if (LRRelation == DirectionRelation.RUFlipsLD) {
                    d = Direction.Left;
                    flipX = true;
                }
            } else if (d == Direction.Up) {
                if (UDRelation == DirectionRelation.None) d = Direction.None;
                if (UDRelation == DirectionRelation.RUCopiesLD) d = Direction.Down;
                if (UDRelation == DirectionRelation.RUFlipsLD) {
                    d = Direction.Down;
                    flipY = true;
                }
            } else if (d == Direction.Down) {
                if (UDRelation == DirectionRelation.None) d = Direction.None;
                if (UDRelation == DirectionRelation.LDCopiesRU) d = Direction.Up;
                if (UDRelation == DirectionRelation.LDFlipsRU) {
                    d = Direction.Up;
                    flipY = true;
                }
            }
            return (d, flipX, flipY);
        }

        private const float movCutoff = 0.0000001f;
        /// <summary>
        /// Select the animation according to the direction.
        /// </summary>
        /// <param name="dir">Unnormalized direction vector.</param>
        public void FaceInDirection(Vector2 dir) {
            if (method == AnimationMethod.None) return;
            Direction d1 = Direction.None;
            Direction d2 = Direction.None;
            dir = dir.normalized;
            var x = dir.x * dir.x;
            var y = dir.y * dir.y;
            var lr = (x < movCutoff) ? Direction.None : (dir.x > 0) ? Direction.Right : Direction.Left;
            var ud = (y < movCutoff) ? Direction.None : (dir.y > 0) ? Direction.Up : Direction.Down;
            if (x > y) {
                d1 = lr;
                d2 = ud;
            } else {
                d1 = ud;
                d2 = lr;
            }
            var (direction, flipX, flipY) = ReduceDirection(d1, d2);
            SetDirection(direction, flipX, flipY);
        }

        public void Update(float dT) {
            if (method == AnimationMethod.Frames) {
                beh.SetSprite(frames.Update(dT));
            }
        }

        public void Animate(AnimationType typ, bool loop, [CanBeNull] Action done) {
            if (method == AnimationMethod.Frames) {
                beh.SetSprite(frames.SetAnimationTypeIfPriority(typ, loop, done));
            }
        }
    }

    public Animation animate;
    public MovementModifiers movementModifiers;
    private bool isSummoned = false;

    protected override void Awake() {
        base.Awake();
        bpi = ParametricInfo.WithRandomId(tr.position, 0);
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        enemy = GetComponent<Enemy>();
        ResetV();
        animate.Initialize(this);
        RegisterID();
        UpdateStyleInformation();
    }

    // Do this in Start to make sure that services are loaded...
    protected virtual void Start() => RunAttachedSM();

    public Task Initialize(SMRunner smr) {
        if (smr.sm != null) {
            behaviorScript = null;
            return BeginBehaviorSM(smr, 0);
        }
        return Task.CompletedTask;
    }
    public void Initialize(Vector2 parentLoc, V2RV2 position, MovementModifiers m,
        SMRunner sm, int firingIndex, uint? bpiid, string behName = "") =>
        Initialize(new Velocity(parentLoc, position, m), m, sm, firingIndex, bpiid, null, behName);

    /// <summary>
    /// If parented, we need firing offset to update Velocity's root position with parent.pos + offset every frame.
    /// </summary>
    private Vector2 firedOffset;

    /// <summary>
    /// Initialize a BEH. You are not required to call this, but all BEH that are generated in code should use this.
    /// </summary>
    /// <param name="_velocity">Velocity struct</param>
    /// <param name="m">Movement modifiers applied to spawned entities and velocity</param>
    /// <param name="smr">SM to execute. Set null if no SM needs to be run.</param>
    /// <param name="firingIndex">Firing index of BPI that will be created.</param>
    /// <param name="bpiid">ID of BPI that will be created.</param>
    /// <param name="parent">Transform parent of this BEH. Use sparingly</param>
    /// <param name="behName"></param>
    /// <param name="options"></param>
    public void Initialize(Velocity _velocity, MovementModifiers m, SMRunner smr, int firingIndex=0, 
        uint? bpiid=null, [CanBeNull] BehaviorEntity parent=null, string behName="", RealizedBehOptions? options=null) {
        if (parent != null) TakeParent(parent);
        isSummoned = true;
        tr.localPosition = firedOffset = _velocity.rootPos;
        _velocity.rootPos = bpi.loc = tr.position;
        original_angle = _velocity.angle;
        bpi = new ParametricInfo(_velocity.rootPos, firingIndex, bpiid ?? RNG.GetUInt());
        AssignVelocity(_velocity);
        if (doVelocity) {
            FaceInDirection(velocity.UpdateZero(ref bpi, 0f));
            tr.position = bpi.loc;
        }
        if (IsNontrivialID(behName)) ID = behName;
        movementModifiers = m;
        Initialize(smr);
        //This comes after so SMs run due to ~@ commands are not destroyed by BeginBehaviorSM
        RegisterID();
        UpdateStyleInformation();
        deathDrops = options?.drops;
        if (options.HasValue) {
            var o = options.Value;
            if (o.hp.HasValue) Enemy.SetHP(o.hp.Value, o.hp.Value);
        }
    }

    public override void ResetV() {
        base.ResetV();
        phaseController = SMPhaseController.Normal(0);
        dying = false;
        if (sr != null) {
            sr.enabled = true;
            sr.GetPropertyBlock(pb = new MaterialPropertyBlock());
        }
        tr.localEulerAngles = facingVec = new Vector3(0, 0, 0);
        if (enemy != null) enemy.Initialize(this, sr);
        //Pooled objects should not be running SMs from the inspector, only via Initialize,
        //so there is no RunImmediateSM in ResetV.
    }
    
    /// <summary>
    /// Run the attached behaviorScript.
    /// </summary>
    protected void RunAttachedSM() {
        if (behaviorScript != null) {
            _ = BeginBehaviorSM(SMRunner.RunNoCancel(StateMachineManager.GetSMFromTextAsset(behaviorScript)), phaseController.WhatIsNextPhase(0));
        }
    }

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
        if (ltv.cT.IsCancellationRequested) { ltv.done(); yield break; }
        Velocity vel = new Velocity(ltv.VTP2, GlobalPosition(), V2RV2.Angle(original_angle), movementModifiers);
        float doTime = (ltv.enabledFor < float.Epsilon) ? float.MaxValue : ltv.enabledFor;
        ParametricInfo tbpi = new ParametricInfo(bpi.loc, ltv.firingIndex, newId);
        //Sets initial position correctly for offset-based velocity
        _ = vel.UpdateZero(ref tbpi, 0f);
        SetTransformGlobalPosition(tbpi.loc);
        if (ltv.ThisCannotContinue(tbpi)) { ltv.done(); yield break; }
        for (; tbpi.t < doTime - ETime.FRAME_TIME;) {
            tbpi.loc = bpi.loc;
            vel.UpdateDeltaAssignAcc(ref tbpi, out lastDelta, ETime.FRAME_TIME);
            //Checking the canceller before committing position allows using eg. successive onscreen checks.
            //This is a core use case for move-while. So we split up velocitystep to allow it
            if (ltv.ThisCannotContinue(tbpi)) {
                lastDelta = Vector2.zero;
                ltv.done(); yield break;
            }
            SetTransformGlobalPosition(bpi.loc = tbpi.loc);
            yield return null;
            if (ltv.cT.IsCancellationRequested) {
                lastDelta = Vector2.zero;
                ltv.done(); yield break;
            }
        }
        tbpi.loc = bpi.loc;
        VelocityStepAndLook(ref vel, ref tbpi, doTime - tbpi.t);
        bpi.loc = tbpi.loc;
        lastDelta = Vector2.zero;
        ltv.done();
    }

    public void FadeSpriteOpacity(BPY fader01, float time, CancellationToken cT, Action done) {
        Color c = sr.color;
        var tbpi = ParametricInfo.WithRandomId(bpi.loc, bpi.index);
        c.a = fader01(tbpi);
        sr.color = c;
        RunRIEnumerator(_FadeSpriteOpacity(fader01, tbpi, time, cT, done));
    }
    private IEnumerator _FadeSpriteOpacity(BPY fader01, ParametricInfo tbpi, float time, CancellationToken cT, Action done) {
        if (cT.IsCancellationRequested) { done(); yield break; }
        if (sr == null) throw new Exception($"Tried to fade sprite on BEH without sprite {ID}");
        Color c = sr.color;
        for (tbpi.t = 0f; tbpi.t < time - ETime.FRAME_YIELD; tbpi.t += ETime.FRAME_TIME) {
            yield return null;
            if (cT.IsCancellationRequested) { break; } //Set to target and then leave
            tbpi.loc = bpi.loc;
            c.a = fader01(tbpi);
            sr.color = c;
        }
        c.a = fader01(tbpi);
        sr.color = c;
        done();
    }

    private void SetSprite([CanBeNull] Sprite sprite) {
        if (sprite != null) {
            sr.sprite = sprite;
            pb.SetTexture(PropConsts.mainTex, sprite.texture);
        }
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
        if (allowDrops) DropItemsOnDeath();
        if (isSummoned) PrivateDataHoisting.Destroy(bpi.id);
        UnregisterID();
        if (enemy != null) enemy.IAmDead();
        HardCancel(allowFinalize);
    }

    private void DestroyFinal() {
        if (sr != null) sr.enabled = false;
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
        if (dying) return;
        DestroyInitial(true, drops ?? AmIOutOfHP);
        if (enemy != null) enemy.DoSuicideFire();
        GameManagement.campaign.DestroyNormalEnemy();
        TryDeathEffect();
        animate.Animate(AnimationType.Death, false, DestroyFinal);
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
        BulletManager.RequestSimple(styleName, null, null, new Velocity(bpi.loc, lastDelta.normalized), 0, 0, null);
    }
    
    /// <summary>
    /// Updates the transform and internal rotation measure (for TargetDir) in accordance with a direction.
    /// </summary>
    /// <param name="dir">(Unnormalized) direction vector. If zero, noops.</param>
    public void FaceInDirection(Vector2 dir) {
        if (rotationMethod != RotationMethod.Manual && dir.x * dir.x + dir.y * dir.y > 0f) {
            if (rotationMethod == RotationMethod.InVelocityDirection) {
                FaceInDirectionRaw(Mathf.Atan2(dir.y, dir.x) * M.radDeg);
            } else if (rotationMethod == RotationMethod.VelocityDirectionPlus90) {
                FaceInDirectionRaw(Mathf.Atan2(dir.x, -dir.y) * M.radDeg);
            } else if (rotationMethod == RotationMethod.VelocityDirectionMinus90) {
                FaceInDirectionRaw(Mathf.Atan2(-dir.x, dir.y) * M.radDeg);
            }
        }
    }

    public void FaceInDirectionRaw(float deg) {
        facingVec.z = deg;
        if (rotateTransform) {
            tr.eulerAngles = facingVec;
        }
    }

    private void VelocityStepAndLook(ref Velocity vel, ref ParametricInfo pi, float dT=ETime.FRAME_TIME) {
        vel.UpdateDeltaAssignAcc(ref pi, out lastDelta, dT);
        SetTransformGlobalPosition(pi.loc);
    }

    /// <summary>
    /// Normalized
    /// </summary>
    /// <returns></returns>
    protected virtual Vector2 GetGlobalDirection() => M.PolarToXY(facingVec.z);

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
        var curr_pcs = thisStyleControls; //thisStyleControls may change during iteration. Don't respect changes
        int ct = curr_pcs.Count;
        for (int ii = 0; ii < ct; ++ii) {
            curr_pcs[ii].action(this);
        }
    }

    private void RegularUpdateCollide() {
        if (collisionActive) {
            var cr = CollisionCheck();
            if (grazeFrameCounter-- == 0) {
                grazeFrameCounter = 0;
                if (cr.graze) {
                    grazeFrameCounter = grazeEveryFrames - 1;
                    BulletManager.ExternalBulletProc(0, 1);
                }
            }
            if (cr.collide) {
                BulletManager.ExternalBulletProc(damage, 0);
                if (destructible) InvokeCull();
            }
        }
        beh_cullCtr = (beh_cullCtr + 1) % checkCullEvery;
        if (beh_cullCtr == 0 && cullable && styleIsCameraCullable 
            && bpi.t > FIRST_CULLCHECK_TIME && LocationService.OffPlayableScreenBy(ScreenCullRadius, bpi.loc)) {
            InvokeCull();
        }
    }
    
    protected virtual CollisionResult CollisionCheck() => CollisionResult.noColl;

    protected virtual bool Contains(Vector2 pt) => throw new Exception($"The BEH {ID} does not have a collision method.");

    protected virtual void RegularUpdateRender() {
        FaceInDirection(lastDelta);
        animate.FaceInDirection(lastDelta);
        animate.Update(ETime.FRAME_TIME);
        if (pb != null) {
            pb.SetFloat(PropConsts.time, bpi.t);
            sr.SetPropertyBlock(pb);
        }
    }

    public override void RegularUpdate() {
        if (dying) {
            base.RegularUpdate();
            RegularUpdateRender();
        } else {
            if (nextUpdateAllowed) {
                //Note: Don't profile during parallelization
                RegularUpdateMove();
                base.RegularUpdate();
            } else nextUpdateAllowed = true;
            RegularUpdateControl();
            RegularUpdateCollide();
            RegularUpdateRender();
        }
    }

    protected bool nextUpdateAllowed = true;

    public override int UpdatePriority => UpdatePriorities.BEH;

    #region Interfaces

    public Vector2 LocalPosition() {
        if (displayChangedPosition) {
            if (parented) return tr.localPosition;
            return bpi.loc;
        }
        throw new NotImplementedException("Cannot get local position on BEH with ignoreMovement");
    }

    /// <summary>
    /// For external consumption
    /// </summary>
    /// <returns></returns>
    public virtual Vector2 GlobalPosition() => bpi.loc;

    /// <summary>
    /// For inner consumption by pathers and other objects that use extra location systems
    /// </summary>
    public Vector2 RawGlobalPosition() => bpi.loc;

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
        if (sm.sm == null || sm.cT.IsCancellationRequested) return;
        HardCancel(false);
        phaseController.SetDesiredNext(startAtPatternId);
        using (CancellationTokenSource cT = new CancellationTokenSource()) {
            using (CancellationTokenSource joint = CancellationTokenSource.CreateLinkedTokenSource(cT.Token, sm.cT)) {
                using (var smh = new SMHandoff(this, sm, joint.Token)) {
                    behaviorToken.Add(cT);
                    try {
                        await sm.sm.Start(smh);
                        behaviorToken.Remove(cT);
                    } catch (Exception e) {
                        behaviorToken.Remove(cT);
                        if (!(e is OperationCanceledException)) {
                            Log.UnityError(Log.StackInnerException(e).Message); //This is only here for the vaguest of debugging purposes.
                        }
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
                            if (DeathEffectOnParentCull && sm.cT.IsCancellationRequested) TryDeathEffect();
                            InvokeCull();
                        }
                    }
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
        if (sm.sm == null || sm.cT.IsCancellationRequested) return;
        using (CancellationTokenSource pcTS = new CancellationTokenSource()) {
            using (CancellationTokenSource joint = CancellationTokenSource.CreateLinkedTokenSource(sm.cT, pcTS.Token)) {
                using (var smh = new SMHandoff(this, sm, joint.Token)) {
                    behaviorToken.Add(pcTS);
                    try {
                        await sm.sm.Start(smh);
                        behaviorToken.Remove(pcTS);
                    } catch (Exception e) {
                        behaviorToken.Remove(pcTS);
                        if (!(e is OperationCanceledException)) {
                            Log.UnityError(Log.StackInnerException(e)
                                .Message); //This is only here for the vaguest of debugging purposes.
                        }
                        //When ending a level, the order of OnDisable is random, so a node running a sub-SM may
                        //be cancelled before its caller, so the caller cannot rely on this line throwing.
                        //This is OK under the standard design pattern which is "check cT after awaiting".
                        if (sm.cT.IsCancellationRequested) throw;
                        //When running external SM, "local cancel" (due to death) is a valid output, and we should not throw.
                        //Same as how Phase does not throw if OpCanceled is raised via shiftphasetoken.
                    }
                    if (sm.cullOnFinish) InvokeCull();
                }
            }
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
    
    public BehaviorEntity GetINode(string behName, uint? bpiid) => BEHPooler.INode(bpi.loc, 
        V2RV2.Angle(original_angle), M.PolarToXY(facingVec.z), bpi.index, bpiid, behName);


#if UNITY_EDITOR
    [ContextMenu("Debug all BEHIDs")]
    public void DebugBEHID() {
        int total = 0;
        foreach (var p in idLookup.Values) {
            total += p.Count;
        }
        Debug.LogFormat("Found {0} BEH", total);
    }

    public void _RunPatternSM(StateMachine sm) => _ = BeginBehaviorSM(SMRunner.RunNoCancel(sm), 0);
    
    
#endif
    
    public float RotationDeg => facingVec.z;

    private static readonly Action noop = () => { };
    [ContextMenu("Animate Attack")]
    public void AnimateAttack() => animate.Animate(AnimationType.Attack, false, noop);

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
    public static float HPRatio(BEHPointer behp) => behp.beh.Enemy.HPRatio;

    public static readonly ExFunction contains =
        ExUtils.Wrap<BehaviorEntity>("Contains", typeof(BEHPointer), typeof(Vector2));
    [UsedImplicitly]
    public static bool Contains(BEHPointer behp, Vector2 pt) => behp.beh.Contains(pt);

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