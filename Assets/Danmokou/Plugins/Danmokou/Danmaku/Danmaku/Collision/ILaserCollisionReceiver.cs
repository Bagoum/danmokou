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
    /// Check if a collision exists between the laser's hurtbox and this object's hitbox. Do not process the collision.
    /// </summary>
    CollisionResult Check(CurvedTileRenderLaser laser, Vector2 laserLoc, float cos, float sin, out int segment, out Vector2 collLoc);

    /// <summary>
    /// Process a collision provided by <see cref="Check"/>.
    /// </summary>
    void ProcessActual(CurvedTileRenderLaser laser, Vector2 laserLoc, float cos, float sin, CollisionResult coll, Vector2 collLoc);
}

/// <summary>
/// An entity that can receive collisions from laser bullets fired by the player.
/// </summary>
public interface IPlayerLaserCollisionReceiver {
    /// <summary>
    /// Check and process a collision between the laser's hurtbox and this object's hitbox.
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
    /// True if the collidee can receive collisions from the given style.
    /// </summary>
    bool ReceivesBulletCollisions(string? style);

    CollisionResult IPlayerLaserCollisionReceiver.Process(CurvedTileRenderLaser laser, PlayerBullet plb, Vector2 laserLoc, float cos, float sin, out int segment) {
        if (ReceivesBulletCollisions(laser.Style) &&
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
    /// True if the collidee can receive collisions from the given style.
    /// </summary>
    bool ReceivesBulletCollisions(string? style);
    
    
    CollisionResult IEnemyLaserCollisionReceiver.Check(CurvedTileRenderLaser laser, Vector2 laserLoc, float cos, float sin, out int segment, out Vector2 collLoc) {
        if (ReceivesBulletCollisions(laser.Style)) {
            return laser.ComputeGrazeCollision(laserLoc, cos, sin, Hurtbox, out segment, out collLoc);
        } else {
            segment = 0;
            collLoc = Vector2.zero;
            return CollisionMath.NoCollision;
        }
    }

    void IEnemyLaserCollisionReceiver.ProcessActual(CurvedTileRenderLaser laser, Vector2 laserLoc, float cos, float sin,
        CollisionResult coll, Vector2 collLoc) {
        if (coll.graze || coll.collide)
            TakeHit(laser, collLoc, coll);
    }

    /// <summary>
    /// Called by the default implementation of <see cref="IPlayerLaserCollisionReceiver.Process"/>
    /// when a collision occurs.
    /// </summary>
    void TakeHit(CurvedTileRenderLaser laser, Vector2 collLoc, CollisionResult collision);
}


}