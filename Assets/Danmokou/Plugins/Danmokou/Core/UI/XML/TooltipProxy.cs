using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.UI.XML {
public class TooltipProxy : IRegularUpdater {
    private readonly List<IDisposable> tokens = new();
    public UIGroup TT { get; }
    public UIRenderSpace Render => TT.Render;
    private UINode? prevFollowing;
    private UINode? following;
    private const float lerpTime = 0.2f;
    private float elapsed = 0f;
    private Vector2 lastLoc;
    
    public TooltipProxy(UIGroup tt) {
        this.TT = tt;
        tokens.Add(ETime.RegisterRegularUpdater(this));
    }

    private Vector2 NextLoc {
        get {
            var targetLoc = following!.HTML.DetermineTooltipAbsolutePosition(Render.HTML);
            if (elapsed >= lerpTime) return targetLoc;
            return Vector2.Lerp(prevFollowing!.HTML.DetermineTooltipAbsolutePosition(Render.HTML),
                targetLoc, Easers.EOutSine(elapsed / lerpTime));
        }
    }

    public void Track(UINode node) {
        if (following is null) {
            prevFollowing = node;
            elapsed = lerpTime;
        } else {
            prevFollowing = following;
            elapsed = 0;
        }
        following = node;
        Render.HTML.WithAbsolutePosition(lastLoc = NextLoc);
    }

    public void RegularUpdate() {
        if (TT.Destroyed)
            Close();
        else if (following != null) {
            //TODO: once z-index is supported,
            //it'd be better to use parenting for tooltips that are fixed to nodes
            // (though it'd be more complex for tooltips that can be transferred).
            //This method is always one frame behind since the HTML.worldBound used to locate the tooltip
            // is not updated until UI repaint.
            elapsed += ETime.FRAME_TIME;
            var nxt = NextLoc;
            if (lastLoc != nxt)
                Render.HTML.WithAbsolutePosition(nxt);
        }
    }

    public void Close() {
        tokens.DisposeAll();
        if (!TT.Destroyed)
            TT.LeaveGroup().Log();
    }
}

public class TooltipProxy<T> : TooltipProxy where T: UIGroup {
    public new T TT { get; }
    public TooltipProxy(T tt) : base(tt) {
        this.TT = tt;
    }
}
}