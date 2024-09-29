using BagoumLib.Events;
using BagoumLib.Mathematics;
using Danmokou.Core;
using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace Danmokou.UI.XML {
/// <summary>
/// A helper class managing the display of a visual cursor shown next to the selected node (if supported by the node).
/// </summary>
public record VisualCursor {
    public UIController Controller { get; }
    public VisualElement CursorHTML { get; }
    private VisualCursorTargetView? follow;
    private Vector2 source = Vector2.zero;
    private Vector2? target;
    private Vector2 lastResultLoc = new(-42f, -42.1f);
    private float elapsed = 0f;
    private const float animTime = 0.12f;
    private readonly PushLerper<float> opacity = new(
        (a, b) => b > a ? 0.3f : 0.08f, (a, b, t) => BMath.LerpU(a, b, a > b ? t : Easers.EOutSine(t)));
    
    public VisualCursor(UIController controller) {
        this.Controller = controller;
        CursorHTML = XMLUtils.Prefabs.Cursor.CloneTreeNoContainer();
        Controller.UIContainer.Add(CursorHTML);
        Controller.AddToken(opacity.Subscribe(f => CursorHTML.style.opacity = f));
        opacity.Push(0);
    }

    public void SetTarget(VisualCursorTargetView n) {
        follow = n;
    }

    public void UnsetTarget(VisualCursorTargetView n) {
        if (n == follow) {
            opacity.PushIfNotSame(0);
            follow = null;
        }
    }

    private void SetLerpedLocation(Vector2 nxtTarget) {
        var nxtResultLoc = Vector2.LerpUnclamped(source, nxtTarget, 
            Easers.EOutQuad(BMath.Clamp(0, 1, elapsed / animTime)));
        CursorHTML.WithAbsolutePosition(nxtResultLoc);
        lastResultLoc = nxtResultLoc;
    }

    public void Update() {
        var dT = Time.deltaTime;
        if (follow is { Node: { Render: { IsAnimatingInTree: false } } }) {
            var nxtTarget = follow.HTML.worldBound.center;
            if (float.IsNaN(nxtTarget.magnitude)) goto no_target;
            opacity.PushIfNotSame(1);
            if (nxtTarget != target && (target is not { } t || (nxtTarget - t).magnitude > 10)) {
                elapsed = target is null ? animTime : 0;
                source = lastResultLoc;
                SetLerpedLocation(nxtTarget);
            } else if (elapsed < animTime) {
                elapsed += dT;
                SetLerpedLocation(nxtTarget);
            }
            target = nxtTarget;
            opacity.Update(dT);
            return;
        }
        no_target:
        opacity.Update(dT);
        if (opacity <= 0f) {
            target = null;
        }
    }
}
}