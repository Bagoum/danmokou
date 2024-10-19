using System;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
/// <summary>
/// Settings for rerendering UITK into a sprite in world space.
/// <br/>By default, this is configured to simply rerender to a sprite filling the entire UI camera.
/// </summary>
public record WorldSpaceUITKSettings(int RenderGroup) {
    /// <summary>
    /// The world quad describing the position of the UI (Defaults to the quad covered by the capturing camera).
    /// </summary>
    public WorldQuad? Quad { get; init; }
    /// <summary>
    /// The sorting layer for the sprite (Defaults to "UI").
    /// </summary>
    public string SortingLayerName { get; init; } = "UI";
    /// <summary>
    /// The sorting order for the sprite (Defaults to 1000).
    /// </summary>
    public int SortingOrder { get; init; } = 1000;
    /// <summary>
    /// The GameObject layer (which determines the capturing camera) of the sprite (Defaults to UI).
    /// </summary>
    public int Layer { get; init; } = LayerMask.NameToLayer("UI");
    
    public WorldSpaceUITKSettings(PanelSettings pane) : this(ServiceLocator.Find<UIBuilderRenderer>().GroupOf(pane)) { }
}

public class WorldSpaceUITK : CoroutineRegularUpdater, IOverrideRenderTarget {
    float IOverrideRenderTarget.Zoom => 1;
    Vector2 IOverrideRenderTarget.ZoomTarget => Vector2.zero;
    Vector2 IOverrideRenderTarget.Offset => Vector2.zero;
    public (CameraInfo cam, int layerMask) AsWorldUI { get; private set; }
    (CameraInfo, int)? IOverrideRenderTarget.AsWorldUI => AsWorldUI;
    public Vector2? TextureSizeMult { get; private set; }
    private WorldQuad quad;
    //public float? ResolutionMult => 0.5f;

    private MeshRenderer mesh = null!;
    private MaterialPropertyBlock pb = null!;
    
    private void Awake() {
        mesh = GetComponent<MeshRenderer>();
        mesh.GetPropertyBlock(pb = new());
    }

    private IDisposable? renderToken;
    public void Initialize(WorldSpaceUITKSettings settings) {
        mesh.sortingLayerName = settings.SortingLayerName;
        mesh.sortingOrder = settings.SortingOrder;
        var tr = transform;
        var layerMask = 1 << (gameObject.layer = settings.Layer);
        var capturer = CameraRenderer.FindCapturer(layerMask).Value.CamInfo;
        AsWorldUI = (capturer, layerMask);
        quad = settings.Quad ?? new WorldQuad(capturer.Area.AsRect, 0, Quaternion.identity);
        tr.position = quad.Center;
        tr.localScale = new(quad.BaseRect.width, quad.BaseRect.height, 1);
        tr.localEulerAngles = quad.Rotation.eulerAngles;
        TextureSizeMult = new(quad.BaseRect.width / UIBuilderRenderer.UICamInfo.ScreenWidth,
                              quad.BaseRect.height / UIBuilderRenderer.UICamInfo.ScreenHeight);
        Listen(ServiceLocator.Find<UIBuilderRenderer>().RTGroups, rtg => {
            renderToken?.Dispose();
            renderToken = null;
            if (rtg.TryGetValue(settings.RenderGroup, out var pane)) {
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

    /// <summary>
    /// Find the world location of an item located at `xy` on the rendering panel.
    /// </summary>
    /// <param name="xy">The position of the item in 0->1 coordinates ((0,0) at bottom left).</param>
    private Vector3 PanelToWorld(Vector2 xy) => quad.LocationAtRectCoords(xy);

    private Vector2 WorldToScreen(Vector3 world) => AsWorldUI.cam.WorldToScreen(world);

    /// <summary>
    /// Find the screen location of an item located at `xy` on the rendering panel.
    /// </summary>
    /// <param name="xy">The position of the item in 0->1 coordinates ((0,0) at bottom left).</param>
    public Vector2 PanelToScreen(Vector2 xy) => WorldToScreen(PanelToWorld(xy));
    
    protected override void OnDisable() {
        renderToken?.Dispose();
        base.OnDisable();
    }
}
}