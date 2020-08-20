using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Danmaku;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using LocationService = Danmaku.LocationService;

public class BottomTracker : RegularUpdater {
    private BehaviorEntity source;
    [CanBeNull] private Enemy enemy;
    private Transform tr;
    public TextMeshPro text;
    private CancellationToken cT;
    public GameObject container;
    private bool containerActive = true;

    private void Awake() {
        tr = transform;
    }

    public BottomTracker Initialize(BehaviorEntity beh, string sname, CancellationToken canceller) {
        source = beh;
        source.TryAsEnemy(out enemy);
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
            var target = source.GlobalPosition();
            if (containerActive != LocationService.OnPlayableScreenBy(5f, target)) {
                container.SetActive(containerActive = !containerActive);
            }
            if (containerActive) {
                var p = tr.localPosition;
                p.x = source.GlobalPosition().x;
                if (Mathf.Abs(p.x) > maxF) p.x = Mathf.Sign(p.x) * maxF;
                tr.localPosition = p;
                text.color = new Color(1, 1, 1, Mathf.Clamp01(Mathf.Lerp(0.1f, 1.5f, enemy == null ? 1 : enemy.DisplayHPRatio)));
            }
        }
    }

    private const float maxF = 4.6f;
}