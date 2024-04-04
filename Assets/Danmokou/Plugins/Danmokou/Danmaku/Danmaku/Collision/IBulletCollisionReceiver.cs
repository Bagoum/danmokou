using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Graphics;
using UnityEngine;

namespace Danmokou.Danmaku {
/// <summary>
/// An entity that can receive collisions from generic bullets fired by enemies.
/// </summary>
public interface IEnemyBulletCollisionReceiver {
    /// <summary>
    /// Process a collision between the bullet's hurtbox and this object's hitbox.
    /// </summary>
    CollisionResult Process(Bullet bullet);
}

/// <summary>
/// An entity that can receive collisions from generic bullets fired by the player.
/// </summary>
public interface IPlayerBulletCollisionReceiver {
    /// <summary>
    /// Process a collision between the bullet's hurtbox and this object's hitbox.
    /// </summary>
    CollisionResult Process(Bullet bullet, PlayerBullet plb);
}

/// <summary>
/// Implementation of <see cref="IPlayerBulletCollisionReceiver"/> that handles collision when the
///  entity is circular. 
/// </summary>
public interface ICircularPlayerBulletCollisionReceiver : IPlayerBulletCollisionReceiver {
    /// <summary>
    /// The location of the collidee. Used for determining buckets.
    /// </summary>
    Vector2 Location { get; }
    
    /// <summary>
    /// The radius of the collidee's hitbox.
    /// </summary>
    float CollisionRadius { get; }
    
    /// <summary>
    /// True if the collidee can receive collisions from the given style.
    /// </summary>
    bool ReceivesBulletCollisions(string? style);

    CollisionResult IPlayerBulletCollisionReceiver.Process(Bullet bullet, PlayerBullet plb) {
        if (ReceivesBulletCollisions(bullet.myStyle.style) &&
            bullet.ComputeCircleCollision(Location, CollisionRadius, out var loc)) {
            TakeHit(bullet, loc, plb);
            return new(true, false);
        } else
            return CollisionMath.NoCollision;
    }

    /// <summary>
    /// Called by the default implementation of <see cref="IPlayerBulletCollisionReceiver.Process"/>
    /// when a collision occurs.
    /// </summary>
    void TakeHit(Bullet bullet, Vector2 collLoc, PlayerBullet plb);
}

/// <summary>
/// Implementation of <see cref="IEnemyBulletCollisionReceiver"/> that handles collision when the
///  entity is circular and has a grazebox.
/// </summary>
public interface ICircularGrazableEnemyBulletCollisionReceiver: IEnemyBulletCollisionReceiver {
    /// <summary>
    /// The collision information of the collidee.
    /// </summary>
    Hurtbox Hurtbox { get; }

    /// <summary>
    /// True if the collidee can receive collisions from the given style.
    /// </summary>
    bool ReceivesBulletCollisions(string? style);
    
    
    CollisionResult IEnemyBulletCollisionReceiver.Process(Bullet bullet) {
        if (ReceivesBulletCollisions(bullet.myStyle.style)) {
            var coll = bullet.ComputeGrazeCollision(Hurtbox, out var loc);
            if (coll.graze || coll.collide)
                TakeHit(bullet, loc, coll);
            return coll;
        } else
            return CollisionMath.NoCollision;
    }

    /// <summary>
    /// Called by the default implementation of <see cref="IPlayerBulletCollisionReceiver.Process"/>
    /// when a collision occurs.
    /// </summary>
    void TakeHit(Bullet bullet, Vector2 collLoc, CollisionResult collision);
}


}