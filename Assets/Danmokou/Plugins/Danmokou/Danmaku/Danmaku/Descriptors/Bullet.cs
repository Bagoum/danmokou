using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Functional;
using Danmokou.Behavior;
using UnityEngine;
using Danmokou.DMath;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DataHoist;
using Danmokou.Player;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEditor;

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
    //for enemy bullets
    public Maybe<PlayerController> Target { get; private set; }
    //for player bullets
    public PlayerBullet? Player { get; private set; } = null;
    private BPY? hueShift;
    [Tooltip("This will be instantiated once per recoloring, and used for SM material editing.")]
    public Material material = null!;
    
    protected bool collisionActive = false;

    public CollisionInfo collisionInfo;
    public int Damage => 1;

    protected void SetMaterial(Material newMat) {
        material = newMat;
        if (displayer != null) displayer.SetMaterial(material);
    }
    public int renderPriority;
    private static short rendererIndex = short.MinValue;
    private static readonly HashSet<Bullet> allBullets = new();


    public float fadeInTime;
    public float cycleSpeed;
    public DRenderMode renderMode;
    public SimpleBulletEmptyScript.DisplacementInfo displacement;

    public DefaultColorizing colorizing;
    [Tooltip("Special gradients")] public BulletManager.GradientVariant[] gradients = null!;

    protected override void Awake() {
        base.Awake();
        int sortOrder = rendererIndex++; //By default, this wraps around, which may cause momentary strange behavior if Bullet.ResetIndex is not called every several levels or so.
        if (displayer != null) displayer.SetSortingOrder(sortOrder);
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) {
            mr.sortingOrder = sortOrder;
        }
        var ci = GetComponent<GenericColliderInfo>();
        icollider = (ci == null) ? null : ci.AsCollider;
    }

    public override int UpdatePriority => UpdatePriorities.BULLET;

    /// <summary>
    /// Reset is used to put a bullet in a start-of-life state, eg. when pooling. It is also called in Awake (in BEH).
    /// It is called BEFORE initialize (Awake is called synchronously by Instantiate)
    /// </summary>
    protected override void ResetValues() {
        base.ResetValues();
        collisionActive = collisionInfo.CollisionActiveOnInit;
        allBullets.Add(this);
    }

    public virtual void Initialize(BEHStyleMetadata? style, RealizedBehOptions options, BehaviorEntity? parent, Movement mov, ParametricInfo pi, out int layer) {
        Target = ServiceLocator.MaybeFind<PlayerController>();
        Player = options.playerBullet;
        base.Initialize(style, mov, pi, options.smr, parent, options: options);
        gameObject.layer = layer = options.layer ?? DefaultLayer;
        hueShift = options.hueShift;
    }
    

    public override void RegularUpdateCollision() {
        if (icollider == null) return;
        if (Player.Try(out var plb)) {
            var enemies = Enemy.FrozenEnemies;
            for (int ii = 0; ii < enemies.Count; ++ii) {
                if (enemies[ii].Active &&
                    icollider.CheckCollision(in bpi.loc.x, in bpi.loc.y, Direction, 1f,  
                        enemies[ii].location.x, enemies[ii].location.y, enemies[ii].radius)) {
                    enemies[ii].enemy.TakeHit(in plb, in bpi);
                    if (collisionInfo.destructible) {
                        InvokeCull();
                        return;
                    }
                }
            }
        } else if (Target.Valid) {
            var coll = icollider.CheckGrazeCollision(in bpi.loc.x, in bpi.loc.y, Direction, 1f, Target.Value.Hurtbox);
            Target.Value.ProcessCollision(coll, Damage, in bpi, in collisionInfo.grazeEveryFrames);
            if (coll.collide && collisionInfo.destructible) {
                InvokeCull();
                return;
            }
        }
    }

    public override void InvokeCull() {
        if (dying) return;
        collisionActive = false;
        allBullets.Remove(this);
        base.InvokeCull();
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

    protected override void UpdateDisplayerRender() {
        base.UpdateDisplayerRender();
        displayer!.SetHueShift(hueShift?.Invoke(bpi) ?? 0f);
    }


#if UNITY_EDITOR
    public static int NumBullets => allBullets.Count;
    
#endif
}
}