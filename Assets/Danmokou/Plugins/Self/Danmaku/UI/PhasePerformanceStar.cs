using System;
using System.Collections;
using System.Collections.Generic;
using DMK.Behavior;
using DMK.Core;
using DMK.DMath;
using DMK.Scriptables;
using DMK.Services;
using UnityEngine;

public class PhasePerformanceStar : CoroutineRegularUpdater {
    public float unfilledScale = 1f;
    public float filledScale = 1.2f;
    public SpriteRenderer unfilled = null!;
    public SpriteRenderer filled = null!;
    public SFXConfig? fillSound;
    public float scaleTime = 0.2f;
    public float rotTime = 0.2f;

    private void Awake() {
        unfilled.enabled = false;
        filled.enabled = false;
    }

    public void Show(Color? fillColor) {
        if (fillColor.Try(out var c)) {
            SFXService.Request(fillSound);
            filled.enabled = true;
            filled.color = c;
            var tr = filled.transform;
            tr.localScale = Vector3.zero;
            RunDroppableRIEnumerator(tr.ScaleTo(filledScale, scaleTime, M.EOutSine));
            RunDroppableRIEnumerator(tr.RotateTo(new Vector3(0, 0, 720), rotTime, M.EOutSine));
        } else {
            unfilled.enabled = true;
            var tr = unfilled.transform;
            tr.localScale = Vector3.zero;
            RunDroppableRIEnumerator(tr.ScaleTo(unfilledScale, scaleTime, M.EOutSine));
        }
    }
}