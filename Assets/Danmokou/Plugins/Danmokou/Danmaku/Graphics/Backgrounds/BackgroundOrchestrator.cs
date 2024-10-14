using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Functional;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using JetBrains.Annotations;
using Danmokou.SM;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using static BagoumLib.Tasks.WaitingUtils;

namespace Danmokou.Graphics.Backgrounds {
public interface IBackgroundOrchestrator {
    void QueueTransition(BackgroundTransition bgt);
    void ConstructTarget(GameObject? bgp, bool withTransition = true, bool destroyIfExists = false);
    IDisposable AddDistorter(Transform tr, CameraInfo tracker);
}
public class BackgroundOrchestrator : CoroutineRegularUpdater, IBackgroundOrchestrator {
    private Transform tr = null!;
    //private BackgroundCombiner BackgroundCombiner { get; set; } = null!;
    public BackgroundController? FromBG { get; private set; }
    public BackgroundController? ToBG { get; private set; }
    
    private readonly Dictionary<GameObject, BackgroundController> instantiated = new();
    private BackgroundTransition? nextRequestedTransition;

    public GameObject defaultBGCPrefab = null!;

    public Material orchestratorBaseMaterial = null!;
    private Material mat = null!;
    private CameraRenderer cmr = null!;
    private float mixTime = 0f;
    public float Time { get; private set; }
    
    private GameObject? lastRequestedBGC;
    //We don't update backgrounds immediately, since there are use-cases where we push two backgrounds
    // one after another and want the first one to be ignored.
    private Action? pendingBackgroundUpdate;
    private readonly OverrideEvented<(Transform tr, CameraInfo cam)?> distorter = new(null);
    
    private void Awake() {
        cmr = GetComponent<CameraRenderer>();
        mat = new Material(orchestratorBaseMaterial);
        tr = transform;
        Time = 0f;
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IBackgroundOrchestrator>(this, new ServiceLocator.ServiceOptions { Unique = true });
        Listen(SaveData.s.Backgrounds, _ => ShowHide());
        Listen(RenderHelpers.PreferredResolution, _ => UpdateTextures());
        Listen(IGraphicsSettings.SettingsEv, _ => UpdateDistortion());
    }
    
    public override void FirstFrame() {
        if (FromBG == null) {
            var bgc = (lastRequestedBGC == null) ? defaultBGCPrefab : lastRequestedBGC;
            lastRequestedBGC = null;
            pendingBackgroundUpdate = null;
            FromBG = CreateBGC(bgc);
            ShowHide();
        }
        ResetMaterial();
        UpdateTextures();
    }

    public override void RegularUpdate() {
        if (ETime.LastUpdateForScreen) UpdateTextures();
        base.RegularUpdate();
        if (EngineStateManager.State <= EngineState.RUN) { 
            mixTime += ETime.FRAME_TIME;
            Time += ETime.FRAME_TIME;
            pendingBackgroundUpdate?.Invoke();
            pendingBackgroundUpdate = null;
        }
    }
    public override EngineState UpdateDuring => EngineState.MENU_PAUSE;
    
    private void ShowHide() {
        if (FromBG != null) {
            FromBG.ShowHideBySettings(SaveData.s.Backgrounds);
        }
        if (ToBG != null) {
            ToBG.ShowHideBySettings(SaveData.s.Backgrounds);
        }
        CheckForNonRenderingBG();
    }

    private void UpdateTextures() {
        if (FromBG == null || !FromBG.IsDrawing) 
            return;
        var sco = !GameManagement.Instance.InstanceActiveGuardInScene ? Vector2.zero :
            cmr.CamInfo.ToScreenPoint(LocationHelpers.PlayableBounds.center + LocationHelpers.PlayableScreenCenter)
            - cmr.CamInfo.ToScreenPoint(LocationHelpers.PlayableScreenCenter);
        mat.SetVector(screenCenterOffset, new Vector4(sco.x, sco.y, 0, 0));
        mat.SetTexture(PropConsts.fromTex, FromBG.capturer.Captured);
        if (ToBG != null) 
            mat.SetTexture(PropConsts.toTex, ToBG.capturer.Captured);
        mat.SetFloat(PropConsts.time, mixTime);
        mat.SetFloat(distortTime, Time);
        if (distorter.Value is { } val) {
            var dc = val.cam.ToScreenPoint(val.tr.position);
            mat.SetVector(distortCenter, new Vector4(dc.x, dc.y, 0, 0));
        }
    }
    
