using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.DMath;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {
public class EdgeTracker : RegularUpdater {
    public bool moveHorizontally = true;
    public bool moveVertically = false;
    private BehaviorEntity source = null!;
    private Transform tr = null!;
    public TextMeshPro text = null!;
    private ICancellee cT = null!;
    public GameObject container = null!;
    private bool containerActive = true;

    private void Awake() {
        tr = transform;
    }

    public EdgeTracker Initialize(BehaviorEntity beh, string sname, ICancellee canceller) {
        source = beh;
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
                if (moveHorizontally) {
                    p.x = Mathf.Clamp(source.GlobalPosition().x,
                        LocationHelpers.PlayableBounds.left + yield,
                        LocationHelpers.PlayableBounds.right - yield);
                }
                if (moveVertically) {
                    p.y = Mathf.Clamp(source.GlobalPosition().y,
                        LocationHelpers.PlayableBounds.bot + yield,
                        LocationHelpers.PlayableBounds.top - yield);
                }
                tr.localPosition = p;
            }
        }
    }

    private const float yield = 0.4f;
}
}