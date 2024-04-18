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
    [NonSerialized] public MaterialPropertyBlock? pb;
    [NonSerialized] public DisturbedAnd renderingAllowed = new();

    public void Deconstruct(out PanelSettings p, out int g) {
        p = pane;
        g = renderGroup;
    }
}
public class UIBuilderRenderer : RegularUpdater {
    public const int ADV_INTERACTABLES_GROUP = 1;
    //Resolution at which UI resources are designed.
    public static readonly (int w, int h) UIResolution = (3840, 2160);
    public static readonly Vector2 UICenter = new(1920, 1080);
    private static Vector2 globalTransform;
    private readonly DMCompactingArray<UIController> controllers = new(8);
    public RenderablePane[] settings = null!;
    public RenderTexture unsetRT = null!;

    private readonly Dictionary<int, RenderTexture> groupToRT = new();
    /// <summary>
    /// Render textures for each UITK rendering group.
    /// <br/>Consumers may manually render these textures if they are not configured to render by default in
    ///  <see cref="settings"/>, or if their rendering has been disabled via <see cref="DisableDefaultRenderingForGroup"/>.
    /// </summary>
    public Evented<Dictionary<int, RenderTexture>> RTGroups { get; } = new(null!);

    public IDisposable RegisterController(UIController c, int priority) {
        return controllers.AddPriority(c, priority);
    }
    
    public bool IsHighestPriorityActiveMenu(UIController c) {
        for (int ii = 0; ii < controllers.Count; ++ii)
            if (controllers.ExistsAt(ii) && controllers[ii].MenuActive)
                return (controllers[ii] == c);
        return false;
    }

    public void ApplyScrollHeightFix(VisualElement ve) {
        //while (ve != null) {
        //    ve.styleSheets.Add(scrollHeightFix);
         //   ve = ve.parent;
        //}
    }

    private void Awake() {
        foreach (var s in settings)
            if (s.renderer != null) {
                s.renderer!.GetPropertyBlock(s.pb = new MaterialPropertyBlock());
                AddToken(s.renderingAllowed.Subscribe(b => s.renderer.enabled = b));
            }
        globalTransform = transform.position;
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
        Listen(RenderHelpers.PreferredResolution, RemakeTexture);
    }

    public override void RegularUpdate() {
        globalTransform = transform.position;
    }

    private void RemakeTexture((int w, int h) res) {
        var oldRTs = new Dictionary<int, RenderTexture>(groupToRT);
        
        /* See LetterboxedInput.cs.
        A world space canvas relates mouse input only to the target texture to which the canvas is rendering. For example, if my canvas renders to a camera with a target texture of resolution 1920x1080, then the canvas will perceive a mouse input at position <1900, 1060> as occuring in the top right of the canvas, regardless of where on the screen this input is located.

        UIToolkit panels have an issue in that the way they relate input depends both on the target texture and the screen, because the transformation from mouse location (starting at the bottom left) to UXML location (starting at the top left) is done with something like `uxml_y = Screen.height - mouse_y`. 
        If my panel renders to a texture of resolution 1920x1080 and the screen has resolution 1920x1080, then the panel will perceive a mouse input at position <1900, 1060> as occuring at the top right of the panel (specifically, at UXML position <1900, 20>).  However, if my screen instead has resolution 3840x2160, then the panel will perceive a mouse input at position <1900, 1060> as ocurring at the *center right* of the panel, at UXML position (1900, 1080). 
        
        By overriding the ScreenToPanelSpaceFunction, we can work around this problem.
         */
        var screenToPanelWorkaround = (Func<Vector2, Vector2>)(loc => loc + new Vector2(0, res.h - Screen.height));

        foreach (var (s, g) in settings) {
            s.scale = res.w / (float)UIResolution.w;
            var rt = s.targetTexture = groupToRT[g] = RenderHelpers.DefaultTempRT(res);
            //Prevents a blank frame when the texture is changed 
            if (oldRTs.TryGetValue(g, out var oldRT))
                UnityEngine.Graphics.Blit(oldRT, rt);
            s.SetScreenToPanelSpaceFunction(screenToPanelWorkaround);
        }
        foreach (var v in oldRTs.Values)
            v.Release();
        for (int ii = 0; ii < settings.Length; ++ii)
            if (settings[ii].renderer != null) {
                settings[ii].pb!.SetTexture(PropConsts.renderTex, groupToRT[settings[ii].renderGroup]);
                settings[ii].renderer!.SetPropertyBlock(settings[ii].pb);
            }
        RTGroups.OnNext(groupToRT);
    }

    /*public void AddToPane(VisualElement ve, int renderGroup) {
        GroupToLeaf[renderGroup].Add(ve);
    }*/

    /// <summary>
    /// Disable the default rendering (handled by <see cref="UIBuilderRenderer"/>) for UI panes.
    /// Instead, the UI pane may be manually rendered by listening to <see cref="RTGroups"/> and assigning the texture
    ///  to a sprite.
    /// </summary>
    /// <param name="renderGroup">The render group number as configured in <see cref="RenderablePane"/>.</param>
    public IDisposable DisableDefaultRenderingForGroup(int renderGroup) {
        for (int ii = 0; ii < settings.Length; ++ii)
            if (settings[ii].renderGroup == renderGroup)
                return settings[ii].renderingAllowed.AddConst(false);
        throw new Exception($"No render group by id {renderGroup}");
    }

    public static float ToXMLDimX(float screenX) =>
        screenX / MainCamera.ScreenWidth * UIResolution.w;
    public static float ToXMLDimY(float screenY) =>
        screenY / MainCamera.ScreenHeight * UIResolution.h;
    public static Vector2 ToXMLDims(Vector2 screenDim) =>
        new(ToXMLDimX(screenDim.x), ToXMLDimY(screenDim.y));
    
    public static Vector2 ToXMLOffset(Vector2 screenDim) =>
        new(ToXMLDimX(screenDim.x), -ToXMLDimY(screenDim.y));

    public static Vector2 ToXMLPos(Vector2 screenPosition) =>
        UICenter + ToXMLOffset(screenPosition - globalTransform);
    

    protected override void OnDisable() {
        foreach (var p in settings) {
            if (p.renderer != null) {
                p.pb!.SetTexture(PropConsts.renderTex, unsetRT);
                p.renderer.SetPropertyBlock(p.pb);
            }
            p.pane.targetTexture = unsetRT;
        }
        foreach (var v in groupToRT.Values)
            v.Release();
        groupToRT.Clear();
        base.OnDisable();
    }

    public override EngineState UpdateDuring => EngineState.LOADING_PAUSE;
    public override int UpdatePriority => UpdatePriorities.SLOW;
}
}
