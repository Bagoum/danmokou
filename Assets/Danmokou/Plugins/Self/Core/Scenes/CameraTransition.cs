using System;
using System.Collections;
using DMK.Core;
using DMK.Graphics;
using DMK.Scriptables;
using DMK.Services;
using JetBrains.Annotations;
using UnityEngine;
using FT = DMK.Scriptables.CameraTransitionConfig.TransitionConfig.FixedType;

namespace DMK.Scenes {
public class CameraTransition : MonoBehaviour {
    private static CameraTransition main = null!;
    private static CameraTransitionConfig? inherited;

    public CameraTransitionConfig defaultConfig = null!;
    private MaterialPropertyBlock pb = null!;
    private SpriteRenderer sr = null!;

    private void Awake() {
        main = this;
        pb = new MaterialPropertyBlock();
        sr = GetComponent<SpriteRenderer>();
        if (inherited != null) {
            StartCoroutine(FadeOut(inherited));
            inherited = null;
        } else {
            Deactivate();
        }
    }

    private void Deactivate() {
        sr.enabled = false;
    }

    private void Activate() {
        sr.enabled = true;
    }

    public static void Fade(CameraTransitionConfig? cfg, out float waitIn, out float waitOut) {
        inherited = (cfg != null) ? cfg : main.defaultConfig;
        waitIn = inherited.fadeIn.time;
        waitOut = inherited.fadeOut.time;
        main.StartCoroutine(main.FadeIn(inherited));
    }

    private void SetReverse(Material mat, CameraTransitionConfig.TransitionConfig cfg) {
        EnableDisableKW(mat, cfg.reverseKeyword, "FT_REVERSE");
        EnableDisableKW(mat, cfg.fixedType == FT.CIRCLEWIPE, "REQ_CIRCLE");
        EnableDisableKW(mat, cfg.fixedType == FT.YWIPE, "REQ_Y");
        EnableDisableKW(mat, cfg.fixedType == FT.EMPTY, "REQ_EMPTY");
    }

    private void EnableDisableKW(Material mat, bool success, string kw) {
        if (success) mat.EnableKeyword(kw);
        else mat.DisableKeyword(kw);
    }

    private IEnumerator FadeIn(CameraTransitionConfig cfg) {
        sr.sharedMaterial = cfg.fadeIn.material;
        pb.SetTexture(PropConsts.trueTex, cfg.fadeToTex);
        pb.SetTexture(PropConsts.faderTex, cfg.fadeIn.transitionTexture);
        SetReverse(cfg.fadeIn.material, cfg.fadeIn);
        Activate();
        DependencyInjection.Find<ISFXService>().RequestSFX(cfg.fadeIn.sfx);
        for (float t = 0; t < cfg.fadeIn.time; t += ETime.ASSUME_SCREEN_FRAME_TIME) {
            pb.SetFloat(PropConsts.fillRatio, cfg.fadeIn.Value(t));
            sr.SetPropertyBlock(pb);
            yield return null;
        }
        pb.SetFloat(PropConsts.fillRatio, 1);
        sr.SetPropertyBlock(pb);
    }

    private IEnumerator FadeOut(CameraTransitionConfig cfg) {
        sr.sharedMaterial = cfg.fadeOut.material;
        pb.SetTexture(PropConsts.trueTex, cfg.fadeToTex);
        pb.SetTexture(PropConsts.faderTex, cfg.fadeOut.transitionTexture);
        SetReverse(cfg.fadeOut.material, cfg.fadeOut);
        Activate();
        for (float t = 0; t < cfg.fadeOut.time; t += ETime.ASSUME_SCREEN_FRAME_TIME) {
            pb.SetFloat(PropConsts.fillRatio, cfg.fadeOut.Value(t));
            sr.SetPropertyBlock(pb);
            yield return null;
            //Put this here so it works well with "long first frames"
            if (t == 0) 
                DependencyInjection.Find<ISFXService>().RequestSFX(cfg.fadeOut.sfx);
        }
        pb.SetFloat(PropConsts.fillRatio, 0);
        sr.SetPropertyBlock(pb);
        Deactivate();
    }
}
}