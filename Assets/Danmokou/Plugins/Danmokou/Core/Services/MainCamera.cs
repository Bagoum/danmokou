using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.UI;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.Profiling;
using PropConsts = Danmokou.Graphics.PropConsts;

namespace Danmokou.Services {

public class MainCamera : RegularUpdater {
    private static readonly int ShaderScrnWidthID = Shader.PropertyToID("_ScreenWidth");
    private static readonly int ShaderScrnHeightID = Shader.PropertyToID("_ScreenHeight");
    private static readonly int PixelsPerUnitID = Shader.PropertyToID("_PPU");
    private static readonly int GlobalXOffsetID = Shader.PropertyToID("_GlobalXOffset");
    private static readonly int MonitorAspectID = Shader.PropertyToID("_MonitorAspect");

    protected Camera mainCam = null!;
    public static float VertRadius { get; private set; }
    public static float HorizRadius { get; private set; }
    public static float Aspect => HorizRadius / VertRadius;
    public static float ScreenWidth => HorizRadius * 2;
    public static float ScreenHeight => VertRadius * 2;
    private Vector2 position; // Cached to allow requests for screen coordinates from MovementLASM off main thread
    private Transform tr = null!;

    public Material finalRenderMaterial = null!;

    public Camera BackgroundCamera = null!;
    public Camera LowDirectCamera = null!;
    public Camera MiddleCamera = null!;
    public Camera HighDirectCamera = null!;
    public Camera TopCamera = null!;
    public Camera Effects3DCamera = null!;
    public Camera ShaderEffectCamera = null!;
    public static RenderTexture RenderTo { get; protected set; } = null!;

    protected virtual void Awake() {
        mainCam = GetComponent<Camera>();
        VertRadius = mainCam.orthographicSize;
        HorizRadius = VertRadius * 16f / 9f;
        tr = transform;
        position = tr.position;
    }

    private void RecreateRT((int w, int h) res) {
        if (RenderTo != null) RenderTo.Release();
        RenderTo = RenderHelpers.DefaultTempRT(res);
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(RenderHelpers.PreferredResolution, RecreateRT);
        Listen(IGraphicsSettings.SettingsEv, ReassignGlobalShaderVariables);
    }

    public void ReassignGlobalShaderVariables(IGraphicsSettings s) {
        HorizRadius = VertRadius * (s.Resolution.w / (float)s.Resolution.h);
        Shader.SetGlobalFloat(ShaderScrnHeightID, ScreenHeight);
        Shader.SetGlobalFloat(ShaderScrnWidthID, ScreenWidth);
        Shader.SetGlobalFloat(PixelsPerUnitID, s.Resolution.h / ScreenHeight);
        Shader.SetGlobalFloat(GlobalXOffsetID, LocationHelpers.PlayableBounds.center.x);
        if (s.Shaders) Shader.EnableKeyword("FANCY");
        else Shader.DisableKeyword("FANCY");
    }

    [ContextMenu("Debug sizes")]
    public void DebugSizes() {
        mainCam = GetComponent<Camera>();
        Debug.Log($"Vertical {mainCam.orthographicSize} pixels {mainCam.pixelWidth} {mainCam.pixelHeight}");
    }

    /*
    public static bool OutOfViewBy(Vector2 loc, float units) {
        loc -= main.position;
        return (loc.x < Danmaku.LocationService.left - units) ||
               (loc.x > Danmaku.LocationService.right + units) ||
               (loc.y < Danmaku.LocationService.bot - units) ||
               (loc.y > Danmaku.LocationService.top + units);
    }*/
    

    /// <summary>
    /// Convert camera-relative coordinates into UV coordinates
    /// </summary>
    /// <param name="xy">Camera-relative position</param>
    /// <returns></returns>
    public static Vector2 RelativeToScreenUV(Vector2 xy) {
        return new(0.5f + xy.x / ScreenWidth, 0.5f + xy.y / ScreenHeight);
    }
    

    private void OnPreRender() {
        mainCam.targetTexture = RenderTo;
    }

    private void OnPostRender() {
        mainCam.targetTexture = null;
        if (saveNext) {
            saveNext = false;
            FileUtils.WriteTex("DMK_Saves/Aya/mainCamPostRender.jpg", RenderTo.IntoTex());
        }
        finalRenderMaterial.SetFloat(MonitorAspectID, Screen.width / (float)Screen.height);
        UnityEngine.Graphics.Blit(RenderTo, null as RenderTexture, finalRenderMaterial);
    }
    
    private bool saveNext = false;
    [ContextMenu("Save next PostRender")]
    public void SaveNextPostRender() {
        saveNext = true;
    }

    public override void RegularUpdate() { }

    protected override void OnDisable() {
        RenderTo.Release();
        RenderTo = null!;
        base.OnDisable();
    }
}
}
