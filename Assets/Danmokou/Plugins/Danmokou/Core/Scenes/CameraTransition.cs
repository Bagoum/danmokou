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
using FT = Danmokou.Scriptables.TransitionConfig.FixedType;

namespace Danmokou.Scenes {
public interface ICameraTransition {
    void Fade(ICameraTransitionConfig? cfg, out float waitIn, out float waitOut);
    public void StallFadeOutUntil(Func<bool> cond);
}
/// <summary>
/// Class that generates a material that can be used to blit a camera overlay onto the screen.
/// <br/>The material is consumed by <see cref="DMKMainCamera"/>.
/// </summary>
public class CameraTransition : RegularUpdater, ICameraTransition {
    private static ICameraTransitionConfig? inherited;

    public CameraTransitionConfig defaultConfig = null!;
    public (Material mat, Texture mainTex)? Render { get; private set; }
    private SpriteRenderer sr = null!;

    private void Awake() {
        sr = GetComponent<SpriteRenderer>();
        if (inherited != null) {
            StartCoroutine(FadeOut(inherited));
            inherited = null;
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<ICameraTransition>(this, new() { Unique = true });
    }
    
    public override void RegularUpdate() { }

    public void Fade(ICameraTransitionConfig? cfg, out float waitIn, out float waitOut) {
        inherited = cfg ?? defaultConfig;
        waitIn = inherited.FadeIn.time;
        waitOut = inherited.FadeOut.time;
        StartCoroutine(FadeIn(inherited));
    }


    private void SetReverse(Material mat, TransitionConfig cfg) {
        EnableDisableKW(mat, cfg.reverseKeyword, "FT_REVERSE");
        EnableDisableKW(mat, cfg.fixedType == FT.CIRCLEWIPE, "REQ_CIRCLE");
        EnableDisableKW(mat, cfg.fixedType == FT.YWIPE, "REQ_Y");
        EnableDisableKW(mat, cfg.fixedType == FT.EMPTY, "REQ_EMPTY");
    }

    private void EnableDisableKW(Material mat, bool success, string kw) {
        if (success) mat.EnableKeyword(kw);
        else mat.DisableKeyword(kw);
    }

    private IEnumerator FadeIn(ICameraTransitionConfig cfg) {
        var m = cfg.FadeIn.material;
        Render = (m, cfg.FadeToTex);
        m.SetTexture(PropConsts.faderTex, cfg.FadeIn.transitionTexture);
        SetReverse(cfg.FadeIn.material, cfg.FadeIn);
        ServiceLocator.Find<ISFXService>().Request(cfg.FadeIn.sfx);
        for (float t = 0; t < cfg.FadeIn.time; t += ETime.ASSUME_SCREEN_FRAME_TIME) {
            m.SetFloat(PropConsts.fillRatio, cfg.FadeIn.Value(t));
            yield return null;
        }
        m.SetFloat(PropConsts.fillRatio, 1);
        //the scene ends after this
    }

    private IEnumerator FadeOut(ICameraTransitionConfig cfg) {
        var m = cfg.FadeOut.material;
        Render = (m, cfg.FadeToTex);
        m.SetTexture(PropConsts.faderTex, cfg.FadeOut.transitionTexture);
        SetReverse(cfg.FadeOut.material, cfg.FadeOut);
        bool didSfx = false;
        //Give an extra frame before opening up, to allow Stall calls if necessary
        for (float t = -ETime.FRAME_TIME; t < cfg.FadeOut.time - ETime.FRAME_TIME; t += ETime.ASSUME_SCREEN_FRAME_TIME) {
            if (t > 0 && fadeOutStallers.Count > 0) {
                while (fadeOutStallers.Any(f => !f())) {
                    yield return null;
                }
                fadeOutStallers.Clear();
            }
            m.SetFloat(PropConsts.fillRatio, cfg.FadeOut.Value(Math.Max(t, 0)));
            yield return null;
            //Put this here so it works well with "long first frames"
            if (!didSfx) 
                ServiceLocator.Find<ISFXService>().Request(cfg.FadeOut.sfx);
            didSfx = true;
        }
        Render = null;
        cfg.OnTransitionComplete?.Invoke();
    }

    private readonly List<Func<bool>> fadeOutStallers = new();
    public void StallFadeOutUntil(Func<bool> cond) {
        fadeOutStallers.Add(cond);
    }
}
}