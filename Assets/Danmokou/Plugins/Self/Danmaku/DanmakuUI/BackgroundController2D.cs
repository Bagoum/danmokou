using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Danmaku;
using DMath;
using JetBrains.Annotations;


public struct Kakera {
    public Vector2 loc;
    public readonly float baseShapeRot;
    public readonly Vector2 uv;
    private Vector2 vel;
    private readonly float gravity;
    public Vector3 rotations;
    private Vector3 rotationVels;
    private readonly Vector3 rotationAccels;

    public Kakera(Vector2 location, float baseShapeRot, float maxInitVelMag, float gravity, Vector2 rotAccelMag) {
        loc = location;
        this.baseShapeRot = baseShapeRot;
        uv = MainCamera.RelativeToScreenUV(loc);
        vel = M.CosSin(RNG.GetFloatOffFrame(0f, M.TAU)) * RNG.GetFloatOffFrame(0f, maxInitVelMag);
        this.gravity = gravity;
        rotationAccels =
            M.Spherical(RNG.GetFloatOffFrame(0f, M.TAU), RNG.GetFloatOffFrame(0f, M.PI)) *
            RNG.GetFloatOffFrame(rotAccelMag.x, rotAccelMag.y);
        rotationVels = RNG.GetFloatOffFrame(1f, 2f) * rotationAccels;
        rotations = Vector3.zero;
    }

    public void DoUpdate(float dT) {
        vel.y -= gravity * dT;
        rotationVels += rotationAccels * dT;
        loc += vel * dT;
        rotations += rotationVels * dT;
    }
}
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
    }

    public EffectType type;
    public WipeTexConfig WipeTex;
    public Wipe1Config Wipe1;
    public WipeFromCenterConfig WipeFromCenter;
    public ShatterConfig Shatter4;
    public WipeYConfig WipeY;
    
    /// <summary>
    /// Upper bound on the time required for the TRANSITION SHADER to fully complete.
    /// Note: if the implementation uses a callback to finish, you can return 0 here.
    /// </summary>
    public float TimeToFinish() {
        if (type == EffectType.Wipe1) return Wipe1.time + 1f;
        else if (type == EffectType.WipeTex) return WipeTex.time + 1f;
        else if (type == EffectType.WipeFromCenter) return WipeFromCenter.time + 1f;
        else if (type == EffectType.Shatter4) return 0f;
        else if (type == EffectType.WipeY) return WipeY.time + 1f;
        else return 0f;
    }

    [Serializable]
    public class WipeTexConfig {
        public float time;
        public Texture2D tex;
        public bool WhiteFirst;
        
        public void Apply(Material mat) {
            mat.SetFloat(PropConsts.maxTime, time);
            mat.SetTexture(PropConsts.faderTex, tex);
            mat.SetFloat(PropConsts.pmDirection, WhiteFirst ? 1 : -1);
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
        }
    }
    [Serializable]
    public class WipeFromCenterConfig {
        public float time;

        public void Apply(Material mat) {
            mat.SetFloat(PropConsts.maxTime, time);
        }
    }

    [Serializable]
    public class WipeYConfig {
        public bool up;
        public float time;
        
        public void Apply(Material mat) {
            mat.SetFloat(PropConsts.maxTime, time);
            mat.SetFloat(PropConsts.pmDirection, up ? 1 : -1);
        }
    }

    [Serializable]
    public class ShatterConfig {
        public Sprite fragmentSprite;
        public Material fragmentMaterial;
        public float fragmentRadius;
        public float SquareMeshWidth => 2f * fragmentRadius;
        public float fragMaxInitSpeed;
        public float fragGravity;
        public Vector2 fragRotAccelMag;
        [CanBeNull] private Mesh mesh;
        public Mesh Mesh {
            get {
                if (mesh == null) mesh = MeshGenerator.RenderInfo.FromSprite(fragmentSprite, SquareMeshWidth);
                return mesh;
            }
        }

        public void Tile4(List<Kakera> fragments) {
            float s = fragmentRadius * Mathf.Sqrt(2f);
            for (float w = 0f; w < MainCamera.ScreenWidth + s; w += s) {
                for (float h = 0f; h < MainCamera.ScreenHeight + s; h += s) {
                    fragments.Add(new Kakera(new Vector2(w - MainCamera.HorizRadius, h - MainCamera.VertRadius), 
                        Mathf.PI/4, fragMaxInitSpeed, fragGravity, fragRotAccelMag));
                }
            }
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
    public Sprite bgSprite;
    private Mesh mesh;
    public Material bgMaterial;
    private MaterialPropertyBlock backgroundPB;
    protected override void Awake() {
        backgroundPB = new MaterialPropertyBlock();
        backgroundPB.SetTexture(PropConsts.mainTex, bgSprite.texture);
        backgroundPB.SetFloat(PropConsts.time, BackgroundOrchestrator.Time);
        ReassignVariables();
        mesh = MeshGenerator.RenderInfo.FromSprite(bgSprite);
        base.Awake();
    }

    [ContextMenu("Reassign")]
    private void ReassignVariables() {
        backgroundPB.SetColor(PropConsts.tint, tint);
    }

    private void Update() {
        backgroundPB.SetFloat(PropConsts.time, BackgroundOrchestrator.Time);
    }

    protected override void Render(Camera c) {
        if (!Application.isPlaying) return;
        //Sprite renders to given camera
        if (c == capturer.Camera) {
            capturer.Draw(tr, mesh, bgMaterial, backgroundPB, DrawToLayer);
            return;
        }
        base.Render(c);
    }
}