using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using DMath;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Danmaku.LocationService;

namespace Danmaku {
public class PlayerInput : BehaviorEntity {
    public SOCircle hitbox;
    public SpriteRenderer hitboxSprite;

    [Header("Movement")] public float blueBoxRadius = .1f;
    public float freeSpeed;
    public float focusSpeed;
    [Tooltip("120 frames per sec")] public int lnrizeSpeed = 10;
    public float lnrizeRatio = .7f;
    private float timeSinceLastStandstill;

    private LayerMask collMask;
    public PlayerConfig thisPlayer;
    public ShotConfig defaultShot;

    private ShotConfig shot;

    protected override void Awake() {
        base.Awake();
        //Log.Unity($"Player awake", level: Log.Level.DEBUG1);
        collMask = LayerMask.GetMask("Wall");
        hitbox.location = tr.position;
        hitboxSprite.enabled = SaveData.s.UnfocusedHitbox;

        if (LoadPlayer()) {
            LoadShot();
            FiringDisableRequests = 0;
            BombDisableRequests = 0;
            SMPlayerControlDisable = 0;
        }
    }
    public static int FiringDisableRequests { get; set; } = 0;
    public static int BombDisableRequests { get; set; } = 0;
    public static int SMPlayerControlDisable { get; set; } = 0;
    public static bool AllowPlayerInput => (SMPlayerControlDisable == 0) && !Dialoguer.DialogueActive;

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
            return true;
        }
    }
    private void LoadShot() {
        shot = GameManagement.campaign.Shot;
        var scd = shot == null ? "Default" : shot.description;
        Log.Unity($"Loading player shot: {scd}", level: Log.Level.DEBUG2);
        if (shot == null) shot = defaultShot;
        if (shot != null) GameObject.Instantiate(shot.prefab, tr);
    }

    
    public override int UpdatePriority => UpdatePriorities.PLAYER;
    public override bool ReceivePartialUpdates => true;
    public override void PartialUpdate(float dT) {
        MovementUpdate(dT, out _, out _);
        partialFatigue += dT;
    }

    private float partialFatigue = 0f;

    public static bool IsFocus => ChallengeManager.r.FocusAllowed && (ChallengeManager.r.FocusForced || InputManager.IsFocus);
    public static bool IsFiring =>
        InputManager.IsFiring && PlayerInput.AllowPlayerInput && FiringDisableRequests == 0;
    public static bool IsBombing =>
        InputManager.IsBomb && PlayerInput.AllowPlayerInput && BombDisableRequests == 0;

    public static float FiringTimeFree { get; private set; }
    public static float FiringTimeFocus { get; private set; }
    public static float UnFiringTimeFree { get; private set; }
    public static float UnFiringTimeFocus { get; private set; }
    public static readonly Expression firingTimeFree = Expression.Property(null, 
        typeof(PlayerInput).GetProperty("FiringTimeFree"));
    public static readonly Expression firingTimeFocus = Expression.Property(null, 
        typeof(PlayerInput).GetProperty("FiringTimeFocus"));
    public static readonly Expression unfiringTimeFree = Expression.Property(null, 
        typeof(PlayerInput).GetProperty("UnFiringTimeFree"));
    public static readonly Expression unfiringTimeFocus = Expression.Property(null, 
        typeof(PlayerInput).GetProperty("UnFiringTimeFocus"));

    private Action deathbombAction;
    public int OpenDeathbombWindow(Action onDeathbomb) {
        deathbombAction = onDeathbomb;
        return shot.bomb.DeathbombFrames();
    }

    public void CloseDeathbombWindow() => deathbombAction = null;
    
    private void MovementUpdate(float dT, out float horiz_input, out float vert_input) {
        if (IsBombing && shot.HasBomb && GameManagement.campaign.TryUseBomb()) {
            if (deathbombAction == null) PlayerBombs.DoBomb(shot.bomb, this);
            else {
                deathbombAction();
                CloseDeathbombWindow();
                PlayerBombs.DoDeathbomb(shot.bomb, this);
            }
        }
        horiz_input = InputManager.HorizontalSpeed;
        vert_input = InputManager.VerticalSpeed;
        hitboxSprite.enabled = IsFocus || SaveData.s.UnfocusedHitbox;
        Vector2 velocity = Vector2.zero;
        if (AllowPlayerInput) {
            if (ChallengeManager.r.HorizAllowed) velocity.x = horiz_input;
            if (ChallengeManager.r.VertAllowed) velocity.y = vert_input;
            lastDelta = velocity;
        }
        var velMag = velocity.magnitude;
        if (velMag > float.Epsilon) {
            velocity /= velMag;
            timeSinceLastStandstill += dT;
            if (timeSinceLastStandstill * 120f < lnrizeSpeed && SaveData.s.AllowInputLinearization) {
                velocity *= 1 - lnrizeRatio +
                            lnrizeRatio * Mathf.Floor(1f + timeSinceLastStandstill * 120f) / lnrizeSpeed;
            }
        } else {
            timeSinceLastStandstill = 0f;
        }
        velocity *= IsFocus ? focusSpeed : freeSpeed;
        //Check bounds
        Vector2 pos = tr.position;
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
        //TODO not sure if this works with the current two-updates-per-frame model.
        Physics2D.SyncTransforms();
        bpi.loc = tr.position = hitbox.location = pos + MoveAgainstWall(pos, blueBoxRadius, velocity * dT, collMask);
        //positions.Add(hitbox.location);
    }

    protected override void RegularUpdateMove() {
        float frame_time = ETime.FRAME_TIME;
        if (frame_time > partialFatigue) {
            frame_time -= partialFatigue;
            partialFatigue = 0;
        } else {
            partialFatigue -= frame_time;
            frame_time = 0f;
        }
        MovementUpdate(frame_time, out _, out _);
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        //Hilarious issue. If running a bomb that disables and then re-enables firing,
        //then IsFiring will return false in the movement update and true in the options code.
        //As a result, UnfiringTime will be incorrect and lasers will break.
        //So we have to do the time-set code after coroutines. 
        
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
        } else {
            FiringTimeFree = 0;
            FiringTimeFocus = 0;
            UnFiringTimeFree += ETime.FRAME_TIME;
            UnFiringTimeFocus += ETime.FRAME_TIME;
        }
    }


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

    //private readonly List<Vector2> positions = new List<Vector2>();

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
