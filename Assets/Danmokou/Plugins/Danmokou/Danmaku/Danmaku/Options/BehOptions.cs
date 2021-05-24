using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Services;
using JetBrains.Annotations;
using Danmokou.SM;
using UnityEngine;
using static Danmokou.Danmaku.Options.BehOption;

namespace Danmokou.Danmaku.Options {

/// <summary>
/// Properties that modify the behavior of BEH summons.
/// This includes complex bullets, like pathers, but NOT lasers (<see cref="LaserOption"/>).
/// </summary>
[Reflect]
public class BehOption {
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
    public static BehOption HueShift(GCXU<BPY> shift) => new HueShiftProp(shift);
    
    /// <summary>
    /// Manually construct a two-color gradient for the object.
    /// <br/> Note: Currently only supported on pathers (there is a LaserOption equivalent).
    /// <br/> Note: This will only have effect if you use it with the `recolor` palette.
    /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
    /// </summary>
    public static BehOption Recolor(GCXU<TP4> black, GCXU<TP4> white) => new RecolorProp(black, white);

    /// <summary>
    /// Tint the object. This is a multiplicative effect on its normal color.
    /// <br/> Note: Currently only supported on pathers (there is a LaserOption equivalent).
    /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
    /// </summary>
    public static BehOption Tint(GCXU<TP4> tint) => new TintProp(tint);

    /// <summary>
    /// Rotate the BEH sprite.
    /// Not supported on pathers/lasers.
    /// Does not affect collision.
    /// </summary>
    public static BehOption Rotate(GCXU<BPY> rotator) => new RotateProp(rotator);
    
    /// <summary>
    /// Every frame, the entity will check the condition and destroy itself if it is true.
    /// <br/>Note: This is generally only necessary for player lasers. 
    /// </summary>
    public static BehOption Delete(GCXU<Pred> cond) => new DeleteProp(cond);

    /// <summary>
    /// Mark a Bullet as fired by the player, and allow it to check collision against enemies.
    /// <br/>Note: Currently only supported for pathers/lasers, and not for generic complex bullets.
    /// </summary>
    public static BehOption Player(int cdFrames, int bossDmg, int stageDmg, string effect) =>
        new PlayerBulletProp(new PlayerBulletCfg(cdFrames, bossDmg, stageDmg, ResourceManager.GetEffect(effect)));

    /// <summary>
    /// Set the ID of the object.
    /// </summary>
    public static BehOption Name(string name) => new NameProp(name);
    
    #region impl
    
    public class CompositeProp : ValueProp<BehOption[]>, IUnrollable<BehOption> {
        public IEnumerable<BehOption> Values => value;
        public CompositeProp(params BehOption[] props) : base(props) { }
    }
    
    public class ValueProp<T> : BehOption {
        public readonly T value;
        public ValueProp(T value) => this.value = value;
    }

    public class ItemsProp : ValueProp<ItemDrops> {
        public ItemsProp(ItemDrops i) : base(i) { }
    }

    public class SmoothProp : BehOption {}
    public class SMProp: ValueProp<StateMachine> {
        public SMProp(StateMachine sm) : base(sm) { } 
    }

    public class ScaleProp : ValueProp<GCXF<float>> {
        public ScaleProp(GCXF<float> f) : base(f) { }
    }
    public class HPProp : ValueProp<GCXF<float>> {
        public HPProp(GCXF<float> f) : base(f) { }
    }

    public class LayerProp : ValueProp<Layer> {
        public LayerProp(Layer l) : base(l) { }
    }
    public class HueShiftProp : ValueProp<GCXU<BPY>> {
        public HueShiftProp(GCXU<BPY> f) : base(f) { }
    }
    public class TintProp : ValueProp<GCXU<TP4>> {
        public TintProp(GCXU<TP4> f) : base(f) { }
    }
    public class RotateProp : ValueProp<GCXU<BPY>> {
        public RotateProp(GCXU<BPY> f) : base(f) { }
    }
    public class RecolorProp : BehOption {
        public readonly GCXU<TP4> black;
        public readonly GCXU<TP4> white;

