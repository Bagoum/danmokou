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
using PropConsts = Danmokou.Graphics.PropConsts;

namespace Danmokou.Services {

public class MainCamera : RegularUpdater {
    public enum CameraType {
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
        /// <summary>
        /// Camera that doesn't actually render anything, but is marked as MainCamera for Unity purposes.
        /// </summary>
        Main,
        UI
    }
    private static readonly int ShaderScrnWidthID = Shader.PropertyToID("_ScreenWidth");
    private static readonly int ShaderScrnHeightID = Shader.PropertyToID("_ScreenHeight");
    private static readonly int ShaderPixelWidthID = Shader.PropertyToID("_PixelWidth");
    private static readonly int ShaderPixelHeightID = Shader.PropertyToID("_PixelHeight");
    private static readonly int PixelsPerUnitID = Shader.PropertyToID("_PPU");
    private static readonly int ResourcePixelsPerUnitID = Shader.PropertyToID("_RPPU");
    private static readonly int RenderRatioID = Shader.PropertyToID("_RenderR");
    private static readonly int GlobalXOffsetID = Shader.PropertyToID("_GlobalXOffset");
    public static MainCamera main = null!;

    public static Camera mainCam = null!;
    public static float VertRadius { get; private set; }
    public static float HorizRadius { get; private set; }
    public static float ScreenWidth => HorizRadius * 2;
    public static float ScreenHeight => VertRadius * 2;
    private Vector2 position; // Cached to allow requests for screen coordinates from MovementLASM off main thread
    private Transform tr = null!;

    public Shader ayaShader = null!;
    private Material ayaMaterial = null!;

    public Camera BackgroundCamera = null!;
    public Camera LowDirectCamera = null!;
    public Camera MiddleCamera = null!;
    public Camera HighDirectCamera = null!;
    public Camera TopCamera = null!;
    public Camera Effects3DCamera = null!;
    public Camera ShaderEffectCamera = null!;
    public static RenderTexture RenderTo { get; private set; } = null!;

    private static readonly CameraType[] AyaCameras = {
        CameraType.Background, CameraType.LowDirectRender, CameraType.Middle,
        CameraType.HighDirectRender, CameraType.Top, CameraType.Effects3D, CameraType.Shader
    };

    public Camera FindCamera(CameraType type) => type switch {
        CameraType.Background => BackgroundCamera,
        CameraType.LowDirectRender => LowDirectCamera,
        CameraType.Middle => MiddleCamera,
        CameraType.HighDirectRender => HighDirectCamera,
        CameraType.Top => TopCamera,
        CameraType.Effects3D => Effects3DCamera,
        CameraType.Shader => ShaderEffectCamera,
        CameraType.UI => UIManager.Camera,
        _ => mainCam
    };

    /*
    public Material postprocessor;
    [Range(1, 16)] public int bloomIterations = 2;
    public float bloomThreshold = 1;
*/

    private void Awake() {
        MainCamera.main = this;
        mainCam = GetComponent<Camera>();
        VertRadius = mainCam.orthographicSize;
        HorizRadius = VertRadius * mainCam.pixelWidth / mainCam.pixelHeight;
        tr = transform;
        position = tr.position;
        ayaMaterial = new Material(ayaShader);
        ReassignGlobalShaderVariables();
        if (RenderTo == null) RecreateRT();
    }

    private void RecreateRT() {
        if (RenderTo != null) RenderTo.Release();
        RenderTo = RenderHelpers.DefaultTempRT();
    }

    protected override void BindListeners() {
        Listen(SaveData.ResolutionHasChanged, RecreateRT);
        base.BindListeners();
    }

    /// <summary>
    /// PPU of default game resources, such as utility images and UI objects. This does not change with resolution.
    /// </summary>
    public static float ResourcePPU => GraphicsUtils.BestResolution.h / ScreenHeight;

