using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Services;
using UnityEngine;

public class CameraRenderer : MonoBehaviour {
    private Camera cam = null!;
    private void Awake() {
        cam = GetComponent<Camera>();
    }

    private void OnPreRender() {
        cam.targetTexture = MainCamera.RenderTo;
    }
}