using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using Danmokou.SM;
using Scriptor;
using UnityEngine;
using static Danmokou.Danmaku.Options.BehOption;

namespace Danmokou.Danmaku.Options {

/// <summary>
/// Properties that modify the behavior of BEH summons.
/// This includes complex bullets, like pathers, but NOT lasers (<see cref="LaserOption"/>).
/// </summary>
[Reflect]
public record BehOption {
    /// <summary>
    /// Make the movement of the bullet smoother. (Pather only)
    /// </summary>
    public static BehOption Smooth() => new SmoothProp();

    /// <summary>
    /// Run a StateMachine on a Bullet.
    /// <br/>This SM is run "superfluously": once it is finished, the object will continue to exist.
    /// </summary>
    public static BehOption SM(StateMachine sm) => new SMProp(sm);
    
    /// <summary>
    /// Set the scale of the object. Support depends on object.
    /// <br/>For pathers, sets the y scale.
    /// </summary>
    public static BehOption S(GCXF<float> scale) => new ScaleProp(scale);

    /// <summary>
    /// Set the starting HP of an enemy summon.
    /// <br/>This will throw an error if used on a non-enemy.
    /// </summary>
    public static BehOption HP(GCXF<float> hp) => new HPProp(hp);

    /// <summary>
    /// Set the amount of damage that a Bullet does.
    /// </summary>
    public static BehOption Damage(GCXF<float> damage) => new DamageProp(damage);

    public static BehOption Drops3(int value, int ppp, int life) => Drops4(value, ppp, life, 0);
    public static BehOption Drops4(int value, int ppp, int life, int power) => new ItemsProp(new ItemDrops(value, ppp, life, power, 0));
    
    /// <summary>
    /// Renders a Bullet on the lower projectile rendering layer.
    /// </summary>
    public static BehOption Low() => new LayerProp(Layer.LowProjectile);
    
    /// <summary>
    /// Renders a Bullet on the higher projectile rendering layer.
    /// </summary>
    public static BehOption High() => new LayerProp(Layer.HighProjectile);
    
    /// <summary>
    /// Provide a function that indicates how much to shift the color of the summon (in degrees) at any point in time.
    /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
    /// </summary>
    public static BehOption HueShift(BPY shift) => new HueShiftProp(shift);
    
    /// <summary>
    /// Manually construct a two-color gradient for the object.
    /// <br/> Note: Currently only supported on pathers (there is a LaserOption equivalent).
    /// <br/> Note: This will only have effect if you use it with the `recolor` palette.
    /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
    /// </summary>
    public static BehOption Recolor(TP4 black, TP4 white) => new RecolorProp(black, white);

    /// <summary>
    /// Tint the object. This is a multiplicative effect on its normal color.
    /// <br/> Note: Currently only supported on pathers (there is a LaserOption equivalent).
    /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
    /// </summary>
    public static BehOption Tint(TP4 tint) => new TintProp(tint);

    /// <summary>
    /// Rotate the BEH sprite.
    /// Not supported on pathers/lasers.
    /// Does not affect collision.
    /// </summary>
    public static BehOption Rotate(BPY rotator) => new RotateProp(rotator);
    
    /// <summary>
    /// Every frame, the entity will check the condition and destroy itself if it is true.
    /// <br/>Note: This is generally only necessary for player lasers. 
    /// </summary>
    public static BehOption Delete(Pred cond) => new DeleteProp(cond);

    /// <summary>
    /// Mark a Bullet as fired by the player, and allow it to check collision against enemies.
    /// <br/>Note: Currently only supported for pathers/lasers, and not for generic complex bullets.
    /// </summary>
    public static BehOption Player(int cdFrames, int bossDmg, int stageDmg, string onHit) =>
        new PlayerBulletProp(new PlayerBulletCfg(cdFrames, false, bossDmg, stageDmg), ResourceManager.GetEffect(onHit));
    /// <summary>
    /// Add an on-hit effect to the bullet. (For player bullets, use <see cref="Player"/> instead.)
    /// </summary>
    public static BehOption OnHit(string onHit) => new OnHitProp(ResourceManager.GetEffect(onHit));
    
    /// <summary>
    /// Set the ID of the object.
    /// </summary>
    public static BehOption Name(string name) => new NameProp(name);
    
    /// <summary>
    /// Set that an NPC bullet is not allowed to cause grazes against the player.
    /// </summary>
    public static BehOption NoGraze() => new NoGrazeFlag();

    /// <summary>
    /// Set the bullet styles to which this entity is vulnerable (Enemy only).
    /// </summary>
    public static BehOption Vuln(StyleSelector styles) => new VulnProp(styles);
    
    /// <summary>
    /// Set a per-frame multiplier for the amount of damage this entity recieves (Enemy only).
    /// <br/>If this is LEQ 0, then bullets will pass through the enemy.
    /// </summary>
    public static BehOption ReceiveDamage(BPY mult) => new ReceivedDamageProp(mult);
    
    #region impl
    
    public record CompositeProp : ValueProp<BehOption[]>, IUnrollable<BehOption> {
        public IEnumerable<BehOption> Values => value;
        public CompositeProp(params BehOption[] props) : base(props) { }
    }
    
    public record ValueProp<T> : BehOption {
        public readonly T value;
        public ValueProp(T value) => this.value = value;
    }

    public record ItemsProp(ItemDrops items) : BehOption;

    public record SmoothProp : BehOption {}

    public record SMProp(StateMachine sm) : BehOption;

    public record ScaleProp(GCXF<float> f) : BehOption;
    
    public record HPProp(GCXF<float> f) : BehOption;

    public record DamageProp(GCXF<float> damage) : BehOption;

    public record LayerProp(Layer layer) : BehOption;
    
    public record HueShiftProp(BPY f) : BehOption;
    
