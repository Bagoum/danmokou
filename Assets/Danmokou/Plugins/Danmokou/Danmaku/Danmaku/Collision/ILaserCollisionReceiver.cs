using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Graphics;
using UnityEngine;

namespace Danmokou.Danmaku {
/// <summary>
/// An entity that can receive collisions from laser bullets fired by enemies.
/// </summary>
public interface IEnemyLaserCollisionReceiver {
    /// <summary>
    /// Process a collision between the laser's hurtbox and this object's hitbox.
    /// </summary>
    CollisionResult Process(CurvedTileRenderLaser laser, Vector2 laserLoc, float cos, float sin, out int segment);
}

/// <summary>
/// An entity that can receive collisions from laser bullets fired by the player.
/// </summary>
public interface IPlayerLaserCollisionReceiver {
    /// <summary>
    /// Process a collision between the laser's hurtbox and this object's hitbox.
    /// </summary>
    CollisionResult Process(CurvedTileRenderLaser laser, PlayerBullet plb, Vector2 laserLoc, float cos, float sin, out int segment);
}

/// <summary>
/// Implementation of <see cref="IPlayerLaserCollisionReceiver"/> that handles collision when the
///  entity is circular. 
/// </summary>
public interface ICircularPlayerLaserCollisionReceiver : IPlayerLaserCollisionReceiver {
    /// <summary>
    /// The location of the collidee. Used for determining buckets.
    /// </summary>
    Vector2 Location { get; }
    
    /// <summary>
    /// The radius of the collidee's hitbox.
    /// </summary>
    float CollisionRadius { get; }
    
    /// <summary>
    /// True if the collidee can receive collisions.
    /// </summary>
    bool ReceivesBulletCollisions { get; }

    CollisionResult IPlayerLaserCollisionReceiver.Process(CurvedTileRenderLaser laser, PlayerBullet plb, Vector2 laserLoc, float cos, float sin, out int segment) {
        if (ReceivesBulletCollisions &&
            laser.ComputeCircleCollision(laserLoc, cos, sin, Location, CollisionRadius, out segment, out var loc)) {
            TakeHit(laser, loc, plb);
            return new(true, false);
        } else {
            segment = 0;
            return CollisionMath.NoCollision;
        }
    }

    /// <summary>
    /// Called by the default implementation of <see cref="IPlayerLaserCollisionReceiver.Process"/>
    /// when a collision occurs.
    /// </summary>
    void TakeHit(CurvedTileRenderLaser laser, Vector2 collLoc, PlayerBullet plb);
}

/// <summary>
/// Implementation of <see cref="IEnemyLaserCollisionReceiver"/> that handles collision when the
///  entity is circular and has a grazebox.
/// </summary>
public interface ICircularGrazableEnemyLaserCollisionReceiver: IEnemyLaserCollisionReceiver {
    /// <summary>
    /// The collision information of the collidee.
    /// </summary>
    Hurtbox Hurtbox { get; }
    
    /// <summary>
    /// True if the collidee can receive collisions.
    /// </summary>
    bool ReceivesBulletCollisions { get; }
    
    
    CollisionResult IEnemyLaserCollisionReceiver.Process(CurvedTileRenderLaser laser, Vector2 laserLoc, float cos, float sin, out int segment) {
        if (ReceivesBulletCollisions) {
            var coll = laser.ComputeGrazeCollision(laserLoc, cos, sin, Hurtbox, out segment, out var loc);
            if (coll.graze || coll.collide)
                TakeHit(laser, loc, coll);
            return coll;
        } else {
            segment = 0;
            return CollisionMath.NoCollision;
        }
    }

    /// <summary>
    /// Called by the default implementation of <see cref="IPlayerLaserCollisionReceiver.Process"/>
    /// when a collision occurs.
    /// </summary>
    void TakeHit(CurvedTileRenderLaser laser, Vector2 collLoc, CollisionResult collision);
}


}