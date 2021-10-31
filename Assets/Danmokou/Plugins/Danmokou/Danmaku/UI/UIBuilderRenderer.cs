using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using BagoumLib.Tweening;
using Danmokou.Behavior;
using Danmokou.Core;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.UIElements;
using PropConsts = Danmokou.Graphics.PropConsts;

namespace Danmokou.UI {
public class UIBuilderRenderer : CoroutineRegularUpdater {
    public PanelSettings settings = null!;
    public Material uiMaterial = null!;

    public RenderTexture unsetRT = null!;

    private RenderTexture rt = null!;
    private Cancellable allCancel = null!;
    private Transform tr = null!;
    private SpriteRenderer sr = null!;

    private const int frontOrder = 32000;
    private int normalOrder;

    public enum RAction {
        SLIDE,
        FADE
    }

    //each action type can be run independently
    private readonly Dictionary<RAction, Cancellable> cancellers =
        new Dictionary<RAction, Cancellable>();

    private ICancellee CreateNewCanceller(RAction a) {
        if (cancellers.TryGetValue(a, out var existing)) existing.Cancel();
        var c = new Cancellable();
        cancellers[a] = c;
        return new JointCancellee(allCancel, c);
    }

    public Task<Completion> Slide(Vector2? start, Vector2 end, float time, Easer? smooth)
        => Tween.TweenTo(start ?? tr.localPosition, end, time, x => tr.localPosition = x, smooth, 
            CreateNewCanceller(RAction.SLIDE)).Run(this);
    
    public Task<Completion> Fade(float? start, float end, float time, Easer? smooth)
        => Tween.TweenTo(start ?? sr.color.a, end, time, sr.SetAlpha, smooth, 
            CreateNewCanceller(RAction.FADE)).Run(this);

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

    public void MoveToNormal() {
        sr.sortingOrder = normalOrder;
    }

    public void MoveToFront() {
        sr.sortingOrder = frontOrder;
    }

    private void RemakeTexture((int w, int h) res) {
        if (rt != null) rt.Release();
        rt = RenderHelpers.DefaultTempRT(res);
        settings.targetTexture = rt;
        uiMaterial.SetTexture(PropConsts.renderTex, rt);
    }

    protected override void OnDisable() {
        settings.targetTexture = unsetRT;
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
