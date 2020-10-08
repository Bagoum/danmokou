using System;
using System.Collections;
using System.Collections.Generic;
using DMath;
using UnityEngine;
using Collision = DMath.Collision;

namespace Danmaku {
public class PoC : RegularUpdater {
    public LRUD direction;
    public SOPlayerHitbox target;
    private Transform tr;

    private static PoC main;
    public static Vector2 Direction => main.direction.Direction();
    public static bool Autocollect { get; private set; }

    private void Awake() {
        tr = transform;
        main = this;
    }

    private bool IsColliding() {
        var v2 = tr.position; 
        if      (direction == LRUD.UP && target.location.y > v2.y) return true;
        else if (direction == LRUD.DOWN && target.location.y < v2.y) return true;
        else if (direction == LRUD.LEFT && target.location.x < v2.x) return true;
        else if (direction == LRUD.RIGHT && target.location.x > v2.x) return true;
        else return false;
    }
    
    public override void RegularUpdate() {
        Autocollect = IsColliding();
    }
}
}
