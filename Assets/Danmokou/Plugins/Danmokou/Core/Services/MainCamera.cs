using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Events;
using BagoumLib.Functional;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Scenes;
using Danmokou.UI;
using Danmokou.UI.XML;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.Serialization;

namespace Danmokou.Services {

public class MainCamera : CameraRenderer, IRenderGroupOutput {
    private static readonly int MonitorAspectID = Shader.PropertyToID("_MonitorAspect");

    private static MainCamera singleton = null!;
    public static CameraInfo MCamInfo => singleton.CamInfo;

    public Material finalRenderMaterial = null!;

    public CameraRenderer BackgroundCamera = null!;
    public CameraRenderer LowCamera = null!;
    public CameraRenderer HighCamera = null!;
    RenderTexture IRenderGroupOutput.RenderTo => RenderTo;
    public static RenderTexture RenderTo => 
        TmpOverrideRenderTo.Try(out var tmp) ? tmp :
        EvRenderTo.HasValue ? EvRenderTo.Value : null!;
    protected static Maybe<RenderTexture> TmpOverrideRenderTo = Maybe<RenderTexture>.None;
    public static ReplayEvent<RenderTexture> EvRenderTo { get; } = new(1);

    protected override void Awake() {
        base.Awake();
        singleton = this;
        RecreateRT(RenderHelpers.PreferredResolution);
    }

    private void RecreateRT((int w, int h) res) {
        Logs.Log($"Updating MainCamera render texture in {res}");
        var nxtRender = RenderTexture.active = RenderHelpers.DefaultTempRT(res);
        if (RenderTo != null) RenderTo.Release();
        EvRenderTo.OnNext(nxtRender);
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IRenderGroupOutput>(this);
        Listen(RenderHelpers.PreferredResolution.OnChange, RecreateRT);
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
        OnPostRenderHook();
        if (saveNext) {
            saveNext = false;
            FileUtils.WriteTex("DMK_Saves/Aya/mainCamPostRender.jpg", RenderTo.IntoTex());
        }
        finalRenderMaterial.SetFloat(MonitorAspectID, Screen.width / (float)Screen.height);
        UnityEngine.Graphics.Blit(RenderTo, null as RenderTexture, finalRenderMaterial);
    }
    
    protected virtual void OnPostRenderHook() { }
    
    private bool saveNext = false;
    [ContextMenu("Save next PostRender")]
    public void SaveNextPostRender() {
        saveNext = true;
    }

    protected override void OnDisable() {
        RenderTo.Release();
        EvRenderTo.OnCompleted();
        EvRenderTo.OnNext(null!);
        base.OnDisable();
    }
}
}
