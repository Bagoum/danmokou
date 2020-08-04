using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Danmaku;
using TMPro;
using UnityEngine;

public class BottomTracker : RegularUpdater {
    private BehaviorEntity source;
    private Enemy enemy;
    private Transform tr;
    public TextMeshPro text;
    private CancellationToken cT;

    private void Awake() {
        tr = transform;
    }

    public BottomTracker Initialize(BehaviorEntity beh, string sname, CancellationToken canceller) {
        source = beh;
        enemy = source.Enemy;
        text.text = sname;
        cT = canceller;
        return this;
    }

    public override int UpdatePriority => UpdatePriorities.SLOW;

    public void Finish() {
        DisableRegularUpdates();
        Destroy(gameObject);
    }

    public override void RegularUpdate() {
        if (cT.IsCancellationRequested) {
            Finish();
        } else {
            var p = tr.localPosition;
            p.x = source.GlobalPosition().x;
            if (Mathf.Abs(p.x) > maxF) p.x = Mathf.Sign(p.x) * maxF;
            tr.localPosition = p;
            text.color = new Color(1, 1, 1, Mathf.Clamp01(Mathf.Lerp(0.1f, 1.5f, enemy.HPRatio)));
        }
    }

    private const float maxF = 4.8f;
}