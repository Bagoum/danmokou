using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleCallback : MonoBehaviour {

    public void OnParticleSystemStopped() {
        Debug.Log("DONE");
    }

    protected void Awake() {
        var ps = GetComponent<ParticleSystem>();
        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.Callback;
    }

}