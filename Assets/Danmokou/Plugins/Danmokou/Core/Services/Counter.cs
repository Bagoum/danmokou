using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Core;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Services {
public class Counter : RegularUpdater {
    public static int GrazeFrame { get; private set; }
    //private static int LowEnemyHPRequests;
    private static int LowEnemyHPLastFrame = -100;
    private const int lowEnemyHPDuration = 30;
    private static float ShotgunMultiplier = 0f;
    private const int multipliersFrameCheck = 15;
    public static int FrameNumber { get; private set; }
    public static float Shotgun { get; set; } = 0f;
    public static bool LowHPRequested { get; set; } = false;

    public override void RegularUpdate() {
        GrazeFrame = 0;
        ++FrameNumber;
        LowHPRequested = (FrameNumber - LowEnemyHPLastFrame) <= lowEnemyHPDuration;
        if (FrameNumber % multipliersFrameCheck == 0) {
            Shotgun = ShotgunMultiplier;
            ShotgunMultiplier = 0;
        } else {
            Shotgun = Mathf.Max(Shotgun, ShotgunMultiplier);
        }
    }

    public static void GrazeProc(int ct = 1) {
        GrazeFrame += ct;
    }

    public static void AlertLowEnemyHP() => LowEnemyHPLastFrame = FrameNumber;
    public static void DoShotgun(float f) => ShotgunMultiplier = Mathf.Max(ShotgunMultiplier, f);
    

    public override int UpdatePriority => UpdatePriorities.EOF;
}
}