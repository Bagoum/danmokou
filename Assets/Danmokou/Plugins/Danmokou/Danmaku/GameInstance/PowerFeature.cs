using System;
using System.Reactive;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.DMath;

namespace Danmokou.GameInstance {
/// <summary>
/// A feature for handling power.
/// </summary>
public interface IPowerFeature : IInstanceFeature {
    public Event<Unit> PowerLost { get; }
    public Event<Unit> PowerGained { get; }
    public Event<Unit> PowerFull { get; }
    public double PowerMax { get; }
    public Evented<double> Power { get; }
    public int PowerInt { get; }
    public int PowerIndex { get; }
    public bool AllowPowerItemDrops { get; }
    public void AddPower(double delta);
    
    /// <summary>
    /// Delta should be negative.
    /// </summary>
    public bool TryConsumePower(double delta);

    public void AddPowerItems(int delta);
    public void AddFullPowerItem();
}
public class PowerFeature : BaseInstanceFeature, IPowerFeature {
    public const double powerMax = 4;
    public const double powerMin = 1;
#if UNITY_EDITOR
    public const double powerDefault = 1000;
#else
    public const double powerDefault = 1;
#endif
    public const double powerDeathLoss = -1;
    public const double powerItemValue = 0.05;
    public const double powerToValueConversion = 2;
    
    public Event<Unit> PowerLost { get; } = new();
    public Event<Unit> PowerGained { get; } = new();
    public Event<Unit> PowerFull { get; } = new();

    private InstanceData Inst { get; }

    public double PowerMax => powerMax;
    public Evented<double> Power { get; }
    public int PowerInt => (int)Math.Floor(Power);
    public int PowerIndex => PowerInt - (int) powerMin;
    public bool AllowPowerItemDrops => true;

    public PowerFeature(InstanceData inst) {
        Inst = inst;
        Power = new(inst.mode.OneLife() ? powerMax : M.Clamp(powerMin, powerMax, powerDefault));
    }

    public void OnDied() {
        AddPower(powerDeathLoss);
    }
    
    
    public void AddPower(double delta) {
        var prevFloor = Math.Floor(Power);
        var prevCeil = Math.Ceiling(Power);
        var prevPower = Power.Value;
        Power.Value = M.Clamp(powerMin, powerMax, Power + delta);
        //1.95 is effectively 1, 2.00 is effectively 2
        if (Power < prevFloor) PowerLost.OnNext(default);
        if (prevPower < prevCeil && Power >= prevCeil) {
            if (Power >= powerMax) PowerFull.OnNext(default);
            else PowerGained.OnNext(default);
        }
    }

    public bool TryConsumePower(double delta) {
        if (Power + delta >= powerMin) {
            AddPower(delta);
            return true;
        } else return false;
    }

    public void AddPowerItems(int delta) {
        if (Power >= powerMax)
            Inst.ScoreF.AddValueItems((int)(delta * powerToValueConversion), 1);
        else {
            AddPower(delta * powerItemValue);
            foreach (var f in Inst.Features)
                f.OnItemPower(delta);
        }
    }

    public void AddFullPowerItem() {
        Power.Value = powerMax;
        PowerFull.OnNext(default);
        foreach (var f in Inst.Features)
            f.OnItemFullPower(1);
    }


    public class Disabled : BaseInstanceFeature, IPowerFeature {
        public Event<Unit> PowerLost { get; } = new();
        public Event<Unit> PowerGained { get; } = new();
        public Event<Unit> PowerFull { get; } = new();

        public double PowerMax => powerMax;
        public Evented<double> Power { get; } = new(powerMax);
        public int PowerInt => (int)Math.Floor(Power);
        public int PowerIndex => PowerInt - (int) powerMin;
        public bool AllowPowerItemDrops => false;

        public void AddPower(double delta) { }
        public bool TryConsumePower(double delta) => false;
        public void AddPowerItems(int delta) {
            throw new Exception("Power items are disabled!");
        }

        public void AddFullPowerItem() {
            throw new Exception("Full power items are disabled!");
        }
    }
}


public class PowerFeatureCreator : IFeatureCreator<IPowerFeature> {
    public IPowerFeature Create(InstanceData instance) => new PowerFeature(instance);
}
public class DisabledPowerFeatureCreator : IFeatureCreator<IPowerFeature> {
    public IPowerFeature Create(InstanceData instance) => new PowerFeature.Disabled();
}

}