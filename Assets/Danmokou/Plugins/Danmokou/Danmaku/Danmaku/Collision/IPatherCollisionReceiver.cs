using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Graphics;
using UnityEngine;

namespace Danmokou.Danmaku {
/// <summary>
/// An entity that can receive collisions from pather bullets fired by enemies.
/// </summary>
public interface IEnemyPatherCollisionReceiver {
    /// <summary>
    /// Process a collision between the pather's hurtbox and this object's hitbox.
    /// </summary>
    CollisionResult Process(CurvedTileRenderPather pather, int cutTail, int cutHead);
}

/// <summary>
/// An entity that can receive collisions from pather bullets fired by the player.
/// </summary>
public interface IPlayerPatherCollisionReceiver {
    /// <summary>
    /// Process a collision between the pather's hurtbox and this object's hitbox.
    /// </summary>
    CollisionResult Process(CurvedTileRenderPather pather, PlayerBullet plb, int cutTail, int cutHead);
}

/// <summary>
/// Implementation of <see cref="IPlayerPatherCollisionReceiver"/> that handles collision when the
///  entity is circular. 
/// </summary>
public interface ICircularPlayerPatherCollisionReceiver : IPlayerPatherCollisionReceiver {
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

    CollisionResult IPlayerPatherCollisionReceiver.Process(CurvedTileRenderPather pather, PlayerBullet plb, int cutTail, int cutHead) {
        if (ReceivesBulletCollisions(pather.Style) &&
            pather.ComputeCircleCollision(Location, CollisionRadius, cutTail, cutHead, out var loc)) {
            TakeHit(pather, loc, plb);
            return new(true, false);
        } else
            return CollisionMath.NoCollision;
    }

    /// <summary>
    /// Called by the default implementation of <see cref="IPlayerPatherCollisionReceiver.Process"/>
    /// when a collision occurs.
    /// </summary>
    void TakeHit(CurvedTileRenderPather pather, Vector2 collLoc, PlayerBullet plb);
}

/// <summary>
/// Implementation of <see cref="IEnemyPatherCollisionReceiver"/> that handles collision when the
///  entity is circular and has a grazebox.
/// </summary>
public interface ICircularGrazableEnemyPatherCollisionReceiver: IEnemyPatherCollisionReceiver {
    /// <summary>
    /// The collision information of the collidee.
    /// </summary>
    Hurtbox Hurtbox { get; }
    
    /// <summary>
    /// True if the collidee can receive collisions from the given style.
    /// </summary>
    bool ReceivesBulletCollisions(string? style);
    
    
    CollisionResult IEnemyPatherCollisionReceiver.Process(CurvedTileRenderPather pather, int cutTail, int cutHead) {
        if (ReceivesBulletCollisions(pather.Style)) {
            var coll = pather.ComputeGrazeCollision(Hurtbox, cutTail, cutHead, out var loc);
            if (coll.graze || coll.collide)
                TakeHit(pather, loc, coll);
            return coll;
        } else
            return CollisionMath.NoCollision;
    }

    /// <summary>
    /// Called by the default implementation of <see cref="IPlayerPatherCollisionReceiver.Process"/>
    /// when a collision occurs.
    /// </summary>
    void TakeHit(CurvedTileRenderPather pather, Vector2 collLoc, CollisionResult collision);
}


}