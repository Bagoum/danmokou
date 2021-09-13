using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Player;
using Danmokou.Pooling;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public abstract class Item : Pooled<Item> {
    public static readonly IBSubject<ItemType> ItemCollected = new Event<ItemType>();
    public static readonly IBSubject<ItemType> ItemCulled = new Event<ItemType>();
    
    protected abstract ItemType Type { get; }
    protected virtual bool Autocollectible => true;
    protected virtual bool Attractible => true;
    protected virtual float CollectRadiusBonus => 0;
    protected virtual float speed0 => 1f;
    protected virtual float speed1 => -1.4f;
    protected virtual float peakt => 0.8f;
    protected Vector2 Direction => ((collection != null) ? collection.direction : LRUD.UP).Direction();
    protected virtual Vector2 Velocity(float t) => 
        Mathf.Lerp(speed0, speed1, t * (speed0 / (speed0 - speed1))/peakt) * Direction;

    public SOPlayerHitbox target = null!;

    protected Vector2 loc;

    public SFXConfig? onCollect;

    public enum HomingState {
        NO,
        WAITING,
        HOMING
    }

    protected HomingState State { get; private set; }
    private const float homeRate = 1f;
    private const float screenRange = 0.8f;

    private float timeHoming;
    private const float maxTimeHoming = 1.8f;
    private const float peakedHomeRate = 1f;
    protected float time { get; private set; }

    private const float MinCullTime = 4f;
    protected virtual float CullRadius => 10f;

    private const float lerpIntoOffsetTime = 0.4f;
    protected virtual float MinTimeBeforeHome => 0.9f;

    private Vector2 summonTarget;

    private const short RenderOffsetRange = 1 << 13;
    private static short renderIndex = short.MinValue;
    protected abstract short RenderOffsetIndex { get; }
    protected virtual float RotationTurns => 0;
    protected virtual float RotationTime => 0.8f;

    protected SpriteRenderer sr = null!;
    protected bool autocollected;

    protected PoC? collection { get; private set; }
    
    protected override void Awake() {
        base.Awake();
        sr = GetComponent<SpriteRenderer>();
    }
    
    public virtual void Initialize(Vector2 root, Vector2 targetOffset, PoC? collectionPoint = null) {
        tr.localEulerAngles = Vector3.zero;
        tr.position = loc = root;
        summonTarget = targetOffset;
        State = HomingState.NO;
        time = 0;
        timeHoming = 0f;
        sr.sortingOrder = (short)(renderIndex++ + (short)(RenderOffsetIndex * RenderOffsetRange));
        autocollected = false;
        this.collection = (collectionPoint != null) ? collectionPoint : ServiceLocator.MaybeFind<PoC>();
    }

    public void Autocollect(bool doAutocollect) {
        if (doAutocollect && Autocollectible) {
            autocollected = true;
            SetHome();
        }
    }

    private void SetHome() {
        if (State == HomingState.NO) State = HomingState.WAITING;
    }

    protected virtual void CollectMe(PlayerController collector) {
        ItemCollected.OnNext(Type);
        ServiceLocator.SFXService.Request(onCollect);
        PooledDone();
    }

    public override void RegularUpdate() {
        if (collection != null && collection.Autocollect) Autocollect(true);
        if (State == HomingState.WAITING && time > MinTimeBeforeHome) {
            State = HomingState.HOMING;
        }
        if (CollisionMath.CircleOnPoint(loc, target.itemCollectRadius + CollectRadiusBonus, target.location)) {
            CollectMe(target.Player);
            return;
        } 
        if (State == HomingState.HOMING) {
            timeHoming += ETime.FRAME_TIME;
            loc = Vector2.Lerp(loc, target.location, Mathf.Lerp(homeRate * ETime.FRAME_TIME, peakedHomeRate, timeHoming/maxTimeHoming));
        } else {
            loc += ETime.FRAME_TIME * (Velocity(time) + summonTarget * 
                M.DEOutSine(Mathf.Clamp01(time / lerpIntoOffsetTime)) / lerpIntoOffsetTime);
            if (Attractible && CollisionMath.CircleOnPoint(loc, target.itemAttractRadius, target.location)) SetHome();
            else if (!LocationHelpers.OnScreenInDirection(loc, -screenRange * Direction) || 
                     (time > MinCullTime && !LocationHelpers.OnPlayableScreenBy(CullRadius, loc))) {
                ItemCulled.OnNext(Type);
                PooledDone();
                return;
            }
        }
        tr.localEulerAngles = new Vector3(0, 0, 360 * RotationTurns * 
                                                M.EOutSine(Mathf.Clamp01(time/RotationTime)));
        tr.position = loc;
        time += ETime.FRAME_TIME;
    }
}
}
