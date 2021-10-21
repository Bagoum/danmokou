using System;
using System.Collections;
using BagoumLib;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Behavior {
public class DamageMeasurer : CoroutineRegularUpdater {
    private Enemy enemy = null!;
    private void Awake() {
        enemy = GetComponent<Enemy>();
        RunDroppableRIEnumerator(MeasureDamage());
    }

    // ReSharper disable once FunctionRecursiveOnAllPaths
    private IEnumerator MeasureDamage(int group=120) {
        double total = 0;
        double totalFrames = 0;
        while (true) {
            double lastg = 0;
            for (int ii = 0; ii < group; ++ii, ++totalFrames) {
                var prevHp = enemy.HP;
                yield return null;
                if (Input.GetKeyDown(KeyCode.Q)) {
                    RunDroppableRIEnumerator(MeasureDamage());
                    yield break;
                }
                var dmg = prevHp - enemy.HP;
                total += dmg;
                lastg += dmg;
            }
            Logs.Log($"DPS: {total / (totalFrames / ETime.ENGINEFPS_F)}; " +
                      $"Last {group} frames: {lastg / (group / ETime.ENGINEFPS_F)}",
                false, LogLevel.DEBUG1);
        }
    }
}
}