    public void ReassignGlobalShaderVariables() {
        //Log.Unity($"Camera width: {cam.pixelWidth} Screen width: {Screen.width}");
        Shader.SetGlobalFloat(ShaderScrnHeightID, ScreenHeight);
        Shader.SetGlobalFloat(ShaderScrnWidthID, ScreenWidth);
        Shader.SetGlobalFloat(PixelsPerUnitID, Screen.height / ScreenHeight);
        Shader.SetGlobalFloat(ResourcePixelsPerUnitID, ResourcePPU);
        Shader.SetGlobalFloat(RenderRatioID, Screen.height / (float) GraphicsUtils.BestResolution.h);
        Shader.SetGlobalFloat(ShaderPixelHeightID, Screen.height);
        Shader.SetGlobalFloat(ShaderPixelWidthID, Screen.width);
        Shader.SetGlobalFloat(GlobalXOffsetID, GameManagement.References.bounds.center.x);
        if (SaveData.s.Shaders) Shader.EnableKeyword("FANCY");
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
    /// Camera-relative world coordinates
    /// </summary>
    public Vector2 GetWorldCoordinates(float x, float y) {
        return new Vector2(position.x + x, position.y + y);
    }

    /// <summary>
    /// Convert a width and height into screen ratios.
    /// </summary>
    /// <param name="width">Width in screen units</param>
    /// <param name="height">Height in screen units</param>
    /// <returns></returns>
    public static Vector2 Descale(float width, float height) {
        return new Vector2(width / ScreenWidth, height / ScreenHeight);
    }

    /// <summary>
    /// Convert camera-relative coordinates into UV coordinates
    /// </summary>
    /// <param name="xy">Camera-relative position</param>
    /// <returns></returns>
    public static Vector2 RelativeToScreenUV(Vector2 xy) {
        return new Vector2(0.5f + xy.x / ScreenWidth, 0.5f + xy.y / ScreenHeight);
    }


    public static void SetPBScreenLoc(MaterialPropertyBlock pb, Vector2 loc) {
        Vector2 normLoc = RelativeToScreenUV(loc);
        pb.SetFloat(PropConsts.ScreenX, normLoc.x);
        pb.SetFloat(PropConsts.ScreenY, normLoc.y);
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
        UnityEngine.Graphics.Blit(RenderTo, null as RenderTexture);
    }
    
    private bool saveNext = false;
    [ContextMenu("Save next PostRender")]
    public void SaveNextPostRender() {
        saveNext = true;
    }

    public AyaPhoto RequestAyaPhoto(CRect rect, CameraType[]? cameras=null) {
        return new AyaPhoto(RequestPhotoTex(rect, cameras), rect);
    }

    /// <summary>
    /// Caller must dispose the return value via Object.Destroy.
    /// </summary>
    public Texture2D RequestPhotoTex(CRect rect, CameraType[]? cameras=null) {
        var rt = RenderTexture.active;
        var offset = transform.position;
        ayaMaterial.SetFloat(PropConsts.OffsetX, (rect.x - offset.x) / ScreenWidth);
        ayaMaterial.SetFloat(PropConsts.OffsetY, (rect.y - offset.y) / ScreenHeight);
        float xsr = rect.halfW * 2 / ScreenWidth;
        float ysr = rect.halfH * 2 / ScreenHeight;
        ayaMaterial.SetFloat(PropConsts.ScaleX, xsr);
        ayaMaterial.SetFloat(PropConsts.ScaleY, ysr);
        ayaMaterial.SetFloat(PropConsts.Angle, rect.angle * M.degRad);
        var _renderTo = RenderTo;
        RenderTo = RenderHelpers.DefaultTempRT();
        //Clear is required since the camera list may not contain BackgroundCamera,
        // which is the only one that clears
        RenderTo.GLClear();
        Shader.EnableKeyword("AYA_CAPTURE");
        foreach (var c in (cameras ?? AyaCameras).Select(FindCamera)) {
            c.targetTexture = RenderTo;
            c.Render();
            //Why do we have to set it back? I don't know, but if you don't do this,
            // you'll get flashing behavior when this is called from AyaCamera
            c.targetTexture = _renderTo;
        }
        Shader.DisableKeyword("AYA_CAPTURE");
        var ss = RenderHelpers.DefaultTempRT(((int) (SaveData.s.Resolution.w * xsr), (int) (SaveData.s.Resolution.h * ysr)));
        UnityEngine.Graphics.Blit(RenderTo, ss, ayaMaterial);
        RenderTo.Release();
        RenderTo = _renderTo;
        var tex = ss.IntoTex();
        ss.Release();
        //For debugging
        //FileUtils.WriteTex("DMK_Saves/Aya/temp.jpg", tex);
        
        //For some reason, I've had strange issues with things turning upside down if I return the RT
        // instead of converting it immediately to a tex. IDK but be warned
        RenderTexture.active = rt;
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
