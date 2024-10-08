using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.Transitions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using Danmokou.UI.XML;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.UIElements;
using PropConsts = Danmokou.Graphics.PropConsts;

namespace Danmokou.UI {

public interface IOverrideRenderTarget {
    public float Zoom { get; }
    public Vector2 ZoomTarget { get; }
    public Vector2 Offset { get; }
    public (CameraInfo cam, int layerMask)? AsWorldUI => null;
    public Vector2? TextureSizeMult => null;
}

[Serializable]
public class RenderablePane {
    public PanelSettings pane = null!;
    /// <summary>
    /// All panes with the same group will be rendered to the same renderTex.
    /// </summary>
    public int renderGroup;
    //All panes render to MainCamera.RenderTo by default,
    // unless Target is provided, in which case a TempTexture will be created that can be read by Target.
    public RenderTexture? TempTexture { get; private set; }
    public OverrideEvented<IOverrideRenderTarget?> Target { get; } = new(null);

    public void UpdatedTarget(IOverrideRenderTarget? t) {
        RemakeTexture(RenderHelpers.PreferredResolution);
    }

    public void RemakeTexture((int w, int h) res) {
        pane.scale = res.w / (float)UIBuilderRenderer.UIResolution.w;
        (int w, int h) unscaledRes = res;
        if (Target.Value?.TextureSizeMult is { } mult)
            res = (Mathf.RoundToInt(res.w * mult.x), Mathf.RoundToInt(res.h * mult.y));
        var (nextTex, isTemp) = Target.Value is {} tgt ?
            (RenderHelpers.DefaultTempRT(res), true) :
            (MainCamera.RenderTo, false);
        Logs.Log($"Remaking UI textures for pane {pane.name} in {res}. " + 
                 (isTemp ? "Writes to a temp texture." : "Writes to MainCamera.RenderTo."));
        if (TempTexture != null) {
            if (isTemp)
                //Prevents a blank frame when the texture is changed 
                UnityEngine.Graphics.Blit(TempTexture, nextTex);
            TempTexture.Release();
        }
        pane.targetTexture = nextTex;
        pane.clearColor = isTemp;
        pane.clearDepthStencil = isTemp;
        TempTexture = isTemp ? nextTex : null;
        
        pane.SetScreenToPanelSpaceFunction(loc => {
            /* See LetterboxedInput.cs.
            A world space canvas relates mouse input only to the target texture to which the canvas is rendering. For example, if my canvas renders to a camera with a target texture of resolution 1920x1080, then the canvas will perceive a mouse input at position <1900, 1060> as occuring in the top right of the canvas, regardless of where on the screen this input is located.

            UIToolkit panels have an issue in that the way they relate input depends both on the target texture and the screen, because the transformation from mouse location (starting at the bottom left) to UXML location (starting at the top left) is done with something like `uxml_y = Screen.height - mouse_y`.
            If my panel renders to a texture of resolution 1920x1080 and the screen has resolution 1920x1080, then the panel will perceive a mouse input at position <1900, 1060> as occuring at the top right of the panel (specifically, at UXML position <1900, 20>).  However, if my screen instead has resolution 3840x2160, then the panel will perceive a mouse input at position <1900, 1060> as ocurring at the *center right* of the panel, at UXML position (1900, 1080).
             */
            var panelLoc = loc + new Vector2(0, unscaledRes.h - Screen.height);
            if (panelLoc.x < 0 || panelLoc.x > unscaledRes.w || panelLoc.y < 0 || panelLoc.y > unscaledRes.h)
                return new(float.NaN, float.NaN);
            
            if (Target.Value is {} t) {
                if (t.AsWorldUI is { } coll) {
                    //For actual world UI with Mesh/MeshCollider
                    var screenLoc = new Vector2(panelLoc.x / unscaledRes.w, 1 - panelLoc.y / unscaledRes.h);
                    var ray = coll.cam.Camera.ViewportPointToRay(screenLoc);
                    Debug.DrawRay(ray.origin, ray.direction * 100, Color.magenta);
                    if (Physics.Raycast(ray, out var hit, 100f, coll.layerMask, QueryTriggerInteraction.Collide)) {
                        var tc = hit.textureCoord;
                        Logs.Log($"Tex coord collision at {tc} ({new Vector2(tc.x * res.w, (1 - tc.y) * res.h)})");
                        return new(tc.x * res.w, (1 - tc.y) * res.h);
                    } else
                        return new(float.NaN, float.NaN);
                }
                
                //If this renderer is being sent through a UITKRerenderer, adjust for the zoom/transform applied
                var zoom = t.Zoom;
                var offset = t.Offset;
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (zoom != 1f || offset != Vector2.zero) {
                    var ztarget = t.ZoomTarget;
                    var center = new Vector2(
                        //note that these panel coordinates are based on render texture dims.
                        //as such, we can't use UIBuilderRenderer.ToXMLDims, which is based on UXML coordinates
                        // that are fixed to 3840x2160.
                        res.w * (0.5f + ztarget.x / UIBuilderRenderer.UICamInfo.ScreenWidth),
                        res.h * (0.5f - ztarget.y / UIBuilderRenderer.UICamInfo.ScreenHeight));
                    return center + (panelLoc - center) / zoom + 
                           new Vector2(offset.x / UIBuilderRenderer.UICamInfo.ScreenWidth * res.w / zoom,
                                      -offset.y / UIBuilderRenderer.UICamInfo.ScreenHeight * res.h / zoom);
                }
            }
            return panelLoc;
        });
    }

