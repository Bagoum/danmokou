using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    void ConstructTarget(GameObject bgp, bool withTransition, bool destroyIfExists = false);
}
public class BackgroundOrchestrator : CoroutineRegularUpdater, IBackgroundOrchestrator {
    private Transform tr = null!;
    private BackgroundCombiner BackgroundCombiner { get; set; } = null!;
    public BackgroundController? FromBG { get; private set; }
    public BackgroundController? ToBG { get; private set; }
    
    private readonly Dictionary<GameObject, BackgroundController> instantiated = new Dictionary<GameObject, BackgroundController>();
    private BackgroundTransition? nextRequestedTransition;

    public GameObject backgroundCombiner = null!;
    public Material baseMixerMaterial = null!;
    public GameObject defaultBGCPrefab = null!;
    
    public float Time { get; private set; }

    private void ShowHide() {
        if (FromBG == null) MaybeCreateFirst();
        else {
            if (SaveData.s.Backgrounds) {
                FromBG.Show();
                if (ToBG != null) ToBG.Show();
            } else {
                FromBG.Hide();
                if (ToBG != null) ToBG.Hide();
            }
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
    private void MaybeCreateFirst() {
        if (SaveData.s.Backgrounds) {
            var bgc = (lastRequestedBGC == null) ? defaultBGCPrefab : lastRequestedBGC;
            lastRequestedBGC = null;
            FromBG = CreateBGC(bgc);
        }
    }

    public static GameObject? NextSceneStartupBGC { get; set; }
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
    public void ConstructTarget(GameObject bgp, bool withTransition, bool destroyIfExists=false) {
        lastRequestedBGC = bgp;
        if (FromBG == null) return;
        if (destroyIfExists || 
            (ToBG == null && FromBG.source != bgp) ||
            (ToBG != null && ToBG.source != bgp)) {
            ClearTransition();
            SetTarget(CreateBGC(bgp), withTransition);
        }
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
            FromBG = bgc;
        }
    }

    private static readonly HashSet<Cancellable> transitionCTS = new HashSet<Cancellable>();
    private void DoTransition(BackgroundTransition bgt) {
        if (FromBG == null) return;
        if (ToBG == null) throw new Exception("Cannot do transition when target BG is null");
        var pb = new MaterialPropertyBlock();
        var mat = Instantiate(baseMixerMaterial);
        float timeout = bgt.TimeToFinish();
        var cts = new Cancellable();
        transitionCTS.Add(cts);
        Func<bool>? condition = null;
        if        (bgt.type == BackgroundTransition.EffectType.WipeTex) {
            bgt.WipeTex.Apply(mat);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPE_TEX);
        } else if (bgt.type == BackgroundTransition.EffectType.Wipe1) {
            bgt.Wipe1.Apply(mat);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPE1);
        } else if (bgt.type == BackgroundTransition.EffectType.WipeFromCenter) {
            bgt.WipeFromCenter.Apply(mat);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPEFROMCENTER);
        } else if (bgt.type == BackgroundTransition.EffectType.Shatter4) {
            Action cb = GetCondition(out condition);
            FromBG.Shatter4(bgt.Shatter4, false, cb);
            CombinerKeywords.Apply(mat, CombinerKeywords.TO_ONLY);
        } else if (bgt.type == BackgroundTransition.EffectType.WipeY) {
            bgt.WipeY.Apply(mat);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPEY); //TODO apply these two generics from within the BGT scope.
        }
        BackgroundCombiner.SetMaterial(mat, pb);
        void Finish() {
            if (!cts.Cancelled) FinishTransition();
            transitionCTS.Remove(cts);
        }
        if (condition == null) {
            if (timeout > 0) WaitingUtils.WaitThenCBEvenIfCancelled(this, cts, timeout, false, Finish);
            else throw new Exception("Cannot wait for transition without a timeout or condition");
        } else {
            WaitingUtils.WaitThenCBEvenIfCancelled(this, cts, timeout, condition, Finish);
        }
    }

    protected override void OnDisable() {
        foreach (var cts in transitionCTS) cts.Cancel();
        transitionCTS.Clear();
        FromBG = ToBG = null;
        base.OnDisable();
    }

    private static class CombinerKeywords {
        public const string TO_ONLY = "MIX_TO_ONLY";
        public const string ALPHA = "MIX_ALPHA_BLEND";
        public const string WIPE_TEX = "MIX_WIPE_TEX";
        public const string WIPE1 = "MIX_WIPE1";
        public const string WIPEFROMCENTER = "MIX_WIPE_CENTER";
        public const string WIPEY = "MIX_WIPE_Y";
        private static readonly string[] kws = {TO_ONLY, ALPHA, WIPE_TEX, WIPE1, WIPEFROMCENTER, WIPEY};

        public static void Apply(Material mat, string keyword) {
            foreach (var kw in kws) mat.DisableKeyword(kw);
            mat.EnableKeyword(keyword);
        }
    }
}
}