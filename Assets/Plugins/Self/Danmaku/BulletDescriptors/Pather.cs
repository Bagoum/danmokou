using System;
using DMath;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Danmaku {
public class Pather : FrameAnimBullet {
    private CurvedTileRenderPather ctr;
    private Laser.PointContainer endpt;

    protected override void Awake() {
        ctr = GetComponent<CurvedTileRenderPather>();
        rotationMethod = RotationMethod.Manual;
        ctr.SetCameraCullable(InvokeCull);
        base.Awake();
    }

    private void Initialize(bool isNew, Velocity velocity, SOCircle _target, int firingIndex, uint bpiid, float maxRemember,
        BPY remember, Recolor recolor, ref RealizedBehOptions options) {
        ctr.SetYScale(options.scale); //Needs to be done before Colorize sets first frame
        Colorize(recolor);
        //Order is critical so rBPI override points to initialized data on SM start
        ctr.Initialize(this, material, isNew, velocity, bpiid, firingIndex, remember, maxRemember, ref options);
        base.Initialize(options, null, velocity.WithNoMovement(), firingIndex, bpiid, _target); // Call after Awake/Reset
        SetColdHot(0f, float.MaxValue);
        ctr.Activate(); //This invokes UpdateMesh
    }

    public override Vector2 GlobalPosition() => ctr.GlobalPosition;

    public override void RegularUpdateParallel() {
        if (nextUpdateAllowed) ctr.UpdateMovement(1, ETime.FRAME_TIME);
    }

    protected override void RegularUpdateRender() {
        base.RegularUpdateRender();
        ctr.UpdateRender();
    }

    protected override DMath.CollisionResult CollisionCheck() {
        return ctr.CheckCollision(collisionTarget);
    }

    protected override void SetSprite(Sprite s, float yscale) {
        ctr.SetSprite(s, yscale);
    }

    public override void InvokeCull() {
        if (dying) return;
        ctr.Deactivate();
        base.InvokeCull();
    }

    public static void Request(Recolor recolor, Velocity velocity, int firingIndex, uint bpiid, float maxRemember, DMath.BPY remember, SOCircle collisionTarget, ref RealizedBehOptions opts) {
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
}
}