    public void Unset(RenderTexture unsetter) {
        pane.targetTexture = unsetter;
        if (TempTexture != null)
            TempTexture.Release();
        TempTexture = null;
    }
}

//Note: has a script ordering delay since pane target tex assignment depends on MainCamera events firing first.
public class UIBuilderRenderer : RegularUpdater {
    public const int ADV_INTERACTABLES_GROUP = 1;
    //Resolution at which UI resources are designed.
    public static readonly (int w, int h) UIResolution = (3840, 2160);
    public static readonly Vector2 UICenter = new(1920, 1080);
    public static CameraInfo UICamInfo { get; private set; } = null!;
    private readonly DMCompactingArray<UIController> controllers = new(8);
    public Camera uiCamera = null!;
    public RenderablePane[] settings = null!;
    public RenderTexture unsetRT = null!;

    private readonly Dictionary<int, RenderablePane> groupToRT = new();
    /// <summary>
    /// Render textures for each UITK rendering group.
    /// <br/>Consumers may manually render these textures if they are not configured to render by default in
    ///  <see cref="settings"/>, or if their rendering has been disabled via <see cref="IOverrideRenderTarget"/>.
    /// </summary>
    public Evented<Dictionary<int, RenderablePane>> RTGroups { get; } = new(null!);

    public IDisposable RegisterController(UIController c, int priority) {
        return controllers.AddPriority(c, priority);
    }
    
    public bool IsHighestPriorityActiveMenu(UIController c) {
        for (int ii = 0; ii < controllers.Count; ++ii)
            if (controllers.ExistsAt(ii) && controllers[ii].MenuActive)
                return (controllers[ii] == c);
        return false;
    }

    private void Awake() {
        UICamInfo = new(uiCamera, transform);
        foreach (var s in settings)
            AddToken(s.Target.OnChange.Subscribe(s.UpdatedTarget));
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
        //Since some UI renderers may write to MainCamera.RenderTo, we need to make sure
        // that MainCamera is initialized before we initialize the UI renderers.
        //Note that MainCamera.EvRenderTo is updated by resolution update, so we don't
        // need to listen to resolution here.
        Listen(MainCamera.EvRenderTo, _ => RemakeTexture(RenderHelpers.PreferredResolution));
    }

    public override void RegularUpdate() {
        UICamInfo.Recheck();
    }

    private void RemakeTexture((int w, int h) res) {
        groupToRT.Clear();
        foreach (var pane in settings) {
            pane.RemakeTexture(res);
            groupToRT[pane.renderGroup] = pane;
        }
        RTGroups.OnNext(groupToRT);
    }

    /// <summary>
    /// Convert world dimensions relative to the UI camera into screen dimensions in XML coordinates.
    /// </summary>
    public static Vector2 ToUIXMLDims(Vector2 worldDim) =>
        new(worldDim.x/UICamInfo.ScreenWidth * UIResolution.w, 
            worldDim.y/UICamInfo.ScreenHeight * UIResolution.h);
    
    /// <summary>
    /// Convert a screen point ((0,0) bottom left, (1,1) top right)
    /// to an XML position ((0,0) top left, (3840,2160) bottom right).
    /// </summary>
    public static Vector2 ScreenpointToXML(Vector2 screenPoint) => 
        new(UIBuilderRenderer.UIResolution.w * screenPoint.x,
            UIBuilderRenderer.UIResolution.h * (1 - screenPoint.y));

    /// <summary>
    /// Convert an XML position ((0,0) top left, (3840,2160) bottom right)
    /// to a screen point ((0,0) bottom left, (1,1) top right).
    /// </summary>
    public static Vector2 XMLToScreenpoint(Vector2 xmlPos) =>
        new(xmlPos.x / UIBuilderRenderer.UIResolution.w, 
            1 - xmlPos.y / UIBuilderRenderer.UIResolution.h);
    

    protected override void OnDisable() {
        foreach (var p in settings) {
            p.Unset(unsetRT);
        }
        groupToRT.Clear();
        base.OnDisable();
    }

    public override EngineState UpdateDuring => EngineState.LOADING_PAUSE;
    public override int UpdatePriority => UpdatePriorities.SLOW;
}
}
