using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.DataStructures;
using CommunityToolkit.HighPerformance;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.Danmaku {
/// <summary>
/// An entity that can receive collisions from simple bullets, such as players (which receive collisions from enemy bullets) or enemies (which receive collisions from player bullets).
/// <br/>Do not implement directly. Use <see cref="IPlayerSimpleBulletCollisionReceiver"/>, <see cref="IEnemySimpleBulletCollisionReceiver"/>, or one of their child interfaces.
/// </summary>
public interface ISimpleBulletCollisionReceiver {
    /// <summary>
    /// Process collisions for all bullets in a simple bullet pool.
    /// </summary>
    void ProcessSimple(BulletManager.SimpleBulletCollection sbc);

    /// <summary>
    /// Process collisions for bullets in a simple bullet pool.
    /// <br/>This will be called instead of <see cref="ProcessSimple"/> if the
    ///  pool has been bucketed.
    /// </summary>
    void ProcessSimpleBucketed(BulletManager.SimpleBulletCollection sbc, BulletManager.FrameBucketing frame);
}

/// <summary>
/// Partial implementation of <see cref="ISimpleBulletCollisionReceiver"/> that handles bucketing when the
///  entity is approximately circular.
/// <br/>Do not implement alone. Use with <see cref="IPlayerSimpleBulletCollisionReceiver"/> or <see cref="IEnemySimpleBulletCollisionReceiver"/>, or use one of their child interfaces.
/// </summary>
public interface IApproximatelyCircularSimpleBulletCollisionReceiver : ISimpleBulletCollisionReceiver {
    /// <summary>
    /// The location of the collidee. Used for determining buckets.
    /// </summary>
    Vector2 Location { get; }
    
    /// <summary>
    /// The maximum distance from <see cref="Location"/> at which a collision can occur. Used for determining buckets.
    /// </summary>
    float MaxCollisionRadius { get; }
    
    /// <summary>
    /// True if the collidee can receive collisions from the given style.
    /// </summary>
    bool ReceivesBulletCollisions(string? style);

    bool CollidesWithPool(BulletManager.SimpleBulletCollection sbc) => true;

    void ISimpleBulletCollisionReceiver.ProcessSimpleBucketed(BulletManager.SimpleBulletCollection sbc,
        BulletManager.FrameBucketing frame) {
        if (!ReceivesBulletCollisions(sbc.Style) || !CollidesWithPool(sbc)) return;
        var mcd = MaxCollisionRadius + sbc.Collider.MaxRadius * frame.maxScale;
        var mcd2 = new Vector2(mcd, mcd);
        ProcessSimpleBuckets(sbc, sbc.BucketsSpanForPosition(Location - mcd2, Location + mcd2));
    }

    /// <summary>
    /// Process collisions for a set of buckets in a simple bullet pool.
    /// <br/>See <see cref="ISimpleBulletCollisionReceiver.ProcessSimpleBucketed"/>
    /// </summary>
    void ProcessSimpleBuckets(BulletManager.SimpleBulletCollection sbc, ReadOnlySpan2D<List<int>> indexBuckets);
}
/// <summary>
/// An entity that can receive collisions from simple bullets fired by the player.
/// </summary>
public interface IPlayerSimpleBulletCollisionReceiver : ISimpleBulletCollisionReceiver { }

/// <summary>
/// An entity that can receive collisions from simple bullets fired by enemies.
/// </summary>
public interface IEnemySimpleBulletCollisionReceiver : ISimpleBulletCollisionReceiver { }

/// <summary>
/// Implementation of <see cref="IPlayerSimpleBulletCollisionReceiver"/> that handles collision when the
///  entity is circular. Used by enemies.
/// </summary>
public interface ICircularPlayerSimpleBulletCollisionReceiver : IPlayerSimpleBulletCollisionReceiver, IApproximatelyCircularSimpleBulletCollisionReceiver {
    /// <summary>
    /// The radius of the collidee's hitbox.
    /// </summary>
    float CollisionRadius { get; }

    float IApproximatelyCircularSimpleBulletCollisionReceiver.MaxCollisionRadius => CollisionRadius;

    /// <summary>
    /// Receive a hit from a player bullet.
    /// </summary>
    /// <param name="plb">Player bullet configuration.</param>
    /// <param name="bpi">Bullet information.</param>
    /// <returns>True iff the hit occured (may return false if the bullet is configured to do no damage or is on cooldown)</returns>
    bool TakeHit(in PlayerBullet plb, in ParametricInfo bpi);
    
    void ISimpleBulletCollisionReceiver.ProcessSimple(BulletManager.SimpleBulletCollection sbc) {
        throw new NotImplementedException("Assumed that player bullet x enemy collisions must be bucketed");
    }
    void IApproximatelyCircularSimpleBulletCollisionReceiver.ProcessSimpleBuckets(BulletManager.SimpleBulletCollection sbc, ReadOnlySpan2D<List<int>> indexBuckets) {
        var deleted = sbc.Deleted;
        var data = sbc.Data;
        var sbCollider = sbc.Collider;
        var loc = Location;
        float rad = CollisionRadius;
        for (int y = 0; y < indexBuckets.Height; ++y)
        for (int x = 0; x < indexBuckets.Width; ++x) {
            var bucket = indexBuckets[y, x];
            for (int ib = 0; ib < bucket.Count; ++ib) {
                var index = bucket[ib];
                if (deleted[index]) continue;
                ref var sbn = ref data[index];
                if (sbCollider.CheckCollision(in sbn.bpi.loc.x, in sbn.bpi.loc.y, in sbn.direction, in sbn.scale, in loc.x, in loc.y, in rad)
                    && sbn.bpi.ctx.playerBullet.Try(out var plb)
                    && TakeHit(plb, in sbn.bpi)) {
                    sbc.RunCollisionControls(index);
                    if (plb.data.destructible) {
                        sbc.MakeCulledCopy(index);
                        sbc.DeleteSB(index);
                    }
                }
            }
        }
    }
}

/// <summary>
/// Implementation <see cref="IEnemySimpleBulletCollisionReceiver"/> that handles collision when the entity
///  is circular and has a grazebox. Used by the player.
/// </summary>
public interface ICircularGrazableEnemySimpleBulletCollisionReceiver : IEnemySimpleBulletCollisionReceiver,
    IApproximatelyCircularSimpleBulletCollisionReceiver {
    /// <summary>
    /// The collision information of the collidee.
    /// </summary>
    Hurtbox Hurtbox { get; }

    float IApproximatelyCircularSimpleBulletCollisionReceiver.MaxCollisionRadius => Hurtbox.grazeRadius;

    /// <summary>
    /// Process a collision event with an NPC bullet.
    /// </summary>
    /// <param name="meta">Type of the bullet collection.</param>
    /// <param name="coll">Collision result.</param>
    /// <param name="damage">Damage dealt by this collision.</param>
    /// <param name="bulletBPI">Bullet information.</param>
    /// <param name="grazeEveryFrames">The number of frames between successive graze events on this bullet.</param>
    /// <returns>Whether or not a hit was taken (ie. a collision occurred).</returns>
    bool TakeHit(BulletManager.SimpleBulletCollection.CollectionType meta,
        CollisionResult coll, int damage, in ParametricInfo bulletBPI, ushort grazeEveryFrames);
    
    void ISimpleBulletCollisionReceiver.ProcessSimple(BulletManager.SimpleBulletCollection sbc) {
        if (!ReceivesBulletCollisions(sbc.Style) || !CollidesWithPool(sbc)) return;
        var hb = Hurtbox;
        var deleted = sbc.Deleted;
        var data = sbc.Data;
        var dmg = sbc.BC.Damage.Value;
        var allowGraze = sbc.BC.AllowGraze.Value;
        var destroy = sbc.BC.Destructible.Value;
        var meta = sbc.MetaType;
        var cc = sbc.BC.cc;
        var cType = cc.colliderType;
        for (int ii = 0; ii < sbc.Count; ++ii) {
            if (!deleted[ii]) {
                ref var sb = ref data[ii];
                CollisionResult cr;
                switch (cType) {
                    case GenericColliderInfo.ColliderType.Circle:
                        cr = CollisionMath.GrazeCircleOnCircle(in hb, in sb.bpi.loc.x, in sb.bpi.loc.y, in cc.radius, in sb.scale);
                        break;
                    case GenericColliderInfo.ColliderType.Rectangle:
                        cr = CollisionMath.GrazeCircleOnRect(in hb, in sb.bpi.loc.x, in sb.bpi.loc.y, in cc.halfRect, in cc.maxDist2, in sb.scale, in sb.direction);
                        break;
                    case GenericColliderInfo.ColliderType.Line:
                        cr = CollisionMath.GrazeCircleOnRotatedSegment(in hb, in sb.bpi.loc.x, in sb.bpi.loc.y, in cc.radius, in cc.linePt1, 
                            in cc.delta, in sb.scale, in cc.deltaMag2, in cc.maxDist2, in sb.direction);
                        break;
                    default:
                        cr = CollisionMath.NoCollision;
                        break;
                }
                if (cr.graze && !allowGraze)
                    cr = cr.NoGraze();
                if ((cr.graze || cr.collide)
                    && TakeHit(meta, cr, dmg, in sb.bpi, sbc.BC.grazeEveryFrames)) {
                    sbc.RunCollisionControls(ii);
                    if (destroy) {
                        sbc.MakeCulledCopy(ii);
                        sbc.DeleteSB(ii);
                    }
                }
            }
        }
    }

    void IApproximatelyCircularSimpleBulletCollisionReceiver.ProcessSimpleBuckets(BulletManager.SimpleBulletCollection sbc, ReadOnlySpan2D<List<int>> indexBuckets) {
        var hb = Hurtbox;
        var deleted = sbc.Deleted;
        var data = sbc.Data;
        var dmg = sbc.BC.Damage.Value;
        var allowGraze = sbc.BC.AllowGraze.Value;
        var destroy = sbc.BC.Destructible.Value;
        var meta = sbc.MetaType;
        var cc = sbc.BC.cc;
        var cType = cc.colliderType;
        for (int y = 0; y < indexBuckets.Height; ++y)
        for (int x = 0; x < indexBuckets.Width; ++x) {
            var bucket = indexBuckets[y, x];
            for (int ib = 0; ib < bucket.Count; ++ib) {
                var index = bucket[ib];
                if (deleted[index]) continue;
                ref var sb = ref data[index];
                CollisionResult cr;
                switch (cType) {
                    case GenericColliderInfo.ColliderType.Circle:
                        cr = CollisionMath.GrazeCircleOnCircle(in hb, in sb.bpi.loc.x, in sb.bpi.loc.y, in cc.radius, in sb.scale);
                        break;
                    case GenericColliderInfo.ColliderType.Rectangle:
                        cr = CollisionMath.GrazeCircleOnRect(in hb, in sb.bpi.loc.x, in sb.bpi.loc.y, in cc.halfRect, in cc.maxDist2, in sb.scale, in sb.direction);
                        break;
                    case GenericColliderInfo.ColliderType.Line:
                        cr = CollisionMath.GrazeCircleOnRotatedSegment(in hb, in sb.bpi.loc.x, in sb.bpi.loc.y, in cc.radius, in cc.linePt1, 
                            in cc.delta, in sb.scale, in cc.deltaMag2, in cc.maxDist2, in sb.direction);
                        break;
                    default:
                        cr = CollisionMath.NoCollision;
                        break;
                }
                if (cr.graze && !allowGraze)
                    cr = cr.NoGraze();
                if ((cr.graze || cr.collide) 
                    && TakeHit(meta, cr, dmg, in sb.bpi, sbc.BC.grazeEveryFrames)) {
                    sbc.RunCollisionControls(index);
                    if (destroy) {
                        sbc.MakeCulledCopy(index);
                        sbc.DeleteSB(index);
                    }
                }
            }
        }
    }
    
}

/// <summary>
/// Implementation <see cref="IEnemySimpleBulletCollisionReceiver"/> that handles collision when the entity
///  has a generic collider, by approximating the enemy bullets as circular. Used by obstacles.
/// </summary>
public interface IColliderEnemySimpleBulletCollisionReceiver : IEnemySimpleBulletCollisionReceiver,
    IApproximatelyCircularSimpleBulletCollisionReceiver {
    ICollider Collider { get; }
    float IApproximatelyCircularSimpleBulletCollisionReceiver.MaxCollisionRadius => Collider.MaxRadius;
    Vector2 Direction { get; }
    
    /// <summary>
    /// Process a collision event with an NPC bullet.
    /// </summary>
    /// <param name="damage">Damage dealt by this collision.</param>
    /// <param name="bulletBPI">Bullet information.</param>
    /// <param name="grazeEveryFrames">The number of frames between successive graze events on this bullet.</param>
    /// <returns>Whether or not a hit was taken (ie. a collision occurred).</returns>
    bool TakeHit(int damage, in ParametricInfo bulletBPI, ushort grazeEveryFrames);
    
    void ISimpleBulletCollisionReceiver.ProcessSimple(BulletManager.SimpleBulletCollection sbc) {
        throw new NotImplementedException();
    }

    void IApproximatelyCircularSimpleBulletCollisionReceiver.ProcessSimpleBuckets(BulletManager.SimpleBulletCollection sbc, ReadOnlySpan2D<List<int>> indexBuckets) {
        var deleted = sbc.Deleted;
        var data = sbc.Data;
        var dmg = sbc.BC.Damage.Value;
        var destroy = sbc.BC.Destructible.Value;
        //Approximate the bullets as circles
        var cr = sbc.BC.cc.circleCollider.Radius;
        var collider = Collider;
        var loc = Location;
        var rot = Direction;
        for (int y = 0; y < indexBuckets.Height; ++y)
        for (int x = 0; x < indexBuckets.Width; ++x) {
            var bucket = indexBuckets[y, x];
            for (int ib = 0; ib < bucket.Count; ++ib) {
                var index = bucket[ib];
                if (deleted[index]) continue;
                ref var sb = ref data[index];
                if (collider.CheckCollision(in loc.x, in loc.y, rot, 1, in sb.bpi.loc.x, in sb.bpi.loc.y, cr)
                    && TakeHit(dmg, in sb.bpi, sbc.BC.grazeEveryFrames)) {
                    sbc.RunCollisionControls(index);
                    if (destroy) {
                        sbc.MakeCulledCopy(index);
                        sbc.DeleteSB(index);
                    }
                }
            }
        }
    }
}


}