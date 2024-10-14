using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.UI;
using UnityEngine;

namespace Danmokou.Services {
public class WorldCameraContainer : CoroutineRegularUpdater {
    public bool autoShiftCamera;
    private Transform tr = null!;
    private readonly PushLerper<Vector3> camPos = new(1f, (a, b, t) => Vector3.Lerp(a, b, Easers.EOutSine(t)));
    private readonly Dictionary<CameraInfo, OverrideEvented<(CRect,WorldQuad)?>> panRestricts = new();

    private void Awake() {
        tr = transform;
        if (autoShiftCamera) 
            tr.localPosition = 
                new Vector3(-LocationHelpers.PlayableBounds.center.x, -LocationHelpers.PlayableBounds.center.y, 
                    tr.localPosition.z);
        camPos.Push(tr.position);
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
        if (!autoShiftCamera) {
            camPos.Subscribe(p => tr.position = p);
        }
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        camPos.Update(ETime.FRAME_TIME);
    }

    private (CameraInfo, CRect)? lastTrack;
    /// <summary>
    /// Move the camera `cam` so that an object at the world position `objPos` is within the screenspace rect
    ///  described by `restrict`.
    /// </summary>
    public void TrackTarget(Vector3 objPos, CameraInfo cam, CRect restrict) {
        lastTrack = (cam, restrict);
        var objSPos = cam.ToScreenPoint(objPos);
        if (CollisionMath.PointInRect(objSPos, restrict))
            return;
        //Return the world delta that an object would have to move for it to be at the given screen position.
        Vector2 WorldDelta(Vector2 sPosTarget, Vector3 objectPos) {
            return cam.ViewportToWorldFixedZ(sPosTarget, objectPos.z) - objectPos;
        }
        Vector2 RoundToZero(Vector2 v2) {
            if (Math.Abs(v2.x) < 0.001f)
                v2.x = 0;
            if (Math.Abs(v2.y) < 0.001f)
                v2.y = 0;
            return v2;
        }
        
        var movDelta = WorldDelta(CollisionMath.ProjectPointOntoRect(objSPos, restrict), objPos);
        
        if (panRestricts.TryGetValue(cam, out var evRestr) && evRestr.Value is { } _restr) {
            void BoundMovDelta(Vector2 maxMov, Vector2Int axis) {
                maxMov = RoundToZero(maxMov);
                if (Math.Sign(movDelta.x) == axis.x && Math.Sign(maxMov.x) != -axis.x)
                    movDelta.x = axis.x * Math.Min(Math.Abs(movDelta.x), Math.Abs(maxMov.x));
                if (Math.Sign(movDelta.y) == axis.y && Math.Sign(maxMov.y) != -axis.y)
                    movDelta.y = axis.y * Math.Min(Math.Abs(movDelta.y), Math.Abs(maxMov.y));
            }
            var (viewport, restr) = _restr;
            void RestrictForWorldLoc(Vector3 world) {
                BoundMovDelta(WorldDelta(viewport.BotRight, world), new(-1, 1));
                BoundMovDelta(WorldDelta(viewport.TopRight, world), new(-1, -1));
                BoundMovDelta(WorldDelta(viewport.TopLeft, world), new(1, -1));
                BoundMovDelta(WorldDelta(viewport.BotLeft, world), new(1, 1));
            }
            RestrictForWorldLoc(restr.TopLeft);
            RestrictForWorldLoc(restr.TopRight);
            RestrictForWorldLoc(restr.BotLeft);
            RestrictForWorldLoc(restr.BotRight);
        }
        camPos.Push(tr.position - (Vector3)RoundToZero(movDelta));
    }

    /// <summary>
    /// Restrict camera panning so that a quad located at `worldRect` will always occupy the entirety of the viewport area described by `viewportBound`.
    /// </summary>
    public IDisposable RestrictCameraPan(CameraInfo cam, WorldQuad worldQuad, CRect? viewportBound) {
        if (!panRestricts.ContainsKey(cam))
            panRestricts[cam] = new(null);
        return panRestricts[cam].AddConst((viewportBound ?? new(Vector2.zero, Vector2.one, 0), worldQuad));
    }

    protected override void OnDisable() {
        foreach (var v in panRestricts.Values)
            v.OnCompleted();
        panRestricts.Clear();
        base.OnDisable();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos() {
        if (!lastTrack.HasValue) return;
        var (c, r) = lastTrack.Value;
        Gizmos.color = Color.red;
        Gizmos.DrawLineStrip(new[] {
            c.ViewportToWorldFixedZ(new(0,0), 0),
            c.ViewportToWorldFixedZ(new(1,0), 0),
            c.ViewportToWorldFixedZ(new(1,1), 0),
            c.ViewportToWorldFixedZ(new(0,1), 0),
        }, true);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLineStrip(new[] {
            c.ViewportToWorldFixedZ(r.BotLeft, 0),
            c.ViewportToWorldFixedZ(r.BotRight, 0),
            c.ViewportToWorldFixedZ(r.TopRight, 0),
            c.ViewportToWorldFixedZ(r.TopLeft, 0),
        }, true);
    }
#endif
}
}