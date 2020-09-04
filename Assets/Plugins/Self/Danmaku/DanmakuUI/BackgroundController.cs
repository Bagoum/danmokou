using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A component which controls the display of a (dynamic) background image.
/// </summary>
public class BackgroundController : CoroutineRegularUpdater {
    protected Transform tr;
    public Color tint;
    private MaterialPropertyBlock fragmentPB;
    public ArbitraryCapturer capturer;

    private BackgroundTransition.ShatterConfig currentShatter;
    private Action shatterCB;
    
    private readonly List<Kakera> fragments = new List<Kakera>();
    private readonly List<RenderTexture> tempTex = new List<RenderTexture>();
    private int fragRenderLayer;
    private int fragRenderMask;
    private int arb1Layer;
    private int arb1Mask;
    private int arb2Layer;
    private int arb2Mask;
    protected int DrawToLayer { get; private set; }
    [CanBeNull] public GameObject source { get; private set; }
    protected virtual void Awake() {
        tr = transform;
        fragRenderLayer = LayerMask.NameToLayer("LowDirectRender");
        fragRenderMask = LayerMask.GetMask("LowDirectRender");
        arb1Layer = LayerMask.NameToLayer("ARBITRARY_CAPTURE_1");
        arb1Mask = LayerMask.GetMask("ARBITRARY_CAPTURE_1");
        arb2Layer = LayerMask.NameToLayer("ARBITRARY_CAPTURE_2");
        arb2Mask = LayerMask.GetMask("ARBITRARY_CAPTURE_2");
        fragmentPB = new MaterialPropertyBlock();
        if (capturer == null) capturer = MainCamera.CreateArbitraryCapturer(tr);
    }

    private void Start() => AssignLayers();

    private static int nextLayer = 0;
    private void AssignLayers() {
        nextLayer = (nextLayer + 1) % 2;
        var (layer, mask) = nextLayer == 0 ? (arb1Layer, arb1Mask) : (arb2Layer, arb2Mask);
        capturer.Camera.cullingMask = mask;
        SetLayerRecursively(gameObject, DrawToLayer = layer);
    }

    private static void SetLayerRecursively(GameObject o, int layer) {
        if (o == null) return;
        o.layer = layer;
        foreach (Transform ch in o.transform) {
            SetLayerRecursively(ch.gameObject, layer);
        }
    }

    public BackgroundController Initialize(GameObject prefab) {
        source = prefab;
        return this;
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
        var fragmentTex = capturer.Captured;
        if (doCopy) {
            fragmentTex = fragmentTex.CopyAsTemp();
            tempTex.Add(fragmentTex);
        }
        fragmentPB.SetTexture(PropConsts.mainTex, fragmentTex);
        currentShatter = config;
        currentShatter.Tile4(fragments);
        shatterCB = cb;
    } 

    protected virtual void Render(Camera c) {
        if (!Application.isPlaying) return;
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
        capturer.Kill();
        Destroy(gameObject);
    }

    private static readonly Bounds drawBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    private static readonly int uvPropertyId = Shader.PropertyToID("uvRBuffer");
    private readonly Vector4[] uvArr = new Vector4[batchSize];
    private readonly Matrix4x4[] matArr = new Matrix4x4[batchSize];
    private const int batchSize = 1023;
}
