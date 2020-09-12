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
    protected virtual bool Collidable => true;
    protected virtual Func<float, Vector2> Velocity => t => Mathf.Lerp(0.5f, -1.8f, t/ 4f) * PoC.Direction;

    public SOCircle target;

    private Vector2 loc;

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

    private const float minTimeBeforeHome = 1f;

    public void Initialize(Vector2 initialLoc) {
        tr.position = loc = initialLoc;
        state = HomingState.NO;
        time = 0;
        timeHoming = 0f;
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
            loc += Velocity(time) * ETime.FRAME_TIME;
            if (Collidable && Collision.CircleOnPoint(loc, target.itemAttractRadius, target.location)) SetHome();
            else if (!LocationService.OnScreenInDirection(loc, -screenRange * PoC.Direction)) {
                PooledDone();
                return;
            }
        }
        tr.position = loc;
        time += ETime.FRAME_TIME;
    }
}
}
