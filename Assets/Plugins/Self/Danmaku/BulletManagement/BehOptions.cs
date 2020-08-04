using System;
using System.Collections.Generic;
using System.Threading;
using DMath;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using static Danmaku.BehOption;

namespace Danmaku {

/// <summary>
/// Properties that modify the behavior of BEH summons.
/// This includes complex bullets, like pathers, but NOT lasers (<see cref="LaserOption"/>).
/// </summary>
public class BehOption {
    /// <summary>
    /// Make the movement of the bullet smoother. (Pather only)
    /// </summary>
    public static BehOption Smooth() => new SmoothProp();

    /// <summary>
    /// Run a StateMachine on the bullet.
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

    public static BehOption Drops(int value, int ppp, int life) => new ItemsProp(new ItemDrops(value, ppp, life));
    
    public static BehOption Low() => new LayerProp(Layer.LowProjectile);
    public static BehOption High() => new LayerProp(Layer.HighProjectile);
    
    public static BehOption HueShift(GCXF<float> dps) => new HueShiftProp(dps);

    
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
    public class HueShiftProp : ValueProp<GCXF<float>> {
        public HueShiftProp(GCXF<float> f) : base(f) { }
    }
    
}

public readonly struct RealizedBehOptions {
    public readonly bool smooth;
    public readonly SMRunner smr;
    public readonly float scale;
    public readonly int? hp;
    public readonly int? layer;
    public readonly ItemDrops? drops;
    public readonly float hueShift;

    public RealizedBehOptions(BehOptions opts, GenCtx gcx, Vector2 parentOffset, V2RV2 localOffset, CancellationToken cT) {
        smooth = opts.smooth;
        smr = SMRunner.Run(opts.sm, cT, gcx);
        scale = opts.scale?.Invoke(gcx) ?? 1f;
        hp = (opts.hp?.Invoke(gcx)).FMap(x => (int) x);
        layer = opts.layer;
        drops = opts.drops;
        hueShift = opts.hueShift?.Invoke(gcx) ?? 0f;
    }

    public RealizedBehOptions(SMRunner smr, int? layer) {
        this.smr = smr;
        this.layer = layer;
        smooth = false;
        scale = 1f;
        hp = null;
        drops = null;
        hueShift = 0f;
    }
}

public class BehOptions {
    public readonly bool smooth;
    [CanBeNull] public readonly StateMachine sm;
    [CanBeNull] public readonly GCXF<float> scale;
    [CanBeNull] public readonly GCXF<float> hp;
    public readonly MovementModifiers modifiers = MovementModifiers.Default;
    public readonly int? layer = null;
    public readonly ItemDrops? drops = null;
    [CanBeNull] public readonly GCXF<float> hueShift;
    public string ID => "_";

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
            else throw new Exception($"Bullet property {p.GetType()} not handled.");
        }
    }
}
}