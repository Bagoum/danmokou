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
public struct RenderablePane {
    public PanelSettings pane;
    /// <summary>
    /// All panes with the same group will be rendered to the same renderTex.
    /// <br/>Only group 0 will be rendered to screen by UIBuilderRenderer.
    /// <br/>Consumers can obtain render textures for groups by listening to <see cref="UIBuilderRenderer.RTGroups"/>.
    /// </summary>
    public int renderGroup;

    public void Deconstruct(out PanelSettings p, out int g) {
        p = pane;
        g = renderGroup;
    }
}
public class UIBuilderRenderer : CoroutineRegularUpdater {
    public const int ADV_INTERACTABLES_GROUP = 1;
    
    //Resolution at which UI resources are designed.
    public static readonly (int w, int h) UIResolution = (3840, 2160);
    public static readonly Vector2 UICenter = new(1920, 1080);
    private readonly DMCompactingArray<UIController> controllers = new(8);
    
    public RenderablePane[] settings = null!;
    private MaterialPropertyBlock pb = null!;
    public RenderTexture unsetRT = null!;

    private Cancellable allCancel = null!;
    private SpriteRenderer sr = null!;
    public StyleSheet scrollHeightFix = null!;

    private readonly Dictionary<int, RenderTexture> groupToRT = new();
    /// <summary>
    /// Render textures for each UITK rendering group.
    /// <br/>Note that render group 0 is automatically rendered to screen by this class.
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
        (sr = GetComponent<SpriteRenderer>()).GetPropertyBlock(pb = new MaterialPropertyBlock());
        allCancel = new Cancellable();
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
        Listen(RenderHelpers.PreferredResolution, RemakeTexture);
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
        pb.SetTexture(PropConsts.renderTex, groupToRT[0]);
        sr.SetPropertyBlock(pb);
        RTGroups.OnNext(groupToRT);
    }

    /*public void AddToPane(VisualElement ve, int renderGroup) {
        GroupToLeaf[renderGroup].Add(ve);
    }*/

    public static Vector2 ComputeXMLDimensions(Vector2 screenDim) =>
        new(screenDim.x / MainCamera.ScreenWidth * UIResolution.w,
            screenDim.y / MainCamera.ScreenHeight * UIResolution.h);
    
    public static Vector2 ComputeXMLPosition(Vector2 screenPosition) {
        var asDim = ComputeXMLDimensions(screenPosition + LocationHelpers.PlayableBounds.center);
        return new Vector2(UICenter.x + asDim.x, UICenter.y - asDim.y);
    }
    

    protected override void OnDisable() {
        foreach (var (s, _) in settings)
            s.targetTexture = unsetRT;
        pb.SetTexture(PropConsts.renderTex, unsetRT);
        sr.SetPropertyBlock(pb);
        foreach (var v in groupToRT.Values)
            v.Release();
        groupToRT.Clear();
        allCancel.Cancel();
        base.OnDisable();
    }

    public override EngineState UpdateDuring => EngineState.LOADING_PAUSE;
    public override int UpdatePriority => UpdatePriorities.SLOW;
}
}
