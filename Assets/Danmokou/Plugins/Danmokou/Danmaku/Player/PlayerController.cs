using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Behavior.Functions;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Dialogue;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Graphics;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using static Danmokou.Core.GameManagement;
using static Danmokou.DMath.LocationHelpers;

namespace Danmokou.Player {
/// <summary>
/// A team is a code construct which contains a hitbox, several possible shots (of which one is active),
/// and several possible players (of which one is active).
/// </summary>
public class PlayerController : BehaviorEntity {
    public enum PlayerState {
        NORMAL,
        WITCHTIME,
        RESPAWN,
        NULL
    }

    public enum DeathbombState {
        NULL,
        WAITING,
        PERFORMED
    }
    
    #region Consts
    private const float RespawnFreezeTime = 0.1f;
    private const float RespawnDisappearTime = 0.5f;
    private const float RespawnMoveTime = 1.5f;
    private static Vector2 RespawnStartLoc => new Vector2(0, Bot - 1f);
    private static Vector2 RespawnEndLoc => new Vector2(0, BotPlayerBound + 1f);
    private const float WitchTimeSpeedMultiplier = 1.4f;//2f;
    private const float WitchTimeSlowdown = 0.5f;//0.25f;
    private const float WitchTimeAudioMultiplier = 0.8f;
    
    private static readonly IGradient scoreGrad = DropLabel.MakeGradient(
        new Color32(100, 150, 255, 255), new Color32(80, 110, 255, 255));
    private static readonly IGradient scoreGrad_bonus = DropLabel.MakeGradient(
        new Color32(20, 220, 255, 255), new Color32(10, 170, 255, 255));
    private static readonly IGradient pivGrad = DropLabel.MakeGradient(
        new Color32(0, 235, 162, 255), new Color32(0, 172, 70, 255));
    private const int ITEM_LABEL_BUFFER = 4;
    
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
    
    
    #endregion
    
    public static MultiAdder FiringDisabler { get; private set; } = new MultiAdder(0, null);
    public static MultiAdder BombDisabler { get; private set; } = new MultiAdder(0, null);
    public static MultiAdder AllControlDisabler { get; private set; } = new MultiAdder(0, null);
    public static bool PlayerActive => (AllControlDisabler.Value == 0) && !Dialoguer.DialogueActive;
    public static bool RespawnOnHit => GameManagement.Difficulty.respawnOnDeath;
    
    #region Events
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
    
    public static readonly Events.IEvent<(int frames, bool effect)> RequestPlayerInvulnerable =
        new Events.Event<(int, bool)>();
    #endregion

    public ShipConfig[] defaultPlayers = null!;
    public ShotConfig[] defaultShots = null!;
    public Subshot defaultSubshot;
    private ActiveTeamConfig localTeamCfg = null!;
    private ActiveTeamConfig TeamCfg => Instance.TeamCfg ?? localTeamCfg;
    private ShipConfig ship = null!;
    private ShotConfig shot = null!;
    private Subshot subshot;
    //TODO I need a cleaner way to handle laser destruction dependencies
    [UsedImplicitly] public static float PlayerShotItr => playerShotItr;
    private static ushort playerShotItr = 0;
    private ShipController spawnedPlayer = null!;
    private GameObject? spawnedShot;
    private AyaCamera? spawnedCamera;

    public SOPlayerHitbox hitbox = null!;
    public SpriteRenderer hitboxSprite = null!;
    public SpriteRenderer meter = null!;
    private MaterialPropertyBlock meterPB = null!;
    public ParticleSystem speedLines = null!;
    public ParticleSystem onSwitchParticle = null!;
    
    [Header("Movement")]
    [Tooltip("120 frames per sec")] public int lnrizeSpeed = 6;
    public float lnrizeRatio = .7f;
    private float timeSinceLastStandstill;
    public bool IsMoving => timeSinceLastStandstill > 0;
    
