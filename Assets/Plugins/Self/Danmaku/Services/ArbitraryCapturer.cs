using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;

public class ArbitraryCapturer : MonoBehaviour {
    public Camera Camera { get; private set; }
    public RenderTexture Captured { get; private set; }
    private int layer;
    
    private readonly DMCompactingArray<Action<RenderTexture>> listeners = new DMCompactingArray<Action<RenderTexture>>();

    private void Awake() {
        Camera = GetComponent<Camera>();
        Camera.targetTexture = Captured = MainCamera.DefaultTempRT();
        layer = LayerMask.NameToLayer("ARBITRARY_CAPTURE");
    }

    public void Draw(Transform tr, Mesh m, Material mat, MaterialPropertyBlock pb) => 
        Graphics.DrawMesh(m, tr.localToWorldMatrix, mat, layer, Camera, 0, pb);

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
