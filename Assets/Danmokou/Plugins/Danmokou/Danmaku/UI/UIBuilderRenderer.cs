using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.Tweening;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.UI.XML;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.UIElements;
using PropConsts = Danmokou.Graphics.PropConsts;

namespace Danmokou.UI {
public class UIBuilderRenderer : CoroutineRegularUpdater {
    //Resolution at which UI resources are designed.
    public static readonly (int w, int h) UIResolution = (3840, 2160);
    private readonly DMCompactingArray<UIController> controllers = new(8);
    
    public PanelSettings[] settings = null!;
    public Material uiMaterial = null!;
    public RenderTexture unsetRT = null!;

    private RenderTexture rt = null!;
    private Cancellable allCancel = null!;
    private Transform tr = null!;
    private SpriteRenderer sr = null!;

    private const int frontOrder = 32000;
    private int normalOrder;

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
        tr = transform;
        sr = GetComponent<SpriteRenderer>();
        normalOrder = sr.sortingOrder;
        allCancel = new Cancellable();
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(RenderHelpers.PreferredResolution, RemakeTexture);
    }

    private void RemakeTexture((int w, int h) res) {
        if (rt != null) rt.Release();
        rt = RenderHelpers.DefaultTempRT(res);
        /* See LetterboxedInput.cs.
        A world space canvas relates mouse input only to the target texture to which the canvas is rendering. For example, if my canvas renders to a camera with a target texture of resolution 1920x1080, then the canvas will perceive a mouse input at position <1900, 1060> as occuring in the top right of the canvas, regardless of where on the screen this input is located.

        UIToolkit panels have an issue in that the way they relate input depends both on the target texture and the screen, because the transformation from mouse location (starting at the bottom left) to UXML location (starting at the top left) is done with something like `uxml_y = Screen.height - mouse_y`. 
        If my panel renders to a texture of resolution 1920x1080 and the screen has resolution 1920x1080, then the panel will perceive a mouse input at position <1900, 1060> as occuring at the top right of the panel (specifically, at UXML position <1900, 20>).  However, if my screen instead has resolution 3840x2160, then the panel will perceive a mouse input at position <1900, 1060> as ocurring at the *center right* of the panel, at UXML position (1900, 1080). 
        
        By overriding the ScreenToPanelSpaceFunction, we can work around this problem.
         */
        var screenToPanelWorkaround = (Func<Vector2, Vector2>)(loc => loc + new Vector2(0, res.h - Screen.height));
        foreach (var s in settings) {
            s.scale = res.w / (float)UIResolution.w;
            s.targetTexture = rt;
            s.SetScreenToPanelSpaceFunction(screenToPanelWorkaround);
        }
        uiMaterial.SetTexture(PropConsts.renderTex, rt);
    }

    protected override void OnDisable() {
        foreach (var s in settings)
            s.targetTexture = unsetRT;
        uiMaterial.SetTexture(PropConsts.renderTex, unsetRT);
        if (rt != null) rt.Release();
        rt = null!;
        allCancel.Cancel();
        base.OnDisable();
    }

    public override EngineState UpdateDuring => EngineState.LOADING_PAUSE;
    public override int UpdatePriority => UpdatePriorities.SLOW;
}
}
