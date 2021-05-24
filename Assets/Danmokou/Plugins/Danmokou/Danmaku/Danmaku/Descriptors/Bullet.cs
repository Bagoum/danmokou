using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using Danmokou.Behavior;
using UnityEngine;
using Danmokou.DMath;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DataHoist;
using Danmokou.Player;
using Danmokou.Scriptables;
using JetBrains.Annotations;

namespace Danmokou.Danmaku.Descriptors {

//This is for complex bullets with custom behavior
public class Bullet : BehaviorEntity {
    [Header("Bullet Config")] 
    private ICollider? icollider;
    public PlayerBullet? Player { get; private set; } = null;
    private BPY? hueShift;
    [Tooltip("This will be instantiated once per recoloring, and used for SM material editing.")]
    public Material material = null!;

    protected void SetMaterial(Material newMat) {
        material = newMat;
        if (displayer != null) displayer.SetMaterial(material);
    }
    public int renderPriority;
    private static short rendererIndex = short.MinValue;
    private static readonly HashSet<Bullet> allBullets = new HashSet<Bullet>();


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
        allBullets.Add(this);
    }

    public virtual void Initialize(BEHStyleMetadata? style, RealizedBehOptions options, BehaviorEntity? parent, Movement mov, ParametricInfo pi, SOPlayerHitbox _target, out int layer) {
        Player = options.playerBullet;
        base.Initialize(style, mov, pi, options.smr, parent, options: options);
        gameObject.layer = layer = options.layer ?? DefaultLayer;
        collisionTarget = _target;
        hueShift = options.hueShift;
    }


    public override void InvokeCull() {
        if (dying) return;
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

    protected override CollisionResult CollisionCheck() {
        if (icollider == null) return base.CollisionCheck();
        if (Player.Try(out var plb)) {
            var fe = Enemy.FrozenEnemies;
            for (int ii = 0; ii < fe.Count; ++ii) {
                if (fe[ii].Active && icollider.CheckCollision(Loc, Direction, 1f, fe[ii].pos, fe[ii].radius) 
                                  && fe[ii].enemy.TryHitIndestructible(bpi.id, plb.data.cdFrames)) {
                    fe[ii].enemy.QueuePlayerDamage(plb.data.bossDmg, plb.data.stageDmg, plb.firer);
                    fe[ii].enemy.ProcOnHit(plb.data.effect, Loc);
                }
            }
        } else if (collisionTarget.Active) {
            return icollider.CheckGrazeCollision(Loc, Direction, 1f, collisionTarget.Hitbox);
        }
        return CollisionResult.noColl;
        
    }


#if UNITY_EDITOR
    public static int NumBullets => allBullets.Count;
#endif
}
}