    public float baseFocusOverlayOpacity = 0.5f;
    public SpriteRenderer[] focusOverlay = null!;
    private const float FreeFocusLerpTime = 0.3f;
    private float freeFocusLerp01 = 0f;
    
    private PlayerState state;
    private GCancellable<PlayerState>? stateCanceller;
    private DeathbombState deathbomb = DeathbombState.NULL;
    
    #region HP
    
    public float hitInvuln = 6;
    public int HitInvulnFrames => Mathf.CeilToInt(hitInvuln * ETime.ENGINEFPS_F);
    private int invulnerabilityCounter = 0;
    
    
    #endregion
    
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
    #endregion
    
    #region DropLabel
    
    private long labelAccScore = 0;
    private int scoreLabelBuffer = -1;
    private bool scoreLabelBonus = false;
    
    #endregion
    
    #region Colors

    private readonly PushLerper<Color> meterDisplay = new PushLerper<Color>(0.4f, Color.Lerp);
    private readonly PushLerper<Color> meterDisplayInner = new PushLerper<Color>(0.4f, Color.Lerp);
    private readonly PushLerper<Color> meterDisplayShadow = new PushLerper<Color>(0.4f, Color.Lerp);
    
    #endregion
    
    public bool AllowPlayerInput => PlayerActive && StateAllowsInput(state);
    
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
    public bool IsFocus =>
        Restrictions.FocusAllowed && (Restrictions.FocusForced || (InputManager.IsFocus && AllowPlayerInput));
    public bool IsFiring =>
        InputManager.IsFiring && AllowPlayerInput && FiringDisabler.Value == 0;
    public bool IsTryingBomb =>
        InputManager.IsBomb && AllowPlayerInput && BombDisabler.Value == 0;
    public bool IsTryingWitchTime => InputManager.IsMeter && AllowPlayerInput;
    
    private IChallengeManager? challenge;
    private ChallengeManager.Restrictions Restrictions => 
        challenge?.Restriction ?? ChallengeManager.Restrictions.Default;

    public override int UpdatePriority => UpdatePriorities.PLAYER;

    private static void ResetDisablers() {
        FiringDisabler = new MultiAdder(0, null);
        BombDisabler = new MultiAdder(0, null);
        AllControlDisabler = new MultiAdder(0, null);
    }
    
    protected override void Awake() {
        base.Awake();
        localTeamCfg = new ActiveTeamConfig(new TeamConfig(0, defaultSubshot, defaultPlayers.Zip(defaultShots).ToArray()));
        Log.Unity($"Team awake", level: Log.Level.DEBUG1);
        hitbox.location = tr.position;
        hitbox.Player = this;
        hitboxSprite.enabled = SaveData.s.UnfocusedHitbox;
        meter.enabled = false;

        void Preload(GameObject prefab) {
            foreach (var fo in prefab.GetComponentsInChildren<FireOption>()) {
                Log.Unity($"Preload {fo.name}");
                fo.Preload();
            }
        }
        foreach (var (_, s) in TeamCfg.Ships) {
            if (s.isMultiShot) {
                foreach (var ss in s.Subshots!)
                    Preload(ss.prefab);
            } else Preload(s.prefab);
        }
        meter.GetPropertyBlock(meterPB = new MaterialPropertyBlock());
        meterPB.SetFloat(PropConsts.innerFillRatio, (float)InstanceConsts.meterUseThreshold);
        UpdatePB();
        
        _UpdateTeam();
        
        PastPositions.Add(hitbox.location);
        MarisaAPositions.Add(hitbox.location);
        ResetDisablers();
        RunNextState(PlayerState.NORMAL);
        
    }