    public record TintProp(TP4 f) : BehOption;
    
    public record RotateProp(BPY f) : BehOption;
    
    public record RecolorProp(TP4 black, TP4 white) : BehOption;

    public record DeleteProp(Pred f) : BehOption;

    public record VulnProp : ValueProp<StyleSelector> {
        public VulnProp(StyleSelector value) : base(value) { }
    }
    
    public record ReceivedDamageProp : ValueProp<BPY> {
        public ReceivedDamageProp(BPY value) : base(value) { }
    }

    public record PlayerBulletProp(PlayerBulletCfg cfg, EffectStrategy onHit) : BehOption;

    public record OnHitProp(EffectStrategy onHit) : BehOption;

    public record NameProp(string name) : BehOption;

    public record NoGrazeFlag : BehOption;

    #endregion

}

public readonly struct RealizedBehOptions {
    public readonly bool smooth;
    public readonly SMRunner? smr;
    public readonly float scale;
    public readonly int? hp;
    public readonly int? layer;
    public readonly int? damage; //for npc bullets
    public readonly ItemDrops? drops;
    public readonly BPY? hueShift;
    public readonly BPY? rotator;
    public readonly (TP4 black, TP4 white)? recolor;
    public readonly TP4? tint;
    public readonly Pred? delete;
    public readonly PlayerBullet? playerBullet;
    public readonly EffectStrategy? onHit;
    public readonly bool grazeAllowed;
    public readonly StyleSelector? vulnerable;
    public readonly BPY? receivedDamage;

    public RealizedBehOptions(BehOptions opts, GenCtx gcx, PIData fctx, Vector2 parentOffset, V2RV2 localOffset, ICancellee cT) {
        smooth = opts.smooth;
        smr = SMRunner.Run(opts.sm, cT, gcx);
        scale = opts.scale?.Invoke(gcx) ?? 1f;
        hp = (opts.hp?.Invoke(gcx)).FMap(x => (int) x);
        layer = opts.layer;
        damage = (opts.damage?.Invoke(gcx)).FMap(x => (int)x);
        drops = opts.drops;
        hueShift = opts.hueShift;
        tint = opts.tint;
        rotator = opts.rotator;
        if (opts.recolor.Try(out var rc)) {
            recolor = (rc.black, rc.white);
        } else recolor = null;
        delete = opts.delete;
        playerBullet = opts.playerBullet?.Realize(fctx.PlayerController);
        onHit = opts.onHit;
        grazeAllowed = opts.grazeAllowed;
        vulnerable = opts.vulnerable;
        receivedDamage = opts.receivedDamage;
    }

    public RealizedBehOptions(RealizedLaserOptions rlo) {
        this.smr = rlo.smr;
        this.layer = rlo.layer;
        smooth = false;
        scale = 1f;
        hp = null;
        damage = rlo.damage;
        drops = null;
        hueShift = null; //handled by laser renderer
        tint = null;    //likewise
        recolor = null; //likewise
        rotator = null; //not enabled on lasers
        this.delete = rlo.delete;
        playerBullet = rlo.playerBullet;
        onHit = rlo.onHit;
        grazeAllowed = rlo.grazeAllowed;
        vulnerable = null;
        receivedDamage = null;
    }
}

/// <summary>
/// A set of properties modifying the behavior of BEH summons.
/// </summary>
public class BehOptions {
    //Note: If adding GCXU objects here, also add them to
    // the GCXU.ShareTypeAndCompile call in AtomicPAtterns
    public readonly bool smooth;
    public readonly StateMachine? sm;
    public readonly GCXF<float>? scale;
    public readonly GCXF<float>? hp;
    public readonly GCXF<float>? damage;
    public readonly Pred? delete;
    public readonly int? layer = null;
    public readonly ItemDrops? drops = null;
    public readonly BPY? hueShift;
    public readonly TP4? tint;
    public readonly BPY? rotator;
    public readonly (TP4 black, TP4 white)? recolor;
    public readonly PlayerBulletCfg? playerBullet;
    public readonly EffectStrategy? onHit;
    public readonly StyleSelector? vulnerable;
    public readonly BPY? receivedDamage;
    public readonly bool grazeAllowed = true;
    private readonly string? id = null;
    public string ID => id ?? "_";

    public BehOptions(params BehOption[] props) : this(props as IEnumerable<BehOption>) { }

    public BehOptions(IEnumerable<BehOption> props) {
        foreach (var p in props.Unroll()) {
            if (p is SmoothProp) smooth = true;
            else if (p is SMProp smp) sm = smp.sm;
            else if (p is ScaleProp sp) scale = sp.f;
            else if (p is HPProp hpp) hp = hpp.f;
            else if (p is DamageProp dpp) damage = dpp.damage;
            else if (p is LayerProp lp) layer = lp.layer.Int();
            else if (p is ItemsProp ip) drops = ip.items;
            else if (p is HueShiftProp hsp) hueShift = hsp.f;
            else if (p is RotateProp rotp) rotator = rotp.f;
            else if (p is RecolorProp rcp) recolor = (rcp.black, rcp.white);
            else if (p is TintProp tp) tint = tp.f;
            else if (p is DeleteProp dp) delete = dp.f;
            else if (p is VulnProp vn) vulnerable = vn.value;
            else if (p is ReceivedDamageProp rdp) receivedDamage = rdp.value;
            else if (p is PlayerBulletProp pbp) (playerBullet, onHit) = (pbp.cfg, pbp.onHit);
            else if (p is OnHitProp ohp) onHit = ohp.onHit;
            else if (p is NameProp np) id = np.name;
            else if (p is NoGrazeFlag) grazeAllowed = false;
            else throw new Exception($"Bullet property {p.GetType()} not handled.");
        }
    }
}
}