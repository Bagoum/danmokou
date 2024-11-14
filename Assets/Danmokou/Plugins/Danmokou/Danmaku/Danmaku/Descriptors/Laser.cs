using System;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Pooling;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.Danmaku.Descriptors {
public class Laser : FrameAnimBullet {
    public float defaultWidthMultiplier;
    public LaserRenderCfg config = null!;
    public CurvedTileRenderLaser ctr = null!;
    public readonly struct PointContainer {
        public readonly BehaviorEntity? beh;
        public readonly bool exists;

        public PointContainer(BehaviorEntity? beh) {
            this.beh = beh;
            this.exists = beh != null;
        }
    }

    private PointContainer endpt;

    protected override void Awake() {
        ctr = new CurvedTileRenderLaser(config, gameObject);
        base.Awake();
    }
    private void Initialize(bool isNew, BehaviorEntity? parent, in Movement movement, ParametricInfo pi, float cold, float hot, StyleMetadata style, ref RealizedLaserOptions options) {
        pi.ctx.laserController = ctr;
        ctr.SetYScale(options.yScale * defaultWidthMultiplier); //Needs to be done before Colorize sets first frame
        base.Initialize(style, options.AsBEH, parent, in movement, pi, out _); // Call after Awake/Reset
        if (options.endpoint != null) {
            var beh = BEHPooler.INode(Movement.None, new ParametricInfo(Vector2.zero, bpi.index),
                Vector2.right, options.endpoint);
            endpt = new PointContainer(beh);
            ctr.SetupEndpoint(endpt);
        } else ctr.SetupEndpoint(new PointContainer(null));
        ctr.Initialize(this, config, style.RecolorOrThrow.material, isNew, bpi, ref options);
        ISFXService.SFXService.Request(options.firesfx);
        SetColdHot(cold, hot, options.hotsfx, options.repeat);
        ctr.Activate(); //This invokes UpdateMesh
    }
    
    public override void RegularUpdateParallel() {
        ctr.UpdateMovement(ETime.FRAME_TIME);
    }
    public override bool HasNontrivialParallelUpdate => true;

    public override void RegularUpdateCollision() {
        ctr.DoRegularUpdateCollision(collisionActive);
    }
    public override void RegularUpdateFinalize() {
        ctr.UpdateRender();
        base.RegularUpdateFinalize();
    }

    protected override void SetSprite(Sprite s, float yscale) {
        ctr.SetSprite(s, yscale);
    }

    /// <summary>
    /// TODO should I add support to FAB for time advancement?
    /// </summary>
    public override void SetTime(float t) {
        base.SetTime(t);
        ctr.SetLifetime(t);
    }

    protected override void CullHook(bool allowFinalize) {
        ctr.Deactivate();
        if (endpt.exists) {
            endpt.beh!.CullMe(allowFinalize);
            endpt = new PointContainer(null);
        }
        base.CullHook(allowFinalize);
    }

    public static void Request(StyleMetadata style, BehaviorEntity? parent, in Movement vel, ParametricInfo pi, 
        float cold, float hot, ref RealizedLaserOptions options) {
        var created = BEHPooler.RequestUninitialized(style.RecolorOrThrow.prefab, out bool isNew)
            as Laser ?? throw new Exception($"The object {style.style} is not a laser!");
        created.Initialize(isNew, parent, in vel, pi, cold, hot, style, ref options);
    }
    
    protected override void UpdateStyle(StyleMetadata newStyle) {
        base.UpdateStyle(newStyle);
        ctr.UpdateLaserStyle(newStyle.style);
    }

    protected override void SpawnSimple(string styleName) {
        ctr.SpawnSimple(styleName);
    }

    public V2RV2? Index(float time) => ctr.Index(time);
    
    private void OnDestroy() => ctr.Destroy();
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected() {
        ctr.Draw();
    }

    [ContextMenu("Debug mesh sorting layer")]
    public void DebugMeshSorting() {
        var m = GetComponent<MeshRenderer>();
        Debug.Log($"{m.sortingLayerName} {m.sortingOrder}");
    }
#endif
}
}