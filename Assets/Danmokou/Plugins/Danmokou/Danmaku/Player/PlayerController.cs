using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Runtime.CompilerServices;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Expressions;
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
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using static Danmokou.Services.GameManagement;
using static Danmokou.DMath.LocationHelpers;
using static Danmokou.GameInstance.InstanceConsts;

namespace Danmokou.Player {
/// <summary>
/// A team is a code construct which contains a hitbox, several possible shots (of which one is active),
/// and several possible players (of which one is active).
/// </summary>
public partial class PlayerController : BehaviorEntity, ICircularSimpleBulletCollisionReceiver {
    
    #region Serialized
    public ShipConfig[] defaultPlayers = null!;
    public ShotConfig[] defaultShots = null!;
    public Subshot defaultSubshot;
    public AbilityCfg defaultSupport = null!;


    public float MaxCollisionRadius => Ship.grazeboxRadius;
    public Hurtbox Hurtbox { get; private set; }
    public SpriteRenderer hitboxSprite = null!;
    public SpriteRenderer meter = null!;
    public ParticleSystem speedLines = null!;
    public ParticleSystem onSwitchParticle = null!;
    
    [Header("Movement")]
    [Tooltip("120 frames per sec")] public int lnrizeSpeed = 6;
    public float lnrizeRatio = .7f;
    private float timeSinceLastStandstill;
    
    public float baseFocusOverlayOpacity = 0.5f;
    public SpriteRenderer[] focusOverlay = null!;
    public float hitInvuln = 6;
    #endregion

    #region PrivateState
    public DisturbedAnd FiringEnabled { get; } = new();
    public DisturbedAnd BombsEnabled { get; } = new();
    public DisturbedAnd AllControlEnabled { get; } = new();

    private ushort shotItr = 0;
    [UsedImplicitly]
    public float PlayerShotItr() => shotItr;
    
    public ShipConfig Ship { get; private set; } = null!;
    private ShotConfig shot = null!;
    private Subshot subshot;
    
    private ShipController spawnedShip = null!;
    private GameObject? spawnedShot;
    private AyaCamera? spawnedCamera;
    
    public bool IsMoving => timeSinceLastStandstill > 0;
    private float freeFocusLerp01 = 0f;
    private MaterialPropertyBlock meterPB = null!;
    
    public PlayerState State { get; private set; }
    private GCancellable<PlayerState>? stateCanceller;
    private DeathbombState deathbomb = DeathbombState.NULL;
    
    private int hitInvulnerabilityCounter = 0;
    private IChallengeManager? challenge;
    
    #endregion
    
    #region ComputedProperties

    /// <summary>
    /// True iff collisions can occur against the player. This is only false when the player is in the RESPAWN
    ///  state (ie. has an indeterminate position).
    /// </summary>
    public bool ReceivesCollisions { get; private set; } = true;
    public ICancellee BoundingToken => Instance.Request?.InstTracker ?? Cancellable.Null;
    public bool AllowPlayerInput => AllControlEnabled && StateAllowsInput(State);
    private ActiveTeamConfig Team { get; set; } = null!;
    private bool RespawnOnHit => GameManagement.Difficulty.respawnOnDeath;
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
        Restrictions.FocusAllowed && (Restrictions.FocusForced || (InputManager.IsFocus && AllowPlayerInput));
    public bool IsFiring => InputManager.IsFiring && AllowPlayerInput && FiringEnabled;
    public bool IsTryingBomb =>
        InputManager.IsBomb && AllowPlayerInput && BombsEnabled && Team.Support is Ability.Bomb;
    public bool IsTryingWitchTime => InputManager.IsMeter && AllowPlayerInput && Team.Support is Ability.WitchTime;

    public float MeterScorePerValueMultiplier => State == PlayerState.WITCHTIME ? 2 : 1;
    public float MeterPIVPerPPPMultiplier => State == PlayerState.WITCHTIME ? 2 : 1;
    
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

