using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using DMath;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Danmaku {
public class PlayerInput : BehaviorEntity {
    public Vector2 XBounds;
    public Vector2 YBounds;
    public SOCircle hitbox;
    public SpriteRenderer hitboxSprite;

    [Header("Movement")] public float blueBoxRadius = .1f;
    public float freeSpeed;
    public float focusSpeed;
    [Tooltip("120 frames per sec")] public int lnrizeSpeed = 10;
    public float lnrizeRatio = .7f;
    private float timeSinceLastStandstill;

    private LayerMask collMask;
    public ShotConfig defaultShot;

    protected override void Awake() {
        base.Awake();
        //Log.Unity($"Player awake", level: Log.Level.DEBUG1);
        collMask = LayerMask.GetMask("Wall");
        hitbox.location = tr.position;
        hitboxSprite.enabled = SaveData.s.UnfocusedHitbox;
    }

    protected override void Start() {
        var sc = GameManagement.campaign.Shot;
        var scd = sc == null ? "Default" : sc.description;
        Log.Unity($"Loading player shot: {scd}", level: Log.Level.DEBUG2);
        if (sc == null) sc = defaultShot;
        if (sc != null) GameObject.Instantiate(sc.prefab, tr);
    }

    
    public override int UpdatePriority => UpdatePriorities.PLAYER;
    public override bool ReceivePartialUpdates => true;
    public override void PartialUpdate(float dT) {
        MovementUpdate(dT, out _, out _);
        partialFatigue += dT;
    }

    private float partialFatigue = 0f;

    public static bool IsFocus => ChallengeManager.r.FocusAllowed && (ChallengeManager.r.FocusForced || InputManager.IsFocus);
    public static bool FiringAndAllowed => InputManager.IsFiring && PlayerInput.AllowPlayerInput;

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

    private void MovementUpdate(float dT, out float horiz_input, out float vert_input) {
        if (FiringAndAllowed) {
            if (IsFocus) {
                FiringTimeFree = 0;
                FiringTimeFocus += dT;
                UnFiringTimeFree += dT;
                UnFiringTimeFocus = 0;
            } else {
                FiringTimeFree += dT;
                FiringTimeFocus = 0;
                UnFiringTimeFree = 0;
                UnFiringTimeFocus += dT;
            }
        } else {
            FiringTimeFree = 0;
            FiringTimeFocus = 0;
            UnFiringTimeFree += dT;
            UnFiringTimeFocus += dT;
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
        if (pos.x <= XBounds.x) {
            pos.x = XBounds.x;
            velocity.x = Mathf.Max(velocity.x, 0f);
        } else if (pos.x >= XBounds.y) {
            pos.x = XBounds.y;
            velocity.x = Mathf.Min(velocity.x, 0f);
        }
        if (pos.y <= YBounds.x) {
            pos.y = YBounds.x;
            velocity.y = Mathf.Max(velocity.y, 0f);
        } else if (pos.y >= YBounds.y) {
            pos.y = YBounds.y;
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

    public static int SMPlayerControlDisable { get; set; } = 0;
    public static bool AllowPlayerInput => (SMPlayerControlDisable == 0) && !Dialoguer.DialogueActive;

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
