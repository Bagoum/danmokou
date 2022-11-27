using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Graphics;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;
using FT = Danmokou.Scriptables.CameraTransitionConfig.TransitionConfig.FixedType;

namespace Danmokou.Scenes {
public interface ICameraTransition {
    void Fade(CameraTransitionConfig? cfg, out float waitIn, out float waitOut);
    public void StallFadeOutUntil(Func<bool> cond);
}
public class CameraTransition : RegularUpdater, ICameraTransition {
    private static CameraTransitionConfig? inherited;

    public CameraTransitionConfig defaultConfig = null!;
    private MaterialPropertyBlock pb = null!;
    private SpriteRenderer sr = null!;

    private void Awake() {
        pb = new MaterialPropertyBlock();
        sr = GetComponent<SpriteRenderer>();
        if (inherited != null) {
            StartCoroutine(FadeOut(inherited));
            inherited = null;
        } else {
            Deactivate();
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<ICameraTransition>(this, new ServiceLocator.ServiceOptions { Unique = true });
    }
    
    public override void RegularUpdate() { }

    private void Deactivate() {
        sr.enabled = false;
    }

    private void Activate() {
        sr.enabled = true;
    }

    public void Fade(CameraTransitionConfig? cfg, out float waitIn, out float waitOut) {
        inherited = (cfg != null) ? cfg : defaultConfig;
        waitIn = inherited.fadeIn.time;
        waitOut = inherited.fadeOut.time;
        StartCoroutine(FadeIn(inherited));
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
        ServiceLocator.Find<ISFXService>().Request(cfg.fadeIn.sfx);
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
        bool didSfx = false;
        //Give an extra frame before opening up, to allow Stall calls if necessary
        for (float t = -ETime.FRAME_TIME; t < cfg.fadeOut.time - ETime.FRAME_TIME; t += ETime.ASSUME_SCREEN_FRAME_TIME) {
            if (t > 0 && fadeOutStallers.Count > 0) {
                while (fadeOutStallers.Any(f => !f())) {
                    yield return null;
                }
                fadeOutStallers.Clear();
            }
            
            pb.SetFloat(PropConsts.fillRatio, cfg.fadeOut.Value(Math.Max(t, 0)));
            sr.SetPropertyBlock(pb);
            yield return null;
            //Put this here so it works well with "long first frames"
            if (!didSfx) 
                ServiceLocator.Find<ISFXService>().Request(cfg.fadeOut.sfx);
            didSfx = true;
        }
        pb.SetFloat(PropConsts.fillRatio, 0);
        sr.SetPropertyBlock(pb);
        Deactivate();
    }

    private readonly List<Func<bool>> fadeOutStallers = new();
    public void StallFadeOutUntil(Func<bool> cond) {
        fadeOutStallers.Add(cond);
    }
}
}