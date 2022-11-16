using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Player;
using Danmokou.UI;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.Profiling;
using PropConsts = Danmokou.Graphics.PropConsts;

namespace Danmokou.Services {

public interface IScreenshotter {
    Texture2D Screenshot(CRect rect, MainCamera.CamType[]? cameras = null);
}

public class MainCamera : RegularUpdater, IScreenshotter {
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

    private Camera mainCam = null!;
    public static float VertRadius { get; private set; }
    public static float HorizRadius { get; private set; }
    public static float Aspect => HorizRadius / VertRadius;
    public static float ScreenWidth => HorizRadius * 2;
    public static float ScreenHeight => VertRadius * 2;
    private Vector2 position; // Cached to allow requests for screen coordinates from MovementLASM off main thread
    private Transform tr = null!;

    public Shader ayaShader = null!;
    private Material ayaMaterial = null!;
    
    public Material finalRenderMaterial = null!;

    public Camera BackgroundCamera = null!;
    public Camera LowDirectCamera = null!;
    public Camera MiddleCamera = null!;
    public Camera HighDirectCamera = null!;
    public Camera TopCamera = null!;
    public Camera Effects3DCamera = null!;
    public Camera ShaderEffectCamera = null!;
    public static RenderTexture RenderTo { get; private set; } = null!;

    private static readonly CamType[] AyaCameras = {
        CamType.Background, CamType.LowDirectRender, CamType.Middle,
        CamType.HighDirectRender, CamType.Top, CamType.Effects3D, CamType.Shader
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

    private void Awake() {
        mainCam = GetComponent<Camera>();
        VertRadius = mainCam.orthographicSize;
        HorizRadius = VertRadius * 16f / 9f;
        tr = transform;
        position = tr.position;
        ayaMaterial = new Material(ayaShader);
        ReassignGlobalShaderVariables(SaveData.s);
    }

    private void RecreateRT((int w, int h) res) {
        if (RenderTo != null) RenderTo.Release();
        RenderTo = RenderHelpers.DefaultTempRT(res);
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IScreenshotter>(this);

        Listen(RenderHelpers.PreferredResolution, RecreateRT);
        Listen(SaveData.SettingsEv, ReassignGlobalShaderVariables);
    }

    public void ReassignGlobalShaderVariables(SaveData.Settings s) {
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

    [ContextMenu("Screenshot all")]
    public void ScreenshotAll() {
        var tex = Screenshot(new CRect(0, 0, MainCamera.HorizRadius, MainCamera.VertRadius, 0),
            AyaCameras.Append(CamType.UI).ToArray());
        FileUtils.WriteTex("DMK_Saves/Aya/screenshotAll.png", tex, FileUtils.ImageFormat.PNG);
        tex.DestroyTexOrRT();
    }
    
    [ContextMenu("Screenshot all except BG")]
    public void ScreenshotAllExceptBG() {
        var tex = Screenshot(new CRect(0, 0, MainCamera.HorizRadius, MainCamera.VertRadius, 0), 
            AyaCameras.Append(CamType.UI).Skip(1).ToArray());
        FileUtils.WriteTex("DMK_Saves/Aya/screenshotAllExceptWall.png", tex, FileUtils.ImageFormat.PNG);
        tex.DestroyTexOrRT();
    }

    /// <summary>
    /// Caller must dispose the return value via Object.Destroy.
    /// </summary>
    public Texture2D Screenshot(CRect rect, CamType[]? cameras=null) {
        Profiler.BeginSample("Screenshot");
        var offset = transform.position;
        ayaMaterial.SetFloat(PropConsts.OffsetX, (rect.x - offset.x) / ScreenWidth);
        ayaMaterial.SetFloat(PropConsts.OffsetY, (rect.y - offset.y) / ScreenHeight);
        float xsr = rect.halfW * 2 / ScreenWidth;
        float ysr = rect.halfH * 2 / ScreenHeight;
        ayaMaterial.SetFloat(PropConsts.ScaleX, xsr);
        ayaMaterial.SetFloat(PropConsts.ScaleY, ysr);
        ayaMaterial.SetFloat(PropConsts.Angle, rect.angle * M.degRad);
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
        var tex = ss.IntoTex();
        Profiler.EndSample();
        ss.Release();
        //For debugging
        //FileUtils.WriteTex("DMK_Saves/Aya/temp.jpg", tex);
        
        //For some reason, I've had strange issues with things turning upside down if I return the RT
        // instead of converting it immediately to a tex. IDK but be warned
        RenderTexture.active = originalRT;
        Profiler.EndSample();
        return tex;
    }

    public override void RegularUpdate() { }

    protected override void OnDisable() {
        RenderTo.Release();
        RenderTo = null!;
        base.OnDisable();
    }
}
}
