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
    /// <summary>
    /// Take a screenshot of the entire screen area.
    /// <br/>By default, captures all cameras except UI.
    /// <br/>Caller must dispose the return value via Object.Destroy.
    /// </summary>
    RenderTexture Screenshot(DMKMainCamera.CamType[]? cameras = null) => 
        Screenshot(null, cameras);
    
    /// <summary>
    /// Take a screenshot of the screen area described by `rect`, or the entire screen area if it is null.
    /// <br/>By default, captures all cameras except UI.
    /// <br/>Caller must dispose the return value via Object.Destroy.
    /// </summary>
    RenderTexture Screenshot(CRect? rect, DMKMainCamera.CamType[]? cameras = null);
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

    public CameraRenderer FindCamera(CamType type) => type switch {
        CamType.Background => BackgroundCamera,
        CamType.LowDirectRender => LowDirectCamera,
        CamType.Middle => MiddleCamera,
        CamType.HighDirectRender => HighDirectCamera,
        CamType.Top => TopCamera,
        CamType.Effects3D => Effects3DCamera,
        CamType.Shader => ShaderEffectCamera,
        CamType.UI => ServiceLocator.Find<IUIManager>().Camera,
        _ => this
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

    /// <summary>
    /// Caller must dispose the return value via Object.Destroy.
    /// </summary>
    public RenderTexture Screenshot(CRect? rect, CamType[]? cameras=null) {
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
        var originalRenderTo = RenderTo;
        RenderTo = RenderHelpers.DefaultTempRT();
        //Clear is required since the camera list may not contain BackgroundCamera,
        // which is the only one that clears
        Profiler.BeginSample("Clear");
        RenderTo.GLClear();
        Profiler.EndSample();
        Shader.EnableKeyword("AYA_CAPTURE");
        foreach (var c in (cameras ?? AyaCameras).Select(FindCamera)) {
            var camOriginalRenderTo = c.Cam.targetTexture;
            c.Cam.targetTexture = RenderTo;
            Profiler.BeginSample("Render");
            c.Cam.Render();
            Profiler.EndSample();
            c.Cam.targetTexture = camOriginalRenderTo;
        }
        Shader.DisableKeyword("AYA_CAPTURE");
        var ss = RenderHelpers.DefaultTempRT(((int) (SaveData.s.Resolution.Value.w * xsr), 
                                              (int) (SaveData.s.Resolution.Value.h * ysr)));
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
