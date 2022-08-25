using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Graphics;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraRenderer : RegularUpdater, IURPCamera {
    private Camera cam = null!;
    private void Awake() {
        cam = GetComponent<Camera>();
    }

    protected override void BindListeners() {
        AddToken(URPCameraManager.Register(cam, this));
        Listen(MainCamera.RenderToEv, r => cam.targetTexture = r);
    }
    public override void RegularUpdate() {}
}