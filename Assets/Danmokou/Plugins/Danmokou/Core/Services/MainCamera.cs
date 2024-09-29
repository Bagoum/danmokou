using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.UI;
using Danmokou.UI.XML;
using SuzunoyaUnity.Rendering;
using UnityEngine;

namespace Danmokou.Services {

public class MainCamera : CameraRenderer {
    private static readonly int MonitorAspectID = Shader.PropertyToID("_MonitorAspect");

    private static MainCamera singleton = null!;
    public static CameraInfo MCamInfo => singleton.CamInfo;

    public Material finalRenderMaterial = null!;

    public CameraRenderer BackgroundCamera = null!;
    public CameraRenderer LowDirectCamera = null!;
    public CameraRenderer MiddleCamera = null!;
    public CameraRenderer HighDirectCamera = null!;
    public CameraRenderer TopCamera = null!;
    public CameraRenderer Effects3DCamera = null!;
    public CameraRenderer ShaderEffectCamera = null!;
    public static RenderTexture RenderTo { get; protected set; } = null!;

    protected override void Awake() {
        base.Awake();
        singleton = this;
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
        if (s.Shaders) Shader.EnableKeyword("FANCY");
        else Shader.DisableKeyword("FANCY");
    }

    /// <summary>
    /// Convert camera-relative coordinates into UV coordinates
    /// </summary>
    /// <param name="xy">Camera-relative position</param>
    /// <returns></returns>
    public static Vector2 RelativeToScreenUV(Vector2 xy) {
        return new(0.5f + xy.x / MCamInfo.ScreenWidth, 0.5f + xy.y / MCamInfo.ScreenHeight);
    }

    private void OnPostRender() {
        CamInfo.Camera.targetTexture = null;
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

    protected override void OnDisable() {
        RenderTo.Release();
        RenderTo = null!;
        base.OnDisable();
    }
}
}
