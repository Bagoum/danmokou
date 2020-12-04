using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Core;
using DMath;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Danmaku.LocationService;

namespace Danmaku {
public class PlayerInput : BehaviorEntity {
    public SpriteRenderer ghostSource;
    public SOPlayerHitbox hitbox;
    public SpriteRenderer hitboxSprite;
    public SpriteRenderer meter;
    public Color meterDisplayShadow;
    public Color meterDisplayInner;
    private MaterialPropertyBlock meterPB;

    [Header("Movement")] public float blueBoxRadius = .1f;
    [Tooltip("120 frames per sec")] public int lnrizeSpeed = 10;
    public float lnrizeRatio = .7f;
    private float timeSinceLastStandstill;
    public bool IsMoving => timeSinceLastStandstill > 0;

    private LayerMask collMask;
    public PlayerConfig thisPlayer;
    public ShotConfig defaultShot;

    private ShotConfig shot;
    private static Enums.Subshot subshot;
    [UsedImplicitly]
    //This doesn't change if the player is not using a multishot, as opposed to Campaign.subshot
    public static float SubshotValue => (int) subshot;
    private GameObject spawnedShot;
    private AyaCamera spawnedCamera;

    public GameObject ghost;
    public float ghostFadeTime;
    public int ghostFrequency;
    public ParticleSystem speedLines;
    public EffectStrategy RespawnOnHitEffect;
    public EffectStrategy RespawnAfterEffect;
    private PlayerHP health;

    public float baseFocusOverlayOpacity = 0.7f;
    public SpriteRenderer[] focusOverlay;
    private const float FreeFocusLerpTime = 0.3f;
    private float freeFocusLerp01 = 0f;
    
