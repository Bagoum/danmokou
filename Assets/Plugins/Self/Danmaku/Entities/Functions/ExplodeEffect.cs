using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplodeEffect : MonoBehaviour {
    public ParticleSystem[] particles;
    public ScaleAnimator neg1;
    public ScaleAnimator neg2;

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
        StartCoroutine(Operate(time));
    }

    private IEnumerator Operate(float timeMain, float timeExplode = 1f, float secondExplodeDelay = 0.6f) {
        for (float t = 0; t < timeMain; t += ETime.dT) {
            yield return null;
        }
        neg1.gameObject.SetActive(true);
        neg1.AssignTime(timeExplode);
        for (float t = 0; t < secondExplodeDelay; t += ETime.dT) {
            yield return null;
        }
        neg2.gameObject.SetActive(true);
        neg2.AssignTime(timeExplode);
        for (float t = 0; t < timeExplode; t += ETime.dT) {
            yield return null;
        }
        Destroy(gameObject);
    }
}