using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Behavior.Functions {
public class ExplodeEffect : CoroutineRegularUpdater {
    public ParticleSystem[] particles = null!;
    public ScaleAnimator neg1 = null!;
    public ScaleAnimator neg2 = null!;

    private void Awake() {
        neg1.gameObject.SetActive(false);
        neg2.gameObject.SetActive(false);
    }

    public void Initialize(float time, Vector2 loc, ICancellee cT, Action onMainDone) {
        transform.position = loc;
        foreach (var p in particles) {
            var m = p.main;
            m.duration = time;
            p.Play();
        }
        RunDroppableRIEnumerator(Operate(time, cT, onMainDone));
    }

    private IEnumerator Operate(float timeMain, ICancellee cT, Action onMainDone, float timeExplode = 1f, float secondExplodeDelay = 0.6f) {
        for (float t = 0; t < timeMain && !cT.Cancelled; t += ETime.FRAME_TIME) {
            yield return null;
        }
        onMainDone();
        neg1.gameObject.SetActive(true);
        neg1.Initialize(cT, timeExplode);
        for (float t = 0; t < secondExplodeDelay && !cT.Cancelled; t += ETime.FRAME_TIME) {
            yield return null;
        }
        neg2.gameObject.SetActive(true);
        neg2.Initialize(cT, timeExplode);
        for (float t = 0; t < timeExplode && !cT.Cancelled; t += ETime.FRAME_TIME) {
            yield return null;
        }
        Destroy(gameObject);
    }
}
}