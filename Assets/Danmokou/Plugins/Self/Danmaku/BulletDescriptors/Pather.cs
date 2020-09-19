using System;
using DMath;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Danmaku {
public class Pather : Bullet {
    public PatherRenderCfg config;
    private CurvedTileRenderPather ctr;
    private Laser.PointContainer endpt;

    protected override void Awake() {
        ctr = new CurvedTileRenderPather(config, gameObject);
        rotationMethod = RotationMethod.Manual;
        ctr.SetCameraCullable(InvokeCull);
        base.Awake();
    }

    private void Initialize(bool isNew, Velocity velocity, SOCircleHitbox _target, int firingIndex, uint bpiid, float maxRemember,
        BPY remember, FrameAnimBullet.Recolor recolor, ref RealizedBehOptions options) {
        ctr.SetYScale(options.scale); //Needs to be done before Colorize sets first frame
        Colorize(recolor);
        //Order is critical so rBPI override points to initialized data on SM start
        ctr.Initialize(this, config, material, isNew, velocity, bpiid, firingIndex, remember, maxRemember, _target, ref options);
        base.Initialize(options, null, velocity.WithNoMovement(), firingIndex, bpiid, _target, out int layer); // Call after Awake/Reset
        ctr.trailR.gameObject.layer = layer;
        ctr.Activate(); //This invokes UpdateMesh
    }

    public override Vector2 GlobalPosition() => ctr.GlobalPosition;

    public override void RegularUpdateParallel() {
        if (nextUpdateAllowed) ctr.UpdateMovement(ETime.FRAME_TIME);
    }
    
    protected override void RegularUpdateMove() { }

    protected override void RegularUpdateRender() {
        //base.RegularUpdateRender();
        ctr.UpdateRender();
    }

    protected override DMath.CollisionResult CollisionCheck() => ctr.CheckCollision();

    protected override void SetSprite(Sprite s, float yscale = 1f) {
        ctr.SetSprite(s, yscale);
    }

    public override void InvokeCull() {
        if (dying) return;
        ctr.Deactivate();
        base.InvokeCull();
    }

    public static void Request(FrameAnimBullet.Recolor recolor, Velocity velocity, int firingIndex, uint bpiid, float maxRemember, DMath.BPY remember, SOCircleHitbox collisionTarget, ref RealizedBehOptions opts) {
        Pather created = (Pather) BEHPooler.RequestUninitialized(recolor.prefab, out bool isNew);
        created.Initialize(isNew, velocity, collisionTarget, firingIndex, bpiid, maxRemember, remember, recolor, ref opts);
    }

    public override ref DMath.ParametricInfo rBPI => ref ctr.BPI;

    protected override Vector2 GetGlobalDirection() {
        return ctr.GetGlobalDirection();
    }

    protected override void FlipVelX() => ctr.FlipVelX();

    protected override void FlipVelY() => ctr.FlipVelY();

    protected override void UpdateStyleCullable() {
        base.UpdateStyleCullable();
        ctr.SetCameraCullable(styleIsCameraCullable);
    }

    protected override void SpawnSimple(string styleName) {
        ctr.SpawnSimple(styleName);
    }

    private void OnDestroy() => ctr.Destroy();


#if UNITY_EDITOR
    private void OnDrawGizmosSelected() {
        ctr.Draw();
    }

    [ContextMenu("Debug mesh bounds")]
    public void DebugMeshBounds() => ctr.DebugMeshBounds();
#endif
}
}