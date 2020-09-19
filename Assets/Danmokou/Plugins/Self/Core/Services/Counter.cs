using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class Counter : RegularUpdater {
    private static Counter main;
    public static int GrazeFrame { get; private set; }
    private static int LowEnemyHPRequests;
    private static float ShotgunMultiplier = 0f;
    private const int multipliersFrameCheck = 15;
    public static int FrameNumber { get; private set; }
    public static float Shotgun { get; set; } = 0f;
    public static bool LowHPRequested { get; set; } = false;
    private void Awake() {
        main = this;
    }

    public override void RegularUpdate() {
        GrazeFrame = 0;
        if (++FrameNumber % multipliersFrameCheck == 0) {
            Shotgun = ShotgunMultiplier;
            LowHPRequested = LowEnemyHPRequests > 0;
            ShotgunMultiplier = 0;
            LowEnemyHPRequests = 0;
        }
    }

    public static void GrazeProc(int ct = 1) {
        GrazeFrame += ct;
    }

    public static void AlertLowEnemyHP() => ++LowEnemyHPRequests;
    public static void DoShotgun(float f) => ShotgunMultiplier = Mathf.Max(ShotgunMultiplier, f);

    public static int ReadResetLowHPBoss() {
        var x = LowEnemyHPRequests;
        LowEnemyHPRequests = 0;
        return x;
    }

    public static float ReadResetShotgun() {
        var x = ShotgunMultiplier;
        ShotgunMultiplier = 0;
        return x;
    }

    public override int UpdatePriority => UpdatePriorities.SYSTEM;

    
}