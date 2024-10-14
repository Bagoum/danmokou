using System;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Pooling;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.Danmaku.Descriptors {
public class Pather : FrameAnimBullet {
    public PatherRenderCfg config = null!;
    private CurvedTileRenderPather ctr = null!;

    public override Vector2 Location => ctr.GlobalPosition;

    protected override void Awake() {
        ctr = new CurvedTileRenderPather(config, gameObject);
        ctr.SetCameraCullable(InvokeCull);
        base.Awake();
    }

    private void Initialize(bool isNew, in Movement mov, ParametricInfo pi, float maxRemember,
        BPY remember, BEHStyleMetadata style, ref RealizedBehOptions options) {
        ctr.SetYScale(options.scale); //Needs to be done before Colorize sets first frame
        //Order is critical so rBPI override points to initialized data on SM start
        // As a result we also need to assign ctx.bullet early so ctr.initialize can read it
        pi.ctx.bullet = this;
        ctr.Initialize(this, config, style.RecolorOrThrow.material, isNew, in mov, pi, remember, maxRemember, ref options);
        base.Initialize(style, options, null, mov.WithNoMovement(), pi, out _); // Call after Awake/Reset
        ctr.Activate(); //This invokes UpdateMesh
    }

    public override void RegularUpdateParallel() {
        if (nextUpdateAllowed) ctr.UpdateMovement(ETime.FRAME_TIME);
    }
    public override bool HasNontrivialParallelUpdate => true;
    
    protected override void RegularUpdateMove() { }

    public override void RegularUpdate() {
        base.RegularUpdate();
        if (!Dying)
            ctr.CullCheck();
    }

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

    protected override void CullHook(bool allowFinalize) {
        ctr.Deactivate();
        base.CullHook(allowFinalize);
    }

    public static void Request(BEHStyleMetadata style, in Movement mov, ParametricInfo pi, float maxRemember, BPY remember, ref RealizedBehOptions opts) {
        Pather created = (Pather) BEHPooler.RequestUninitialized(style.RecolorOrThrow.prefab, out bool isNew);
        created.Initialize(isNew, in mov, pi, maxRemember, remember, style, ref opts);
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