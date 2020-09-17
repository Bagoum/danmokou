using System;
using System.Collections;
using System.Collections.Generic;
using DMath;
using UnityEngine;
using Collision = DMath.Collision;

namespace Danmaku {

public enum ItemType {
    VALUE,
    PPP,
    LIFE,
    POWER
}
public abstract class Item : Pooled<Item> {
    private bool Collidable => true;
    private const float speed0 = 1f;
    private const float speed1 = -1.8f;
    private const float peakt = 0.8f;
    private Vector2 Velocity(float t) => 
        Mathf.Lerp(speed0, speed1, t * (speed0 / (speed0 - speed1))/peakt) * PoC.Direction;

    public SOCircle target;

    private Vector2 loc;
    private Vector2 lerpInOffset;

    public SFXConfig onCollect;

    public enum HomingState {
        NO,
        WAITING,
        HOMING
    }

    private HomingState state;
    private const float homeRate = 1f;
    private const float screenRange = 0.8f;

    private float timeHoming;
    private const float maxTimeHoming = 1.8f;
    private const float peakedHomeRate = 1f;
    private float time;

    private const float lerpIntoOffsetTime = 0.4f;
    private const float lerpIntoRotationTime = 0.8f;
    private const float minTimeBeforeHome = 1f;

    private Vector2 summonTarget;

    private const short RenderOffsetRange = 1 << 13;
    private static short renderIndex = short.MinValue;
    protected abstract short RenderOffsetIndex { get; }
    protected abstract float RotationTurns { get; }

    private SpriteRenderer sr;
    protected override void Awake() {
        base.Awake();
        sr = GetComponent<SpriteRenderer>();
    }
    
    public void Initialize(Vector2 root, Vector2 targetOffset) {
        tr.localEulerAngles = Vector3.zero;
        tr.position = loc = root;
        summonTarget = targetOffset;
        lerpInOffset = Vector2.zero;
        state = HomingState.NO;
        time = 0;
        timeHoming = 0f;
        sr.sortingOrder = (short)(renderIndex++ + (short)(RenderOffsetIndex * RenderOffsetRange));
    }

    public void Autocollect(bool doAutocollect) {
        if (doAutocollect) SetHome();
    }

    private void SetHome() {
        if (state == HomingState.NO) state = HomingState.WAITING;
    }

    protected virtual void CollectMe() {
        SFXService.Request(onCollect);
        PooledDone();
    }

    public override void RegularUpdate() {
        if (PoC.Autocollect) Autocollect(true);
        if (state == HomingState.WAITING && time > minTimeBeforeHome) {
            state = HomingState.HOMING;
        }
        if (state == HomingState.HOMING) {
            timeHoming += ETime.FRAME_TIME;
            if (Collision.CircleOnPoint(loc, target.itemCollectRadius, target.location)) {
                CollectMe();
                return;
            } else {
                loc = Vector2.Lerp(loc, target.location, Mathf.Lerp(homeRate * ETime.FRAME_TIME, peakedHomeRate, timeHoming/maxTimeHoming));
            }
        } else {
            loc += ETime.FRAME_TIME * (Velocity(time) + summonTarget * 
                M.DEOutSine(Mathf.Clamp01(time / lerpIntoOffsetTime)) / lerpIntoOffsetTime);
            if (Collidable && Collision.CircleOnPoint(loc, target.itemAttractRadius, target.location)) SetHome();
            else if (!LocationService.OnScreenInDirection(loc, -screenRange * PoC.Direction)) {
                PooledDone();
                return;
            }
        }
        tr.localEulerAngles = new Vector3(0, 0, 360 * RotationTurns * 
                                                M.EOutSine(Mathf.Clamp01(time /lerpIntoRotationTime)));
        tr.position = loc + lerpInOffset;
        time += ETime.FRAME_TIME;
    }
}
}
