using System;
using BagoumLib;
using Danmokou.Graphics;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.UI {

/// <summary>
/// Displays UITK render textures for groups configured in <see cref="UIBuilderRenderer"/>.
/// <br/>If a render group is already being rendered to screen by UIBuilderRenderer, then disables that
///  default rendering.
/// <example>
/// rerenderer = VN.Add(new UITKRerenderer(3), sortingID: 10000);
///</example>
/// </summary>
public class UITKRerenderer : Rendered {
    public int UITKRenderGroup { get; }

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
            if (rtg.TryGetValue(c.UITKRenderGroup, out var rt)) {
                sr.enabled = true;
                pb.SetTexture(PropConsts.renderTex, rt);
            } else {
                sr.enabled = false;
                pb.SetTexture(PropConsts.renderTex, unsetRT);
            }
            sr.SetPropertyBlock(pb);
        });
        AddToken(uiBuilder.DisableDefaultRenderingForGroup(c.UITKRenderGroup));
    }

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