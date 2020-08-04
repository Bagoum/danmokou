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
        vel = M.RadToDir(RNG.GetFloatOffFrame(0f, M.TAU)) * RNG.GetFloatOffFrame(0f, maxInitVelMag);
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
public sealed class BackgroundController : CoroutineRegularUpdater {
    private Transform tr;
    public Sprite bgSprite;
    private Mesh mesh;
    public Material bgMaterial;
    public Color tint;
    private MaterialPropertyBlock backgroundPB;
    private MaterialPropertyBlock fragmentPB;
    public ArbitraryCapturer Capturer { get; private set; }

    private BackgroundTransition.ShatterConfig currentShatter;
    private Action shatterCB;
    
    private readonly List<Kakera> fragments = new List<Kakera>();
    private readonly List<RenderTexture> tempTex = new List<RenderTexture>();
    private int fragRenderLayer;
    private int fragRenderMask;
    [CanBeNull] public GameObject source { get; private set; }
    private void Awake() {
        tr = transform;
        backgroundPB = new MaterialPropertyBlock();
        backgroundPB.SetTexture(PropConsts.mainTex, bgSprite.texture);
        backgroundPB.SetFloat(PropConsts.time, BackgroundOrchestrator.Time);
        ReassignVariables();
        fragRenderLayer = LayerMask.NameToLayer("LowDirectRender");
        fragRenderMask = LayerMask.GetMask("LowDirectRender");
        fragmentPB = new MaterialPropertyBlock();
        mesh = MeshGenerator.RenderInfo.FromSprite(bgSprite);
        Capturer = MainCamera.CreateArbitraryCapturer(tr);
    }

    public BackgroundController Initialize(GameObject prefab) {
        source = prefab;
        return this;
    }

    [ContextMenu("Reassign")]
    private void ReassignVariables() {
        backgroundPB.SetColor(PropConsts.tint, tint);
    }
    
    private void Update() {
        backgroundPB.SetFloat(PropConsts.time, BackgroundOrchestrator.Time);
    }
    
    
    private const float YCUTOFF = -10;
    public override void RegularUpdate() {
        base.RegularUpdate();
        if (fragments.Count > 0) {
            bool cullAllFragments = true;
            for (int fi = 0; fi < fragments.Count; ++fi) {
                var f = fragments[fi];
                f.DoUpdate(ETime.FRAME_TIME);
                fragments[fi] = f;
                if (f.loc.y > YCUTOFF) cullAllFragments = false;
            }
            if (cullAllFragments) {
                fragments.Clear();
                tempTex.ForEach(RenderTexture.ReleaseTemporary);
                tempTex.Clear();
                shatterCB();
            }
        }
    }
    
    private static void Tile4(List<Kakera> fragments, float fragmentRadius, Func<Vector2, float, Kakera> fragConstr) {
        float s = fragmentRadius * Mathf.Sqrt(2f);
        for (float w = 0f; w < MainCamera.ScreenWidth + s; w += s) {
            for (float h = 0f; h < MainCamera.ScreenHeight + s; h += s) {
                fragments.Add(fragConstr(new Vector2(w - MainCamera.HorizRadius, h - MainCamera.VertRadius), 
                    Mathf.PI/4));
            }
        }
    }
    //Note: while you can call Fragment multiple times, only one texture can be shared between all calls,
    //so it's generally not useful to do so. Also, it causes overlap flashing.
    public void Shatter4(BackgroundTransition.ShatterConfig config, bool doCopy, Action cb) {
        var fragmentTex = Capturer.Captured;
        if (doCopy) {
            fragmentTex = fragmentTex.CopyAsTemp();
            tempTex.Add(fragmentTex);
        }
        fragmentPB.SetTexture(PropConsts.mainTex, fragmentTex);
        currentShatter = config;
        currentShatter.Tile4(fragments);
        shatterCB = cb;
    } 

    private void Render(Camera c) {
        if (!Application.isPlaying) return;
        //Sprite renders to given camera
        if (c == Capturer.Camera) {
            Capturer.Draw(tr, mesh, bgMaterial, backgroundPB);
            return;
        }
        //Effects render to LowEffects
        if ((c.cullingMask & fragRenderMask) != 0) {
            if (fragments.Count > 0) {
                RenderFragments(c, currentShatter);
            }
        }
    }

    private void RenderFragments(Camera c, BackgroundTransition.ShatterConfig shatter) {
        fragmentPB.SetFloat(PropConsts.SquareMeshWidth, shatter.SquareMeshWidth);
        var instanceCount = fragments.Count;
        for (int done = 0; done < instanceCount; done += batchSize) {
            int run = Math.Min(instanceCount - done, batchSize);
            for (int batchInd = 0; batchInd < run; ++batchInd) {
                var obj = fragments[done + batchInd];
                uvArr[batchInd] = new Vector4(obj.uv.x, obj.uv.y, obj.baseShapeRot, 0);
                matArr[batchInd] = Matrix4x4.TRS(obj.loc, Quaternion.Euler(obj.rotations), Vector3.one);
            }
            fragmentPB.SetVectorArray(uvPropertyId, uvArr);
            CallInstancedDraw(c, shatter.Mesh, shatter.fragmentMaterial, fragmentPB, run);
        }
    }
    private void CallInstancedDraw(Camera c, Mesh m, Material mat, MaterialPropertyBlock pb, int ct) {
        Graphics.DrawMeshInstanced(m, 0, mat,
            matArr,
            count: ct,
            properties: pb,
            castShadows: ShadowCastingMode.Off,
            receiveShadows: false,
            layer: fragRenderLayer,
            camera: c);
    }
    

    protected override void OnEnable() {
        Camera.onPreCull += Render;
        base.OnEnable();
    }

    protected override void OnDisable() {
        Camera.onPreCull -= Render;
        base.OnDisable();
    }

    public void Kill() {
        Capturer.Kill();
        Destroy(gameObject);
    }

    private static readonly Bounds drawBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    private static readonly int uvPropertyId = Shader.PropertyToID("uvRBuffer");
    private readonly Vector4[] uvArr = new Vector4[batchSize];
    private readonly Matrix4x4[] matArr = new Matrix4x4[batchSize];
    private const int batchSize = 1023;
}