using BagoumLib.Functional;
using Danmokou.Behavior;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Scriptables;
using UnityEngine;

public class EnemySelfBulletCollision : CoroutineRegularUpdater, ICircularGrazableEnemySimpleBulletCollisionReceiver {
    private Enemy enemy = null!;
    public EffectStrategy? onHit;
    public Hurtbox Hurtbox { get; private set; }
    
    private void Awake() {
        enemy = GetComponent<Enemy>();
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IEnemySimpleBulletCollisionReceiver>(this);
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        Hurtbox = new(enemy.Location, enemy.CollisionRadius);
    }

    public bool ReceivesBulletCollisions(string? style) => style is not null;

    bool ICircularGrazableEnemySimpleBulletCollisionReceiver.TakeHit(BulletManager.SimpleBulletCollection sbc, CollisionResult coll, int damage, in ParametricInfo bulletBPI) {
        if (sbc.MetaType == BulletManager.SimpleBulletCollection.CollectionType.Normal &&
            bulletBPI.ctx.envFrame.MaybeGetValue<bool>("reflected").ValueOrSNull() is true) {
            return enemy.TakeHit(onHit, new(new(40, sbc.BC.Destructible, 1, 1), null), bulletBPI.loc, bulletBPI.id);
        } else
            return false;
    }
}