    private void UpdatePB() {
        meterPB.SetColor(PropConsts.fillColor, meterDisplay.Value);
        meterPB.SetColor(PropConsts.unfillColor, meterDisplayShadow.Value);
        meterPB.SetColor(PropConsts.fillInnerColor, meterDisplayInner.Value);
        meter.SetPropertyBlock(meterPB);
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(Events.ScoreItemHasReceived, BufferScoreLabel);
        Listen(RequestPlayerInvulnerable, GoldenAuraInvuln);
        RegisterDI<PlayerController>(this);
    }

    public override void FirstFrame() {
        challenge = DependencyInjection.MaybeFind<IChallengeManager>();
    }

    #region TeamManagement

    public void SetSubshot(Subshot nsubshot) => UpdateTeam((int?) null, nsubshot);
    
    public void UpdateTeam(int? playerIndex = null, Subshot? nsubshot = null) {
        bool didUpdate = false;
        if (playerIndex.Try(out var pind) && TeamCfg.SelectedIndex != pind) {
            TeamCfg.SelectedIndex = pind;
            didUpdate = true;
        }
        if (nsubshot.Try(out var s) && TeamCfg.Subshot != s) {
            if (!TeamCfg.HasMultishot)
                InstanceData.UselessPowerupCollected.Proc();
            else
                ++Instance.SubshotSwitches;
            TeamCfg.Subshot = s;
            didUpdate = true;
        }
        if (didUpdate) {
            _UpdateTeam();
            InstanceData.TeamUpdated.Proc();
        }
    }
    public void UpdateTeam((ShipConfig, ShotConfig)? nplayer = null, Subshot? nsubshot = null) {
        int? pind = nplayer.Try(out var p) ? (int?)TeamCfg.Ships.IndexOf(x => x == p) : null;
        UpdateTeam(pind, nsubshot);
    }
    private void _UpdateTeam() {
        if (TeamCfg.Ship != ship) {
            bool fromNull = ship == null;
            ship = TeamCfg.Ship;
            Log.Unity($"Setting team player to {ship.key}");
            if (spawnedPlayer != null) {
                //animate "destruction"
                spawnedPlayer.InvokeCull();
            }
            spawnedPlayer = Instantiate(TeamCfg.Ship.prefab, tr).GetComponent<ShipController>();
            if (!fromNull) {
                var pm = onSwitchParticle.main;
                pm.startColor = new ParticleSystem.MinMaxGradient(spawnedPlayer.meterDisplayInner);
                onSwitchParticle.Play();
            }
        }
        if (TeamCfg.Shot != shot || (TeamCfg.Shot.isMultiShot && TeamCfg.Subshot != subshot)) {
            ++playerShotItr;
            shot = TeamCfg.Shot;
            subshot = TeamCfg.Subshot;
            Log.Unity($"Setting shot to {shot.key}:{subshot}");
            if (DestroyExistingShot())
                DependencyInjection.SFXService.Request(TeamCfg.Shot.onSwap);
            var realized = TeamCfg.Shot.GetSubshot(TeamCfg.Subshot);
            spawnedShot = realized.playerChild ? 
                GameObject.Instantiate(realized.prefab, tr) : 
                GameObject.Instantiate(realized.prefab);
            spawnedShot.GetComponentsInChildren<FireOption>().ForEach(fo => fo.Initialize(this));
            spawnedCamera = spawnedShot.GetComponent<AyaCamera>();
            if (spawnedCamera != null) spawnedCamera.Initialize(this);
        }
        _UpdateTeamColors();
    }

    private void _UpdateTeamColors() {
        meterDisplay.Push(spawnedPlayer.meterDisplay);
        meterDisplayShadow.Push(spawnedPlayer.meterDisplayShadow);
        meterDisplayInner.Push(spawnedPlayer.meterDisplayInner);

        var ps = speedLines.main;
        ps.startColor = spawnedPlayer.speedLineColor;
    }
    
