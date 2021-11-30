using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Tasks;
using Danmokou.DMath;
using Danmokou.Services;
using UnityEngine;
using JetBrains.Annotations;


namespace Danmokou.Graphics.Backgrounds {
/// <summary>
/// Configuration for a background transition effect.
/// After it is finished executing, the BackgroundOrchestrator will destroy the source BackgroundController.
/// A transition is finished executing when it has executed its callback (if required) and its TimeToFinish has elapsed.
/// </summary>
[Serializable]
public struct BackgroundTransition {
    //Note: when extending this, add to DUDrawer.BGT.EnumValues,
    // and add a drawer in DUCaseDrawer.
    public enum EffectType {
        WipeTex,
        Wipe1,
        WipeFromCenter,
        Shatter4,
        WipeY,
        Fade,
    }

    public EffectType type;
    public WipeTexConfig WipeTex;
    public Wipe1Config Wipe1;
    public WipeFromCenterConfig WipeFromCenter;
    public ShatterConfig Shatter4;
    public WipeYConfig WipeY;
    public FadeConfig Fade;
    

    /// <summary>
    /// 
    /// </summary>
    /// <param name="orch">Orchestrator managing transition</param>
    /// <param name="mat">Orchestrator's sprite material</param>
    /// <param name="condition">If provided, the orchestrator can wait for this to be set to true as a marker
    /// of the transition completing.</param>
    /// <returns>Expected time for the transition to finish. The orchestrator should wait for this amount of time
    /// if condition is not provided.</returns>
    public float Apply(BackgroundOrchestrator orch, Material mat, ref Func<bool>? condition) {
        switch (type) {
            case EffectType.WipeTex:
                WipeTex.Apply(mat);
                return WipeTex.time + 1f;
            case EffectType.Wipe1:
                Wipe1.Apply(mat);
                return Wipe1.time + 1f;
            case EffectType.WipeFromCenter:
                WipeFromCenter.Apply(mat);
                return WipeFromCenter.time + 1f;
            case EffectType.Shatter4:            
                Action cb = WaitingUtils.GetCondition(out condition);
                orch.FromBG!.Shatter4(Shatter4, false, cb);
                CombinerKeywords.Apply(mat, CombinerKeywords.TO_ONLY);
                return 0;
            case EffectType.WipeY:
                WipeY.Apply(mat);
                return WipeY.time + 1f;
            case EffectType.Fade:
                Fade.Apply(mat);
                return Fade.time + 1f;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [Serializable]
    public class WipeTexConfig {
        public float time;
        public Texture2D tex = null!;
        public bool WhiteFirst;
        
        public void Apply(Material mat) {
            mat.SetFloat(PropConsts.maxTime, time);
            mat.SetTexture(PropConsts.faderTex, tex);
            mat.SetFloat(PropConsts.pmDirection, WhiteFirst ? 1 : -1);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPE_TEX);
        }
    }

    [Serializable]
    public class Wipe1Config {
        public float time;
        public float initialAngle;
        public bool CCW;

        public void Apply(Material mat) {
            mat.SetFloat(PropConsts.maxTime, time);
            mat.SetFloat(PropConsts.angle0, M.degRad * initialAngle);
            mat.SetFloat(PropConsts.pmDirection, CCW ? 1 : -1);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPE1);
        }
    }
    [Serializable]
    public class WipeFromCenterConfig {
        public float time;

        public void Apply(Material mat) {
            mat.SetFloat(PropConsts.maxTime, time);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPEFROMCENTER);
        }
    }

    [Serializable]
    public class WipeYConfig {
        public bool up;
        public float time;
        
        public void Apply(Material mat) {
            mat.SetFloat(PropConsts.maxTime, time);
            mat.SetFloat(PropConsts.pmDirection, up ? 1 : -1);
            CombinerKeywords.Apply(mat, CombinerKeywords.WIPEY);
        }
    }

    [Serializable]
    public class ShatterConfig : FragmentRendering.FragmentConfig {
        public float fragMaxInitSpeed;
        public float fragGravity;
        public Vector2 fragRotAccelMag;

        public IEnumerable<FragmentRendering.Fragment> Tile4() {
            float s = fragmentRadius * (float)Math.Sqrt(2);
            float width = LocationHelpers.Width + 2f;
            float height = MainCamera.ScreenHeight;
            for (float w = 0f; w < width + s; w += s) {
                for (float h = 0f; h < height + s; h += s) {
                    var loc = new Vector2(w - width / 2f, h - height / 2f);
                    var uv = MainCamera.RelativeToScreenUV(loc);
                    yield return new FragmentRendering.Fragment(loc, uv, 
                        Mathf.PI/4, fragMaxInitSpeed, fragGravity, fragRotAccelMag);
                }
            }
        }
    }

    [Serializable]
    public class FadeConfig {
        public float time;
        
        public void Apply(Material mat) {
            mat.SetFloat(PropConsts.maxTime, time);
            CombinerKeywords.Apply(mat, CombinerKeywords.ALPHA);
        }
    }
}

/// <summary>
/// A component which controls the display of a (dynamic) background image.
/// This component bypasses SpriteRenderer. Instead, it uses a per-instance camera to render
/// itself to a RenderTexture, which it passes to the BackgroundCombiner
/// depending on how it is orchestrated by the BackgroundOrchestrator.
/// Note that some transition effects, such as Shatter, are delegated here. 
/// </summary>
public sealed class BackgroundController2D : BackgroundController {
    public Color tint;
    private (SpriteRenderer sr, MaterialPropertyBlock pb)[] sr = null!;

    public override BackgroundController Initialize(GameObject prefab, BackgroundOrchestrator orchestrator) {
        base.Initialize(prefab, orchestrator);
        sr = GetComponentsInChildren<SpriteRenderer>().Select(s => {
            var m = new MaterialPropertyBlock();
            s.GetPropertyBlock(m);
            m.SetFloat(PropConsts.time, Orchestrator.Time);
            s.color *= tint;
            return (s, m);
        }).ToArray();
        return this;
    }

    private void Update() {
        for (int ii = 0; ii < sr.Length; ++ii) {
            sr[ii].pb.SetFloat(PropConsts.time, Orchestrator.Time);
            sr[ii].sr.SetPropertyBlock(sr[ii].pb);
        }
    }
}
}