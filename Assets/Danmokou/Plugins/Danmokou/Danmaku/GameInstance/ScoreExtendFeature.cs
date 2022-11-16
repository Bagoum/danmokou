using System.Reactive;
using BagoumLib;
using BagoumLib.Events;
using Danmokou.Core;

namespace Danmokou.GameInstance {
/// <summary>
/// A feature for handling extends from score thresholds.
/// </summary>
public interface IScoreExtendFeature : IInstanceFeature {
    public ICObservable<long?> NextScoreLife { get; }
}

public class ScoreExtendFeature : BaseInstanceFeature, IScoreExtendFeature {
    public static readonly long[] scoreLives = {
        2000000,
        5000000,
        10000000,
        15000000,
        20000000,
        25000000,
        30000000,
        40000000,
        50000000,
        60000000,
        70000000,
        80000000,
        100000000
    };
    private InstanceData Inst { get; }
    private Evented<int> nextScoreLifeIndex = new(0);

    public ICObservable<long?> NextScoreLife { get; }

    public ScoreExtendFeature(InstanceData inst) {
        Inst = inst;
        NextScoreLife = Inst.mode.OneLife() ?
            new Evented<long?>(null) :
            nextScoreLifeIndex.Map(scoreLives.TryN);
    }
    
    public void OnContinue() {
        nextScoreLifeIndex.Value = 0;
    }

    public void OnScoreChanged(long score) {
        if (NextScoreLife.Value.Try(out var next) && score >= next) {
            ++nextScoreLifeIndex.Value;
            Inst.LifeExtend(ExtendType.SCORE);
        }
    }
}

public class ScoreExtendFeatureCreator : IFeatureCreator<IScoreExtendFeature> {
    public IScoreExtendFeature Create(InstanceData instance) => new ScoreExtendFeature(instance);
}
}