    private void UpdateDistortion() {
        var hasDistorter = distorter.Value.HasValue;
        var isFancy = SaveData.s.Shaders;
        mat.SetOrUnsetKeyword(hasDistorter && !isFancy, "SHADOW_ONLY");
        mat.SetOrUnsetKeyword(hasDistorter && isFancy, "SHADOW_AND_DISTORT");
    }

    private void ResetMaterial() {
        CombinerKeywords.Apply(mat, CombinerKeywords.FROM_ONLY);
        CheckForNonRenderingBG();
        UpdateDistortion();
        mixTime = 0;
    }

    private void CheckForNonRenderingBG() {
        mat.SetOrUnsetKeyword(FromBG == null || !FromBG.IsDrawing, "NO_BG_RENDER");
    }

    private BackgroundController CreateBGC(GameObject prefab) {
        if (instantiated.TryGetValue(prefab, out var bgc)) {
            bgc.Show();
            return bgc;
        } else {
            return instantiated[prefab] = Instantiate(prefab, tr, false)
                .GetComponent<BackgroundController>()
                .Initialize(prefab, this);
        }
    }

    public void QueueTransition(BackgroundTransition bgt) => nextRequestedTransition = bgt;

    private void FinishTransition() {
        if (ToBG != null && FromBG != null) {
            FromBG.Hide();
            FromBG = ToBG;
            ToBG = null;
        }
        ResetMaterial();
        UpdateTextures();
    }
    public void ConstructTarget(GameObject? bgp, bool withTransition=true, bool destroyIfExists=false) {
        if (bgp is null) return;
        lastRequestedBGC = bgp;
        if (FromBG == null) return;
        pendingBackgroundUpdate = () => {
            if (destroyIfExists ||
                (ToBG == null && FromBG.source != bgp) ||
                (ToBG != null && ToBG.source != bgp)) {
                ClearTransition();
                SetTarget(CreateBGC(bgp), withTransition);
            }
        };
    }

    private void ClearTransition() {
        foreach (var cts in transitionCTS) cts.Cancel();
        transitionCTS.Clear();
        FinishTransition();
    }

    private void SetTarget(BackgroundController bgc, bool withTransition) {
        if (withTransition && nextRequestedTransition.Try(out var transition)) {
            ToBG = bgc;
            DoTransition(transition);
            nextRequestedTransition = null;
        } else {
            if (FromBG != null)
                FromBG.Hide();
            FromBG = bgc;
        }
        ShowHide();
    }

    private static readonly HashSet<Cancellable> transitionCTS = new();
    private void DoTransition(BackgroundTransition bgt) {
        if (FromBG == null) return;
        if (ToBG == null) throw new Exception("Cannot do transition when target BG is null");
        ResetMaterial();
        /*var pb = new MaterialPropertyBlock();
        var mat = Instantiate(baseMixerMaterial);*/
        var cts = new Cancellable();
        transitionCTS.Add(cts);
        Func<bool>? condition = null;
        float timeout = bgt.Apply(this, mat, ref condition);
        //BackgroundCombiner.SetMaterial(mat, pb);
        void Finish() {
            if (!cts.Cancelled) FinishTransition();
            transitionCTS.Remove(cts);
        }
        if (condition is null && timeout <= 0)
            throw new Exception("Cannot wait for transition without a timeout or condition");
        else
            RUWaitingUtils.WaitThenCBEvenIfCancelled(this, cts, timeout, condition ?? (() => true), Finish);
    }

    public IDisposable AddDistorter(Transform transf, CameraInfo tracker) {
        var disp = distorter.AddConst((transf, tracker));
        UpdateDistortion();
        return disp;
    }

    protected override void OnDisable() {
        foreach (var cts in transitionCTS) cts.Cancel();
        transitionCTS.Clear();
        FromBG = ToBG = null;
        base.OnDisable();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        UnityEngine.Graphics.Blit(source, destination, mat);
    }
    
    private static readonly int screenCenterOffset = Shader.PropertyToID("_ScreenCenterOffset");
    private static readonly int distortTime = Shader.PropertyToID("_DistortT");
    private static readonly int distortCenter = Shader.PropertyToID("_DistortCenter");
}
}