using System;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Core;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Graphics.Backgrounds {
/// <summary>
/// A component which combines multiple background images using a transition configuration
/// set by the BackgroundOrchestrator.
/// </summary>
public class BackgroundCombiner : RegularUpdater {
    private int renderMask;
    private MaterialPropertyBlock pb = null!;
    private SpriteRenderer sr = null!;
    private Material mat = null!;

    private float time = 0f;
    private BackgroundOrchestrator orchestrator = null!;
    private void Awake() {
        renderMask = LayerMask.GetMask(LayerMask.LayerToName(gameObject.layer));
        pb = new MaterialPropertyBlock();
        sr = GetComponent<SpriteRenderer>();
        sr.enabled = SaveData.s.Backgrounds;
        mat = sr.material;
        mat.EnableKeyword("MIX_FROM_ONLY");
        sr.SetPropertyBlock(pb);
    }

    public void Initialize(BackgroundOrchestrator _orchestrator) {
        orchestrator = _orchestrator;
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(SaveData.ResolutionChanged, Reconstruct);
    }

    private void Start() => UpdateTextures();

    public void SetMaterial(Material newMat, MaterialPropertyBlock newPB) {
        mat = newMat;
        pb = newPB;
        UpdateTextures();
        sr.material = mat;
        time = 0f;
    }

    private void Reconstruct() {
        sr.enabled = SaveData.s.Backgrounds;
        UpdateTextures();
    }

    private void UpdateTextures() {
        if (!SaveData.s.Backgrounds || orchestrator.FromBG == null) return;
        //Update PB
        var fromTex = orchestrator.FromBG.capturer.Captured;
        var toTex = (orchestrator.ToBG == null) ? null : orchestrator.ToBG.capturer.Captured;
        if (toTex == null) {
            mat.EnableKeyword("MIX_FROM_ONLY");
            pb.SetTexture(PropConsts.fromTex, fromTex);
        } else {
            mat.DisableKeyword("MIX_FROM_ONLY");
            pb.SetTexture(PropConsts.fromTex, fromTex);
            pb.SetTexture(PropConsts.toTex, toTex);
        }
        pb.SetFloat(PropConsts.time, time);
        sr.SetPropertyBlock(pb);
    }

    public override void RegularUpdate() {
        if (ETime.LastUpdateForScreen) UpdateTextures();
        time += ETime.FRAME_TIME;
    }
}
}