    private readonly PushLerper<Color> meterDisplay = new(0.4f, Color.Lerp);
    private readonly PushLerper<Color> meterDisplayInner = new(0.4f, Color.Lerp);
    private readonly PushLerper<Color> meterDisplayShadow = new(0.4f, Color.Lerp);
    
    #endregion

    public override int UpdatePriority => UpdatePriorities.PLAYER;

    protected override void Awake() {
        base.Awake();
        Team = GameManagement.Instance.GetOrSetTeam(new ActiveTeamConfig(new TeamConfig(0, defaultSubshot, defaultSupport, 
            defaultPlayers.Zip(defaultShots, (x, y) => (x, y)).ToArray())));
        Logs.Log($"Team awake", level: LogLevel.DEBUG1);
        hitboxSprite.enabled = SaveData.s.UnfocusedHitbox;
        meter.enabled = false;
        
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
        foreach (var (_, s) in Team.Ships) {
            if (s.isMultiShot) {
                foreach (var ss in s.Subshots!)
                    Preload(ss.prefab);
            } else Preload(s.prefab);
        }
        meter.GetPropertyBlock(meterPB = new MaterialPropertyBlock());
        meterPB.SetFloat(PropConsts.innerFillRatio, (float)Instance.MeterF.MeterUseThreshold);
        UpdatePB();
        
        _UpdateTeam();
        
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
        RegisterService<PlayerController>(this);
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
            _UpdateTeam();
            Instance.TeamUpdated.OnNext(default);
        }
    }
    public void UpdateTeam((ShipConfig, ShotConfig)? nplayer = null, Subshot? nsubshot = null, bool force=false) {
        int? pind = nplayer.Try(out var p) ? (int?)Team.Ships.IndexOf(x => x == p) : null;
        UpdateTeam(pind, nsubshot, force);
    }
    private void _UpdateTeam() {
        if (Team.Ship != Ship) {
            bool fromNull = Ship == null;
            Ship = Team.Ship;
            Hurtbox = new(Hurtbox.location, Ship.hurtboxRadius, Ship.grazeboxRadius);
            Logs.Log($"Setting team player to {Ship.key}");
            if (spawnedShip != null) {
                //animate "destruction"
                spawnedShip.InvokeCull();
            }
            spawnedShip = Instantiate(Team.Ship.prefab, tr).GetComponent<ShipController>();
            if (!fromNull) {
                var pm = onSwitchParticle.main;
                pm.startColor = new ParticleSystem.MinMaxGradient(spawnedShip.meterDisplayInner);
                onSwitchParticle.Play();
            }
        }
        if (Team.Shot != shot || (Team.Shot.isMultiShot && Team.Subshot != subshot)) {
            shotItr = ++shotItrCounter;
            shot = Team.Shot;
            subshot = Team.Subshot;
            Logs.Log($"Setting shot to {shot.key}:{subshot}");
            if (DestroyExistingShot())
                ISFXService.SFXService.Request(Team.Shot.onSwap);
            var realized = Team.Shot.GetSubshot(Team.Subshot);
            spawnedShot = realized.playerChild ? 
                GameObject.Instantiate(realized.prefab, tr) : 
                GameObject.Instantiate(realized.prefab);
            foreach (var fo in spawnedShot.GetComponentsInChildren<FireOption>())
                fo.Initialize(this);
            spawnedCamera = spawnedShot.GetComponent<AyaCamera>();
            if (spawnedCamera != null) spawnedCamera.Initialize(this);
        }
        _UpdateTeamColors();
    }

