using System;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Player;

namespace Danmokou.GameInstance {
/// <summary>
/// A feature for handling faith (the MoF combo-like system).
/// </summary>
public interface IFaithFeature : IInstanceFeature {
    public double Faith { get; }
    public double FaithLenience { get; }
    public DisturbedEvented<float,float> externalFaithDecayMultiplier { get; }
    public Lerpifier<float> VisibleFaith { get; }
    public Lerpifier<float> VisibleFaithLenience { get; }
}

public class FaithFeature : BaseInstanceFeature, IFaithFeature {
    public const double faithPivFallStep = 0.1;
    public const double faithDecayRate = 0.12;
    public const double faithLenienceFall = 5;
    public const double faithLenienceValue = 0.2;
    public const double faithLeniencePointPP = 0.3;
    public const double faithLenienceEnemyDestroy = 0.1;
    public const double faithBoostValue = 0.02;
    public const double faithBoostPointPP = 0.09;
    public const double faithLeniencePhase = 4;
    private InstanceData Inst { get; }
    public double Faith { get; private set; } = 1f;
    public double FaithLenience { get; private set; } = 0f;
    public DisturbedEvented<float,float> externalFaithDecayMultiplier { get;  }= new DisturbedProduct<float>(1);
    
    private double FaithDecayRateMultiplier => (Inst.CurrentBoss != null ? 0.666f : 1f) * externalFaithDecayMultiplier.Value;
    private double FaithLenienceGraze => M.Lerp(0, 3, Inst.Difficulty.Counter, 0.42, 0.3);
    private double FaithBoostGraze => M.Lerp(0, 3, Inst.Difficulty.Counter, 0.033, 0.02);
    public Lerpifier<float> VisibleFaith { get; }
    public Lerpifier<float> VisibleFaithLenience { get; }

    public FaithFeature(InstanceData inst) {
        Inst = inst;
        VisibleFaith = new Lerpifier<float>((a, b, t) => M.Lerp(a, b, Easers.CEOutPow(t, 4f)), 
            () => (float)Faith, 0.2f);
        VisibleFaithLenience = new Lerpifier<float>((a, b, t) => M.Lerp(a, b, Easers.CEOutPow(t, 3f)), 
            () => (float)Math.Min(1, FaithLenience / 3), 0.2f);
    }

    public void AddFaith(double delta) {
        Faith = M.Clamp(0, 1, Faith + delta * Inst.Difficulty.faithAcquireMultiplier);
    }

    public void AddFaithLenience(double time) {
        FaithLenience = Math.Max(FaithLenience, time);
    }

    public void AddLenience(double time) => AddFaithLenience(time);

    public void OnContinueOrCheckpoint() {
        Faith = FaithLenience = 0;
    }

    public void OnPlayerFrame(bool lenient, PlayerController.PlayerState state) {
        if (!lenient) {
            if (FaithLenience > 0) {
                FaithLenience = Math.Max(0, FaithLenience - ETime.FRAME_TIME);
            } else if (Faith > 0) {
                Faith = Math.Max(0, Faith - ETime.FRAME_TIME *
                    faithDecayRate * FaithDecayRateMultiplier * Inst.Difficulty.faithDecayMultiplier);
            } else if (Inst.ScoreF.Multiplier > 1) {
                Inst.ScoreF.Multiplier.Value = Math.Max(1, Inst.ScoreF.Multiplier - faithPivFallStep);
                Faith = 0.5f;
                FaithLenience = faithLenienceFall;
            }
        }
    }

    public void OnRegularUpdate() {
        VisibleFaith.Update(ETime.FRAME_TIME);
        VisibleFaithLenience.Update(ETime.FRAME_TIME);
    }

    public void OnGraze(int delta) {
        AddFaith(delta * FaithBoostGraze);
        AddFaithLenience(FaithLenienceGraze);
    }

    public void OnItemValue(int delta, double _) {
        AddFaith(delta * faithBoostValue);
        AddFaithLenience(faithLenienceValue);
    }

    public void OnItemSmallValue(int delta, double _) {
        AddFaith(delta * faithBoostValue * 0.1);
        AddFaithLenience(faithLenienceValue * 0.1);
    }

    public void OnItemPointPP(int delta, double _) {
        AddFaith(delta * faithBoostPointPP);
        AddFaithLenience(faithLeniencePointPP);
    }

    public void OnPhaseEnd(in PhaseCompletion pc) {
        if (pc.phase.Props.phaseType?.IsPattern() ?? false) AddFaithLenience(faithLeniencePhase);
    }

    public void OnEnemyDestroyed() {
        AddFaithLenience(faithLenienceEnemyDestroy);
    }

    public class Disabled : BaseInstanceFeature, IFaithFeature {
        public double Faith => 0;
        public double FaithLenience => 0;
        public DisturbedEvented<float,float> externalFaithDecayMultiplier { get; } = new DisturbedProduct<float>(1);
        public Lerpifier<float> VisibleFaith { get; }
            = new(M.Lerp,() => 0, 0.2f);
        public Lerpifier<float> VisibleFaithLenience { get; }
            = new(M.Lerp,() => 0, 0.2f);
    }
    
}

public class FaithFeatureCreator : IFeatureCreator<IFaithFeature> {
    public IFaithFeature Create(InstanceData instance) => new FaithFeature(instance);
}
public class DisabledFaithFeatureCreator : IFeatureCreator<IFaithFeature> {
    public IFaithFeature Create(InstanceData instance) => new FaithFeature.Disabled();
}


}