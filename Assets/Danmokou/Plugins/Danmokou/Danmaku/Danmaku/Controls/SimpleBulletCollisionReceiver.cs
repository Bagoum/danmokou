using System;
using System.Collections.Generic;
using BagoumLib.DataStructures;
using CommunityToolkit.HighPerformance;
using Danmokou.Danmaku.Descriptors;
using UnityEngine;

namespace Danmokou.Danmaku {
/// <summary>
/// An entity that can receive collisions from simple bullets, such as players (which receive collisions from enemy bullets) or enemies (which receive collisions from player bullets).
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
/// </summary>
public interface ICircularSimpleBulletCollisionReceiver : ISimpleBulletCollisionReceiver {
    /// <summary>
    /// The location of the collider. Used for determining buckets.
    /// </summary>
    Vector2 Location { get; }
    /// <summary>
    /// The maximum distance from <see cref="Location"/> at which a collision can occur. Used for determining buckets.
    /// </summary>
    float MaxCollisionRadius { get; }
    void ISimpleBulletCollisionReceiver.ProcessSimpleBucketed(BulletManager.SimpleBulletCollection sbc, BulletManager.FrameBucketing frame) {
        var mcd = MaxCollisionRadius + sbc.Collider.MaxRadius * frame.maxScale;
        var mcd2 = new Vector2(mcd, mcd);
        var minBucket = sbc.BucketIndexPair(Location - mcd2);
        var maxBucket = sbc.BucketIndexPair(Location + mcd2);
        ProcessSimpleBuckets(sbc, sbc.bucketsSpan[new Range(minBucket.y, maxBucket.y + 1), new Range(minBucket.x, maxBucket.x + 1)]);
    }
    
    
    /// <summary>
    /// Process collisions for a set of buckets in a simple bullet pool.
    /// <br/>See <see cref="ISimpleBulletCollisionReceiver.ProcessSimpleBucketed"/>
    /// </summary>
    void ProcessSimpleBuckets(BulletManager.SimpleBulletCollection sbc, ReadOnlySpan2D<List<int>> indexBuckets);
}

}