using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;

public class PhasePerformanceStar : CoroutineRegularUpdater {
    public float unfilledScale = 1f;
    public float filledScale = 1.2f;
    public SpriteRenderer unfilled = null!;
    public SpriteRenderer filled = null!;
    public SFXConfig? fillSound;
    [ReflectInto(typeof(Easer))]
    public string scaler = "ceoutback(2.7, t)";
    public float scaleTime = 0.2f;
    [ReflectInto(typeof(Easer))]
    public string rotator = "eoutsine(t)";
    public float rotTime = 0.2f;

    private void Awake() {
        unfilled.enabled = false;
        filled.enabled = false;
    }

    public void Show(Color? fillColor) {
        if (fillColor.Try(out var c)) {
            ServiceLocator.SFXService.Request(fillSound);
            filled.enabled = true;
            filled.color = c;
            var tr = filled.transform;
            tr.localScale = Vector3.zero;
            tr.ScaleTo(filledScale, scaleTime, scaler.Into<Easer>()).Run(this);
            tr.RotateTo(new Vector3(0, 0, 360), rotTime, rotator.Into<Easer>()).Run(this);
        } else {
            unfilled.enabled = true;
            var tr = unfilled.transform;
            tr.localScale = Vector3.zero;
            tr.ScaleTo(unfilledScale, scaleTime, M.EOutSine).Run(this);
        }
    }
}