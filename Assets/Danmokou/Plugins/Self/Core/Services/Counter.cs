using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class Counter : RegularUpdater {
    private static Counter main;
    public static int GrazeFrame { get; private set; }
    private static int LowEnemyHP;
    private static float ShotgunMultiplier = 0f;
    public static int FrameNumber { get; private set; }
    private void Awake() {
        main = this;
    }

    public override void RegularUpdate() {
        GrazeFrame = 0;
        ++FrameNumber;
    }

    public static void GrazeProc(int ct = 1) {
        GrazeFrame += ct;
    }

    public static void AlertLowEnemyHP() => ++LowEnemyHP;
    public static void Shotgun(float f) => ShotgunMultiplier = Mathf.Max(ShotgunMultiplier, f);

    public static int ReadResetLowHPBoss() {
        var x = LowEnemyHP;
        LowEnemyHP = 0;
        return x;
    }

    public static float ReadResetShotgun() {
        var x = ShotgunMultiplier;
        ShotgunMultiplier = 0;
        return x;
    }

    public override int UpdatePriority => UpdatePriorities.SYSTEM;

    
}