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

    public void Initialize(float time, Vector2 loc) {
        transform.position = loc;
        foreach (var p in particles) {
            var m = p.main;
            m.duration = time;
            p.Play();
        }
        RunDroppableRIEnumerator(Operate(time));
    }

    private IEnumerator Operate(float timeMain, float timeExplode = 1f, float secondExplodeDelay = 0.6f) {
        for (float t = 0; t < timeMain; t += ETime.FRAME_TIME) {
            yield return null;
        }
        neg1.gameObject.SetActive(true);
        neg1.Initialize(Cancellable.Null, timeExplode);
        for (float t = 0; t < secondExplodeDelay; t += ETime.FRAME_TIME) {
            yield return null;
        }
        neg2.gameObject.SetActive(true);
        neg2.Initialize(Cancellable.Null,timeExplode);
        for (float t = 0; t < timeExplode; t += ETime.FRAME_TIME) {
            yield return null;
        }
        Destroy(gameObject);
    }
}
}