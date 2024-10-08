using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Functional;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Graphics;
using Danmokou.Services;
using UnityEngine;

/// <summary>
/// Helper script for all cameras (including MainCamera) that tracks the camera movement and
///  dumps the render contents into MainCamera.RenderTo.
/// </summary>
public class CameraRenderer : CoroutineRegularUpdater {
    public Camera Cam => CamInfo.Camera;
    public CameraInfo CamInfo { get; private set; } = null!;
    
    protected virtual void Awake() {
        CamInfo = new(GetComponent<Camera>(), transform);
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
        Listen(IGraphicsSettings.SettingsEv, s => CamInfo.UpdateAspectFields(s.Resolution));
    }

    public override void RegularUpdate() {
        CamInfo.Recheck();
    }

    private void OnPreRender() {
        CamInfo.Camera.targetTexture = MainCamera.RenderTo;
    }

    public static Maybe<CameraRenderer> FindCapturer(int layerMask) {
        var camrs = ServiceLocator.FindAll<CameraRenderer>();
        for (int ii = 0; ii < camrs.Count; ++ii)
            if (camrs.GetIfExistsAt(ii, out var camr) && (camr.Cam.cullingMask & layerMask) > 0)
                return camr;
        return Maybe<CameraRenderer>.None;
    }

    [ContextMenu("Debug viewport info")]
    public void debugViewport() {
        for (var x = -8; x <= 8; x += 4) {
            for (var y = -4.5f; y < 5f; y += 2.25f) {
                Logs.Log($"({x},{y}):{CamInfo.Camera.WorldToViewportPoint(new(x,y,0))}");
            }
        }
    }
}