    /// <summary>
    /// Returns true if there was something to be destroyed.
    /// </summary>
    /// <returns></returns>
    private bool DestroyExistingShot() {
        if (spawnedShot != null) {
            //This is kind of stupid but it's necessary to ensure that
            //the coroutines end immediately rather than at the end of the update loop
            spawnedShot.GetComponentsInChildren<BehaviorEntity>().ForEach(b => b.InvokeCull());
            Destroy(spawnedShot.gameObject);
            return true;
        }
        return false;
    }

    #endregion
    private void SetLocation(Vector2 next) {
        bpi.loc = tr.position = hitbox.location = next;
    }
    
    private void MovementUpdate(float dT) {
        bpi.t += dT;
        if (IsTryingBomb && shot.HasBomb && GameManagement.Difficulty.bombsEnabled) {
            if (deathbomb == DeathbombState.NULL) 
                PlayerBombs.TryBomb(shot.bomb, this, PlayerBombContext.NORMAL);
            else if (deathbomb == DeathbombState.WAITING && 
                     PlayerBombs.TryBomb(shot.bomb, this, PlayerBombContext.DEATHBOMB)) {
                deathbomb = DeathbombState.PERFORMED;
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
        velocity *= IsFocus ? ship.FocusSpeed : ship.freeSpeed;
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
    
    public override void RegularUpdate() {
        base.RegularUpdate();
        if (AllowPlayerInput) {
            GameManagement.Instance.RefillMeterFrame(state);
            if (InputManager.IsSwap) {
                Log.Unity("Updating team");
                UpdateTeam((TeamCfg.SelectedIndex + 1) % TeamCfg.Ships.Length);
            }
        }
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
        meterDisplay.Update(ETime.FRAME_TIME);
        meterDisplayShadow.Update(ETime.FRAME_TIME);
        meterDisplayInner.Update(ETime.FRAME_TIME);
        UpdatePB();
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
    
    public void BufferScoreLabel((long deltaScore, bool bonus) s) {
        labelAccScore += s.deltaScore;
        scoreLabelBuffer = ITEM_LABEL_BUFFER;
        scoreLabelBonus |= s.bonus;
    }
    
    public GameObject InvokeParentedTimedEffect(EffectStrategy effect, float time) {
        var v = tr.position;
        var effectGO = effect.ProcNotNull(v, v, 0);
        effectGO.transform.SetParent(tr);
        var animator = effectGO.GetComponent<TimeBoundAnimator>();
        if (animator != null) animator.Initialize(Cancellable.Null, time);
        return effectGO;
    }
    
    public override void InvokeCull() {
        DestroyExistingShot();
        UpdateInputTimes(false, false); //clears dependent lasers
        base.InvokeCull();
    }
    
    protected override void OnDisable() {
        RequestNextState(PlayerState.NULL);
        base.OnDisable();
    }

    #region HPManagement
    
    
    public void Graze(int graze) {
        if (graze <= 0 || invulnerabilityCounter > 0) return;
        GameManagement.Instance.AddGraze(graze);
    }
    
    public void Hit(int dmg, bool force = false) {
        if (dmg <= 0) return;
        //Log.Unity($"The player has taken a hit for {dmg} hp. Force: {force} Invuln: {invulnerabilityCounter} Deathbomb: {waitingDeathbomb}");
        if (force) _DoHit(dmg);
        else {
            if (invulnerabilityCounter > 0 || deathbomb != DeathbombState.NULL) 
                return;
            deathbomb = DeathbombState.WAITING;
            RunRIEnumerator(WaitDeathbomb(dmg));
        }
    }
    
    private void _DoHit(int dmg) {
        BulletManager.AutodeleteCircleOverTime(new SoftcullProperties(BPI.loc, 1.1f, 0f, 7f, "cwheel"));
        GameManagement.Instance.AddLives(-dmg);
        DependencyInjection.MaybeFind<IRaiko>()?.ShakeExtra(1.5f, 0.9f);
        Invuln(HitInvulnFrames);
        if (RespawnOnHit) RequestNextState(PlayerState.RESPAWN);
        else InvokeParentedTimedEffect(spawnedPlayer.OnHitEffect, hitInvuln);
    }
    
    
    private IEnumerator WaitDeathbomb(int dmg) {
        var frames = shot.bomb.DeathbombFrames();
        if (frames > 0) {
            Log.Unity($"The player has {frames} frames to deathbomb", level: Log.Level.DEBUG2);
            spawnedPlayer.OnPreHitEffect.Proc(bpi.loc, bpi.loc, 1f);
        }
        while (frames-- > 0 && deathbomb == DeathbombState.WAITING) 
            yield return null;
        if (deathbomb != DeathbombState.PERFORMED) 
            _DoHit(dmg);
        else 
            Log.Unity($"The player successfully deathbombed", level: Log.Level.DEBUG2);
        deathbomb = DeathbombState.NULL;
    }
    
    private void Invuln(int frames) {
        ++invulnerabilityCounter;
        RunDroppableRIEnumerator(WaitOutInvuln(frames));
    }
    
    private IEnumerator WaitOutInvuln(int frames) {
        for (int ii = frames; ii > 0; --ii) yield return null;
        --invulnerabilityCounter;
    }
    
    private void GoldenAuraInvuln((int frames, bool showEffect) req) {
        if (req.showEffect)
            InvokeParentedTimedEffect(spawnedPlayer.GoldenAuraEffect,
                req.frames * ETime.FRAME_TIME).transform.SetParent(tr);
        Invuln(req.frames);
    }
    
    #endregion

    #region StateMethods
    
    public void RequestNextState(PlayerState s) => stateCanceller?.Cancel(s);
    
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
        spawnedPlayer.RespawnOnHitEffect.Proc(hitbox.location, hitbox.location, 0f);
        //The hitbox position doesn't update during respawn, so don't allow collision.
        hitbox.Active = false;
        for (float t = 0; t < RespawnFreezeTime; t += ETime.FRAME_TIME) yield return null;
        //Don't update the hitbox location
        tr.position = new Vector2(0f, 100f);
        InvokeParentedTimedEffect(spawnedPlayer.RespawnAfterEffect, hitInvuln - RespawnFreezeTime);
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
            spawnedPlayer.MaybeDrawWitchTimeGhost(f);
            MeterIsActive.Publish(GameManagement.Instance.EnoughMeterToUse ? meterDisplay : meterDisplayInner);
            float meterDisplayRatio = M.EOutSine(Mathf.Clamp01(f / 30f));
            meterPB.SetFloat(PropConsts.fillRatio, GameManagement.Instance.VisibleMeter.NextValue * meterDisplayRatio);
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
    
    
    #endregion
    
    
    #region ExpressionMethods
    
    public static readonly Expression playerID = ExUtils.Property<PlayerController>("PlayerShotItr");
    
    [UsedImplicitly]
    public Vector2 PastPosition(float timeAgo) =>
        PastPositions.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS_F));
    public static readonly ExFunction pastPosition = ExUtils.Wrap<PlayerController>("PastPosition", typeof(float));
    
    [UsedImplicitly]
    public Vector2 PastDirection(float timeAgo) =>
        PastDirections.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS_F));
    public static readonly ExFunction pastDirection = ExUtils.Wrap<PlayerController>("PastDirection", typeof(float));
    
    [UsedImplicitly]
    public Vector2 MarisaAPosition(float timeAgo) =>
        MarisaAPositions.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS_F));
    public static readonly ExFunction marisaAPosition = ExUtils.Wrap<PlayerController>("MarisaAPosition", typeof(float));
    
    [UsedImplicitly]
    public Vector2 MarisaADirection(float timeAgo) =>
        MarisaADirections.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS_F));
    public static readonly ExFunction marisaADirection = ExUtils.Wrap<PlayerController>("MarisaADirection", typeof(float));
    
    #endregion
    
    

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