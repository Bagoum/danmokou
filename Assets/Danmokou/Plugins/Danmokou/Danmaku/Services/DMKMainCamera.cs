using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Mathematics;
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

public interface IScreenshotter {
    public CRect FullScreenArea { get; }
    
    /// <summary>
    /// Take a screenshot of the entire screen area.
    /// <br/>By default, captures all cameras except UI.
    /// <br/>Caller must dispose the return value via Object.Destroy.
    /// </summary>
    RenderTexture Screenshot(DMKMainCamera.CamType[]? cameras = null) => 
        Screenshot(FullScreenArea, cameras);
    
    /// <summary>
    /// Take a screenshot of the screen area described by `rect`.
    /// <br/>By default, captures all cameras except UI.
    /// <br/>Caller must dispose the return value via Object.Destroy.
    /// </summary>
    RenderTexture Screenshot(CRect rect, DMKMainCamera.CamType[]? cameras = null);
}

public class DMKMainCamera : MainCamera, IScreenshotter {
    public enum CamType {
        /// <summary>
        /// Background rendering.
        /// </summary>
        Background,
        /// <summary>
        /// Player bullet rendering.
        /// </summary>
        LowDirectRender,
        /// <summary>
        /// Low effects, low BEH bullets (some lasers and pathers), and player rendering.
        /// </summary>
        Middle,
        /// <summary>
        /// Enemy bullet rendering.
        /// </summary>
        HighDirectRender,
        /// <summary>
        /// High effects, high BEH bullets (some lasers and pathers), and enemy rendering.
        /// </summary>
        Top,
        /// <summary>
        /// 3D camera for some effects.
        /// </summary>
        Effects3D,
        /// <summary>
        /// Camera that applies global shader effects (such as Seija flipping).
        /// </summary>
        Shader,
        // Camera that doesn't actually render anything, but is marked as MainCamera for Unity purposes.
        // Not accessible here because it doesn't have any functionality besides rendering to screen,
        //  which should not be consumed by users.
        //Main,
        UI
    }
    private static readonly int ShaderScrnWidthID = Shader.PropertyToID("_ScreenWidth");
    private static readonly int ShaderScrnHeightID = Shader.PropertyToID("_ScreenHeight");
    private static readonly int PixelsPerUnitID = Shader.PropertyToID("_PPU");
    private static readonly int GlobalXOffsetID = Shader.PropertyToID("_GlobalXOffset");
    private static readonly int MonitorAspectID = Shader.PropertyToID("_MonitorAspect");

    public Shader ayaShader = null!;
    private Material ayaMaterial = null!;

    private static readonly CamType[] AyaCameras = {
        CamType.Background, CamType.LowDirectRender, CamType.Middle,
        CamType.HighDirectRender, CamType.Top, CamType.Effects3D, CamType.Shader
    };
    public static readonly CamType[] AllCameras = {
        CamType.Background, CamType.LowDirectRender, CamType.Middle,
        CamType.HighDirectRender, CamType.Top, CamType.Effects3D, CamType.Shader,
        CamType.UI
    };

    public Camera FindCamera(CamType type) => type switch {
        CamType.Background => BackgroundCamera,
        CamType.LowDirectRender => LowDirectCamera,
        CamType.Middle => MiddleCamera,
        CamType.HighDirectRender => HighDirectCamera,
        CamType.Top => TopCamera,
        CamType.Effects3D => Effects3DCamera,
        CamType.Shader => ShaderEffectCamera,
        CamType.UI => ServiceLocator.Find<IUIManager>().Camera,
        _ => mainCam
    };

    protected override void Awake() {
        base.Awake();
        ayaMaterial = new Material(ayaShader);
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IScreenshotter>(this);
    }

    [ContextMenu("Screenshot all")]
    public void ScreenshotAll() {
        var tex = (this as IScreenshotter).Screenshot(AyaCameras.Append(CamType.UI).ToArray());
        FileUtils.WriteTex("DMK_Saves/Aya/screenshotAll.png", tex, FileUtils.ImageFormat.PNG);
        tex.DestroyTexOrRT();
    }
    
    [ContextMenu("Screenshot all except BG")]
    public void ScreenshotAllExceptBG() {
        var tex = (this as IScreenshotter).Screenshot(AyaCameras.Append(CamType.UI).Skip(1).ToArray());
        FileUtils.WriteTex("DMK_Saves/Aya/screenshotAllExceptWall.png", tex, FileUtils.ImageFormat.PNG);
        tex.DestroyTexOrRT();
    }

    public CRect FullScreenArea => new CRect(transform.position.x, transform.position.y,
        MainCamera.HorizRadius, MainCamera.VertRadius, 0);

    /// <summary>
    /// Caller must dispose the return value via Object.Destroy.
    /// </summary>
    public RenderTexture Screenshot(CRect rect, CamType[]? cameras=null) {
        Profiler.BeginSample("Screenshot");
        var offset = transform.position;
        ayaMaterial.SetFloat(PropConsts.OffsetX, (rect.x - offset.x) / ScreenWidth);
        ayaMaterial.SetFloat(PropConsts.OffsetY, (rect.y - offset.y) / ScreenHeight);
        float xsr = rect.halfW * 2 / ScreenWidth;
        float ysr = rect.halfH * 2 / ScreenHeight;
        ayaMaterial.SetFloat(PropConsts.ScaleX, xsr);
        ayaMaterial.SetFloat(PropConsts.ScaleY, ysr);
        ayaMaterial.SetFloat(PropConsts.Angle, rect.angle * BMath.degRad);
        var originalRT = RenderTexture.active;
        var originalRenderTo = RenderTo;
        RenderTo = RenderHelpers.DefaultTempRT();
        //Clear is required since the camera list may not contain BackgroundCamera,
        // which is the only one that clears
        Profiler.BeginSample("Clear");
        RenderTo.GLClear();
        Profiler.EndSample();
        Shader.EnableKeyword("AYA_CAPTURE");
        foreach (var c in (cameras ?? AyaCameras).Select(FindCamera)) {
            var camOriginalRenderTo = c.targetTexture;
            c.targetTexture = RenderTo;
            Profiler.BeginSample("Render");
            c.Render();
            Profiler.EndSample();
            c.targetTexture = camOriginalRenderTo;
        }
        Shader.DisableKeyword("AYA_CAPTURE");
        var ss = RenderHelpers.DefaultTempRT(((int) (SaveData.s.Resolution.w * xsr), (int) (SaveData.s.Resolution.h * ysr)));
        Profiler.BeginSample("Blit");
        UnityEngine.Graphics.Blit(RenderTo, ss, ayaMaterial);
        Profiler.EndSample();
        RenderTo.Release();
        RenderTo = originalRenderTo;
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
}
}
