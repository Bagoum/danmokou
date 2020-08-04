using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using DMath;
using JetBrains.Annotations;
using UnityEngine.Profiling;
using Collision = DMath.Collision;

namespace Danmaku {

//This is for complex bullets with custom behavior
public abstract class Bullet : BehaviorEntity {
    [Header("Bullet Config")]
    [Tooltip("This will be instantiated once per recoloring, and used for SM material editing.")]
    public Material material;
    public int renderPriority;
    private static short rendererIndex = short.MinValue;
    private static readonly HashSet<Bullet> allBullets = new HashSet<Bullet>();
    public ushort grazeEveryFrames;
    private int grazeFrameCounter = 0;

    protected SOCircle collisionTarget;
    public int damage = 1;
    public bool destructible;
    protected bool collisionActive = false;

    private int defaultLayer;

    protected override void Awake() {
        base.Awake();
        defaultLayer = gameObject.layer;
        int sortOrder = rendererIndex++; //By default, this wraps around, which may cause momentary strange behavior if Bullet.ResetIndex is not called every several levels or so.
        if (sr != null) {
            sr.sortingOrder = sortOrder;
        }
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) {
            mr.sortingOrder = sortOrder;
        }
        ResetV();
    }

    /// <summary>
    /// Reset is used to put a bullet in a start-of-life state, eg. when pooling. It is also called in Awake.
    /// It is called BEFORE initialize (Awake is called synchronously by Instantiate)
    /// </summary>
    public override void ResetV() {
        base.ResetV();
        allBullets.Add(this);
    }

    protected void Initialize(RealizedBehOptions options, [CanBeNull] BehaviorEntity parent, Velocity _velocity, int firingIndex, uint bpiid, SOCircle _target) {
        base.Initialize(_velocity, new MovementModifiers(), options.smr, firingIndex, bpiid, parent, options: options);
        gameObject.layer = options.layer ?? defaultLayer;
        collisionTarget = _target;
    }

    protected virtual CollisionResult CollisionCheck() => CollisionResult.noColl;

    /// <summary>
    /// Check for collision and proc it on BulletManager.
    /// </summary>
    /// <returns>True iff the bullet should be destroyed (Caller must destroy)</returns>
    private bool CollisionCheckReport() {
        var cr = CollisionCheck();
        bool checkGraze = false;
        if (grazeFrameCounter-- == 0) {
            grazeFrameCounter = 0;
            checkGraze = true;
        }
        bool grazeIfCheckable = cr.graze & checkGraze;
        if (cr.collide) {
            BulletManager.ExternalBulletProc(damage, 0);
            return destructible;
        }
        if (grazeIfCheckable) {
            grazeFrameCounter = grazeEveryFrames - 1;
            BulletManager.ExternalBulletProc(0, 1);
        }
        return false;
    }

    public override void InvokeCull() {
        if (dying) return;
        collisionActive = false;
        allBullets.Remove(this);
        base.InvokeCull();
    }

    protected sealed override void RegularUpdateCollide() {
        if (collisionActive && CollisionCheckReport()) InvokeCull();
        else base.RegularUpdateCollide();
    }

    public static void OrphanAll() {
        allBullets.Clear();
    }

    public static void ClearAll() {
        foreach (var b in allBullets.ToArray()) b.InvokeCull();
        if (allBullets.Count != 0) {
            throw new Exception("Some bullets remain after clear: " + allBullets.Count);
        }
    }

#if UNITY_EDITOR
    public static int NumBullets => allBullets.Count;
#endif
}
}