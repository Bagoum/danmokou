using System;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Pooling;
using Danmokou.Scriptables;
using UnityEngine;
using UnityEngine.Profiling;

namespace Danmokou.Danmaku.Descriptors {
public class Pather : FrameAnimBullet {
    public PatherRenderCfg config = null!;
    private CurvedTileRenderPather ctr = null!;

    protected override void Awake() {
        ctr = new CurvedTileRenderPather(config, gameObject);
        ctr.SetCameraCullable(InvokeCull);
        base.Awake();
    }

    private void Initialize(bool isNew, Movement movement, ParametricInfo pi, SOPlayerHitbox _target, float maxRemember,
        BPY remember, BEHStyleMetadata style, ref RealizedBehOptions options) {
        ctr.SetYScale(options.scale); //Needs to be done before Colorize sets first frame
        //Order is critical so rBPI override points to initialized data on SM start
        ctr.Initialize(this, config, style.RecolorOrThrow.material, isNew, movement, pi, remember, maxRemember, _target, ref options);
        base.Initialize(style, options, null, movement.WithNoMovement(), pi, _target, out _); // Call after Awake/Reset
        ctr.Activate(); //This invokes UpdateMesh
    }

    public override Vector2 GlobalPosition() => ctr.GlobalPosition;

    public override void RegularUpdateParallel() {
        if (nextUpdateAllowed) ctr.UpdateMovement(ETime.FRAME_TIME);
    }
    
    protected override void RegularUpdateMove() { }

    protected override void RegularUpdateRender() {
        base.RegularUpdateRender();
        ctr.UpdateRender();
    }

    protected override CollisionResult CollisionCheck() => ctr.CheckCollision();

    protected override void SetSprite(Sprite s, float yscale) {
        ctr.SetSprite(s, yscale);
    }

    public override void InvokeCull() {
        if (dying) return;
        ctr.Deactivate();
        base.InvokeCull();
    }

    public static void Request(BEHStyleMetadata style, Movement movement, ParametricInfo pi, float maxRemember, BPY remember, SOPlayerHitbox collisionTarget, ref RealizedBehOptions opts) {
        Pather created = (Pather) BEHPooler.RequestUninitialized(style.RecolorOrThrow.prefab, out bool isNew);
        created.Initialize(isNew, movement, pi, collisionTarget, maxRemember, remember, style, ref opts);
    }

    public override ref ParametricInfo rBPI => ref ctr.BPI;

    protected override void FlipVelX() => ctr.FlipVelX();

    protected override void FlipVelY() => ctr.FlipVelY();

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