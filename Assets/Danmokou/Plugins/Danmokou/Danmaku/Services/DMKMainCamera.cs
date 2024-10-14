using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Scenes;
using Danmokou.UI;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.Profiling;
using PropConsts = Danmokou.Graphics.PropConsts;

namespace Danmokou.Services {

public interface IScreenshotter {
    CameraRenderer FindCamera(DMKMainCamera.CamType type);
    
    /// <summary>
    /// Take a screenshot of the entire screen area.
    /// <br/>By default, captures all cameras except UI.
    /// <br/>Caller must dispose the return value via Object.Destroy.
    /// </summary>
    RenderTexture Screenshot(CameraRenderer[]? cameras = null) => 
        cameras is null ? Screenshot(null as CRect?) : Screenshot(null as CRect?, cameras);
    
    /// <inheritdoc cref="Screenshot(CameraRenderer[])"/>
    RenderTexture Screenshot(DMKMainCamera.CamType[]? cameras = null) => 
        Screenshot(null, cameras);
    
    /// <summary>
    /// Take a screenshot of the screen area described by `rect`, or the entire screen area if it is null.
    /// <br/>By default, captures all cameras except UI.
    /// <br/>Caller must dispose the return value via Object.Destroy.
    /// </summary>
    RenderTexture Screenshot(CRect? rect, CameraRenderer[] cameras);
    
    /// <inheritdoc cref="Screenshot(CRect?, CameraRenderer[])"/>
    RenderTexture Screenshot(CRect? rect, DMKMainCamera.CamType[]? cameras = null) => 
        Screenshot(rect, (cameras ?? DMKMainCamera.WorldCameraTypes).Select(FindCamera).ToArray());

    
}

public class DMKMainCamera : MainCamera, IScreenshotter {
    public enum CamType {
        /// <summary>
        /// Background rendering.
        /// </summary>
        Background,
        /// <summary>
        /// Player bullet rendering, low effects, low BEH bullets (some lasers and pathers), and player rendering.
        /// </summary>
        Low,
        /// <summary>
        /// Enemy bullet rendering, high effects, high BEH bullets (some lasers and pathers), and enemy rendering.
        /// </summary>
        High,
        /// <summary>
        /// UI rendering.
        /// </summary>
        UI,
        // Camera that doesn't actually render anything, but is marked as MainCamera for Unity purposes.
        // Not accessible here because it doesn't have any functionality besides rendering to screen,
        //  which should not be consumed by users.
        //Main,
    }

    public Shader ayaShader = null!;
    private Material ayaMaterial = null!;
    private CameraTransition? ctr;

    public static readonly CamType[] WorldCameraTypes = {
        CamType.Background,CamType.Low, CamType.High,
    };
    public static readonly CamType[] AllCameraTypes = {
        CamType.Background, CamType.Low, CamType.High,  CamType.UI
    };

    public CameraRenderer FindCamera(CamType type) => type switch {
        CamType.Background => BackgroundCamera,
        CamType.Low => LowCamera,
        CamType.High => HighCamera,
        CamType.UI => ServiceLocator.Find<IUIManager>().Camera,
        _ => this
    };

    protected override void Awake() {
        base.Awake();
        ayaMaterial = new Material(ayaShader);
        ctr = GetComponent<CameraTransition>();
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IScreenshotter>(this);
    }

    [ContextMenu("Screenshot all")]
    public void ScreenshotAll() {
        var tex = (this as IScreenshotter).Screenshot(AllCameraTypes);
        FileUtils.WriteTex("DMK_Saves/Aya/screenshotAll.png", tex, FileUtils.ImageFormat.PNG);
        tex.DestroyTexOrRT();
    }
    
    [ContextMenu("Screenshot all except BG")]
    public void ScreenshotAllExceptBG() {
        var tex = (this as IScreenshotter).Screenshot(AllCameraTypes[1..]);
        FileUtils.WriteTex("DMK_Saves/Aya/screenshotAllExceptWall.png", tex, FileUtils.ImageFormat.PNG);
        tex.DestroyTexOrRT();
    }

    /// <summary>
    /// Caller must dispose the return value via Object.Destroy.
    /// </summary>
    public RenderTexture Screenshot(CRect? rect, CameraRenderer[] cameras) {
        var r = rect ?? MCamInfo.Area;
        Profiler.BeginSample("Screenshot");
        var offset = transform.position;
        ayaMaterial.SetFloat(PropConsts.OffsetX, (r.x - offset.x) / MCamInfo.ScreenWidth);
        ayaMaterial.SetFloat(PropConsts.OffsetY, (r.y - offset.y) / MCamInfo.ScreenHeight);
        float xsr = r.halfW * 2 / MCamInfo.ScreenWidth;
        float ysr = r.halfH * 2 / MCamInfo.ScreenHeight;
        ayaMaterial.SetFloat(PropConsts.ScaleX, xsr);
        ayaMaterial.SetFloat(PropConsts.ScaleY, ysr);
        ayaMaterial.SetFloat(PropConsts.Angle, r.angle * BMath.degRad);
        var originalRT = RenderTexture.active;
        var overrideRT = RenderHelpers.CloneRTFormat(MainCamera.RenderTo);
        MainCamera.TmpOverrideRenderTo = overrideRT;
        //Clear is required since the camera list may not contain BackgroundCamera,
        // which is the only one that clears
        Profiler.BeginSample("Clear");
        overrideRT.GLClear();
        Profiler.EndSample();
        Shader.EnableKeyword("AYA_CAPTURE");
        foreach (var c in cameras.OrderBy(x => x.Cam.depth)) {
            var camOriginalRenderTo = c.Cam.targetTexture;
            c.Cam.targetTexture = overrideRT;
            Profiler.BeginSample("Render");
            c.Cam.Render();
            Profiler.EndSample();
            c.Cam.targetTexture = camOriginalRenderTo;
        }
        Shader.DisableKeyword("AYA_CAPTURE");
        var ss = RenderHelpers.DefaultTempRT(((int) (SaveData.s.Resolution.Value.w * xsr), 
                                              (int) (SaveData.s.Resolution.Value.h * ysr)));
        Profiler.BeginSample("Blit");
        UnityEngine.Graphics.Blit(overrideRT, ss, ayaMaterial);
        Profiler.EndSample();
        overrideRT.Release();
        MainCamera.TmpOverrideRenderTo = Maybe<RenderTexture>.None;
        Profiler.BeginSample("Commit");
        //var tex = ss.IntoTex();
        //ss.Release();
        Profiler.EndSample();
        //For debugging
        //FileUtils.WriteTex("DMK_Saves/Aya/temp.jpg", tex);
        
        //For some reason, I've had strange issues with things turning upside down if I return the RT
        // instead of converting it immediately to a tex. IDK but be warned
        RenderTexture.active = originalRT;
        Profiler.EndSample();
        return ss;
    }

    protected override void OnPostRenderHook() {
        if (ctr != null && ctr.Render is {} render) {
            UnityEngine.Graphics.Blit(render.mainTex, RenderTo, render.mat);
        }
    }
}
}
