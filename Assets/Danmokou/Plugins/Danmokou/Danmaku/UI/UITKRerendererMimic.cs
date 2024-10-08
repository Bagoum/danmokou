using System;
using System.Collections.Generic;
using BagoumLib;
using Suzunoya.Display;
using Suzunoya.Entities;
using SuzunoyaUnity;
using SuzunoyaUnity.Mimics;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using PropConsts = Danmokou.Graphics.PropConsts;

namespace Danmokou.UI {

/* Potential implementation for rendering UITK contents directly to RenderGroup RT -
   not currently feasible since we can't control UITK render time, so it usually ends up rendering UI
   after the RenderGroup RT has been rendered/blitted to its final destination.
public class UITKToRGAdapter : IOverrideRenderTarget {
    public float Zoom => RG.Zoom;
    public Vector2 ZoomTarget => RG.ZoomTarget.Value._();
    public Vector2 Offset => RG.ComputedLocation.Value._();
    public RenderTexture WriteTo => RG.Captured;
    public UnityRenderGroup RG { get; }
    private List<IDisposable> tokens = new();
    
    public UITKToRGAdapter(UnityRenderGroup rg, int group) {
        this.RG = rg;
        var uiBuilder = ServiceLocator.Find<UIBuilderRenderer>();
        IDisposable? renderToken = null;
        var listenToken = uiBuilder.RTGroups.Subscribe(rtg => {
            renderToken?.Dispose();
            renderToken = rtg.GetValueOrDefault(group)?.Target.AddConst(this);
        });
        //no need to listen - this token will be destroyed by EntityActive.OnCompleted
        rg.EntityActive.Subscribe(s => {
            if (s >= EntityState.Deleted) {
                listenToken?.Dispose();
                renderToken?.Dispose();
            }
        });
    }
}*/

/// <summary>
/// Displays UITK render textures for groups configured in <see cref="UIBuilderRenderer"/>.
/// <br/>If a render group is already being rendered to screen by UIBuilderRenderer, then disables that
///  default rendering.
/// <example>
/// rerenderer = VN.Add(new UITKRerenderer(3), sortingID: 10000);
///</example>
/// </summary>
public class UITKRerenderer : Rendered, IOverrideRenderTarget {
    public int UITKRenderGroup { get; }

    private RenderGroup RG => RenderGroup.Value ?? throw new Exception("No render group for rerenderer");
    public float Zoom => RG.Zoom;
    public Vector2 ZoomTarget => RG.ZoomTarget.Value._();
    public Vector2 Offset => RG.ComputedLocation.Value._();

    public UITKRerenderer(int uitkRenderGroup) : base() {
        this.UITKRenderGroup = uitkRenderGroup;
        UseSortingIDAsReference = false;
    }
}

/// <inheritdoc cref="UITKRerenderer"/>
public class UITKRerendererMimic : RenderedMimic {
    public override Type[] CoreTypes => new[] { typeof(UITKRerenderer) };
    private SpriteRenderer sr = null!;
    private MaterialPropertyBlock pb = null!;
    public RenderTexture unsetRT = null!;

    protected override void Awake() {
        base.Awake();
        (sr = GetComponent<SpriteRenderer>()).GetPropertyBlock(pb = new MaterialPropertyBlock());
    }
    public override string SortingLayerFromPrefab => sr.sortingLayerName;
    public override void _Initialize(IEntity ent) => Initialize((ent as UITKRerenderer)!);

    private void Initialize(UITKRerenderer c) {
        base.Initialize(c);
        var uiBuilder = ServiceLocator.Find<UIBuilderRenderer>();
        Listen(uiBuilder.RTGroups, rtg => {
            renderToken?.Dispose();
            renderToken = null;
            if (rtg.TryGetValue(c.UITKRenderGroup, out var pane)) {
                sr.enabled = true;
                AddToken(renderToken = pane.Target.AddConst(c));
                pb.SetTexture(PropConsts.renderTex, pane.TempTexture);
            } else {
                sr.enabled = false;
                pb.SetTexture(PropConsts.renderTex, unsetRT);
            }
            sr.SetPropertyBlock(pb);
        });
    }

    private IDisposable? renderToken;

    protected override void SetSortingLayer(int layer) => sr.sortingLayerID = layer;

    protected override void SetSortingID(int id) => sr.sortingOrder = id;

    protected override void SetVisible(bool visible) => sr.enabled = visible;

    protected override void SetTint(Color c) => sr.color = c;
    
    protected override void EntityDestroyed() {
        pb.SetTexture(PropConsts.renderTex, unsetRT);
        sr.SetPropertyBlock(pb);
        base.OnDisable();
    }
}
}