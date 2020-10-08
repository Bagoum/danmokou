using System;
using System.Collections;
using System.Collections.Generic;
using DMath;
using UnityEngine;
using Collision = DMath.Collision;

namespace Danmaku {
public abstract class Item : Pooled<Item> {
    protected virtual bool Autocollectible => true;
    protected virtual bool Attractible => true;
    protected virtual float CollectRadiusBonus => 0;
    protected virtual float speed0 => 1f;
    protected virtual float speed1 => -1.8f;
    protected virtual float peakt => 0.8f;
    protected virtual Vector2 Velocity(float t) => 
        Mathf.Lerp(speed0, speed1, t * (speed0 / (speed0 - speed1))/peakt) * PoC.Direction;

    public SOPlayerHitbox target;

    protected Vector2 loc;

    public SFXConfig onCollect;

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
    private const float minTimeBeforeHome = 1f;

    private Vector2 summonTarget;

    private const short RenderOffsetRange = 1 << 13;
    private static short renderIndex = short.MinValue;
    protected abstract short RenderOffsetIndex { get; }
    protected virtual float RotationTurns => 0;
    protected virtual float RotationTime => 0.8f;

    protected SpriteRenderer sr;
    protected override void Awake() {
        base.Awake();
        sr = GetComponent<SpriteRenderer>();
    }
    
    public virtual void Initialize(Vector2 root, Vector2 targetOffset) {
        tr.localEulerAngles = Vector3.zero;
        tr.position = loc = root;
        summonTarget = targetOffset;
        State = HomingState.NO;
        time = 0;
        timeHoming = 0f;
        sr.sortingOrder = (short)(renderIndex++ + (short)(RenderOffsetIndex * RenderOffsetRange));
    }

    public void Autocollect(bool doAutocollect) {
        if (doAutocollect && Autocollectible) SetHome();
    }

    private void SetHome() {
        if (State == HomingState.NO) State = HomingState.WAITING;
    }

    protected virtual void CollectMe() {
        SFXService.Request(onCollect);
        PooledDone();
    }

    public override void RegularUpdate() {
        if (PoC.Autocollect) Autocollect(true);
        if (State == HomingState.WAITING && time > minTimeBeforeHome) {
            State = HomingState.HOMING;
        }
        if (Collision.CircleOnPoint(loc, target.itemCollectRadius + CollectRadiusBonus, target.location)) {
            CollectMe();
            return;
        } 
        if (State == HomingState.HOMING) {
            timeHoming += ETime.FRAME_TIME;
            loc = Vector2.Lerp(loc, target.location, Mathf.Lerp(homeRate * ETime.FRAME_TIME, peakedHomeRate, timeHoming/maxTimeHoming));
        } else {
            loc += ETime.FRAME_TIME * (Velocity(time) + summonTarget * 
                M.DEOutSine(Mathf.Clamp01(time / lerpIntoOffsetTime)) / lerpIntoOffsetTime);
            if (Attractible && Collision.CircleOnPoint(loc, target.itemAttractRadius, target.location)) SetHome();
            else if (!LocationService.OnScreenInDirection(loc, -screenRange * PoC.Direction) || 
                     (time > MinCullTime && !LocationService.OnPlayableScreenBy(CullRadius, loc))) {
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
