﻿using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Functional;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using UnityEngine;
using Danmokou.DMath;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DataHoist;
using Danmokou.Player;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine.Profiling;

namespace Danmokou.Danmaku.Descriptors {

[Serializable]
public struct CollisionInfo {
    public bool CollisionActiveOnInit;
    public bool destructible;
    public ushort grazeEveryFrames;
}

//This is for complex bullets with custom behavior
public class Bullet : BehaviorEntity {
    [Header("Bullet Config")] 
    private ICollider? icollider;
    //for player bullets
    public PlayerBullet? Player { get; private set; }
    public EffectStrategy? OnHit { get; private set; }
    private BPY? hueShift;
    [Tooltip("This will be instantiated once per recoloring, and used for SM material editing.")]
    public Material material = null!;

    protected bool Displayer(out DisplayController d) => TryDependent<DisplayController>(out d);
    
    protected bool collisionActive = false;

    public CollisionInfo collisionInfo;
    public bool GrazeAllowed { get; private set; } = true;
    public int Damage { get; private set; } = 1;
    public bool IsColliding { get; set; }
    public float CollidingTime { get; set; }
    public float UnCollidingTime { get; set; }

    protected void SetMaterial(Material newMat) {
        material = newMat;
        if (Displayer(out var d)) d.SetMaterial(material);
    }
    public int renderPriority;
    private static short rendererIndex = short.MinValue;

    public float fadeInTime;
    public float cycleSpeed;
    public DRenderMode renderMode;
    public SimpleBulletEmptyScript.DisplacementInfo displacement;

    public DefaultColorizing colorizing;
    [Tooltip("Special gradients")] public BulletManager.GradientVariant[] gradients = null!;

    protected override void Awake() {
        base.Awake();
        var ci = GetComponent<GenericColliderInfo>();
        icollider = (ci == null) ? null : ci.AsCollider;
    }

    public override int UpdatePriority => UpdatePriorities.BULLET;

    /// <summary>
    /// Reset is used to put a bullet in a start-of-life state, eg. when pooling. It is also called in Awake (in BEH).
    /// It is called BEFORE initialize (Awake is called synchronously by Instantiate)
    /// </summary>
    protected override void ResetValues(bool isFirst) {
        base.ResetValues(isFirst);
        collisionActive = collisionInfo.CollisionActiveOnInit;
    }

    public virtual void Initialize(StyleMetadata? style, RealizedBehOptions options, BehaviorEntity? parent, in Movement mov, ParametricInfo pi, out int layer) {
        pi.ctx.bullet = this;
        Player = pi.ctx.playerBullet = options.playerBullet;
        OnHit = pi.ctx.onHit = options.onHit;
        base.Initialize(style, in mov, pi, options.smr, parent, options: options);
        gameObject.layer = layer = options.layer ?? DefaultLayer;
        hueShift = options.hueShift;
        Damage = options.damage ?? 1;
        GrazeAllowed = options.grazeAllowed;
        IsColliding = false;
        CollidingTime = 0;
        UnCollidingTime = 0;
    }
    
    public bool ComputeCircleCollision(Vector2 location, float radius, out Vector2 collisionLocation) {
        if (icollider!.CheckCollision(in bpi.loc.x, in bpi.loc.y, Direction, 1f,  
                location.x, location.y, radius)) {
            collisionLocation = bpi.loc;
            return true;
        } else {
            collisionLocation = Vector2.zero;
            return false;
        }
    }
    
    public CollisionResult ComputeGrazeCollision(Hurtbox hb, out Vector2 collisionLocation) {
        collisionLocation = bpi.loc;
        var coll = icollider!.CheckGrazeCollision(in bpi.loc.x, in bpi.loc.y, Direction, 1f, hb);
        if (coll.graze && !GrazeAllowed)
            return coll.NoGraze();
        return coll;
    }

    public override void RegularUpdateCollision() {
        IsColliding = false;
        if (icollider == null) return;
        bool ShouldDestroyAfterCollision() {
            IsColliding = true;
            Style.IterateCollideControls(this);
            if (collisionInfo.destructible) {
                InvokeCull();
                return true;
            } else {
                return false;
            }
        }
        Profiler.BeginSample("Generic bullet collisions");
        if (Player.Try(out var plb)) {
            var collidees = ServiceLocator.FindAll<IPlayerBulletCollisionReceiver>();
            for (int ic = 0; ic < collidees.Count; ++ic)
                if (collidees.GetIfExistsAt(ic, out var receiver) && receiver.Process(this, plb).collide && ShouldDestroyAfterCollision())
                    return;
        } else {
            var collidees = ServiceLocator.FindAll<IEnemyBulletCollisionReceiver>();
            for (int ic = 0; ic < collidees.Count; ++ic)
                if (collidees.GetIfExistsAt(ic, out var receiver) && receiver.Process(this).collide && ShouldDestroyAfterCollision())
                    return;
        }
        Profiler.EndSample();
        FinalizeCollisionTimings();
    }
    

    public void FinalizeCollisionTimings() {
        if (IsColliding) {
            CollidingTime += ETime.FRAME_TIME;
            UnCollidingTime = 0;
        } else {
            CollidingTime = 0;
            UnCollidingTime += ETime.FRAME_TIME;
        }
    }

    protected override void CullHook(bool allowFinalize) {
        collisionActive = false;
        base.CullHook(allowFinalize);
    }

    public static void ClearAll() {
        foreach (var b in GameObject.FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            if (b.Enabled)
                b.InvokeCull();
    }

    protected override void UpdateRendering(bool isFirstFrame) {
        var hasDisplay = Displayer(out var d);
        if (isFirstFrame) {
            int sortOrder = rendererIndex++; //By default, this wraps around, which may cause momentary strange behavior if Bullet.ResetIndex is not called every several levels or so.
            if (hasDisplay) 
                d.SetSortingOrder(sortOrder);
            var mr = GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sortingOrder = sortOrder;
        }
        if (hasDisplay)
            d.SetHueShift(hueShift?.Invoke(bpi) ?? 0f);
        base.UpdateRendering(isFirstFrame);
    }
    
#if UNITY_EDITOR
    public static int NumBullets => GameObject.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length;
#endif
}
}