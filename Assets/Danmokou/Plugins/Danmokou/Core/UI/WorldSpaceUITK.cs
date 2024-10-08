using System;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using UnityEngine;

namespace Danmokou.UI.XML {
public class WorldSpaceUITK : CoroutineRegularUpdater, IOverrideRenderTarget {
    public float Zoom => 1;
    public Vector2 ZoomTarget => Vector2.zero;
    public Vector2 Offset => Vector2.zero;
    public (CameraInfo, int)? AsWorldUI { get; private set; }
    public Vector2? TextureSizeMult { get; private set; }

    private MeshRenderer mesh = null!;
    private MaterialPropertyBlock pb = null!;
    private bool initialized = false;
    
    private void Awake() {
        mesh = GetComponent<MeshRenderer>();
        mesh.sortingLayerName = "UI";
        mesh.GetPropertyBlock(pb = new());
    }

    private IDisposable? renderToken;
    public void Initialize(CRect worldLoc, float worldZ, int group) {
        var tr = transform;
        tr.position = worldLoc.Center.WithZ(worldZ);
        tr.localScale = new(worldLoc.halfW * 2, worldLoc.halfH * 2, 1);
        TextureSizeMult = new(worldLoc.halfW / UIBuilderRenderer.UICamInfo.HorizRadius,
                              worldLoc.halfH / UIBuilderRenderer.UICamInfo.VertRadius);
        initialized = true;
        var uiBuilder = ServiceLocator.Find<UIBuilderRenderer>();
        var layerMask = 1 << gameObject.layer;
        AsWorldUI = (CameraRenderer.FindCapturer(layerMask).Value.CamInfo, layerMask);
        Listen(uiBuilder.RTGroups, rtg => {
            renderToken?.Dispose();
            renderToken = null;
            if (rtg.TryGetValue(group, out var pane)) {
                mesh.enabled = true;
                AddToken(renderToken = pane.Target.AddConst(this));
                pb.SetTexture(PropConsts.mainTex, pane.TempTexture);
            } else {
                mesh.enabled = false;
                pb.SetTexture(PropConsts.mainTex, null);
            }
            mesh.SetPropertyBlock(pb);
        });
    }

    protected override void OnDisable() {
        renderToken?.Dispose();
        base.OnDisable();
    }
}
}