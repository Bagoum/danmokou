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
    private Dictionary<CameraInfo, OverrideEvented<(CRect, float)?>> panRestricts = new();

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
        var movDelta = WorldDelta(CollisionMath.ProjectPointOntoRect(objSPos, restrict), objPos);
        void BoundMovDelta(Vector2 maxMov, Vector2Int axis) {
            maxMov = RoundToZero(maxMov);
            if (Math.Sign(movDelta.x) == axis.x && Math.Sign(maxMov.x) != -axis.x)
                movDelta.x = axis.x * Math.Min(Math.Abs(movDelta.x), Math.Abs(maxMov.x));
            if (Math.Sign(movDelta.y) == axis.y && Math.Sign(maxMov.y) != -axis.y)
                movDelta.y = axis.y * Math.Min(Math.Abs(movDelta.y), Math.Abs(maxMov.y));
        }
        Vector2 RoundToZero(Vector2 v2) {
            if (Math.Abs(v2.x) < 0.001f)
                v2.x = 0;
            if (Math.Abs(v2.y) < 0.001f)
                v2.y = 0;
            return v2;
        }
        bool IsOOB(Vector2 v2) => (v2.x <= 0 || v2.x >= 1) && (v2.y <= 0 || v2.y >= 1);
        
        if (panRestricts.TryGetValue(cam, out var evRestr) && evRestr.Value is { } restr) {
            var (r, z) = restr;
            if (!IsOOB(cam.ToScreenPoint((r.BotRight + movDelta).WithZ(z))))
                BoundMovDelta(WorldDelta(new(1, 0), r.BotRight.WithZ(z)), new(-1, 1));
            if (!IsOOB(cam.ToScreenPoint((r.TopRight + movDelta).WithZ(z))))
                BoundMovDelta(WorldDelta(new(1, 1), r.TopRight.WithZ(z)), new(-1, -1));
            if (!IsOOB(cam.ToScreenPoint((r.TopLeft + movDelta).WithZ(z))))
                BoundMovDelta(WorldDelta(new(0, 1), r.TopLeft.WithZ(z)), new(1, -1));
            if (!IsOOB(cam.ToScreenPoint((r.BotLeft + movDelta).WithZ(z))))
                BoundMovDelta(WorldDelta(new(0, 0), r.BotLeft.WithZ(z)), new(1, 1));
        }
        camPos.Push(tr.position - (Vector3)RoundToZero(movDelta));
    }

    /// <summary>
    /// Move the camera `cam` so that an object at the screen position `screenPos` is within the screenspace rect
    ///  described by `restrict`.
    /// </summary>
    public void TrackScreenTarget(Vector3 screenPos, CameraInfo cam, CRect restrict) {
        var objPos = cam.ViewportToWorldFixedZ(screenPos, screenPos.z);
        TrackTarget(objPos, cam, restrict);
    }

    /// <summary>
    /// Move the camera `cam` so that an object at the XML position `xmlPos` is within the screenspace rect
    ///  described by `restrict`.
    /// </summary>
    public void TrackXMLTarget(Vector3 xmlPos, CameraInfo cam, CRect restrict) =>
        TrackScreenTarget(UIBuilderRenderer.XMLToScreenpoint(xmlPos), cam, restrict);

    /// <summary>
    /// Restrict camera panning so that a quad located at `worldRect` will always occupy the entirety of the screen.
    /// </summary>
    public IDisposable RestrictCameraPan(CameraInfo cam, CRect worldRect, float worldZ) {
        if (!panRestricts.ContainsKey(cam))
            panRestricts[cam] = new(null);
        return panRestricts[cam].AddConst((worldRect, worldZ));
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