        public RecolorProp(GCXU<TP4> b, GCXU<TP4> w) {
            black = b;
            white = w;
        }
    }
    public class DeleteProp : ValueProp<GCXU<Pred>> {
        public DeleteProp(GCXU<Pred> f) : base(f) { }
    }
    
    public class PlayerBulletProp : ValueProp<PlayerBulletCfg> {
        public PlayerBulletProp(PlayerBulletCfg cfg) : base(cfg) { }
    }

    public class NameProp : ValueProp<string> {
        public NameProp(string name) : base(name) { }
    }
    
    #endregion
    
}

public readonly struct RealizedBehOptions {
    public readonly bool smooth;
    public readonly SMRunner smr;
    public readonly float scale;
    public readonly int? hp;
    public readonly int? layer;
    public readonly ItemDrops? drops;
    public readonly BPY? hueShift;
    public readonly BPY? rotator;
    public readonly (TP4 black, TP4 white)? recolor;
    public readonly TP4? tint;
    public readonly Pred? delete;
    public readonly PlayerBullet? playerBullet;

    public RealizedBehOptions(BehOptions opts, GenCtx gcx, FiringCtx fctx, Vector2 parentOffset, V2RV2 localOffset, ICancellee cT) {
        smooth = opts.smooth;
        smr = SMRunner.Run(opts.sm, cT, gcx);
        scale = opts.scale?.Invoke(gcx) ?? 1f;
        hp = (opts.hp?.Invoke(gcx)).FMap(x => (int) x);
        layer = opts.layer;
        drops = opts.drops;
        hueShift = opts.hueShift?.Invoke(gcx, fctx);
        tint = opts.tint?.Invoke(gcx, fctx);
        rotator = opts.rotator?.Invoke(gcx, fctx);
        if (opts.recolor.Try(out var rc)) {
            recolor = (rc.black.Invoke(gcx, fctx), rc.white.Invoke(gcx, fctx));
        } else recolor = null;
        delete = opts.delete?.Invoke(gcx, fctx);
        playerBullet = opts.playerBullet?.Realize(fctx.PlayerController);
    }

    public RealizedBehOptions(RealizedLaserOptions rlo) {
        this.smr = rlo.smr;
        this.layer = rlo.layer;
        smooth = false;
        scale = 1f;
        hp = null;
        drops = null;
        hueShift = null; //handled by laser renderer
        tint = null;    //likewise
        recolor = null; //likewise
        rotator = null; //not enabled on lasers
        this.delete = rlo.delete;
        playerBullet = rlo.playerBullet;
    }
}

public class BehOptions {
    public readonly bool smooth;
    public readonly StateMachine? sm;
    public readonly GCXF<float>? scale;
    public readonly GCXF<float>? hp;
    public readonly GCXU<Pred>? delete;
    public readonly int? layer = null;
    public readonly ItemDrops? drops = null;
    public readonly GCXU<BPY>? hueShift;
    public readonly GCXU<TP4>? tint;
    public readonly GCXU<BPY>? rotator;
    public readonly (GCXU<TP4> black, GCXU<TP4> white)? recolor;
    public readonly PlayerBulletCfg? playerBullet;
    private readonly string? id = null;
    public string ID => id ?? "_";

    public BehOptions(params BehOption[] props) : this(props as IEnumerable<BehOption>) { }

    public BehOptions(IEnumerable<BehOption> props) {
        foreach (var p in props.Unroll()) {
            if (p is SmoothProp) smooth = true;
            else if (p is SMProp smp) sm = smp.value;
            else if (p is ScaleProp sp) scale = sp.value;
            else if (p is HPProp hpp) hp = hpp.value;
            else if (p is LayerProp lp) layer = lp.value.Int();
            else if (p is ItemsProp ip) drops = ip.value;
            else if (p is HueShiftProp hsp) hueShift = hsp.value;
            else if (p is RotateProp rotp) rotator = rotp.value;
            else if (p is RecolorProp rcp) recolor = (rcp.black, rcp.white);
            else if (p is TintProp tp) tint = tp.value;
            else if (p is DeleteProp dp) delete = dp.value;
            else if (p is PlayerBulletProp pbp) playerBullet = pbp.value;
            else if (p is NameProp np) id = np.value;
            else throw new Exception($"Bullet property {p.GetType()} not handled.");
        }
    }
}
}