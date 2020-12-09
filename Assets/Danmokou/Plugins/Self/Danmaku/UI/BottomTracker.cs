using DMK.Behavior;
using DMK.Core;
using DMK.DMath;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

namespace DMK.UI {
public class BottomTracker : RegularUpdater {
    private BehaviorEntity source;
    [CanBeNull] private Enemy enemy;
    private Transform tr;
    public TextMeshPro text;
    private ICancellee cT;
    public GameObject container;
    private bool containerActive = true;

    private void Awake() {
        tr = transform;
    }

    public BottomTracker Initialize(BehaviorEntity beh, string sname, ICancellee canceller) {
        source = beh;
        source.TryAsEnemy(out enemy);
        text.text = sname;
        cT = canceller;
        return this;
    }

    public override int UpdatePriority => UpdatePriorities.SLOW;

    public void Finish() {
        DisableUpdates();
        Destroy(gameObject);
    }

    public override void RegularUpdate() {
        if (cT.Cancelled) {
            Finish();
        } else {
            var target = source.GlobalPosition();
            if (containerActive != LocationHelpers.OnPlayableScreenBy(5f, target)) {
                container.SetActive(containerActive = !containerActive);
            }
            if (containerActive) {
                var p = tr.localPosition;
                p.x = Mathf.Clamp(source.GlobalPosition().x,
                    GameManagement.References.bounds.left + yield,
                    GameManagement.References.bounds.right - yield);
                tr.localPosition = p;
                text.color = new Color(1, 1, 1,
                    Mathf.Clamp01(Mathf.Lerp(0.1f, 1.5f, enemy == null ? 1 : enemy.DisplayBarRatio)));
            }
        }
    }

    private const float yield = 0.4f;
}
}