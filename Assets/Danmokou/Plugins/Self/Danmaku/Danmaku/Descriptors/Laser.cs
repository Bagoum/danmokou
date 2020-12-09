using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku.Options;
using DMK.DMath;
using DMK.Graphics;
using DMK.Pooling;
using DMK.Scriptables;
using DMK.Services;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace DMK.Danmaku.Descriptors {
public class Laser : FrameAnimBullet {
    public float defaultWidthMultiplier;
    public LaserRenderCfg config;
    public CurvedTileRenderLaser ctr;
    public readonly struct PointContainer {
        [CanBeNull] public readonly BehaviorEntity beh;
        public readonly bool exists;

        public PointContainer([CanBeNull] BehaviorEntity beh) {
            this.beh = beh;
            this.exists = beh != null;
        }
    }

    private PointContainer endpt;

    protected override void Awake() {
        ctr = new CurvedTileRenderLaser(config, gameObject);
        base.Awake();
    }
    private void Initialize(bool isNew, BehaviorEntity parent, Movement movement, SOPlayerHitbox _target, int firingIndex, uint bpiid, float cold, float hot, BEHStyleMetadata style, ref RealizedLaserOptions options) {
        ctr.SetYScale(options.yScale * defaultWidthMultiplier); //Needs to be done before Colorize sets first frame
        base.Initialize(style, options.AsBEH, parent, movement, firingIndex, bpiid, _target, out _); // Call after Awake/Reset
        if (options.endpoint != null) {
            var beh = BEHPooler.INode(Vector2.zero, V2RV2.Zero, Vector2.right, firingIndex, null, options.endpoint);
            endpt = new PointContainer(beh);
            ctr.SetupEndpoint(endpt);
        } else ctr.SetupEndpoint(new PointContainer(null));
        ctr.Initialize(this, config, style.recolor.GetOrLoadRecolor().material, isNew, bpi.id, firingIndex, ref options);
        SFXService.Request(options.firesfx);
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

    protected override DMath.CollisionResult CollisionCheck() => ctr.CheckCollision(collisionTarget);

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
            endpt.beh.InvokeCull();
            endpt = new PointContainer(null);
        }
        base.InvokeCull();
    }

    public static void Request(BEHStyleMetadata style, BehaviorEntity parent, Movement vel, int firingIndex, uint bpiid, 
        float cold, float hot, SOPlayerHitbox collisionTarget, ref RealizedLaserOptions options) {
        Laser created = (Laser) BEHPooler.RequestUninitialized(style.recolor.GetOrLoadRecolor().prefab, out bool isNew);
        created.Initialize(isNew, parent, vel, collisionTarget, firingIndex, bpiid, cold, hot, style, ref options);
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