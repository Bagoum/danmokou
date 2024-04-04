using System;
using System.Reactive;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Player;
using Newtonsoft.Json.Serialization;

namespace Danmokou.GameInstance {
/// <summary>
/// A feature for handling meter-based special abilities.
/// </summary>
public interface IMeterFeature : IInstanceFeature {
    Event<Unit> MeterBecameUsable { get; }
    double MeterUseThreshold { get; }
    bool EnoughMeterToUse { get; }
    double Meter { get; }
    int MeterFrames { get; }
    double MeterForSwap => 0;

    //Gems are the meter restore item
    bool AllowGemDrops { get; }
    void AddGems(int delta);
    IDisposable? TryStartMeter();
    bool TryUseMeterFrame();

    bool TryConsumeMeterDiscrete(double amount);
    
    Lerpifier<float> VisibleMeter { get; }
}
public class MeterFeature : BaseInstanceFeature, IMeterFeature {
    public const double meterBoostGem = 0.021;
    public const double meterRefillRate = 0.002;
    public const double meterUseRate = 0.314;
    public const double meterUseThreshold = 0.42;
    public const double meterUseInstantCost = 0.042;
    public Event<Unit> MeterBecameUsable { get; }= new();
    public double MeterUseThreshold => meterUseThreshold;
    public double MeterForSwap =>  M.Lerp(0, 3, Inst.Difficulty.Counter, 0.06, 0.12);

    public Lerpifier<float> VisibleMeter { get; }
    private InstanceData Inst { get; }
    public double Meter { get; private set; }

    public bool AllowGemDrops => true;
    public bool EnoughMeterToUse => Meter >= meterUseThreshold;
    private double MeterBoostGraze => M.Lerp(0, 3, Inst.Difficulty.Counter, 0.010, 0.006);
    public int MeterFrames { get; private set; }
    private DMCompactingArray<Unit> consumers = new();

    public MeterFeature(InstanceData inst) {
        Inst = inst;
        ResetMeter();
        VisibleMeter = new Lerpifier<float>((a, b, t) => M.Lerp(a, b, Easers.CEOutPow(t, 3f)), 
            () => (float)Meter, 0.2f);
    }
    
    public void AddMeter(double delta) {
        var belowThreshold = !EnoughMeterToUse;
        Meter = M.Clamp(0, 1, Meter + delta * Inst.Difficulty.meterAcquireMultiplier);
        if (belowThreshold && EnoughMeterToUse) {
            consumers.Compact();
            if (consumers.Count == 0)
                MeterBecameUsable.OnNext(default);
        }
    }

    private void ResetMeter() => 
        Meter = Inst.mode.IsOneCard() ? 0 : 0.7;
    
    public void AddGems(int delta) {
        AddMeter(delta * meterBoostGem);
        foreach (var f in Inst.Features)
            f.OnItemGem(delta);
    }
    
    public IDisposable? TryStartMeter() {
        if (EnoughMeterToUse) {
            Meter -= meterUseInstantCost;
            return consumers.Add(default);
        } else return null;
    }

    public bool TryUseMeterFrame() {
        var consume = meterUseRate * Inst.Difficulty.meterUsageMultiplier * ETime.FRAME_TIME;
        if (Meter >= consume) {
            Meter -= consume;
            return true;
        } else {
            Meter = 0;
            return false;
        }
    }

    public bool TryConsumeMeterDiscrete(double amount) {
        double consume = Math.Max(0, amount * Inst.Difficulty.meterUsageMultiplier);
        if (Meter >= consume) {
            Meter -= consume;
            return true;
        } else
            return false;
    }

    public void OnContinueOrCheckpoint() => ResetMeter();
    public void OnDied() => Meter = 1;

    public void OnGraze(int delta) {
        AddMeter(delta * MeterBoostGraze);
    }

    public void OnPlayerFrame(bool lenient, PlayerController.PlayerState state) {
        if (state == PlayerController.PlayerState.NORMAL)
            AddMeter(meterRefillRate * ETime.FRAME_TIME);
        else if (state == PlayerController.PlayerState.WITCHTIME)
            ++MeterFrames;
    }

    public void OnRegularUpdate() {
        VisibleMeter.Update(ETime.FRAME_TIME);
    }


    public class Disabled : BaseInstanceFeature, IMeterFeature {
        public Event<Unit> MeterBecameUsable { get; } = new();
        public double MeterUseThreshold => 1;
        public bool EnoughMeterToUse => false;
        public double Meter => 0;
        public int MeterFrames => 0;
        public bool AllowGemDrops => false;

        public void AddGems(int delta) => 
            throw new Exception("Meter restore gems are disabled!");

        public IDisposable? TryStartMeter() => null;

        public bool TryUseMeterFrame() => false;

        public bool TryConsumeMeterDiscrete(double amount) => amount <= 0;

        public Lerpifier<float> VisibleMeter { get; } 
            = new(M.Lerp, () => 0, 0.2f);
    }
}

public class MeterFeatureCreator : IFeatureCreator<IMeterFeature> {
    public IMeterFeature Create(InstanceData instance) => new MeterFeature(instance);
}
public class DisabledMeterFeatureCreator : IFeatureCreator<IMeterFeature> {
    public IMeterFeature Create(InstanceData instance) => new MeterFeature.Disabled();
}
}