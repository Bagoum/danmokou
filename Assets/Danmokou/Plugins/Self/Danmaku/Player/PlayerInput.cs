using System;
using System.Collections;
using System.Linq.Expressions;
using DMK.Behavior;
using DMK.Behavior.Display;
using DMK.Behavior.Functions;
using DMK.Core;
using DMK.Dialogue;
using DMK.DMath;
using DMK.Expressions;
using DMK.GameInstance;
using DMK.Graphics;
using DMK.Scriptables;
using DMK.Services;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using static DMK.DMath.LocationHelpers;

namespace DMK.Player {
public class PlayerInput : BehaviorEntity {
    public SpriteRenderer ghostSource = null!;
    public SOPlayerHitbox hitbox = null!;
    public SpriteRenderer hitboxSprite = null!;
    public SpriteRenderer meter = null!;
    public Color meterDisplayShadow;
    public Color meterDisplayInner;
    private MaterialPropertyBlock meterPB = null!;

    [Header("Movement")] public float blueBoxRadius = .1f;
    [Tooltip("120 frames per sec")] public int lnrizeSpeed = 10;
    public float lnrizeRatio = .7f;
    private float timeSinceLastStandstill;
    public bool IsMoving => timeSinceLastStandstill > 0;

    private LayerMask collMask;
    public PlayerConfig thisPlayer = null!;
    public ShotConfig defaultShot = null!;

    public ShotConfig Shot { get; private set; } = null!;
    private Subshot Subshot { get; set; } = Subshot.TYPE_D;
    //This doesn't change if the player is not using a multishot, as opposed to Instance.Subshot.
    private float _subshotValue => Shot.isMultiShot ? (int) Subshot : 1;
    [UsedImplicitly]
    public static float SubshotValue { get; private set; }
    //TODO I need a cleaner way to handle laser destruction dependencies
    [UsedImplicitly] public static float PlayerShotItr => playerShotItr;
    private static ushort playerShotItr = 0;
    private GameObject? spawnedShot;
    private AyaCamera? spawnedCamera;

    public GameObject ghost = null!;
    public float ghostFadeTime;
    public int ghostFrequency;
    public ParticleSystem speedLines = null!;
    public EffectStrategy RespawnOnHitEffect = null!;
    public EffectStrategy RespawnAfterEffect = null!;
    private PlayerHP health = null!;

    public float baseFocusOverlayOpacity = 0.7f;
    public SpriteRenderer[] focusOverlay = null!;
    private const float FreeFocusLerpTime = 0.3f;
    private float freeFocusLerp01 = 0f;

    public static MultiAdder FiringDisabler { get; private set; } = new MultiAdder(0, null);
    public static MultiAdder BombDisabler { get; private set; } = new MultiAdder(0, null);
    public static MultiAdder AllControlDisabler { get; private set; } = new MultiAdder(0, null);
    public static bool PlayerActive => (AllControlDisabler.Value == 0) && !Dialoguer.DialogueActive;
    public bool AllowPlayerInput => PlayerActive && StateAllowsInput(state);

    public bool IsFocus =>
        Restrictions.FocusAllowed && (Restrictions.FocusForced || (InputManager.IsFocus && AllowPlayerInput));
    public bool IsFiring =>
        InputManager.IsFiring && AllowPlayerInput && FiringDisabler.Value == 0;
    public bool IsTryingBomb =>
        InputManager.IsBomb && AllowPlayerInput && BombDisabler.Value == 0;
    public bool IsTryingWitchTime => InputManager.IsMeter && AllowPlayerInput;
    
    public override int UpdatePriority => UpdatePriorities.PLAYER;
    

    /// <summary>
    /// Called when the player activates the meter.
    /// </summary>
    public static readonly Events.Event0 PlayerActivatedMeter = new Events.Event0();
    /// <summary>
    /// Called when the player deactivates the meter.
    /// </summary>
    public static readonly Events.Event0 PlayerDeactivatedMeter = new Events.Event0();
    /// <summary>
    /// Called every frame during meter activation.
    /// </summary>
    public static readonly Events.IEvent<Color> MeterIsActive = new Events.Event<Color>();
    /// <summary>
    /// Call this to change the player shot.
    /// Note that effects will not persist if a new player is created (ie. on scene change).
    /// </summary>
    public static readonly Events.IEvent<(ShotConfig?, Subshot?)> RequestShotUpdate =
        new Events.Event<(ShotConfig?, Subshot?)>();
    /// <summary>
    /// Call this to change the player character.
    /// Note that effects will not persist if a new player is created (ie. on scene change).
    /// </summary>
    public static readonly Events.IEvent<PlayerConfig> RequestPlayerUpdate = new Events.Event<PlayerConfig>();

