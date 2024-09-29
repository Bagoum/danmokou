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
    public bool RendersToUICamera { get; }
}

[Serializable]
public class RenderablePane {
    public PanelSettings pane = null!;
    /// <summary>
    /// All panes with the same group will be rendered to the same renderTex.
    /// </summary>
    public int renderGroup;
    /// <summary>
    /// If provided, UIBuilderRenderer will render the contents of the renderTex to the sprite renderer.
    /// <br/>If multiple panes share the same group, only one needs to include this.
    /// <br/>Alternatively, consumers can manually render panes by listening to <see cref="UIBuilderRenderer.RTGroups"/>.
    /// (ADV UI rendering uses manual pane rendering in order to render the UI as "part of the world" rather than on the UI layer.)
    /// </summary>
    public SpriteRenderer? renderer;
    public RenderTexture? Texture { get; private set; }
    public OverrideEvented<IOverrideRenderTarget?> Target { get; } = new(null);
    [NonSerialized] public MaterialPropertyBlock? pb;

    public void UpdatedTarget(IOverrideRenderTarget? t) {
        if (renderer != null) {
            renderer.enabled = t is null;
        }
    }

    public RenderTexture RemakeTexture((int w, int h) res) {
        pane.scale = res.w / (float)UIBuilderRenderer.UIResolution.w;
        var newTex = RenderHelpers.DefaultTempRT(res);
        if (Texture != null) {
            //Prevents a blank frame when the texture is changed 
            UnityEngine.Graphics.Blit(Texture, newTex);
            Texture.Release();
        }
        pane.targetTexture = Texture = newTex;
        if (renderer != null) {
            pb!.SetTexture(PropConsts.renderTex, Texture);
            renderer.SetPropertyBlock(pb);
        }
        
        pane.SetScreenToPanelSpaceFunction(loc => {
            /* See LetterboxedInput.cs.
            A world space canvas relates mouse input only to the target texture to which the canvas is rendering. For example, if my canvas renders to a camera with a target texture of resolution 1920x1080, then the canvas will perceive a mouse input at position <1900, 1060> as occuring in the top right of the canvas, regardless of where on the screen this input is located.

            UIToolkit panels have an issue in that the way they relate input depends both on the target texture and the screen, because the transformation from mouse location (starting at the bottom left) to UXML location (starting at the top left) is done with something like `uxml_y = Screen.height - mouse_y`.
            If my panel renders to a texture of resolution 1920x1080 and the screen has resolution 1920x1080, then the panel will perceive a mouse input at position <1900, 1060> as occuring at the top right of the panel (specifically, at UXML position <1900, 20>).  However, if my screen instead has resolution 3840x2160, then the panel will perceive a mouse input at position <1900, 1060> as ocurring at the *center right* of the panel, at UXML position (1900, 1080).
             */
            var panelLoc = loc + new Vector2(0, res.h - Screen.height);
            
            //If you have BoxColliders and mesh/quads attached to actual world space UI,
            // you can use an approach like this to remap input.
            /*var originalLoc = new Vector2(loc.x * Screen.width / res.w, (Screen.height - loc.y) * Screen.height / res.h);
            var cameraRay = MainCamera.MCamInfo.Camera.ScreenPointToRay(originalLoc);
            Debug.DrawRay(cameraRay.origin, cameraRay.direction * 100, Color.magenta);
            if (Physics.Raycast(cameraRay, out var hit, 100f, LayerMask.GetMask("UI"), QueryTriggerInteraction.Collide)) {
                var tc = hit.textureCoord;
                return new(tc.x * res.w, (1 - tc.y) * res.h);
            }*/
            
            //If this renderer is being sent through a UITKRerenderer, adjust for the zoom applied
            if (Target.Value is {} t) {
                var zoom = t.Zoom;
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (zoom != 1f) {
                    var ztarget = t.ZoomTarget;
                    var center = new Vector2(
                        //note that these panel coordinates are based on render texture dims.
                        //as such, we can't use UIBuilderRenderer.ToXMLDims, which is based on UXML coordinates
                        // that are fixed to 3840x2160.
                        res.w * (0.5f + ztarget.x / UIBuilderRenderer.UICamInfo.ScreenWidth),
                        res.h * (0.5f - ztarget.y / UIBuilderRenderer.UICamInfo.ScreenHeight));
                    return center + (panelLoc - center) / zoom;
                }
            }
            return panelLoc;
        });
        
        return Texture;
    }

    public void Unset(RenderTexture unsetter) {
        if (renderer != null) {
            pb!.SetTexture(PropConsts.renderTex, unsetter);
            renderer.SetPropertyBlock(pb);
        }
        pane.targetTexture = unsetter;
        if (Texture != null)
            Texture.Release();
        Texture = null;
    }
}

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
        foreach (var s in settings) {
            if (s.renderer != null) {
                s.renderer!.GetPropertyBlock(s.pb = new MaterialPropertyBlock());
            }
            AddToken(s.Target.Subscribe(s.UpdatedTarget));
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
        Listen(RenderHelpers.PreferredResolution, RemakeTexture);
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
