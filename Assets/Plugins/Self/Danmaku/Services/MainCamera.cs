using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Danmaku;
using UnityEngine;

public class MainCamera : MonoBehaviour {
    private static readonly int ShaderScrnWidthID = Shader.PropertyToID("_ScreenWidth");
    private static readonly int ShaderScrnHeightID = Shader.PropertyToID("_ScreenHeight");
    private static readonly int ShaderPixelWidthID = Shader.PropertyToID("_PixelWidth");
    private static readonly int ShaderPixelHeightID = Shader.PropertyToID("_PixelHeight");
    private static readonly int PixelsPerUnitID = Shader.PropertyToID("_PPU");
    private static readonly int ResourcePixelsPerUnitID = Shader.PropertyToID("_RPPU");
    private static readonly int RenderRatioID = Shader.PropertyToID("_RenderR");
    public static MainCamera main;

    private static Camera cam;
    public static float VertRadius { get; private set; }
    public static float HorizRadius { get; private set; }
    public static float ScreenWidth => HorizRadius * 2;
    public static float ScreenHeight => VertRadius * 2;
    private Vector2 position; // Cached to allow requests for screen coordinates from MovementLASM off main thread
    private Transform tr;

    /*
    public Material postprocessor;
    [Range(1, 16)] public int bloomIterations = 2;
    public float bloomThreshold = 1;
*/
    
    private void Awake() {
        MainCamera.main = this;
        cam = GetComponent<Camera>();
        VertRadius = cam.orthographicSize;
        HorizRadius = VertRadius * cam.pixelWidth / cam.pixelHeight;
        tr = transform;
        position = tr.position;
        ReassignGlobalShaderVariables();
    }


    public void ReassignGlobalShaderVariables() {
        //Log.Unity($"Camera width: {cam.pixelWidth} Screen width: {Screen.width}");
        Shader.SetGlobalFloat(ShaderScrnHeightID, 2 * VertRadius);
        Shader.SetGlobalFloat(ShaderScrnWidthID, 2 * HorizRadius);
        Shader.SetGlobalFloat(PixelsPerUnitID, Screen.height / ScreenHeight);
        Shader.SetGlobalFloat(ResourcePixelsPerUnitID, Consts.BestResolution.h / ScreenHeight);
        Shader.SetGlobalFloat(RenderRatioID, Screen.height / (float)Consts.BestResolution.h);
        Shader.SetGlobalFloat(ShaderPixelHeightID, Screen.height);
        Shader.SetGlobalFloat(ShaderPixelWidthID, Screen.width);
        if (SaveData.s.Shaders) Shader.EnableKeyword("FANCY");
        else Shader.DisableKeyword("FANCY");
        
    }

    public static void LoadInEditor() {
        var x = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<MainCamera>();
        x.Awake();
    }

    [ContextMenu("Debug sizes")]
    public void DebugSizes() {
        cam = GetComponent<Camera>();
        Debug.Log($"Vertical {cam.orthographicSize} pixels {cam.pixelWidth} {cam.pixelHeight}");
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


    public void OnDestroy() {
        main = null;
    }

    private readonly List<RenderTexture> samples = new List<RenderTexture>();
    private const int PrefilterDownsample = 0;
    private const int Downsample = 1;
    private const int Upsample = 2;
    private const int UpsampleFinalize = 3;
    private const int UpsampleDebug = 4;
    private const int RemapOnly = 5;
    /*
    private void OnPostRender() {
        postprocessor.SetFloat("_BloomThreshold", bloomThreshold);
        var rt = renderTarget;
        int w = rt.width / 2;
        int h = rt.height / 2;
        samples.Clear();
        var dst = RenderTexture.GetTemporary(w, h, 0, rt.format);
        samples.Add(dst);
        Graphics.Blit(rt, dst, postprocessor, PrefilterDownsample);
        var src = dst;
        int ii = 1;
        for (; ii < bloomIterations; ++ii) {
            w /= 2;
            h /= 2;
            if (h < 4 || w < 4) break;
            dst = RenderTexture.GetTemporary(w, h, 0, rt.format);
            samples.Add(dst);
            Graphics.Blit(src, dst, postprocessor, Downsample);
            src = dst;
        }
        for (ii -= 2; ii >= 0; --ii) {
            dst = samples[ii];
            Graphics.Blit(src, dst, postprocessor, Upsample);
            RenderTexture.ReleaseTemporary(src);
            src = dst;
        }
        if (debugBloom) {
            Graphics.Blit(src, null as RenderTexture, postprocessor, UpsampleDebug);
        } else {
            Graphics.Blit(src, null as RenderTexture, postprocessor, UpsampleFinalize);
        }
        RenderTexture.ReleaseTemporary(src);
    }*/

    public bool debugBloom;

    public static RenderTexture DefaultTempRT() => RenderTexture.GetTemporary(SaveData.s.Resolution.Item1, 
        SaveData.s.Resolution.Item2, 0, RenderTextureFormat.ARGB32);

    public static ArbitraryCapturer CreateArbitraryCapturer(Transform tr) => 
        GameObject.Instantiate(GameManagement.ArbitraryCapturer, tr, false)
                    .GetComponent<ArbitraryCapturer>();
}
