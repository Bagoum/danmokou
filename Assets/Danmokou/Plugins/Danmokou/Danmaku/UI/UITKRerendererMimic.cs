using System;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Graphics;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.UI {
public class UITKRerenderer : Rendered {
    public int UITKRenderGroup { get; }

    public UITKRerenderer(int uitkRenderGroup) : base() {
        this.UITKRenderGroup = uitkRenderGroup;
    }
}
/// <summary>
/// Displays UITK render textures for groups other
///  than the 0 render group in <see cref="UIBuilderRenderer"/>.
/// </summary>
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
        ServiceLocator.Find<UIBuilderRenderer>().RTGroups.Subscribe(rtg => {
            if (rtg.TryGetValue(c.UITKRenderGroup, out var rt)) {
                sr.enabled = true;
                pb.SetTexture(PropConsts.renderTex, rt);
            } else {
                sr.enabled = false;
                pb.SetTexture(PropConsts.renderTex, unsetRT);
            }
            sr.SetPropertyBlock(pb);
        });
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