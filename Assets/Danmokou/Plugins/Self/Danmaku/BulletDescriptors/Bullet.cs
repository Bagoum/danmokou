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

public readonly struct PlayerBulletCfg {
    public readonly int cdFrames;
    public readonly int bossDmg;
    public readonly int stageDmg;
    public readonly EffectStrategy effect;

    public PlayerBulletCfg(int cd, int boss, int stage, EffectStrategy eff) {
        cdFrames = cd;
        bossDmg = boss;
        stageDmg = stage;
        effect = eff;
    }
}
//This is for complex bullets with custom behavior
public abstract class Bullet : BehaviorEntity {
    [Header("Bullet Config")] 
    private PlayerBulletCfg? playerBullet = null;
    [Tooltip("This will be instantiated once per recoloring, and used for SM material editing.")]
    public Material material;
    public int renderPriority;
    private static short rendererIndex = short.MinValue;
    private static readonly HashSet<Bullet> allBullets = new HashSet<Bullet>();

    protected SOCircle collisionTarget;

    private int defaultLayer;

    [CanBeNull] public Sprite sprite;
    public virtual FrameAnimBullet.BulletAnimSprite[] Frames =>
        (sprite == null) ? throw new Exception("Cannot generate frames for null sprite in Bullet") :
        new[] {new FrameAnimBullet.BulletAnimSprite {s = sprite}};
    
    
    public float fadeInTime;
    public float cycleSpeed;
    public RenderMode renderMode;
    public SimpleBulletEmptyScript.DisplacementInfo displacement;

    public DefaultColorizing colorizing;
    [Tooltip("Special gradients")] public BulletManager.GradientVariant[] gradients;

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

    public override int UpdatePriority => UpdatePriorities.BULLET;

    /// <summary>
    /// Reset is used to put a bullet in a start-of-life state, eg. when pooling. It is also called in Awake.
    /// It is called BEFORE initialize (Awake is called synchronously by Instantiate)
    /// </summary>
    public override void ResetV() {
        base.ResetV();
        allBullets.Add(this);
    }

    protected void Initialize(RealizedBehOptions options, [CanBeNull] BehaviorEntity parent, Velocity _velocity, int firingIndex, uint bpiid, SOCircle _target, out int layer) {
        base.Initialize(_velocity, options.smr, firingIndex, bpiid, parent, options: options);
        gameObject.layer = layer = options.layer ?? defaultLayer;
        collisionTarget = _target;
        if ((playerBullet = options.playerBullet) != null) DataHoisting.PreserveID(bpiid);
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
    
    protected virtual void Colorize(FrameAnimBullet.Recolor r) {
        style = r.style;
        if (r.sprites == null) return;
        material = r.material;
        SetSprite(r.sprites[0].s);
    }

    public virtual void ColorizeOverwrite(FrameAnimBullet.Recolor r) => Colorize(r);

    protected abstract void SetSprite(Sprite s, float yscale = 1f);
    

#if UNITY_EDITOR
    public static int NumBullets => allBullets.Count;
#endif
}
}