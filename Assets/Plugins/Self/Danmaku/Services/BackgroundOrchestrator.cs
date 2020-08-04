using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SM;
using UnityEngine;

namespace Danmaku {
public class BackgroundOrchestrator : MonoBehaviour {
    private static BackgroundOrchestrator main;
    private Transform tr;
    public static BackgroundController FromBG { get; private set; }
    [CanBeNull] public static BackgroundController ToBG { get; private set; }
    private static BackgroundTransition? nextRequestedTransition;

    public Material baseMixerMaterial;
    public GameObject defaultBGCPrefab;
    
    public static float Time { get; private set; }

    public static void RecreateTextures() {
        if (FromBG != null) FromBG.Capturer.RecreateTexture();
    }

    private void Awake() {
        tr = transform;
        main = this;
        FromBG = Instantiate(defaultBGCPrefab, tr, false).GetComponent<BackgroundController>().Initialize(defaultBGCPrefab);
        Time = 0f;
    }

    private void Update() {
        Time += ETime.dT;
    }

    [ContextMenu("Debug fromto")]
    public void Debugfromto() {
        Debug.Log(ToBG == null ?
            $"From: {FromBG.gameObject.name}" :
            $"From: {FromBG.gameObject.name}; To: {ToBG.gameObject.name}");
    }


    public static void QueueTransition(BackgroundTransition bgt) => nextRequestedTransition = bgt;

    private static void FinishTransition() {
        if (ToBG != null) {
            FromBG.Kill();
            FromBG = ToBG;
            ToBG = null;
        }

    }

    public static void ConstructTarget(GameObject bgp, bool withTransition, bool destroyIfExists=false) {
        if (destroyIfExists || 
            (ToBG == null && FromBG.source != bgp) ||
            (ToBG != null && ToBG.source != bgp)) {
            SetTarget(Instantiate(bgp, main.tr, false).GetComponent<BackgroundController>().Initialize(bgp), withTransition);
        }
    }

    private static void SetTarget(BackgroundController bgc, bool withTransition) {
        foreach (var cts in transitionCTS) cts.Cancel();
        FinishTransition();
        if (withTransition && nextRequestedTransition.HasValue) {
            ToBG = bgc;
            DoTransition(nextRequestedTransition.Value);
            nextRequestedTransition = null;
        } else {
            FromBG = bgc;
        }
    }

    private static readonly HashSet<CancellationTokenSource> transitionCTS = new HashSet<CancellationTokenSource>();
    private static void DoTransition(BackgroundTransition bgt) => main._DoTransition(bgt);

    private void _DoTransition(BackgroundTransition bgt) {
        if (ToBG == null) throw new Exception("Cannot do transition when target BG is null");
        var pb = new MaterialPropertyBlock();
        var mat = Instantiate(main.baseMixerMaterial);
        float timeout = bgt.TimeToFinish();
        var cts = new CancellationTokenSource();
        transitionCTS.Add(cts);
        Func<bool> condition = null;
        if (bgt.type == BackgroundTransition.EffectType.WipeTex) {
            bgt.WipeTex.Apply(mat);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPE_TEX);
        } else if (bgt.type == BackgroundTransition.EffectType.Wipe1) {
            bgt.Wipe1.Apply(mat);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPE1);
        } else if (bgt.type == BackgroundTransition.EffectType.WipeFromCenter) {
            bgt.WipeFromCenter.Apply(mat);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPEFROMCENTER);
        } else if (bgt.type == BackgroundTransition.EffectType.Shatter4) {
            Action cb = WaitingUtils.GetCondition(out condition);
            FromBG.Shatter4(bgt.Shatter4, false, cb);
            CombinerKeywords.Apply(mat, CombinerKeywords.TO_ONLY);
        } else if (bgt.type == BackgroundTransition.EffectType.WipeY) {
            bgt.WipeY.Apply(mat);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPEY); //TODO apply these two generics from within the BGT scope.
        }
        BackgroundCombiner.SetMaterial(mat, pb);
        void Finish() {
            if (!cts.IsCancellationRequested) FinishTransition();
            transitionCTS.Remove(cts);
        }
        if (condition == null) {
            if (timeout > 0) WaitingUtils.WaitThenCBEvenIfCancelled(GlobalBEH.Main, cts.Token, timeout, false, Finish);
            else throw new Exception("Cannot wait for transition without a timeout or condition");
        } else {
            WaitingUtils.WaitThenCBEvenIfCancelled(GlobalBEH.Main, cts.Token, timeout, condition, Finish);
        }
    }

    private void OnDisable() {
        foreach (var cts in transitionCTS) cts.Cancel();
        FromBG = ToBG = null;
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