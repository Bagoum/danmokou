using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommunityToolkit.HighPerformance;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Descriptors;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Reflection;
using UnityEngine;

public class BulletBlocker : CoroutineRegularUpdater, IBehaviorEntityDependent, 
    IColliderEnemySimpleBulletCollisionReceiver,
    IEnemyLaserCollisionReceiver {
    public ICollider Collider { get; private set; } = null!;
    private RectCollider? rectCollider;
    public Collider2D? unityCollider;
    private BehaviorEntity beh = null!;
    [ReflectInto(typeof(BulletManager.StyleSelector))]
    public string affectedStyles = "";
    private BulletManager.StyleSelector Styles = null!;

    public Vector2 Location => beh.Location + M.RotateVector(colliderOffset, Direction);
    private Vector2 colliderOffset = Vector2.zero;
    public Vector2 Direction { get; private set; } = Vector2.right;
    public bool ReceivesBulletCollisions(string style) => Styles.Matches(style);

    private void Awake() {
        beh = GetComponent<BehaviorEntity>();
        beh.LinkDependentUpdater(this);
        Collider = GetCollider(this, unityCollider, ref colliderOffset);
        rectCollider = Collider as RectCollider;
        Styles = affectedStyles.Into<BulletManager.StyleSelector>();
    }

    public static ICollider GetCollider(MonoBehaviour go, Collider2D? unityCollider, ref Vector2 colliderOffset) {
        switch (unityCollider) {
            case BoxCollider2D box:
                var size = box.size;
                var scale = box.transform.lossyScale;
                colliderOffset = box.offset;
                return new RectCollider(size.x * scale.x / 2f, size.y * scale.y / 2f);
            default:
                return go.GetComponent<GenericColliderInfo>().AsCollider;
        }
    }


    public void Alive() {
        EnableUpdates();
    }

    protected override void BindListeners() {
        RegisterService<IEnemySimpleBulletCollisionReceiver>(this);
        RegisterService<IEnemyLaserCollisionReceiver>(this);
        base.BindListeners();
    }

    public void Died() {
        DisableUpdates();
    }

    public override void RegularUpdate() {
        Direction = M.CosSinDeg(beh.RotatorRotation ?? 0f);
        base.RegularUpdate();
    }

    public bool CollidesWithPool(BulletManager.SimpleBulletCollection sbc) =>
        sbc.MetaType == BulletManager.SimpleBulletCollection.CollectionType.Normal;


    public bool TakeHit(int damage, in ParametricInfo bulletBPI, ushort grazeEveryFrames) {
        return true;
    }

    CollisionResult IEnemyLaserCollisionReceiver.Check(CurvedTileRenderLaser laser, Vector2 laserLoc, float cos, float sin, out int segment, out Vector2 collLoc) {
        if (ReceivesBulletCollisions(laser.Laser.myStyle.style ?? "") &&
            laser.ComputeRectCollision(laserLoc, cos, sin, Location, rectCollider!.halfRect, Direction, out segment,
                out collLoc)) {
            return new(true, false);
        } else {
            segment = 0;
            collLoc = Vector2.zero;
            return CollisionMath.NoCollision;
        }
    }

    void IEnemyLaserCollisionReceiver.ProcessActual(CurvedTileRenderLaser laser, Vector2 laserLoc, float cos, float sin, CollisionResult coll,
        Vector2 collLoc) {
        if (coll.collide || coll.graze)
            TakeHit(1, in laser.BPI, 0);
    }

    [ContextMenu("Debug matches")]
    public void DebugMatches() {
        var ss = affectedStyles.Into<BulletManager.StyleSelector>();
        var sb = new StringBuilder();
        foreach (var s in BulletManager.SIMPLEBULLETKEYS.OrderBy(x => RNG.GetInt(0, 100)).Take(50))
            sb.Append($"{s}: {ss.Matches(s)}\n");
        Logs.Log(sb.ToString());
    }
}