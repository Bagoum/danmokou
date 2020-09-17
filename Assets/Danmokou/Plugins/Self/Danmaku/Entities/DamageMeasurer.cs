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

    private IEnumerator MeasureDamage(int group=60) {
        int total = 0;
        int totalFrames = 0;
        while (true) {
            var lastg = 0;
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
            Debug.Log($"DPS: {total / (totalFrames / 120f)}; Last few frames: {lastg / (group / 120f)}");
        }
    }
}
}
