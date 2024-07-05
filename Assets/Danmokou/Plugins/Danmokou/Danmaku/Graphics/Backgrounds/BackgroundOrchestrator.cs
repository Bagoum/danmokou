using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using JetBrains.Annotations;
using Danmokou.SM;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using static BagoumLib.Tasks.WaitingUtils;

namespace Danmokou.Graphics.Backgrounds {
public interface IBackgroundOrchestrator {
    void QueueTransition(BackgroundTransition bgt);
    void ConstructTarget(GameObject bgp, bool withTransition = true, bool destroyIfExists = false);
}
public class BackgroundOrchestrator : CoroutineRegularUpdater, IBackgroundOrchestrator {
    private Transform tr = null!;
    private BackgroundCombiner BackgroundCombiner { get; set; } = null!;
    public BackgroundController? FromBG { get; private set; }
    public BackgroundController? ToBG { get; private set; }
    
    private readonly Dictionary<GameObject, BackgroundController> instantiated = new();
    private BackgroundTransition? nextRequestedTransition;

    public GameObject backgroundCombiner = null!;
    public Material baseMixerMaterial = null!;
    public GameObject defaultBGCPrefab = null!;
    
    public float Time { get; private set; }

    private void ShowHide() {
        if (FromBG != null) {
            FromBG.ShowHideBySettings(SaveData.s.Backgrounds);
        }
        if (ToBG != null) {
            ToBG.ShowHideBySettings(SaveData.s.Backgrounds);
        }
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

    private GameObject? lastRequestedBGC;
    public override void FirstFrame() {
        if (FromBG == null) {
            var bgc = (lastRequestedBGC == null) ? defaultBGCPrefab : lastRequestedBGC;
            lastRequestedBGC = null;
            FromBG = CreateBGC(bgc);
            ShowHide();
        }
    }
    public static GameObject? NextSceneStartupBGC { get; set; }
    //We don't update backgrounds immediately, since there are use-cases where we push two backgrounds
    // one after another and want the first one to be ignored.
    private Action? pendingBackgroundUpdate;
    private void Awake() {
        tr = transform;
        lastRequestedBGC = NextSceneStartupBGC;
        NextSceneStartupBGC = null;
        Time = 0f;
        BackgroundCombiner = Instantiate(backgroundCombiner, Vector3.zero, Quaternion.identity)
            .GetComponent<BackgroundCombiner>();
        BackgroundCombiner.Initialize(this);
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IBackgroundOrchestrator>(this, new ServiceLocator.ServiceOptions { Unique = true });
        Listen(RenderHelpers.PreferredResolution, _ => ShowHide());
    }

    public override void RegularUpdate() {
        pendingBackgroundUpdate?.Invoke();
        pendingBackgroundUpdate = null;
        base.RegularUpdate();
        Time += ETime.FRAME_TIME;
    }


    public void QueueTransition(BackgroundTransition bgt) => nextRequestedTransition = bgt;

    private void FinishTransition() {
        if (ToBG != null && FromBG != null) {
            FromBG.Hide();
            FromBG = ToBG;
            ToBG = null;
        }
    }
    public void ConstructTarget(GameObject bgp, bool withTransition=true, bool destroyIfExists=false) {
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
        if (withTransition && nextRequestedTransition.HasValue) {
            ToBG = bgc;
            DoTransition(nextRequestedTransition.Value);
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
        var pb = new MaterialPropertyBlock();
        var mat = Instantiate(baseMixerMaterial);
        var cts = new Cancellable();
        transitionCTS.Add(cts);
        Func<bool>? condition = null;
        float timeout = bgt.Apply(this, mat, ref condition);
        BackgroundCombiner.SetMaterial(mat, pb);
        void Finish() {
            if (!cts.Cancelled) FinishTransition();
            transitionCTS.Remove(cts);
        }
        if (condition is null && timeout <= 0)
            throw new Exception("Cannot wait for transition without a timeout or condition");
        else
            RUWaitingUtils.WaitThenCBEvenIfCancelled(this, cts, timeout, condition ?? (() => true), Finish);
    }

    protected override void OnDisable() {
        foreach (var cts in transitionCTS) cts.Cancel();
        transitionCTS.Clear();
        FromBG = ToBG = null;
        base.OnDisable();
    }
}


public static class CombinerKeywords {
    public const string TO_ONLY = "MIX_TO_ONLY";
    public const string ALPHA = "MIX_ALPHA_BLEND";
    public const string WIPE_TEX = "MIX_WIPE_TEX";
    public const string WIPE1 = "MIX_WIPE1";
    public const string WIPEFROMCENTER = "MIX_WIPE_CENTER";
    public const string WIPEY = "MIX_WIPE_Y";
    private static readonly string[] kws = {TO_ONLY, ALPHA, WIPE_TEX, WIPE1, WIPEFROMCENTER};

    public static void Apply(Material mat, string keyword) {
        foreach (var kw in kws) mat.DisableKeyword(kw);
        mat.EnableKeyword(keyword);
    }
}
}