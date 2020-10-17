using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DMath;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Danmaku {
public class DamageMeasurer : CoroutineRegularUpdater {
    private Enemy enemy;
    private void Awake() {
        enemy = GetComponent<Enemy>();
        RunDroppableRIEnumerator(MeasureDamage());
    }

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
            Log.Unity($"DPS: {total / (totalFrames / ETime.ENGINEFPS)}; " +
                      $"Last {group} frames: {lastg / (group / ETime.ENGINEFPS)}",
                false, Log.Level.DEBUG1);
        }
    }
}
}
