using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.HighPerformance;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Descriptors;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Reflection;
using UnityEngine;

public class BulletBlocker : CoroutineRegularUpdater, IBehaviorEntityDependent, IColliderEnemySimpleBulletCollisionReceiver {
    public ICollider Collider { get; private set; } = null!;
    public Collider2D? unityCollider;
    private BehaviorEntity beh = null!;
    [ReflectInto(typeof(BulletManager.StyleSelector))]
    public string affectedStyles = "";
    private HashSet<string> Styles = null!;

    public Vector2 Location => beh.Location;
    public Vector2 Direction { get; private set; } = Vector2.right;
    public bool ReceivesBulletCollisions => true;

    private void Awake() {
        beh = GetComponent<BehaviorEntity>();
        beh.LinkDependentUpdater(this);
        Collider = GetCollider(this, unityCollider);
        Styles = affectedStyles.Into<BulletManager.StyleSelector>().Simple.ToHashSet();
    }

    public static ICollider GetCollider(MonoBehaviour go, Collider2D? unityCollider) {
        switch (unityCollider) {
            case BoxCollider2D box:
                var size = box.size;
                var scale = box.transform.lossyScale;
                return new RectCollider(size.x * scale.x / 2f, size.y * scale.y / 2f);
            default:
                return go.GetComponent<GenericColliderInfo>().AsCollider;
        }
    }

    protected override void BindListeners() {
        RegisterService<IEnemySimpleBulletCollisionReceiver>(this);
        base.BindListeners();
    }

    public override void RegularUpdate() {
        Direction = M.CosSinDeg(beh.RotatorRotation ?? 0f);
        base.RegularUpdate();
    }

    public bool CollidesWithPool(BulletManager.SimpleBulletCollection sbc) =>
        sbc.MetaType == BulletManager.SimpleBulletCollection.CollectionType.Normal && Styles.Contains(sbc.Style);


    public bool TakeHit(int damage, in ParametricInfo bulletBPI, ushort grazeEveryFrames) {
        return true;
    }

    public void Alive() {
        Direction = Vector2.right;
    }
}