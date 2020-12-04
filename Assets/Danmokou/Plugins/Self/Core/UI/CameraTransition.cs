using System;
using System.Collections;
using JetBrains.Annotations;
using UnityEngine;

public class CameraTransition : MonoBehaviour {
    private static CameraTransition main;
    private static bool inheritFadeIn = false;
    private static CameraTransitionConfig inherited;

    public CameraTransitionConfig defaultConfig;
    private MaterialPropertyBlock pb;
    public Material mat;
    private SpriteRenderer sr;

    private void Awake() {
        main = this;
        pb = new MaterialPropertyBlock();
        sr = GetComponent<SpriteRenderer>();
        sr.material = mat;
        if (inheritFadeIn) {
            StartCoroutine(FadeOut(inherited));
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

    public static void Fade([CanBeNull] CameraTransitionConfig cfg, out float waitIn, out float waitOut) {
        inherited = (cfg != null) ? cfg : main.defaultConfig;
        inheritFadeIn = true;
        waitIn = inherited.fadeIn.time;
        waitOut = inherited.fadeOut.time;
        main.StartCoroutine(main.FadeIn(inherited));
    }

    private void SetReverse(CameraTransitionConfig.TransitionConfig cfg) {
        EnableDisableKW(cfg.reverse, "FT_REVERSE");
        EnableDisableKW(cfg.fixedType == CameraTransitionConfig.TransitionConfig.FixedType.CIRCLEWIPE, "REQ_CIRCLE");
        EnableDisableKW(cfg.fixedType == CameraTransitionConfig.TransitionConfig.FixedType.YWIPE, "REQ_Y");
        EnableDisableKW(cfg.fixedType == CameraTransitionConfig.TransitionConfig.FixedType.EMPTY, "REQ_EMPTY");
    }

    private void EnableDisableKW(bool success, string kw) {
        if (success) mat.EnableKeyword(kw);
        else mat.DisableKeyword(kw);
    }

    private IEnumerator FadeIn(CameraTransitionConfig cfg) {
        pb.SetTexture(PropConsts.trueTex, cfg.fadeToTex);
        pb.SetTexture(PropConsts.faderTex, cfg.fadeIn.transitionTexture);
        SetReverse(cfg.fadeIn);
        Activate();
        for (float t = 0; t < cfg.fadeIn.time; t += ETime.ASSUME_SCREEN_FRAME_TIME) {
            pb.SetFloat(PropConsts.fillRatio, cfg.fadeIn.Value(t));
            sr.SetPropertyBlock(pb);
            yield return null;
        }
        pb.SetFloat(PropConsts.fillRatio, 1);
        sr.SetPropertyBlock(pb);
    }
    private IEnumerator FadeOut(CameraTransitionConfig cfg) {
        pb.SetTexture(PropConsts.trueTex, cfg.fadeToTex);
        pb.SetTexture(PropConsts.faderTex, cfg.fadeOut.transitionTexture);
        SetReverse(cfg.fadeOut);
        Activate();
        for (float t = cfg.fadeOut.time; t > 0; t -= ETime.ASSUME_SCREEN_FRAME_TIME) {
            pb.SetFloat(PropConsts.fillRatio, cfg.fadeOut.Value(t));
            sr.SetPropertyBlock(pb);
            yield return null;
        }
        pb.SetFloat(PropConsts.fillRatio, 0);
        sr.SetPropertyBlock(pb);
        Deactivate();
    }
}