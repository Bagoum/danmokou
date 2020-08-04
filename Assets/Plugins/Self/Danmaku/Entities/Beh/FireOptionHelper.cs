﻿using System;
using System.Collections;
using System.Threading;
using Danmaku;
using DMath;
using UnityEngine;

public class FireOptionHelper : BehaviorEntity {
    public bool fireIfFocus;

    public override void RegularUpdate() {
        //If the player is not firing, we have to cancel.
        //However, if the player is firing but in the other method, we pause.
        if (!FireOption.FiringAndAllowed || (fireIfFocus == InputManager.IsFocus)) {
            base.RegularUpdate();
        }
    }

    public override int UpdatePriority => UpdatePriorities.PLAYER2;
}