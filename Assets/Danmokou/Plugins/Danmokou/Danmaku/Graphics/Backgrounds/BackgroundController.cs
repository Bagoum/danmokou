using System;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Services;
using JetBrains.Annotations;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using static Danmokou.Graphics.FragmentRendering;
using Object = UnityEngine.Object;

namespace Danmokou.Graphics.Backgrounds {
/// <summary>
/// A component which controls the display of a (dynamic) background image.
/// </summary>
public class BackgroundController : CoroutineRegularUpdater {
    protected Transform tr = null!;
    public ArbitraryCapturer capturer = null!;
    public bool runWhileHidden;

    private FragmentRenderInstance? currentShatter;

    private int arb1Layer;
    private int arb1Mask;
    private int arb2Layer;
    private int arb2Mask;
    private int arbNullLayer;
    protected int DrawToLayer { get; private set; }
    public GameObject? source { get; private set; }
    protected BackgroundOrchestrator Orchestrator { get; private set; } = null!;

    protected virtual void Awake() {
        tr = transform;
        arb1Layer = LayerMask.NameToLayer("ARBITRARY_CAPTURE_1");
        arb1Mask = LayerMask.GetMask("ARBITRARY_CAPTURE_1");
        arb2Layer = LayerMask.NameToLayer("ARBITRARY_CAPTURE_2");
        arb2Mask = LayerMask.GetMask("ARBITRARY_CAPTURE_2");
        arbNullLayer = LayerMask.NameToLayer("ARBITRARY_CAPTURE_NULL");
        if (capturer == null)
            capturer = Object.Instantiate(GameManagement.Prefabs.arbitraryCapturer, tr, false)
                .GetComponent<ArbitraryCapturer>();
    }

    private void Start() => AssignLayersNext();

    private static int nextLayer = 0;

    private void AssignLayersNext() {
        nextLayer = (nextLayer + 1) % 2;
        var (layer, mask) = nextLayer == 0 ? (arb1Layer, arb1Mask) : (arb2Layer, arb2Mask);
        _AssignLayers(layer, mask);
    }

    private void _AssignLayers(int layer, int mask) {
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

    public virtual BackgroundController Initialize(GameObject prefab, BackgroundOrchestrator orchestrator) {
        source = prefab;
        Orchestrator = orchestrator;
        return this;
    }

    private const float YCUTOFF = -10;

    public override void RegularUpdate() {
        base.RegularUpdate();
        if (currentShatter?.DoUpdate() == true)
            currentShatter = null;
    }

    //Note: while you can call Fragment multiple times, only one texture can be shared between all calls,
    //so it's generally not useful to do so. Also, it causes overlap flashing.
    public void Shatter4(BackgroundTransition.ShatterConfig config, bool doCopy, Action cb) {
        currentShatter?.Destroy();
        currentShatter =
            new FragmentRenderInstance(config, config.Tile4(), null, capturer.Captured, cb);
    }

    private void Render(Camera c) {
        if (!Application.isPlaying) return;
        //Effects render to LowEffects
        FragmentRendering.Render(c, currentShatter);
    }

    protected override void OnEnable() {
        Camera.onPreCull += Render;
        base.OnEnable();
    }

    protected override void OnDisable() {
        Camera.onPreCull -= Render;
        base.OnDisable();
    }

    public void Hide() {
        if (runWhileHidden) {
            _AssignLayers(arbNullLayer, 0);
            capturer.Camera.gameObject.SetActive(false);
        } else {
            gameObject.SetActive(false);
        }
    }

    public void Show() {
        capturer.Camera.gameObject.SetActive(true);
        gameObject.SetActive(true);
        AssignLayersNext();
    }

    public void Kill() {
        capturer.Kill();
        Destroy(gameObject);
    }
}
}