    private void _UpdateTeamColors() {
        meterDisplay.Push(spawnedShip.meterDisplay);
        meterDisplayShadow.Push(spawnedShip.meterDisplayShadow);
        meterDisplayInner.Push(spawnedShip.meterDisplayInner);

        var ps = speedLines.main;
        ps.startColor = spawnedShip.speedLineColor;
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
    private void SetLocation(Vector2 next) {
        bpi.loc = tr.position = next;
        Hurtbox = new(next, Hurtbox.radius, Hurtbox.largeRadius);
        LocationHelpers.UpdateVisiblePlayerLocation(next);
    }
    
    private void MovementUpdate(float dT) {
        bpi.t += dT;
        if (IsTryingBomb && GameManagement.Difficulty.bombsEnabled && Team.Support is Ability.Bomb b) {
            if (deathbomb == DeathbombState.NULL)
                b.TryBomb(this, BombContext.NORMAL);
            else if (deathbomb == DeathbombState.WAITING && b.TryBomb(this, BombContext.DEATHBOMB)) {
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
        velocity *= IsFocus ? Ship.FocusSpeed : Ship.freeSpeed;
        if (spawnedCamera != null) velocity *= spawnedCamera.CameraSpeedMultiplier;
        velocity *= StateSpeedMultiplier(State);
        velocity *= (float) GameManagement.Difficulty.playerSpeedMultiplier;
        //Check bounds
        Vector2 pos = tr.position;
        if (StateAllowsLocationUpdate(State)) {
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
            var prev = Hurtbox.location;
            SetMovementDelta(velocity * dT);
            spawnedShip.SetMovementDelta(velocity * dT);
            SetLocation(pos + velocity * dT); 
            if (IsMoving) {
                var delta = Hurtbox.location - prev;
                var dir = delta.normalized;
                PastPositions.Add(Hurtbox.location);
                PastDirections.Add(dir);
                if (IsFocus) {
                    //Add offset to all tracking positions so they stay the same relative position
                    for (int ii = 0; ii < MarisaAPositions.Count; ++ii) {
                        MarisaAPositions[ii] += delta;
                    }
                } else {
                    MarisaAPositions.Add(Hurtbox.location);
                    MarisaADirections.Add(dir);
                }
            } else {
                SetMovementDelta(Vector2.zero);
                spawnedShip.SetMovementDelta(Vector2.zero);
            }
        }
    }
    
    public override void RegularUpdate() {
        base.RegularUpdate();
        if (AllControlEnabled) 
            Instance.UpdatePlayerFrame(State);
        if (AllowPlayerInput) {
            if (InputManager.IsSwap) {
                Logs.Log("Updating team");
                UpdateTeam((Team.SelectedIndex + 1) % Team.Ships.Length);
            }
        }
        for (int ii = 0; ii < grazeCooldowns.Keys.Count; ++ii)
            if (grazeCooldowns.Keys.GetMarkerIfExistsAt(ii, out var dm))
                if (--grazeCooldowns[dm.Value] <= 0)
                    dm.MarkForDeletion();
        grazeCooldowns.Keys.Compact();
        collisions = new(0, 0);
        
        //Hilarious issue. If running a bomb that disables and then re-enables firing,
        //then IsFiring will return false in the movement update and true in the options code.
        //As a result, UnfiringTime will be incorrect and lasers will break.
        //So we have to do the time-set code after coroutines. 

        UpdateInputTimes(IsFocus, IsFiring);
        
        
        if (--scoreLabelBuffer == 0 && labelAccScore > 0) {
            DropDropLabel(scoreLabelBonus ? scoreGrad_bonus : scoreGrad, $"{labelAccScore:n0}", 
                multiplier: Mathf.Max(1, (float)Math.Log(labelAccScore / 100.0, 100)));
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

    private CollisionsAccumulation collisions;
    public override void RegularUpdateFinalize() {
        if (collisions.damage > 0)
            Hit(1);
        else
            AddGraze(collisions.graze);
        base.RegularUpdateFinalize();
    }

    public void ProcessSimple(BulletManager.SimpleBulletCollection sbc) {
        var hb = Hurtbox;
        var deleted = sbc.Deleted;
        var data = sbc.Data;
        var dmg = sbc.BC.Damage.Value;
        var allowGraze = sbc.BC.AllowGraze.Value;
        var destroy = sbc.BC.Destructible.Value;
        for (int ii = 0; ii < sbc.Count; ++ii) {
            if (!deleted[ii]) {
                ref var sbn = ref data[ii];
                var cr = 
                    //This is also a valid way to do collision, though it's about 5% slower
                    //Collider.CheckGrazeCollision(in sbn.bpi.loc.x, in sbn.bpi.loc.y, in sbn.direction, in sbn.scale, in hitbox);
                    sbc.CheckGrazeCollision(in hb, in sbn);
                if (cr.graze && !allowGraze)
                    cr = cr.NoGraze();
                if ((cr.collide || cr.graze) 
                    && ProcessCollision(in cr, in dmg, in sbn.bpi, in sbc.BC.grazeEveryFrames)) {
                    sbc.RunCollisionControls(ii);
                    if (destroy) {
                        sbc.MakeCulledCopy(ii);
                        sbc.DeleteSB(ii);
                    }
                }
            }
        }
    }

    public void ProcessSimpleBuckets(BulletManager.SimpleBulletCollection sbc, ReadOnlySpan2D<List<int>> indexBuckets) {
        var hb = Hurtbox;
        var deleted = sbc.Deleted;
        var data = sbc.Data;
        var dmg = sbc.BC.Damage.Value;
        var allowGraze = sbc.BC.AllowGraze.Value;
        var destroy = sbc.BC.Destructible.Value;
        for (int y = 0; y < indexBuckets.Height; ++y)
        for (int x = 0; x < indexBuckets.Width; ++x) {
            var bucket = indexBuckets[y, x];
            for (int ib = 0; ib < bucket.Count; ++ib) {
                var index = bucket[ib];
                if (deleted[index]) continue;
                ref var sbn = ref data[index];
                var cr = sbc.CheckGrazeCollision(in hb, in sbn);
                if (cr.graze && !allowGraze)
                    cr = cr.NoGraze();
                if ((cr.collide || cr.graze) 
                    && ProcessCollision(in cr, in dmg, in sbn.bpi, in sbc.BC.grazeEveryFrames)) {
                    sbc.RunCollisionControls(index);
                    if (destroy) {
                        sbc.MakeCulledCopy(index);
                        sbc.DeleteSB(index);
                    }
                }
            }
        }
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
    
    public void BufferScoreLabel(long deltaScore, bool bonus) {
        labelAccScore += deltaScore;
        scoreLabelBuffer = ITEM_LABEL_BUFFER;
        scoreLabelBonus |= bonus;
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

    /// <summary>
    /// Process a collision event with an NPC bullet by checking that it is not on graze cooldown, then append it to the accumulation of events <see cref="collisions"/>.
    /// </summary>
    /// <param name="coll">Collision result.</param>
    /// <param name="damage">Damage dealt by this collision.</param>
    /// <param name="bulletBPI">Bullet information.</param>
    /// <param name="grazeEveryFrames">The number of frames between successive graze events on this bullet.</param>
    /// <returns>Whether or not a hit was taken (ie. a collision occurred).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ProcessCollision(in CollisionResult coll, in int damage, in ParametricInfo bulletBPI, in ushort grazeEveryFrames) {
        if (coll.collide) {
            collisions = collisions.WithDamage(damage);
            return true;
        } else if (coll.graze && TryGrazeBullet(bulletBPI.id, grazeEveryFrames)) {
            collisions = collisions.WithGraze(1);
            return false;
        } else
            return false;
    }

    public void TakeHit() => collisions = collisions.WithDamage(1);
    
    //Note that we should not use a dictionary from ID to frame number, since graze cooldowns freeze with the player
    private readonly DictionaryWithKeys<uint, int> grazeCooldowns = new();
    public bool TryGrazeBullet(uint id, int cooldownFrames) {
        if (grazeCooldowns.Data.ContainsKey(id))
            return false;
        grazeCooldowns[id] = cooldownFrames;
        return true;
    }
    
    public void AddGraze(int graze) {
        if (graze <= 0 || hitInvulnerabilityCounter > 0) return;
        GameManagement.Instance.AddGraze(graze);
    }
    
    public void Hit(int dmg, bool force = false) {
        if (dmg <= 0) return;
        //Log.Unity($"The player has taken a hit for {dmg} hp. Force: {force} Invuln: {invulnerabilityCounter} Deathbomb: {waitingDeathbomb}");
        if (force) {
            _DoHit(dmg);
        }
        else {
            if (hitInvulnerabilityCounter > 0 || deathbomb != DeathbombState.NULL) 
                return;
            var frames = (Team.Support as Ability.Bomb)?.DeathbombFrames ?? 0;
            if (frames > 0) {
                deathbomb = DeathbombState.WAITING;
                RunRIEnumerator(WaitDeathbomb(dmg, frames));
            } else
                _DoHit(dmg);
        }
    }
    
    private void _DoHit(int dmg) {
        BulletManager.AutodeleteCircleOverTime(SoftcullProperties.OverTimeDefault(BPI.loc, 1.35f, 0f, 12f));
        BulletManager.RequestPowerAura("powerup1", 0, 0, new RealizedPowerAuraOptions(
            new PowerAuraOptions(new[] {
                PowerAuraOption.Color(_ => ColorHelpers.CV4(spawnedShip.meterDisplay)),
                PowerAuraOption.Time(_ => 1.5f),
                PowerAuraOption.Iterations(_ => -1f),
                PowerAuraOption.Scale(_ => 5f),
                PowerAuraOption.Static(), 
                PowerAuraOption.High(), 
            }), GenCtx.Empty, BPI.loc, Cancellable.Null, null!));
        GameManagement.Instance.AddLives(-dmg);
        ServiceLocator.FindOrNull<IRaiko>()?.Shake(1.5f, null, 0.9f);
        Invuln(HitInvulnFrames);
        if (RespawnOnHit) {
            spawnedShip.DrawGhost(2f);
            RequestNextState(PlayerState.RESPAWN);
        }
        else InvokeParentedTimedEffect(spawnedShip.OnHitEffect, hitInvuln);
    }
    
    
    private IEnumerator WaitDeathbomb(int dmg, int frames) {
        spawnedShip.OnPreHitEffect.Proc(bpi.loc, bpi.loc, 1f);
        using var gcx = GenCtx.New(this, V2RV2.Zero);
        BulletManager.RequestPowerAura("powerup1", 0, 0, new RealizedPowerAuraOptions(
            new PowerAuraOptions(new[] {
                PowerAuraOption.Color(_ => ColorHelpers.CV4(spawnedShip.meterDisplay.WithA(1.5f))),
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
            _DoHit(dmg);
        else 
            Logs.Log($"The player successfully deathbombed");
        deathbomb = DeathbombState.NULL;
    }
    
    private void Invuln(int frames) {
        ++hitInvulnerabilityCounter;
        RunDroppableRIEnumerator(WaitOutInvuln(frames));
    }
    
    private IEnumerator WaitOutInvuln(int frames) {
        var bombDisable = BombsEnabled.AddConst(false);
        int ii = frames;
        for (; ii > 60; --ii)
            yield return null;
        bombDisable.Dispose();
        for (; ii > 0; --ii)
            yield return null;
        --hitInvulnerabilityCounter;
    }

    public void MakeInvulnerable(int frames, bool showEffect) {
        Logs.Log($"The player will be invulnerable for {frames} frames (display effect: {showEffect})");
        GoldenAuraInvuln(frames, showEffect);
    }

    private void GoldenAuraInvuln(int frames, bool showEffect) {
        if (showEffect)
            InvokeParentedTimedEffect(spawnedShip.GoldenAuraEffect,
                frames * ETime.FRAME_TIME).transform.SetParent(tr);
        Invuln(frames);
    }
    
    #endregion

    #region StateMethods
    
    public void RequestNextState(PlayerState s) => stateCanceller?.Cancel(s);
    
    private IEnumerator ResolveState(PlayerState next, ICancellee<PlayerState> canceller) {
        return next switch {
            PlayerState.NORMAL => StateNormal(canceller),
            PlayerState.WITCHTIME => throw new Exception($"Cannot generically request {nameof(PlayerState.WITCHTIME)} state"),
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
        ReceivesCollisions = true;
        State = PlayerState.NORMAL;
        while (true) {
            if (MaybeCancelState(cT)) yield break;
            if (IsTryingWitchTime && GameManagement.Instance.MeterF.TryStartMeter() is {} meterToken) {
                RunDroppableRIEnumerator(StateWitchTime(cT, meterToken));
                yield break;
            }
            yield return null;
        }
    }
    private IEnumerator StateRespawn(ICancellee<PlayerState> cT) {
        State = PlayerState.RESPAWN;
        spawnedShip.RespawnOnHitEffect.Proc(Hurtbox.location, Hurtbox.location, 0f);
        //The hitbox position doesn't update during respawn, so don't allow collision.
        ReceivesCollisions = false;
        for (float t = 0; t < RespawnFreezeTime; t += ETime.FRAME_TIME) yield return null;
        //Don't update the hitbox location
        tr.position = new Vector2(0f, 100f);
        InvokeParentedTimedEffect(spawnedShip.RespawnAfterEffect, hitInvuln - RespawnFreezeTime);
        //Respawn doesn't respect state cancellations
        for (float t = 0; t < RespawnDisappearTime; t += ETime.FRAME_TIME) yield return null;
        for (float t = 0; t < RespawnMoveTime; t += ETime.FRAME_TIME) {
            tr.position = Vector2.Lerp(RespawnStartLoc, RespawnEndLoc, t / RespawnMoveTime);
            yield return null;
        }
        SetLocation(RespawnEndLoc);
        ReceivesCollisions = true;
        
        if (!MaybeCancelState(cT)) RunDroppableRIEnumerator(StateNormal(cT));
    }
    private IEnumerator StateWitchTime(ICancellee<PlayerState> cT, IDisposable meterToken) {
        GameManagement.Instance.LastMeterStartFrame = ETime.FrameNumber;
        State = PlayerState.WITCHTIME;
        speedLines.Play();
        using var t = ETime.Slowdown.AddConst(WitchTimeSlowdown);
        using var _mt = meterToken;
        meter.enabled = true;
        PlayerActivatedMeter.OnNext(default);
        for (int f = 0; !MaybeCancelState(cT) &&
            IsTryingWitchTime && Instance.MeterF.TryUseMeterFrame(); ++f) {
            spawnedShip.MaybeDrawWitchTimeGhost(f);
            MeterIsActive.OnNext(Instance.MeterF.EnoughMeterToUse ? meterDisplay : meterDisplayInner);
            float meterDisplayRatio = M.EOutSine(Mathf.Clamp01(f / 30f));
            meterPB.SetFloat(PropConsts.fillRatio, Instance.MeterF.VisibleMeter.Value * meterDisplayRatio);
            meter.SetPropertyBlock(meterPB);
            yield return null;
        }
        meter.enabled = false;
        PlayerDeactivatedMeter.OnNext(default);
        speedLines.Stop();
        //MaybeCancelState already run in the for loop
        if (!cT.Cancelled(out _)) RunDroppableRIEnumerator(StateNormal(cT));
    }
    
    
    #endregion
    
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
        Handles.DrawWireDisc(position, Vector3.forward, Hurtbox.largeRadius);
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