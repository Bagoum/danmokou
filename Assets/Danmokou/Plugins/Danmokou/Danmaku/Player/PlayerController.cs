﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Runtime.CompilerServices;
using System.Text;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Expressions;
using BagoumLib.Mathematics;
using CommunityToolkit.HighPerformance;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Behavior.Functions;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
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
using Danmokou.Core.DInput;
using Danmokou.Danmaku.Descriptors;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using static Danmokou.Services.GameManagement;
using static Danmokou.DMath.LocationHelpers;

namespace Danmokou.Player {
/// <summary>
/// A team is a code construct which contains a hitbox, several possible shots (of which one is active),
/// and several possible players (of which one is active).
/// </summary>
public partial class PlayerController : BehaviorEntity, 
    ICircularGrazableEnemySimpleBulletCollisionReceiver, 
    ICircularGrazableEnemyPatherCollisionReceiver,
    ICircularGrazableEnemyLaserCollisionReceiver,
    ICircularGrazableEnemyBulletCollisionReceiver {
    
    #region Serialized
    public ShipConfig[] defaultPlayers = null!;
    public ShotConfig[] defaultShots = null!;
    public Subshot defaultSubshot;
    public AbilityCfg[] defaultSupports = null!;


    public float MaxCollisionRadius => Ship.grazeboxRadius;
    public Hurtbox Hurtbox { get; private set; }
    public CircleCollider2D unityCollider = null!;
    public SpriteRenderer hitboxSprite = null!;
    public SpriteRenderer meter = null!;
    public ParticleSystem speedLines = null!;
    public ParticleSystem onSwitchParticle = null!;
    
    [Header("Movement")]
    [Tooltip("120 frames per sec")] public int lnrizeSpeed = 6;
    public float lnrizeRatio = .7f;
    private float timeSinceLastStandstill;

    public enum InputInControlMethod {
        INPUT_ACTIVE,
        NONE_SINCE_RESPAWN,
        NONE_SINCE_LONGPAUSE
    }

    /// <summary>
    /// True if the player has input commands after a forced standstill (such as respawn or dialogue).
    /// </summary>
    public InputInControlMethod InputInControl { get; set; } = InputInControlMethod.NONE_SINCE_LONGPAUSE;
    
    public float baseFocusOverlayOpacity = 0.5f;
    public SpriteRenderer[] focusOverlay = null!;
    public float hitInvuln = 6;
    public bool deleteBulletsOnDeath = true;
    
    [Header("Traditional respawn handler")]
    public float RespawnFreezeTime = 0.1f;
    public float RespawnDisappearTime = 0.5f;
    public float RespawnMoveTime = 1.5f;
    public Vector2 RespawnStartLoc = new(0, -5f);
    public Vector2 RespawnEndLoc = new(0, -3f);
    
    #endregion

    #region PrivateState
    public DisturbedAnd FiringEnabled { get; } = new();
    public DisturbedAnd BombsEnabled { get; } = new();
    public static DisturbedAnd AllControlEnabled { get; } = new();
    public static OverrideEvented<StyleSelector?> CollisionsForPool { get; } = new(null);
    public OverrideEvented<Func<PlayerController, BulletCollisionType, ParametricInfo, bool>?> CollisionChecker { get; } = new(null);
    public OverrideEvented<Func<PlayerController, BulletCollisionType, ParametricInfo, int?>?> GrazeChecker { get; } = new(null);

    private ushort shotItr = 0;
    [UsedImplicitly]
    public float PlayerShotItr() => shotItr;
    
    public ShipConfig Ship { get; private set; } = null!;
    private ShotConfig shot = null!;
    private Subshot subshot;
    private PlayerMovement movementHandler = null!;
    
    public ShipController SpawnedShip { get; private set; } = null!;
    private GameObject? spawnedShot;
    private AyaCamera? spawnedCamera;
    
    private float focusRingDisplayLerp = 0f;
    private MaterialPropertyBlock meterPB = null!;

    public PlayerStateFlow StateFlow { get; private set; } = null!;
    public PlayerState State => StateFlow.State;
    private DeathbombState deathbomb = DeathbombState.NULL;

    public DisturbedOr HitInvuln { get; } = new(false);
    
    private IChallengeManager? challenge;
    
    #endregion
    
    #region ComputedProperties

    /// <summary>
    /// True iff bullet collisions can occur against the player. This is only false when the player is in the RESPAWN
    ///  state (ie. has an indeterminate position).
    /// </summary>
    public bool ReceivesBulletCollisions(string? style) =>
        State is not PlayerState.Respawn &&
        (style is null or BulletManager.BulletFlakeName ||
         CollisionsForPool.Value?.Matches(style) is not false);

    /// <summary>
    /// True iff obstacle collisions can occur against the player.
    ///  This is only false when the player is in the RESPAWN state (ie. has an indeterminate position).
    /// </summary>
    public bool ReceivesWallCollisions =>
        State is not PlayerState.Respawn && InputInControl != InputInControlMethod.NONE_SINCE_RESPAWN;
    public ICancellee BoundingToken => Instance.Request?.InstTracker ?? Cancellable.Null;
    public bool AllowPlayerInput => AllControlEnabled && StateAllowsInput(State);
    private ActiveTeamConfig Team { get; set; } = null!;
    private bool RespawnOnHit => GameManagement.Instance.ConfigurationF.UseTraditionalRespawn;
    public int HitInvulnFrames => Mathf.CeilToInt(hitInvuln * ETime.ENGINEFPS_F);
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
        Restrictions.FocusAllowed && (Restrictions.FocusForced || (InputManager.IsFocus && AllowPlayerInput)) &&
        Team.Ship.focusAllowed;
    public bool ShowFocusRings { get; set; }
    public bool IsFiring => InputManager.IsFiring && AllowPlayerInput && FiringEnabled;
    public bool IsTryingBomb =>
        InputManager.IsBomb && AllowPlayerInput && BombsEnabled && Team.Support is Ability.Bomb;
    public bool IsTryingWitchTime => InputManager.IsMeter && AllowPlayerInput && Team.Support is Ability.WitchTime;

    public float MeterScorePerValueMultiplier => State is PlayerState.WitchTime ? 2 : 1;
    public float MeterPIVPerPPPMultiplier => State is PlayerState.WitchTime ? 2 : 1;
    
    private ChallengeManager.Restrictions Restrictions => 
        challenge?.Restriction ?? ChallengeManager.Restrictions.Default;
    
    #endregion
    
    //TODO I need a cleaner way to handle laser destruction dependencies. but this is pretty ok
    private static ushort shotItrCounter = 0;

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
    private readonly CircularList<Vector2> PastPositions = new(POSITION_REMEMBER_FRAMES);
    private readonly CircularList<Vector2> PastDirections = new(POSITION_REMEMBER_FRAMES);
    /// <summary>
    /// Unlike normal position tracking, MarisaA positions freeze when focused.
    /// </summary>
    private readonly CircularList<Vector2> MarisaAPositions = new(POSITION_REMEMBER_FRAMES);
    private readonly CircularList<Vector2> MarisaADirections = new(POSITION_REMEMBER_FRAMES);
    #endregion
    
    #region DropLabel
    
    private long labelAccScore = 0;
    private int scoreLabelBuffer = -1;
    private bool scoreLabelBonus = false;
    
    #endregion
    
    #region Colors

    public readonly PushLerper<Color> meterDisplay = new(0.4f, Color.Lerp);
    public readonly PushLerper<Color> meterDisplayInner = new(0.4f, Color.Lerp);
    private readonly PushLerper<Color> meterDisplayShadow = new(0.4f, Color.Lerp);
    private readonly PushLerper<float> meterDisplayOpacity = new(0.1f);
    
    #endregion

    private int obstacleCollisionLayer;

    public override int UpdatePriority => UpdatePriorities.PLAYER;

    protected override void Awake() {
        base.Awake();
        StateFlow = new PlayerStateFlow(this);
        obstacleCollisionLayer = LayerMask.NameToLayer("Wall");
        var dfltTeams = new (ShipConfig, ShotConfig, IAbilityCfg?)[defaultPlayers.Length];
        for (int ii = 0; ii < defaultPlayers.Length; ++ii)
            dfltTeams[ii] = (defaultPlayers[ii], defaultShots.ModIndex(ii), defaultSupports?.ModIndex(ii));
        Team = GameManagement.Instance.GetOrSetTeam(new ActiveTeamConfig(new TeamConfig(0, defaultSubshot, dfltTeams)));
        Logs.Log($"Team awake", level: LogLevel.DEBUG1);
        hitboxSprite.enabled = SaveData.s.UnfocusedHitbox;
        meter.enabled = true;
        meterDisplayOpacity.Push(0);
        
        var initialPosition = tr.position;
        PastPositions.Add(initialPosition);
        PastDirections.Add(Vector2.down);
        MarisaAPositions.Add(initialPosition);
        MarisaADirections.Add(Vector2.down);

        void Preload(GameObject prefab) {
            foreach (var fo in prefab.GetComponentsInChildren<FireOption>()) {
                fo.Preload();
            }
        }
        foreach (var (_, s, _) in Team.Ships) {
            if (s.isMultiShot) {
                foreach (var ss in s.Subshots!)
                    Preload(ss.prefab);
            } else Preload(s.prefab);
        }
        meter.GetPropertyBlock(meterPB = new MaterialPropertyBlock());
        meterPB.SetFloat(PropConsts.innerFillRatio, (float)Instance.MeterF.MeterUseThreshold);
        UpdatePB();
        
        RealizeTeam();

        StateFlow.Start();
    }

    private void UpdatePB() {
        meterPB.SetColor(PropConsts.fillColor, meterDisplay.Value);
        meterPB.SetColor(PropConsts.unfillColor, meterDisplayShadow.Value);
        meterPB.SetColor(PropConsts.fillInnerColor, meterDisplayInner.Value);
        meter.SetPropertyBlock(meterPB);
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(meterDisplayOpacity, op => meter.color = meter.color.WithA(op));
        RegisterService<PlayerController>(this);
        RegisterService<IEnemySimpleBulletCollisionReceiver>(this);
        RegisterService<IEnemyPatherCollisionReceiver>(this);
        RegisterService<IEnemyLaserCollisionReceiver>(this);
        RegisterService<IEnemyBulletCollisionReceiver>(this);
    }

    public override void FirstFrame() {
        challenge = ServiceLocator.FindOrNull<IChallengeManager>();
    }

    #region TeamManagement

    public void SetSubshot(Subshot nsubshot) => UpdateTeam((int?) null, nsubshot);
    
    public void UpdateTeam(int? playerIndex = null, Subshot? nsubshot = null, bool force=false) {
        bool didUpdate = false;
        if (playerIndex.Try(out var pind) && Team.SelectedIndex != pind) {
            Team.SelectedIndex = pind;
            didUpdate = true;
        }
        if (nsubshot.Try(out var s) && Team.Subshot != s) {
            if (!Team.HasMultishot)
                Instance.UselessPowerupCollected.OnNext(default);
            else
                ++Instance.SubshotSwitches;
            Team.Subshot = s;
            didUpdate = true;
        }
        if (didUpdate || force) {
            RealizeTeam();
            Instance.TeamUpdated.OnNext(default);
        }
    }
    public void UpdateTeam((ShipConfig, ShotConfig, IAbilityCfg?)? nplayer = null, Subshot? nsubshot = null, bool force=false) {
        int? pind = nplayer.Try(out var p) ? (int?)Team.Ships.IndexOf(x => x == p) : null;
        UpdateTeam(pind, nsubshot, force);
    }
    private void RealizeTeam() {
        var sb = new StringBuilder();
        if (Team.Ship != Ship) {
            bool fromNull = Ship == null;
            Ship = Team.Ship;
            if (movementHandler != null!)
                movementHandler.Cleanup();
            movementHandler = Ship.movementHandler == null ? 
                new PlayerMovement.Standard() : 
                Ship.movementHandler.Value;
            movementHandler.Setup();
            Hurtbox = new(Hurtbox.location, Ship.hurtboxRadius, Ship.grazeboxRadius);
            unityCollider.radius = Ship.hurtboxRadius;
            sb.Append($"Set ship to {Ship.key}\n");
            if (SpawnedShip != null) {
                //animate "destruction"
                SpawnedShip.InvokeCull();
            }
            SpawnedShip = Instantiate(Team.Ship.prefab, tr).GetComponent<ShipController>();
            if (!fromNull) {
                var pm = onSwitchParticle.main;
                pm.startColor = new ParticleSystem.MinMaxGradient(SpawnedShip.meterDisplayInner);
                onSwitchParticle.Play();
            }
        }
        if (Team.Shot != shot || (Team.Shot.isMultiShot && Team.Subshot != subshot)) {
            shotItr = ++shotItrCounter;
            shot = Team.Shot;
            subshot = Team.Subshot;
            sb.Append($"Set shot to {shot.key}:{subshot}\n");
            if (DestroyExistingShot())
                ISFXService.SFXService.Request(Team.Shot.onSwap);
            var realized = Team.Shot.GetSubshot(Team.Subshot);
            spawnedShot = realized.playerChild ? 
                GameObject.Instantiate(realized.prefab, tr) : 
                GameObject.Instantiate(realized.prefab);
            foreach (var fo in spawnedShot.GetComponentsInChildren<IPlayerShotElement>())
                fo.Initialize(this);
        }
        Logs.Log($"Updated player team:\n{sb}");
        _UpdateTeamColors();
    }

    private void _UpdateTeamColors() {
        meterDisplay.Push(SpawnedShip.meterDisplay);
        meterDisplayShadow.Push(SpawnedShip.meterDisplayShadow);
        meterDisplayInner.Push(SpawnedShip.meterDisplayInner);

        var ps = speedLines.main;
        ps.startColor = SpawnedShip.speedLineColor;
    }
    
    /// <summary>
    /// Returns true if there was something to be destroyed.
    /// </summary>
    /// <returns></returns>
    private bool DestroyExistingShot() {
        if (spawnedShot != null) {
            //This is kind of stupid but it's necessary to ensure that
            //the coroutines end immediately rather than at the end of the update loop
            foreach (var b in spawnedShot.GetComponentsInChildren<BehaviorEntity>())
                b.InvokeCull();
            Destroy(spawnedShot.gameObject);
            return true;
        }
        return false;
    }

    #endregion
    public void SetLocation(Vector2 next) {
        bpi.loc = tr.position = next;
        Hurtbox = new(next, Hurtbox.radius, Hurtbox.grazeRadius);
        LocationHelpers.UpdatePlayerLocation(next, next);
    }

    public float CombinedSpeedMultiplier =>
        (IsFocus ? Ship.FocusSpeed : Ship.freeSpeed) *
        (spawnedCamera != null ? spawnedCamera.CameraSpeedMultiplier : 1) *
        StateSpeedMultiplier(State) * (float)GameManagement.Difficulty.playerSpeedMultiplier;
    
    private void MovementUpdate(float dT) {
        bpi.t += dT;
        if (IsTryingBomb && GameManagement.Difficulty.bombsEnabled && Team.Support is Ability.Bomb b) {
            InputInControl = InputInControlMethod.INPUT_ACTIVE;
            if (deathbomb == DeathbombState.NULL)
                b.TryBomb(this, BombContext.NORMAL);
            else if (deathbomb == DeathbombState.WAITING && b.TryBomb(this, BombContext.DEATHBOMB)) {
                deathbomb = DeathbombState.PERFORMED;
            }
        }
        hitboxSprite.enabled = IsFocus || SaveData.s.UnfocusedHitbox;
        if (StateAllowsPlayerMovement(State)) {
            var delta = movementHandler.UpdateNextDesiredDelta(this, Ship, dT, out bool didInput);
            if (didInput)
                InputInControl = InputInControlMethod.INPUT_ACTIVE;
            if (delta.sqrMagnitude > 0) {
                timeSinceLastStandstill += dT;
                if (timeSinceLastStandstill * 120f < lnrizeSpeed && SaveData.s.AllowInputLinearization) {
                    delta *= 1 - lnrizeRatio +
                                lnrizeRatio * Mathf.Floor(1f + timeSinceLastStandstill * 120f) / lnrizeSpeed;
                }
            } else {
                delta = Vector2.zero;
                timeSinceLastStandstill = 0f;
            }
            Vector2 pos = tr.position;
            var desiredDelta = delta;
            var oob = CheckOOB(ref pos, ref delta);
            bool collided = false, squashed = false;
            Vector2 moveDelta = delta, carryDelta = Vector2.zero;
            if (ReceivesWallCollisions) {
                (moveDelta, carryDelta) = CollisionMath.CollideAndSlide(delta, pos, Hurtbox.radius, 1 << obstacleCollisionLayer, 
                    out collided, out squashed);
                delta = moveDelta + carryDelta;
            }
            SetLocation(pos += delta); 
            if (squashed || 
                oob.HasFlag(LocationHelpers.Direction.Left) && pos.x < LeftPlayerBound || 
                oob.HasFlag(LocationHelpers.Direction.Right) && pos.x > RightPlayerBound || 
                oob.HasFlag(LocationHelpers.Direction.Down) && pos.y < BotPlayerBound || 
                oob.HasFlag(LocationHelpers.Direction.Up) && pos.y > TopPlayerBound) {
                LoseLives(1, true, true);
                SetMovementDelta(Vector2.zero);
                SpawnedShip.SetMovementDelta(Vector2.zero);
                return;
            }
            SetMovementDelta(moveDelta, desiredDelta);
            SpawnedShip.SetMovementDelta(moveDelta, desiredDelta);
            if (delta.x * delta.x + delta.y * delta.y > 0) {
                PastPositions.Add(Hurtbox.location);
                PastDirections.Add(desiredDelta.normalized);
                if (IsFocus) {
                    //Add offset to all tracking positions so they stay the same relative position
                    for (int ii = 0; ii < MarisaAPositions.Count; ++ii) {
                        MarisaAPositions.RelativeIndex(ii) += delta;
                    }
                } else {
                    MarisaAPositions.Add(Hurtbox.location);
                    MarisaADirections.Add(desiredDelta.normalized);
                }
            }
        } else {
            movementHandler.UpdateMovementNotAllowed(this, Ship, dT);
            SetMovementDelta(Vector2.zero);
            SpawnedShip.SetMovementDelta(Vector2.zero);
        }
    }

    public override void RegularUpdate() {
        ShowFocusRings = IsFocus;
        base.RegularUpdate();
        if (AllControlEnabled) 
            Instance.UpdatePlayerFrame(State);
        if (AllowPlayerInput) {
            if (InputManager.IsSwap && Team.Ships.Length > 1) {
                var meterReq = Instance.MeterF.MeterForSwap;
                if (Instance.MeterF.TryConsumeMeterDiscrete(meterReq)) {
                    UpdateTeam((Team.SelectedIndex + 1) % Team.Ships.Length);
                } else
                    PlayerMeterFailed.OnNext(default);
                if (meterReq > 0)
                    RunDroppableRIEnumerator(ShowMeterDisplay(0.5f, Cancellable.Null));
            }
            
        }
        for (int ii = 0; ii < grazeCooldowns.Keys.Count; ++ii)
            if (grazeCooldowns.Keys.GetMarkerIfExistsAt(ii, out var dm))
                if (--grazeCooldowns[dm.Value] <= 0)
                    dm.MarkForDeletion();
        grazeCooldowns.Keys.Compact();
        collisions = new(0, 0);
        pickedUpFlakes = 0;
        
        //Hilarious issue. If running a bomb that disables and then re-enables firing,
        //then IsFiring will return false in the movement update and true in the options code.
        //As a result, UnfiringTime will be incorrect and lasers will break.
        //So we have to do the time-set code after coroutines. 

        UpdateInputTimes(IsFocus, IsFiring);
        
        
        if (--scoreLabelBuffer == 0 && labelAccScore > 0) {
            var baseTTL = 0.9f;
            var mult = Mathf.Max(1, (float)Math.Log(labelAccScore / 100.0, 100));
            DropHelpers.DropDropLabel(Location, scoreLabelBonus ? DropHelpers.Cyan : DropHelpers.Azure, $"{labelAccScore:n0}", 
                baseTTL * mult, size: mult);
            labelAccScore = 0;
            scoreLabelBonus = false;
        }

        focusRingDisplayLerp = Mathf.Clamp01(focusRingDisplayLerp + (ShowFocusRings ? 1 : -1) * 
            ETime.FRAME_TIME / FreeFocusLerpTime);

        for (int ii = 0; ii < focusOverlay.Length; ++ii) {
            focusOverlay[ii].SetAlpha(focusRingDisplayLerp * baseFocusOverlayOpacity);
        }
        meterDisplay.Update(ETime.FRAME_TIME);
        meterDisplayShadow.Update(ETime.FRAME_TIME);
        meterDisplayInner.Update(ETime.FRAME_TIME);
        meterDisplayOpacity.Update(ETime.FRAME_TIME);
        UpdatePB();
    }
    
    protected override void RegularUpdateMove() {
        MovementUpdate(ETime.FRAME_TIME);
    }

    private CollisionsAccumulation collisions;
    private int pickedUpFlakes = 0;
    public override void RegularUpdateFinalize() {
        if (collisions.damage > 0)
            LoseLives(1);
        else
            AddGraze(collisions.graze);
        if (pickedUpFlakes > 0) {
            Instance.ScoreF.AddBulletFlakeItem(pickedUpFlakes);
            ISFXService.SFXService.RequestSFXEvent(ISFXService.SFXEventType.FlakeItemCollected);
        }
        base.RegularUpdateFinalize();
    }
    
    #region CollisionHandling
    
    /// <summary>
    /// Receive a fixed amount of damage.
    /// </summary>
    public void TakeHit(int damage = 1) => collisions = collisions.WithDamage(damage);
    
    
    public bool TakeHit(BulletCollisionType typ, CollisionResult coll, int damage, in ParametricInfo bulletBPI, ushort grazeEveryFrames, Vector2? collLoc = null) {
        var at = collLoc ?? bulletBPI.LocV2;
        if (coll.collide) {
            if (CollisionChecker.Value?.Invoke(this, typ, bulletBPI) is false)
                return false;
            collisions = collisions.WithDamage(damage);
            if (bulletBPI.ctx.onHit != null)
                bulletBPI.ctx.onHit.Proc(at, Location, Hurtbox.radius);
            return true;
        } else if (coll.graze) {
            collisions = collisions.WithGraze(GrazeChecker.Value?.Invoke(this, typ, bulletBPI) ?? 
                                              TryGrazeBullet(bulletBPI.id, grazeEveryFrames));
            return false;
        } else
            return false;
    }
    
    bool ICircularGrazableEnemySimpleBulletCollisionReceiver.TakeHit(BulletManager.SimpleBulletCollection sbc, 
        CollisionResult coll, int damage, in ParametricInfo bulletBPI) {
        if (sbc.MetaType == BulletManager.SimpleBulletCollection.CollectionType.BulletClearFlake) {
            if (coll.collide)
                ++pickedUpFlakes;
            return coll.collide;
        } else if (sbc.MetaType == BulletManager.SimpleBulletCollection.CollectionType.Normal)
            return TakeHit(BulletCollisionType.SimpleBullet, coll, damage, bulletBPI, sbc.BC.grazeEveryFrames);
        else {
            Logs.Log($"A enemy bullet / player collision occurred with unsupported type {sbc.MetaType}.", level: LogLevel.ERROR);
            return false;
        }
    }

    void ICircularGrazableEnemyPatherCollisionReceiver.TakeHit(CurvedTileRenderPather pather, Vector2 collLoc,
        CollisionResult collision) => 
        TakeHit(BulletCollisionType.Pather, collision, pather.Pather.Damage, in pather.BPI, pather.Pather.collisionInfo.grazeEveryFrames, collLoc);
    
    void ICircularGrazableEnemyLaserCollisionReceiver.TakeHit(CurvedTileRenderLaser laser, Vector2 collLoc,
        CollisionResult collision) => 
        TakeHit(BulletCollisionType.Laser, collision, laser.Laser.Damage, in laser.BPI, laser.Laser.collisionInfo.grazeEveryFrames, collLoc);
    
    void ICircularGrazableEnemyBulletCollisionReceiver.TakeHit(Bullet bullet, Vector2 collLoc,
        CollisionResult collision) => 
        TakeHit(BulletCollisionType.SimpleBullet, collision, bullet.Damage, in bullet.rBPI, bullet.collisionInfo.grazeEveryFrames, collLoc);
    
    #endregion

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
    
    public void BufferScoreLabel(long deltaScore, bool bonus) {
        labelAccScore += deltaScore;
        scoreLabelBuffer = ITEM_LABEL_BUFFER;
        scoreLabelBonus |= bonus;
    }
    
    public void ResetInput() => InputInControl = InputInControlMethod.NONE_SINCE_LONGPAUSE;
    
    public GameObject InvokeParentedTimedEffect(EffectStrategy effect, float time) {
        var v = tr.position;
        var effectGO = effect.ProcNotNull(v, v, 0);
        effectGO.transform.SetParent(tr);
        var animator = effectGO.GetComponent<TimeBoundAnimator>();
        if (animator != null) animator.Initialize(Cancellable.Null, time);
        return effectGO;
    }
    
    protected override void CullHook(bool allowFinalize) {
        DestroyExistingShot();
        UpdateInputTimes(false, false); //clears dependent lasers
        base.CullHook(allowFinalize);
    }
    
    protected override void OnDisable() {
        StateFlow.SetNext(new PlayerState.NULL());
        movementHandler.Cleanup();
        base.OnDisable();
    }

    #region HPManagement
    
    //Note that we should not use a dictionary from ID to frame number, since graze cooldowns freeze with the player
    private readonly DictionaryWithKeys<uint, int> grazeCooldowns = new();
    public int TryGrazeBullet(uint id, int cooldownFrames) {
        if (grazeCooldowns.Data.ContainsKey(id))
            return 0;
        grazeCooldowns[id] = cooldownFrames;
        return 1;
    }
    
    public void AddGraze(int graze) {
        if (graze <= 0 || HitInvuln) return;
        GameManagement.Instance.AddGraze(graze);
    }
    
    public void LoseLives(int livesLost, bool ignoreInvulnerability = false, bool forceTraditionalRespawn = false) {
        if (livesLost <= 0) return;
        //Log.Unity($"The player has taken a hit for {dmg} hp. Force: {force} Invuln: {invulnerabilityCounter} Deathbomb: {waitingDeathbomb}");
        if (ignoreInvulnerability) {
            _DoLoseLives(livesLost, forceTraditionalRespawn);
        }
        else {
            if (HitInvuln || deathbomb != DeathbombState.NULL) 
                return;
            var frames = (Team.Support as Ability.Bomb)?.DeathbombFrames ?? 0;
            if (frames > 0) {
                deathbomb = DeathbombState.WAITING;
                RunRIEnumerator(WaitDeathbomb(livesLost, frames, forceTraditionalRespawn));
            } else
                _DoLoseLives(livesLost, forceTraditionalRespawn);
        }
    }
    
    private void _DoLoseLives(int livesLost, bool forceTraditionalRespawn) {
        if (deleteBulletsOnDeath)
            BulletManager.AutodeleteCircleOverTime(new(Location, 1.35f, 0f, 12f, null));
        BulletManager.RequestPowerAura("powerup1", 0, 0, new RealizedPowerAuraOptions(
            new PowerAuraOptions(new[] {
                PowerAuraOption.Color(_ => SpawnedShip.meterDisplay.CV4()),
                PowerAuraOption.Time(_ => 1.5f),
                PowerAuraOption.Iterations(_ => -1f),
                PowerAuraOption.Scale(_ => 5f),
                PowerAuraOption.Static(), 
                PowerAuraOption.High(), 
            }), GenCtx.Empty, Location, Cancellable.Null, null!));
        GameManagement.Instance.BasicF.AddLives(-livesLost);
        ServiceLocator.FindOrNull<IRaiko>()?.Shake(1.5f, null, 0.9f);
        Invuln(HitInvulnFrames);
        if (forceTraditionalRespawn || RespawnOnHit) {
            SpawnedShip.DrawGhost(2f);
            StateFlow.SetNext(new PlayerState.Respawn());
        }
        else InvokeParentedTimedEffect(SpawnedShip.OnHitEffect, hitInvuln);
    }
    
    
    private IEnumerator WaitDeathbomb(int livesLost, int frames, bool forceTraditionalRespawn) {
        SpawnedShip.OnPreHitEffect.Proc(bpi.loc, bpi.loc, 1f);
        using var gcx = GenCtx.New(this);
        BulletManager.RequestPowerAura("powerup1", 0, 0, new RealizedPowerAuraOptions(
            new PowerAuraOptions(new[] {
                PowerAuraOption.Color(_ => SpawnedShip.meterDisplay.WithA(1.5f).CV4()),
                PowerAuraOption.InitialTime(_ => 0.7f * frames / 120f), 
                PowerAuraOption.Time(_ => 1.7f * frames / 120f),
                PowerAuraOption.Iterations(_ => 0.8f),
                PowerAuraOption.Scale(_ => Mathf.Pow(frames / 30f, 0.7f)),
                PowerAuraOption.High(), 
            }), gcx, Vector2.zero, Cancellable.Null, null!));
        var framesRem = frames;
        while (framesRem-- > 0 && deathbomb == DeathbombState.WAITING) 
            yield return null;
        if (deathbomb != DeathbombState.PERFORMED) 
            _DoLoseLives(livesLost, forceTraditionalRespawn);
        else 
            Logs.Log($"The player successfully deathbombed");
        deathbomb = DeathbombState.NULL;
    }
    
    private void Invuln(int frames) {
        var token = HitInvuln.AddConst(true);
        RunDroppableRIEnumerator(WaitOutInvuln(frames, token));
    }
    
    private IEnumerator WaitOutInvuln(int frames, IDisposable token) {
        var bombDisable = BombsEnabled.AddConst(false);
        using var _ = token;
        int ii = frames;
        for (; ii > 60; --ii)
            yield return null;
        bombDisable.Dispose();
        for (; ii > 0; --ii)
            yield return null;
    }

    public void MakeInvulnerable(int frames, bool showEffect) {
        Logs.Log($"The player will be invulnerable for {frames} frames (display effect: {showEffect})");
        GoldenAuraInvuln(frames, showEffect);
    }

    private void GoldenAuraInvuln(int frames, bool showEffect) {
        if (showEffect)
            InvokeParentedTimedEffect(SpawnedShip.GoldenAuraEffect,
                frames * ETime.FRAME_TIME).transform.SetParent(tr);
        Invuln(frames);
    }
    
    #endregion
    

    private void OnTriggerEnter2D(Collider2D other) {
        Console.WriteLine(other.gameObject.name);
    }

    #region ItemMethods

    public void AddPowerItems(int delta) {
        Instance.PowerF.AddPowerItems(delta);
    }
    public void AddFullPowerItems(int _) {
        Instance.PowerF.AddFullPowerItem();
    }
    public void AddValueItems(int delta, double multiplier) {
        double bonus = MeterScorePerValueMultiplier;
        BufferScoreLabel(Instance.ScoreF.AddValueItems(delta, bonus * multiplier), bonus > 1);
    }
    public void AddSmallValueItems(int delta, double multiplier) {
        double bonus = MeterScorePerValueMultiplier;
        BufferScoreLabel(Instance.ScoreF.AddSmallValueItems(delta, bonus * multiplier), bonus > 1);
    }
    public void AddPointPlusItems(int delta) {
        Instance.ScoreF.AddPointPlusItems(delta, MeterPIVPerPPPMultiplier);
    }
    public void AddGems(int delta) {
        Instance.MeterF.AddGems(delta);
    }
    public void AddOneUpItem() {
        Instance.AddOneUpItem();
    }
    public void AddLifeItems(int delta) {
        Instance.LifeItemF.AddLifeItems(delta);
    }
    
    #endregion

    #region ExpressionMethods
    
    [UsedImplicitly]
    public Vector2 PastPosition(float timeAgo) =>
        PastPositions.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS_F));
    public static readonly ExFunction pastPosition = ExFunction.Wrap<PlayerController>(nameof(PastPosition), typeof(float));
    
    [UsedImplicitly]
    public Vector2 PastDirection(float timeAgo) =>
        PastDirections.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS_F));
    public static readonly ExFunction pastDirection = ExFunction.Wrap<PlayerController>(nameof(PastDirection), typeof(float));
    
    [UsedImplicitly]
    public Vector2 MarisaAPosition(float timeAgo) =>
        MarisaAPositions.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS_F));
    public static readonly ExFunction marisaAPosition = ExFunction.Wrap<PlayerController>(nameof(MarisaAPosition), typeof(float));
    
    [UsedImplicitly]
    public Vector2 MarisaADirection(float timeAgo) =>
        MarisaADirections.SafeIndexFromBack((int) (timeAgo * ETime.ENGINEFPS_F));
    public static readonly ExFunction marisaADirection = ExFunction.Wrap<PlayerController>(nameof(MarisaADirection), typeof(float));
    
    public static readonly ExFunction playerID = ExFunction.Wrap<PlayerController>(nameof(PlayerShotItr));
    
    #endregion
    
    

#if UNITY_EDITOR
    private void OnDrawGizmos() {
        Handles.color = Color.cyan;
        var position = transform.position;
        Handles.DrawWireDisc(position, Vector3.forward, Hurtbox.radius);
        Handles.color = Color.blue;
        Handles.DrawWireDisc(position, Vector3.forward, Hurtbox.grazeRadius);
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