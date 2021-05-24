using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.DataStructures;
using Danmokou.Core;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Danmokou.Graphics {
public class ArbitraryCapturer : MonoBehaviour {
    public Camera Camera { get; private set; } = null!;
    public RenderTexture Captured { get; private set; } = null!;

    private readonly DMCompactingArray<Action<RenderTexture>>
        listeners = new DMCompactingArray<Action<RenderTexture>>();

    private void Awake() {
        Camera = GetComponent<Camera>();
        Camera.targetTexture = Captured = MainCamera.DefaultTempRT();
    }

    public void Draw(Transform tr, Mesh m, Material mat, MaterialPropertyBlock pb, int layer) =>
        UnityEngine.Graphics.DrawMesh(m, tr.localToWorldMatrix, mat, layer, Camera, 0, pb);

    private void OnDestroy() {
        Captured.Release();
    }

    public void RecreateTexture() {
        Captured.Release();
        Camera.targetTexture = Captured = MainCamera.DefaultTempRT();
    }

    public void Kill() {
        Destroy(gameObject);
    }
}
}