    private static void ResetDisablers() {
        FiringDisabler = new MultiAdder(0, null);
        BombDisabler = new MultiAdder(0, null);
        AllControlDisabler = new MultiAdder(0, null);
    }
    protected override void Awake() {
        base.Awake();
        health = GetComponent<PlayerHP>();
        //Log.Unity($"Player awake", level: Log.Level.DEBUG1);
        collMask = LayerMask.GetMask("Wall");
        hitbox.location = tr.position;
        hitboxSprite.enabled = SaveData.s.UnfocusedHitbox;
        meter.enabled = false;
        meter.GetPropertyBlock(meterPB = new MaterialPropertyBlock());
        meterPB.SetFloat(PropConsts.innerFillRatio, (float)InstanceData.meterUseThreshold);
        meterPB.SetColor(PropConsts.unfillColor, meterDisplayShadow);
        meterPB.SetColor(PropConsts.fillInnerColor, meterDisplayInner);
        meter.SetPropertyBlock(meterPB);

        if (LoadPlayer()) {
            health.Setup(this);
            PastPositions.Add(hitbox.location);
            MarisaAPositions.Add(hitbox.location);
            LoadShot(true, GameManagement.Instance.Shot, GameManagement.Instance.Subshot);
            ResetDisablers();
            RunNextState(PlayerState.NORMAL);
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(Events.ScoreItemHasReceived, BufferScoreLabel);
        Listen(RequestShotUpdate, ss => LoadShot(false, ss.Item1, ss.Item2));
        Listen(RequestPlayerUpdate, p => {
            if (p != thisPlayer) {
                InvokeCull();
                Instantiate(p.prefab);
            }
        });
    }

    private IChallengeManager? challenge;
    private ChallengeManager.Restrictions Restrictions => 
        challenge?.Restriction ?? ChallengeManager.Restrictions.Default;

    public override void FirstFrame() {
        challenge = DependencyInjection.MaybeFind<IChallengeManager>();
    }
    

    /// <summary>
    /// Returns true if this object survived, false if it was destroyed.
    /// </summary>
    private bool LoadPlayer() {
        var p = GameManagement.Instance.Player;
        if (p != null && p != thisPlayer) {
            Log.Unity($"Reconstructing player object from {thisPlayer.key} to {p.key}", level: Log.Level.DEBUG2);
            GameObject.Instantiate(p.prefab, tr.position, Quaternion.identity);
            InvokeCull();
            return false;
        } else {
            Log.Unity($"Player object {thisPlayer.key} loaded", level:Log.Level.DEBUG2);
            p = thisPlayer;
            hitbox.radius = p.hitboxRadius * (float) GameManagement.Difficulty.playerHitboxMultiplier;
            hitbox.largeRadius = p.grazeboxRadius * (float) GameManagement.Difficulty.playerGrazeboxMultiplier;
            return true;
        }
    }

    /// <summary>
    /// Returns true if there was something to be destroyed.
    /// </summary>
    /// <returns></returns>
    public bool DestroyExistingShot() {
        if (spawnedShot != null) {
            //This is kind of stupid but it's necessary to ensure that
            //the coroutines end immediately rather than at the end of the update loop
            spawnedShot.GetComponentsInChildren<BehaviorEntity>().ForEach(b => b.InvokeCull());
            Destroy(spawnedShot.gameObject);
            return true;
        }
        return false;
    }
    private void LoadShot(bool firstLoad, ShotConfig? shot, Subshot? newSubshot) {
        if (shot == null) shot = defaultShot;
        var _newSubshot = newSubshot ?? Subshot;
        if (!firstLoad && Shot == shot && (!shot.isMultiShot || _newSubshot == Subshot)) {
            Subshot = _newSubshot;
            return;
        }
        ++playerShotItr;
        Subshot = _newSubshot;
        Shot = shot;
        if (firstLoad) {
            //Load all the reflection functions now to avoid lag on subshot switch.
            Shot.Subshots?.ForEach(s => s.prefab.GetComponentsInChildren<FireOption>().ForEach(fo => fo.Preload()));
        }
        Log.Unity($"Loading player shot: {Shot.key} : sub {Subshot.Describe()}", level: Log.Level.DEBUG2);
        if (DestroyExistingShot()) 
            SFXService.Request(Shot.onSwap);
        var realized = Shot.GetSubshot(Subshot);
        spawnedShot = realized.playerChild ? 
            GameObject.Instantiate(realized.prefab, tr) : 
            GameObject.Instantiate(realized.prefab);
        spawnedShot.GetComponentsInChildren<FireOption>().ForEach(fo => fo.Initialize(this));
        spawnedCamera = spawnedShot.GetComponent<AyaCamera>();
        if (spawnedCamera != null) spawnedCamera.Initialize(this);
    }


    #region FiringHelpers
    public float TimeFree { get; private set; }
    public float TimeFocus { get; private set; }
    public float FiringTimeFree { get; private set; }
    public float FiringTimeFocus { get; private set; }
    public float FiringTime { get; private set; }
    public float UnFiringTimeFree { get; private set; }
    public float UnFiringTimeFocus { get; private set; }
    public float UnFiringTime { get; private set; }

    private const int POSITION_REMEMBER_FRAMES = 120;
    private readonly CircularList<Vector2> PastPositions = new CircularList<Vector2>(POSITION_REMEMBER_FRAMES);
    private readonly CircularList<Vector2> PastDirections = new CircularList<Vector2>(POSITION_REMEMBER_FRAMES);
    /// <summary>
    /// Unlike normal position tracking, MarisaA positions freeze when focused.
    /// </summary>
    private readonly CircularList<Vector2> MarisaAPositions = new CircularList<Vector2>(POSITION_REMEMBER_FRAMES);
    private readonly CircularList<Vector2> MarisaADirections = new CircularList<Vector2>(POSITION_REMEMBER_FRAMES);
    
    [UsedImplicitly]
    public Vector2 PastPosition(float timeAgo) =>
        PastPositions.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS));
    public static readonly ExFunction pastPosition = ExUtils.Wrap<PlayerInput>("PastPosition", typeof(float));
    
