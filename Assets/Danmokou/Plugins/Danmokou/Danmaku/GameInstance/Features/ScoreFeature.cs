using System;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.DMath;

namespace Danmokou.GameInstance {

/// <summary>
/// A feature for handling scoring (note that score extends are handled in <see cref="IScoreExtendFeature"/>).
/// </summary>
public interface IScoreFeature : IInstanceFeature {
    Evented<long> MaxScore { get; }
    Evented<long> Score { get; }
    DisturbedAggregation<double> Multiplier { get; }
    long ValueItemPoints { get; }
    double SmallValueRatio { get; }
    Lerpifier<long> VisibleScore { get; }
    
    bool AllowPointPlusItemDrops { get; }
    
    /// <summary>
    /// Returns score delta.
    /// </summary>
    long AddValueItems(int delta, double multiplier);

    long AddSmallValueItems(int delta, double multiplier);
    long AddBulletFlakeItem(int delta);
    void AddPointPlusItems(int delta, double multiplier);
    void AddScore(long delta);
}

public class ScoreFeature : BaseInstanceFeature, IScoreFeature, ITokenized {
    public const long smallValueItemPoints = 314;
    public const long valueItemPoints = 3142;
    public long ValueItemPoints => valueItemPoints;
    public const double smallValueRatio = 0.1;
    public double SmallValueRatio => smallValueRatio;
    public const long flakeItemPoints = 42;
    public const double pivPerPPP = 0.01;
    private InstanceData Inst { get; }
    public Evented<long> MaxScore { get; }
    public Evented<long> Score { get; } = new(0);
    public DisturbedAggregation<double> Multiplier { get; } = new(1);
    
    public Lerpifier<long> VisibleScore { get; }
    public bool AllowPointPlusItemDrops { get; }
    
    public ScoreFeature(InstanceData inst, ScoreFeatureCreator creator) {
        Inst = inst;
        MaxScore = new(creator.HighScore ?? 9001);
        VisibleScore = new Lerpifier<long>((a, b, t) => (long)M.Lerp(a, b, (double)Easers.EOutSine(t)), 
            () => Score, 1.3f);
        AllowPointPlusItemDrops = creator.AllowPointPlusItems;
        var grazeMult = new Evented<Aggregator<double, double>>(new(0, (acc, x) => acc + x));
        Tokens.Add(Inst.Graze.Subscribe(g => {
            grazeMult.Value.Data = g / (double)1337;
            grazeMult.OnNext(grazeMult.Value);
        }));
        Tokens.Add(Multiplier.AddDisturbance(grazeMult));
    }
    

    public void AddScore(long delta) {
        Score.Value += delta;
        MaxScore.Value = Math.Max(MaxScore, Score);
        foreach (var f in Inst.Features)
            f.OnScoreChanged(Score);
    }
    
    public long AddValueItems(int delta, double multiplier) {
        long scoreDelta = (long) Math.Round(delta * valueItemPoints * Multiplier.Value * multiplier);
        AddScore(scoreDelta);
        foreach (var f in Inst.Features)
            f.OnItemValue(delta, multiplier);
        return scoreDelta;
    }
    public long AddSmallValueItems(int delta, double multiplier) {
        long scoreDelta = (long) Math.Round(delta * smallValueItemPoints * Multiplier.Value * multiplier);
        AddScore(scoreDelta);
        foreach (var f in Inst.Features)
            f.OnItemSmallValue(delta, multiplier);
        return scoreDelta;
    }

    public long AddBulletFlakeItem(int delta) {
        long scoreDelta = flakeItemPoints * delta;
        AddScore(scoreDelta);
        return scoreDelta;
    }
    public void AddPointPlusItems(int delta, double multiplier) {
        Multiplier.Value += delta * pivPerPPP * multiplier;
        foreach (var f in Inst.Features)
            f.OnItemPointPP(delta, multiplier);
    }

    public void OnContinueOrCheckpoint() {
        Score.Value = 0;
        VisibleScore.HardReset();
        Multiplier.Value = 1;
    }

    public void OnRegularUpdate() {
        VisibleScore.Update(ETime.FRAME_TIME);
    }

    void IDisposable.Dispose() {
        Multiplier.OnCompleted();
        Tokens.DisposeAll();
    }
}

public record ScoreFeatureCreator(long? HighScore) : IFeatureCreator<IScoreFeature> {
    public bool AllowPointPlusItems { get; init; } = true;
    public IScoreFeature Create(InstanceData instance) => new ScoreFeature(instance, this);
}

}