    protected override void Awake() {
        base.Awake();
        health = GetComponent<PlayerHP>();
        //Log.Unity($"Player awake", level: Log.Level.DEBUG1);
        collMask = LayerMask.GetMask("Wall");
        hitbox.location = tr.position;
        hitboxSprite.enabled = SaveData.s.UnfocusedHitbox;
        meter.enabled = false;
        meter.GetPropertyBlock(meterPB = new MaterialPropertyBlock());
        meterPB.SetFloat(PropConsts.innerFillRatio, (float)CampaignData.meterUseThreshold);
        meterPB.SetColor(PropConsts.unfillColor, meterDisplayShadow);
        meterPB.SetColor(PropConsts.fillInnerColor, meterDisplayInner);
        meter.SetPropertyBlock(meterPB);

        if (LoadPlayer()) {
            PastPositions.Clear();
            PastPositions.Add(hitbox.location);
            MarisaAPositions.Clear();
            MarisaAPositions.Add(hitbox.location);
            LoadShot(true);
            FiringDisableRequests = 0;
            BombDisableRequests = 0;
            SMPlayerControlDisable = 0;
            RunNextState(PlayerState.NORMAL);
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(Events.ScoreItemHasReceived, BufferScoreLabel);
    }

    [CanBeNull] private IChallengeManager challenge;
    private ChallengeManager.Restrictions Restrictions => 
        challenge?.Restriction ?? ChallengeManager.Restrictions.Default;

    //This might technically be unsafe w.r.t replays, but camera fade time should cover this.
    private void Start() {
        challenge = DependencyInjection.MaybeFind<IChallengeManager>();
    }

    public static int FiringDisableRequests { get; set; } = 0;
    public static int BombDisableRequests { get; set; } = 0;
    public static int SMPlayerControlDisable { get; set; } = 0;
    public static bool PlayerActive => (SMPlayerControlDisable == 0) && !Dialoguer.DialogueActive;
    public bool AllowPlayerInput => PlayerActive && StateAllowsInput(state);

    /// <summary>
    /// Returns true if this object survived, false if it was destroyed.
    /// </summary>
    private bool LoadPlayer() {
        var p = GameManagement.campaign.Player;
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
    private void LoadShot(bool firstLoad) {
        if (!firstLoad && (subshot == GameManagement.campaign.Subshot || !shot.isMultiShot)) return;
        shot = GameManagement.campaign.Shot;
        if (shot == null) shot = defaultShot;
        if (firstLoad) {
            //Load all the reflection functions now to avoid lag on subshot switch.
            shot.Subshots?.ForEach(s => s.prefab.GetComponentsInChildren<FireOption>().ForEach(fo => fo.Preload()));
        }
        //Note: playerinput.subshot should not change if the shot is not a multishot
        subshot = GameManagement.campaign.Subshot;
        Log.Unity($"Loading player shot: {shot.key} : sub {subshot.Describe()}", level: Log.Level.DEBUG2);
        if (spawnedShot != null) {
            //This is kind of stupid but it's necessary to ensure that
            //the coroutines end immediately rather than at the end of the update loop
            spawnedShot.GetComponentsInChildren<BehaviorEntity>().ForEach(b => b.InvokeCull());
            Destroy(spawnedShot.gameObject);
            SFXService.Request(shot.onSwap);
        }
        var realized = shot.GetSubshot(subshot);
        spawnedShot = realized.playerChild ? 
            GameObject.Instantiate(realized.prefab, tr) : 
            GameObject.Instantiate(realized.prefab);
        spawnedShot.GetComponentsInChildren<FireOption>().ForEach(fo => fo.Initialize(this));
        spawnedCamera = spawnedShot.GetComponent<AyaCamera>();
        if (spawnedCamera != null) spawnedCamera.Initialize(this);
    }

    
    public override int UpdatePriority => UpdatePriorities.PLAYER;

    public bool IsFocus => Restrictions.FocusAllowed && (Restrictions.FocusForced || InputManager.IsFocus);
    public bool IsFiring =>
        InputManager.IsFiring && AllowPlayerInput && FiringDisableRequests == 0;
    public bool IsTryingBomb =>
        InputManager.IsBomb && AllowPlayerInput && BombDisableRequests == 0;
    public bool IsTryingWitchTime => InputManager.IsMeter && AllowPlayerInput;

    #region FiringHelpers
    public static float TimeFree { get; private set; }
    public static float TimeFocus { get; private set; }
    public static float FiringTimeFree { get; private set; }
    public static float FiringTimeFocus { get; private set; }
    public static float FiringTime { get; private set; }
    public static float UnFiringTimeFree { get; private set; }
    public static float UnFiringTimeFocus { get; private set; }
    public static float UnFiringTime { get; private set; }

    private const int POSITION_REMEMBER_FRAMES = 120;
    private static readonly CircularList<Vector2> PastPositions = new CircularList<Vector2>(POSITION_REMEMBER_FRAMES);
    private static readonly CircularList<Vector2> PastDirections = new CircularList<Vector2>(POSITION_REMEMBER_FRAMES);
    /// <summary>
    /// Unlike normal position tracking, MarisaA positions freeze when focused.
    /// </summary>
    private static readonly CircularList<Vector2> MarisaAPositions = new CircularList<Vector2>(POSITION_REMEMBER_FRAMES);
    private static readonly CircularList<Vector2> MarisaADirections = new CircularList<Vector2>(POSITION_REMEMBER_FRAMES);
    [UsedImplicitly]
    public static Vector2 PastPosition(float timeAgo) =>
        PastPositions.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS));
    public static readonly ExFunction pastPosition = ExUtils.Wrap<PlayerInput>("PastPosition", typeof(float));
    [UsedImplicitly]
    public static Vector2 PastDirection(float timeAgo) =>
        PastDirections.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS));
    public static readonly ExFunction pastDirection = ExUtils.Wrap<PlayerInput>("PastDirection", typeof(float));
    [UsedImplicitly]
    public static Vector2 MarisaAPosition(float timeAgo) =>
        MarisaAPositions.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS));
    public static readonly ExFunction marisaAPosition = ExUtils.Wrap<PlayerInput>("MarisaAPosition", typeof(float));
    [UsedImplicitly]
    public static Vector2 MarisaADirection(float timeAgo) =>
        MarisaADirections.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS));
    public static readonly ExFunction marisaADirection = ExUtils.Wrap<PlayerInput>("MarisaADirection", typeof(float));
    
    #endregion

    private Action deathbombAction;
    public int OpenDeathbombWindow(Action onDeathbomb) {
        deathbombAction = onDeathbomb;
        return shot.bomb.DeathbombFrames();
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
        if (IsTryingBomb && shot.HasBomb && GameManagement.Difficulty.bombsEnabled) {
            if (deathbombAction == null) PlayerBombs.TryBomb(shot.bomb, this, PlayerBombContext.NORMAL);
            else if (PlayerBombs.TryBomb(shot.bomb, this, PlayerBombContext.DEATHBOMB)) {
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
            //CRITICAL
            //This updates the positions of all walls, etc in the collision engine.
            //If you do not do this, collision detection against moving walls will be jittery and only work
            //properly once every few frames, since the physics engine won't recognize that the wall has moved
            //until the physics update is called (.02 seconds). 
            //It only affects moving walls and does not matter for player movement (the player does not have a collider).
            //TODO find out if this works with the regular-update model (if it's ever necessary).
            //Physics2D.SyncTransforms();
            var prev = hitbox.location;
            SetLocation(pos + velocity * dT); 
                    // + MoveAgainstWall(pos, blueBoxRadius, velocity * dT, collMask);
            //positions.Add(hitbox.location);
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
        RESPAWN
    }

    private static bool StateAllowsInput(PlayerState s) {
        switch (s) {
            case PlayerState.RESPAWN: return false;
            default: return true;
        }
    }

    private static bool StateAllowsLocationUpdate(PlayerState s) {
        switch (s) {
            case PlayerState.RESPAWN: return false;
            default: return true;
        }
    }

    private static float StateSpeedMultiplier(PlayerState s) {
        switch (s) {
            case PlayerState.WITCHTIME: return WitchTimeSpeedMultiplier;
            default: return 1f;
        }
    }

    private PlayerState state;
    private const float RespawnFreezeTime = 0.1f;
    private const float RespawnDisappearTime = 0.5f;
    private const float RespawnMoveTime = 1.5f;
    private static Vector2 RespawnStartLoc => new Vector2(0, LocationService.Bot - 1f);
    private static Vector2 RespawnEndLoc => new Vector2(0, LocationService.BotPlayerBound + 1f);
    private const float WitchTimeSpeedMultiplier = 1.4f;//2f;
    private const float WitchTimeSlowdown = 0.5f;//0.25f;
    private const float WitchTimeAudioMultiplier = 0.8f;

    public void RequestNextState(PlayerState s) => stateCanceller?.Cancel(s);
    private GCancellable<PlayerState> stateCanceller;
    
    private IEnumerator ResolveState(PlayerState next, ICancellee<PlayerState> canceller) {
        switch (next) {
            case PlayerState.NORMAL: return StateNormal(canceller);
            case PlayerState.WITCHTIME: return StateWitchTime(canceller);
            case PlayerState.RESPAWN: return StateRespawn(canceller);
            default: throw new Exception($"Unhandled player state: {next}");
        }
    }

    private void RunNextState(PlayerState next) {
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
        GameManagement.campaign.MeterInUse = false;
        hitbox.Active = true;
        state = PlayerState.NORMAL;
        while (true) {
            if (MaybeCancelState(cT)) yield break;
            if (IsTryingWitchTime && GameManagement.campaign.TryStartMeter()) {
                RunDroppableRIEnumerator(StateWitchTime(cT));
                yield break;
            }
            yield return null;
        }
    }
    private IEnumerator StateRespawn(ICancellee<PlayerState> cT) {
        GameManagement.campaign.MeterInUse = false;
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
        GameManagement.campaign.MeterInUse = true;
        state = PlayerState.WITCHTIME;
        speedLines.Play();
        var t = ETime.Slowdown.CreateMultiplier(WitchTimeSlowdown, MultiMultiplier.Priority.CLEAR_SCENE);
        meter.enabled = true;
        PlayerActivatedMeter.Proc();
        for (int f = 0; !MaybeCancelState(cT) &&
            IsTryingWitchTime && GameManagement.campaign.TryUseMeterFrame(); ++f) {
            if (f % ghostFrequency == 0) {
                Instantiate(ghost).GetComponent<Ghost>().Initialize(ghostSource.sprite, tr.position, ghostFadeTime);
            }
            MeterIsActive.Publish(GameManagement.campaign.EnoughMeterToUse ? meter.color : meterDisplayInner);
            float meterDisplayRatio = M.EOutSine(Mathf.Clamp01(f / 30f));
            meterPB.SetFloat(PropConsts.fillRatio, (float)GameManagement.campaign.Meter * meterDisplayRatio);
            meter.SetPropertyBlock(meterPB);
            yield return null;
        }
        meter.enabled = false;
        PlayerDeactivatedMeter.Proc();
        t.TryRevoke();
        speedLines.Stop();
        GameManagement.campaign.MeterInUse = false;
        if (!cT.Cancelled(out _)) RunDroppableRIEnumerator(StateNormal(cT));
    }

    public static readonly Events.Event0 PlayerActivatedMeter = new Events.Event0();
    public static readonly Events.Event0 PlayerDeactivatedMeter = new Events.Event0();
    /// <summary>
    /// Called every frame during meter activation.
    /// </summary>
    public static readonly Events.IEvent<Color> MeterIsActive = new Events.Event<Color>();

    protected override void RegularUpdateMove() {
        MovementUpdate(ETime.FRAME_TIME);
    }

    public override void RegularUpdate() {
        LoadShot(false);
        base.RegularUpdate();
        if (AllowPlayerInput) GameManagement.campaign.RefillMeterFrame(state);
        //Hilarious issue. If running a bomb that disables and then re-enables firing,
        //then IsFiring will return false in the movement update and true in the options code.
        //As a result, UnfiringTime will be incorrect and lasers will break.
        //So we have to do the time-set code after coroutines. 

        if (IsFocus) {
            TimeFocus += ETime.FRAME_TIME;
            TimeFree = 0;
        } else {
            TimeFocus = 0;
            TimeFree += ETime.FRAME_TIME;
        }
        
        if (IsFiring) {
            if (IsFocus) {
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


    private static Vector2 MoveAgainstWall(Vector2 source, float blueBoxRadius, Vector2 delta, LayerMask mask) {
        RaycastHit2D ray = Physics2D.CircleCast(source, blueBoxRadius, delta.normalized, delta.magnitude, mask);
        if (ray.collider != null) {
            Vector2 adjusted = Vector2.zero;
            while (ray.distance < float.Epsilon) {
                //If we are inside the object, move outwards along the normal, and then try to move back.
                Vector2 movBack = blueBoxRadius * ray.normal;
                adjusted += movBack;
                delta -= movBack;
                source += movBack;
                ray = Physics2D.CircleCast(source, blueBoxRadius, delta.normalized, delta.magnitude, mask);

                //In some cases moving out and back can actually disconnect the collision. 
                if (ray.collider == null) {
                    return delta + movBack;
                }
            }
            //Move along the delta-vector as far as the ray goes.
            Vector2 rawMove = delta.normalized * ray.distance;
            adjusted += rawMove;
            delta -= rawMove;

            //Then move along the delta-vector's dot product with the surface.
            adjusted += delta - M.ProjectionUnit(delta, ray.normal);
            return adjusted;
        } else {
            return delta;
        }
    }

    public static readonly Expression timeFree = ExUtils.Property<PlayerInput>("TimeFree");
    public static readonly Expression timeFocus = ExUtils.Property<PlayerInput>("TimeFocus");
    public static readonly Expression firingTimeFree = ExUtils.Property<PlayerInput>("FiringTimeFree");
    public static readonly Expression firingTimeFocus = ExUtils.Property<PlayerInput>("FiringTimeFocus");
    public static readonly Expression firingTime = ExUtils.Property<PlayerInput>("FiringTime");
    public static readonly Expression unfiringTimeFree = ExUtils.Property<PlayerInput>("UnFiringTimeFree");
    public static readonly Expression unfiringTimeFocus = ExUtils.Property<PlayerInput>("UnFiringTimeFocus");
    public static readonly Expression unfiringTime = ExUtils.Property<PlayerInput>("UnFiringTime");
    public static readonly Expression subshotValue = ExUtils.Property<PlayerInput>("SubshotValue");
    
    
    public GameObject InvokeParentedTimedEffect(EffectStrategy effect, float time) {
        var v = tr.position;
        var effectGO = effect.ProcNotNull(v, v, 0);
        effectGO.transform.SetParent(tr);
        var animator = effectGO.GetComponent<TimeBoundAnimator>();
        if (animator != null) animator.Initialize(Cancellable.Null, time);
        return effectGO;
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