    [UsedImplicitly]
    public Vector2 PastDirection(float timeAgo) =>
        PastDirections.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS));
    public static readonly ExFunction pastDirection = ExUtils.Wrap<PlayerInput>("PastDirection", typeof(float));
    
    [UsedImplicitly]
    public Vector2 MarisaAPosition(float timeAgo) =>
        MarisaAPositions.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS));
    public static readonly ExFunction marisaAPosition = ExUtils.Wrap<PlayerInput>("MarisaAPosition", typeof(float));
    
    [UsedImplicitly]
    public Vector2 MarisaADirection(float timeAgo) =>
        MarisaADirections.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS));
    public static readonly ExFunction marisaADirection = ExUtils.Wrap<PlayerInput>("MarisaADirection", typeof(float));
    
    #endregion

    private Action? deathbombAction;
    public int OpenDeathbombWindow(Action onDeathbomb) {
        deathbombAction = onDeathbomb;
        return Shot.bomb.DeathbombFrames();
    }

    public void CloseDeathbombWindow() => deathbombAction = null;

    public Vector2 DesiredMovement01 {
        get {
            if (!AllowPlayerInput) return Vector2.zero;
            var vel0 = new Vector2(
                Restrictions.HorizAllowed ? InputManager.HorizontalSpeed01 : 0,
                Restrictions.VertAllowed ? InputManager.VerticalSpeed01 : 0
            );
            var mag = vel0.magnitude;
            if (mag > 1f) {
                return vel0 / mag;
            } else if (mag < 0.03f) {
                return Vector2.zero;
            } else return vel0;
        }
    }

    private void MovementUpdate(float dT) {
        bpi.t += dT;
        if (IsTryingBomb && Shot.HasBomb && GameManagement.Difficulty.bombsEnabled) {
            if (deathbombAction == null) 
                PlayerBombs.TryBomb(Shot.bomb, this, PlayerBombContext.NORMAL);
            else if (PlayerBombs.TryBomb(Shot.bomb, this, PlayerBombContext.DEATHBOMB)) {
                deathbombAction();
                CloseDeathbombWindow();
            }
        }
        hitboxSprite.enabled = IsFocus || SaveData.s.UnfocusedHitbox;
        Vector2 velocity = DesiredMovement01;
        if (velocity.sqrMagnitude > 0) {
            timeSinceLastStandstill += dT;
            if (timeSinceLastStandstill * 120f < lnrizeSpeed && SaveData.s.AllowInputLinearization) {
                velocity *= 1 - lnrizeRatio +
                            lnrizeRatio * Mathf.Floor(1f + timeSinceLastStandstill * 120f) / lnrizeSpeed;
            }
        } else {
            velocity = Vector2.zero;
            timeSinceLastStandstill = 0f;
        }
        velocity *= IsFocus ? thisPlayer.FocusSpeed : thisPlayer.freeSpeed;
        if (spawnedCamera != null) velocity *= spawnedCamera.CameraSpeedMultiplier;
        velocity *= StateSpeedMultiplier(state);
        velocity *= (float) GameManagement.Difficulty.playerSpeedMultiplier;
        SetDirection(velocity);
        //Check bounds
        Vector2 pos = tr.position;
        if (StateAllowsLocationUpdate(state)) {
            if (pos.x <= LeftPlayerBound) {
                pos.x = LeftPlayerBound;
                velocity.x = Mathf.Max(velocity.x, 0f);
            } else if (pos.x >= RightPlayerBound) {
                pos.x = RightPlayerBound;
                velocity.x = Mathf.Min(velocity.x, 0f);
            }
            if (pos.y <= BotPlayerBound) {
                pos.y = BotPlayerBound;
                velocity.y = Mathf.Max(velocity.y, 0f);
            } else if (pos.y >= TopPlayerBound) {
                pos.y = TopPlayerBound;
                velocity.y = Mathf.Min(velocity.y, 0f);
            }
            var prev = hitbox.location;
            SetLocation(pos + velocity * dT); 
            if (IsMoving) {
                var delta = hitbox.location - prev;
                var dir = delta.normalized;
                PastPositions.Add(hitbox.location);
                PastDirections.Add(dir);
                if (IsFocus) {
                    //Add offset to all tracking positions so they stay the same relative position
                    for (int ii = 0; ii < MarisaAPositions.arr.Length; ++ii) {
                        MarisaAPositions.arr[ii] += delta;
                    }
                } else {
                    MarisaAPositions.Add(hitbox.location);
                    MarisaADirections.Add(dir);
                }
            }
        }
    }

    private void SetLocation(Vector2 next) {
        bpi.loc = tr.position = hitbox.location = next;
    }

    public enum PlayerState {
        NORMAL,
        WITCHTIME,
        RESPAWN,
        NULL
    }

    private static bool StateAllowsInput(PlayerState s) =>
        s switch {
            PlayerState.RESPAWN => false,
            _ => true
        };

    private static bool StateAllowsLocationUpdate(PlayerState s) =>
        s switch {
            PlayerState.RESPAWN => false,
            _ => true
        };

    private static float StateSpeedMultiplier(PlayerState s) {
        return s switch {
            PlayerState.WITCHTIME => WitchTimeSpeedMultiplier,
            _ => 1f
        };
    }

    private PlayerState state;
    private const float RespawnFreezeTime = 0.1f;
    private const float RespawnDisappearTime = 0.5f;
    private const float RespawnMoveTime = 1.5f;
    private static Vector2 RespawnStartLoc => new Vector2(0, Bot - 1f);
    private static Vector2 RespawnEndLoc => new Vector2(0, BotPlayerBound + 1f);
    private const float WitchTimeSpeedMultiplier = 1.4f;//2f;
    private const float WitchTimeSlowdown = 0.5f;//0.25f;
    private const float WitchTimeAudioMultiplier = 0.8f;

    public void RequestNextState(PlayerState s) => stateCanceller?.Cancel(s);
    private GCancellable<PlayerState>? stateCanceller;
    
    private IEnumerator ResolveState(PlayerState next, ICancellee<PlayerState> canceller) {
        return next switch {
            PlayerState.NORMAL => StateNormal(canceller),
            PlayerState.WITCHTIME => StateWitchTime(canceller),
            PlayerState.RESPAWN => StateRespawn(canceller),
            _ => throw new Exception($"Unhandled player state: {next}")
        };
    }

    private void RunNextState(PlayerState next) {
        stateCanceller = null;
        if (next == PlayerState.NULL) return;
        var canceller = stateCanceller = new GCancellable<PlayerState>();
        RunDroppableRIEnumerator(ResolveState(next, canceller));
    }

    private bool MaybeCancelState(ICancellee<PlayerState> cT) {
        if (cT.Cancelled(out var next)) {
            RunNextState(next);
            return true;
        } else return false;
    }
    //Assumption for state enumerators is that the token is not initially cancelled.
    private IEnumerator StateNormal(ICancellee<PlayerState> cT) {
        GameManagement.Instance.StopUsingMeter();
        hitbox.Active = true;
        state = PlayerState.NORMAL;
        while (true) {
            if (MaybeCancelState(cT)) yield break;
            if (IsTryingWitchTime && GameManagement.Instance.TryStartMeter()) {
                RunDroppableRIEnumerator(StateWitchTime(cT));
                yield break;
            }
            yield return null;
        }
    }
    private IEnumerator StateRespawn(ICancellee<PlayerState> cT) {
        GameManagement.Instance.StopUsingMeter();
        state = PlayerState.RESPAWN;
        RespawnOnHitEffect.Proc(hitbox.location, hitbox.location, 0f);
        //The hitbox position doesn't update during respawn, so don't allow collision.
        hitbox.Active = false;
        for (float t = 0; t < RespawnFreezeTime; t += ETime.FRAME_TIME) yield return null;
        //Don't update the hitbox location
        tr.position = new Vector2(0f, 100f);
        InvokeParentedTimedEffect(RespawnAfterEffect, health.hitInvuln - RespawnFreezeTime);
        //Respawn doesn't respect state cancellations
        for (float t = 0; t < RespawnDisappearTime; t += ETime.FRAME_TIME) yield return null;
        for (float t = 0; t < RespawnMoveTime; t += ETime.FRAME_TIME) {
            tr.position = Vector2.Lerp(RespawnStartLoc, RespawnEndLoc, t / RespawnMoveTime);
            yield return null;
        }
        SetLocation(RespawnEndLoc);
        hitbox.Active = true;
        
        if (!MaybeCancelState(cT)) RunDroppableRIEnumerator(StateNormal(cT));
    }
    private IEnumerator StateWitchTime(ICancellee<PlayerState> cT) {
        GameManagement.Instance.StartUsingMeter();
        state = PlayerState.WITCHTIME;
        speedLines.Play();
        var t = ETime.Slowdown.CreateModifier(WitchTimeSlowdown, MultiOp.Priority.CLEAR_SCENE);
        meter.enabled = true;
        PlayerActivatedMeter.Proc();
        for (int f = 0; !MaybeCancelState(cT) &&
            IsTryingWitchTime && GameManagement.Instance.TryUseMeterFrame(); ++f) {
            if (f % ghostFrequency == 0) {
                Instantiate(ghost).GetComponent<Ghost>().Initialize(ghostSource.sprite, tr.position, ghostFadeTime);
            }
            MeterIsActive.Publish(GameManagement.Instance.EnoughMeterToUse ? meter.color : meterDisplayInner);
            float meterDisplayRatio = M.EOutSine(Mathf.Clamp01(f / 30f));
            meterPB.SetFloat(PropConsts.fillRatio, (float)GameManagement.Instance.Meter * meterDisplayRatio);
            meter.SetPropertyBlock(meterPB);
            yield return null;
        }
        meter.enabled = false;
        PlayerDeactivatedMeter.Proc();
        t.TryRevoke();
        speedLines.Stop();
        GameManagement.Instance.StopUsingMeter();
        if (!cT.Cancelled(out _)) RunDroppableRIEnumerator(StateNormal(cT));
    }

    protected override void RegularUpdateMove() {
        MovementUpdate(ETime.FRAME_TIME);
    }

    private void UpdateInputTimes(bool focus, bool fire) {
        if (focus) {
            TimeFocus += ETime.FRAME_TIME;
            TimeFree = 0;
        } else {
            TimeFocus = 0;
            TimeFree += ETime.FRAME_TIME;
        }
        
        if (fire) {
            if (focus) {
                FiringTimeFree = 0;
                FiringTimeFocus += ETime.FRAME_TIME;
                UnFiringTimeFree += ETime.FRAME_TIME;
                UnFiringTimeFocus = 0;
            } else {
                FiringTimeFree += ETime.FRAME_TIME;
                FiringTimeFocus = 0;
                UnFiringTimeFree = 0;
                UnFiringTimeFocus += ETime.FRAME_TIME;
            }
            FiringTime += ETime.FRAME_TIME;
            UnFiringTime = 0f;
        } else {
            FiringTimeFree = 0;
            FiringTimeFocus = 0;
            FiringTime = 0f;
            UnFiringTimeFree += ETime.FRAME_TIME;
            UnFiringTimeFocus += ETime.FRAME_TIME;
            UnFiringTime += ETime.FRAME_TIME;
        }
    }

    public override void RegularUpdate() {
        SubshotValue = _subshotValue;
        base.RegularUpdate();
        if (AllowPlayerInput) GameManagement.Instance.RefillMeterFrame(state);
        //Hilarious issue. If running a bomb that disables and then re-enables firing,
        //then IsFiring will return false in the movement update and true in the options code.
        //As a result, UnfiringTime will be incorrect and lasers will break.
        //So we have to do the time-set code after coroutines. 

        UpdateInputTimes(IsFocus, IsFiring);
        
        if (--scoreLabelBuffer == 0 && labelAccScore > 0) {
            DropDropLabel(scoreLabelBonus ? scoreGrad_bonus : scoreGrad, $"{labelAccScore:n0}");
            labelAccScore = 0;
            scoreLabelBonus = false;
        }

        freeFocusLerp01 = Mathf.Clamp01(freeFocusLerp01 + (IsFocus ? 1 : -1) * 
            ETime.FRAME_TIME / FreeFocusLerpTime);

        for (int ii = 0; ii < focusOverlay.Length; ++ii) {
            focusOverlay[ii].SetAlpha(freeFocusLerp01 * baseFocusOverlayOpacity);
        }
    }

    public override void InvokeCull() {
        DestroyExistingShot();
        UpdateInputTimes(false, false); //clears dependent lasers
        base.InvokeCull();
    }

    public void BufferScoreLabel((long deltaScore, bool bonus) s) {
        labelAccScore += s.deltaScore;
        scoreLabelBuffer = ITEM_LABEL_BUFFER;
        scoreLabelBonus |= s.bonus;
    }

    private long labelAccScore = 0;
    private int scoreLabelBuffer = -1;
    private bool scoreLabelBonus = false;
    private const int ITEM_LABEL_BUFFER = 4;

    private static readonly IGradient scoreGrad = DropLabel.MakeGradient(
        new Color32(100, 150, 255, 255), new Color32(80, 110, 255, 255));
    private static readonly IGradient scoreGrad_bonus = DropLabel.MakeGradient(
        new Color32(20, 220, 255, 255), new Color32(10, 170, 255, 255));
    private static readonly IGradient pivGrad = DropLabel.MakeGradient(
        new Color32(0, 235, 162, 255), new Color32(0, 172, 70, 255));
    
    public GameObject InvokeParentedTimedEffect(EffectStrategy effect, float time) {
        var v = tr.position;
        var effectGO = effect.ProcNotNull(v, v, 0);
        effectGO.transform.SetParent(tr);
        var animator = effectGO.GetComponent<TimeBoundAnimator>();
        if (animator != null) animator.Initialize(Cancellable.Null, time);
        return effectGO;
    }
    
    public static readonly Expression playerID = ExUtils.Property<PlayerInput>("PlayerShotItr");
    
    protected override void OnDisable() {
        RequestNextState(PlayerState.NULL);
        base.OnDisable();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos() {
        Handles.color = Color.cyan;
        var position = transform.position;
        Handles.DrawWireDisc(position, Vector3.forward, hitbox.radius);
        Handles.color = Color.blue;
        Handles.DrawWireDisc(position, Vector3.forward, hitbox.largeRadius);
        Handles.color = Color.black;
        Handles.DrawLine(position + Vector3.up * .5f, position + Vector3.down * .5f);
        Handles.DrawLine(position + Vector3.right * .2f, position + Vector3.left * .2f);
        Handles.color = Color.yellow;
        /*for (int ii = 0; ii < 30 && ii < positions.Count; ++ii) {
            Handles.DrawWireDisc(positions[positions.Count - 1 - ii], Vector3.forward, hitbox.radius);
        }*/
    }
#endif
}
}
