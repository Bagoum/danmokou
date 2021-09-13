using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Pooling;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEditor;
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
    private void Initialize(bool isNew, BehaviorEntity? parent, Movement movement, ParametricInfo pi, SOPlayerHitbox _target, float cold, float hot, BEHStyleMetadata style, ref RealizedLaserOptions options) {
        pi.ctx.laserController = ctr;
        ctr.SetYScale(options.yScale * defaultWidthMultiplier); //Needs to be done before Colorize sets first frame
        base.Initialize(style, options.AsBEH, parent, movement, pi, _target, out _); // Call after Awake/Reset
        if (options.endpoint != null) {
            var beh = BEHPooler.INode(Movement.None, new ParametricInfo(Vector2.zero, bpi.index),
                Vector2.right, options.endpoint);
            endpt = new PointContainer(beh);
            ctr.SetupEndpoint(endpt);
        } else ctr.SetupEndpoint(new PointContainer(null));
        ctr.Initialize(this, config, style.RecolorOrThrow.material, isNew, bpi, ref options);
        ServiceLocator.SFXService.Request(options.firesfx);
        SetColdHot(cold, hot, options.hotsfx, options.repeat);
        ctr.Activate(); //This invokes UpdateMesh
    }
    
    public override void RegularUpdateParallel() {
        ctr.UpdateMovement(ETime.FRAME_TIME);
    }

    protected override void RegularUpdateRender() {
        base.RegularUpdateRender();
        ctr.UpdateRender();
    }

    protected override CollisionResult CollisionCheck() => ctr.CheckCollision(collisionTarget);

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

    public override void InvokeCull() {
        if (dying) return;
        ctr.Deactivate();
        if (endpt.exists) {
            endpt.beh!.InvokeCull();
            endpt = new PointContainer(null);
        }
        base.InvokeCull();
    }

    public static void Request(BEHStyleMetadata style, BehaviorEntity? parent, Movement vel, ParametricInfo pi, 
        float cold, float hot, SOPlayerHitbox collisionTarget, ref RealizedLaserOptions options) {
        Laser created = (Laser) BEHPooler.RequestUninitialized(style.RecolorOrThrow.prefab, out bool isNew);
        created.Initialize(isNew, parent, vel, pi, collisionTarget, cold, hot, style, ref options);
    }
    
    protected override void UpdateStyle(BEHStyleMetadata newStyle) {
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