using System;
using BagoumLib;
using BagoumLib.Events;
using Danmokou.Behavior.Items;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Player;
using Danmokou.Services;

namespace Danmokou.GameInstance {
/// <summary>
/// A feature for handling rank (dynamic difficulty).
/// </summary>
public interface IRankFeature : IInstanceFeature {
    /// <summary>
    /// True iff rank level was increased
    /// </summary>
    public IBSubject<bool> RankLevelChanged { get; } 
    public int MinRankLevel { get; }
    public int MaxRankLevel { get; }
    public int RankLevel { get; }
    public double RankRatio { get; }
    public Lerpifier<float> VisibleRankPointFill { get; }
}

public class RankFeature : BaseInstanceFeature, IRankFeature {
    #region Consts
    /// <summary>
    /// Inclusive
    /// </summary>
    public const int minRankLevel = 0;
    /// <summary>
    /// Inclusive
    /// </summary>
    public const int maxRankLevel = 42;
    public int MinRankLevel => minRankLevel;
    public int MaxRankLevel => maxRankLevel;

    //Note: since you get half of this by default, you only need the other half to go up or down a level.

    public static double RankPointsRequiredForLevel(int level) =>
        2000;

    ///M.BlockRound(100, 1000 * (1 + Math.Log(Math.Max(1, level), 4)));
    
    public static double DefaultRankPointsForLevel(int level) => RankPointsRequiredForLevel(level) * 0.5;

    public const double RankPointsGraze = 8;
    public const double RankPointsCollectItem = 3;
    public const double RankPointsMissedItem = -5;
    public const double RankPointsScoreExtend = 420;
    public const double RankPointsDeath = -10000;
    public const double RankPointsBomb = -1000;

    public double RankPointsForCard(CardRecord cr) => 100 * cr.stars;
    public double RankPointsPerSecond => M.Lerp(0, 3, Inst.Difficulty.Counter, 10, 42);

    #endregion
    
    public IBSubject<bool> RankLevelChanged { get; } = new Event<bool>();
    
    private InstanceData Inst { get; }
    public int RankLevel { get; set; }
    public double RankPoints { get; set; }
    public double RankPointsRequired => RankPointsRequiredForLevel(RankLevel);
    public double RankRatio => (RankLevel - minRankLevel) / (double)(maxRankLevel - minRankLevel);

    //Each difficulty has its own rank limits, which form a subset of
    // the entire [minRankLevel, maxRankLevel] range
    private int minLevelByDifficulty => Inst.Difficulty.ApproximateStandard switch {
        FixedDifficulty.Lunatic => 12,
        FixedDifficulty.Hard => 8,
        FixedDifficulty.Normal => 4,
        _ => minRankLevel
    };
    private int maxLevelByDifficulty => Inst.Difficulty.ApproximateStandard switch {
        FixedDifficulty.Lunatic => maxRankLevel,
        FixedDifficulty.Hard => 35,
        FixedDifficulty.Normal => 29,
        _ => 23
    };
    
    public Lerpifier<float> VisibleRankPointFill { get; }
    
    
    public RankFeature(InstanceData inst) {
        Inst = inst;
        
        this.RankLevel = Inst.Difficulty.customRank ?? Inst.Difficulty.ApproximateStandard.DefaultRank();
        this.RankPoints = DefaultRankPointsForLevel(RankLevel);
        VisibleRankPointFill = new Lerpifier<float>((a, b, t) => M.Lerp(a, b, M.EOutPow(t, 2f)),
            () => (float) (RankPoints / RankPointsRequired), 0.3f);
        
        Tokens.Add(Inst.ExtendAcquired.Subscribe(HandleExtend));
        Tokens.Add(Item.ItemCollected.Subscribe(_ => AddRankPoints(RankPointsCollectItem)));
        Tokens.Add(Item.ItemCulled.Subscribe(_ => AddRankPoints(RankPointsMissedItem)));
        Tokens.Add(PlayerController.BombFired.Subscribe(_ => AddRankPoints(RankPointsBomb)));
    }
    
    
    public void AddRankPoints(double delta) {
        if (!Inst.InstanceActiveGuard) return;
        while (delta != 0) {
            RankPoints += delta;
            if (RankPoints < 0) {
                delta = RankPoints;
                RankPoints = 0;
                if (!SetRankLevel(RankLevel - 1)) break;
            } else if (RankPoints > RankPointsRequired) {
                delta = RankPoints - RankPointsRequired;
                RankPoints = RankPointsRequired;
                if (!SetRankLevel(RankLevel + 1)) break;
            } else
                delta = 0;
        }
    }
    
    public bool SetRankLevel(int level, double? points = null) {
        if (!Inst.InstanceActiveGuard) return false;
        level = M.Clamp(minLevelByDifficulty, maxLevelByDifficulty, level);
        if (RankLevel == level) return false;
        bool increaseRank = level > RankLevel;
        RankLevel = level;
        RankPoints = M.Clamp(0, RankPointsRequired - 1, points ?? DefaultRankPointsForLevel(level));
        RankLevelChanged.OnNext(increaseRank);
        return true;
    }

    void HandleExtend(ExtendType ext) {
        AddRankPoints(RankPointsScoreExtend);
    }

    public void OnPlayerFrame(bool lenient, PlayerController.PlayerState state) {
        if (Inst.PlayerActiveFrames % ETime.ENGINEFPS == 0) {
            AddRankPoints(RankPointsPerSecond);
        }
    }

    public void OnRegularUpdate() {
        VisibleRankPointFill.Update(ETime.FRAME_TIME);
    }

    public void OnGraze(int delta) {
        AddRankPoints(delta * RankPointsGraze);
    }
    
    public void OnDied() {
        AddRankPoints(RankPointsDeath);
    }

    public void OnContinue() {
        SetRankLevel(minLevelByDifficulty);
    }

    public void OnPhaseEnd(in PhaseCompletion pc, in CardRecord? crec) {
        if (crec.Try(out var c))
            AddRankPoints(RankPointsForCard(c));
    }

    public class Disabled : BaseInstanceFeature, IRankFeature {
        public IBSubject<bool> RankLevelChanged { get; } = new Event<bool>();
        public int MinRankLevel => 0;
        public int MaxRankLevel => 1;
        public int RankLevel => 0;
        public double RankRatio => 0;
        public Lerpifier<float> VisibleRankPointFill { get; } = new((a, b, t) => M.Lerp(a, b, M.EOutPow(t, 2f)),
        () => 0, 0.3f);
    }
}

public class RankFeatureCreator : IFeatureCreator<IRankFeature> {
    public IRankFeature Create(InstanceData instance) => new RankFeature(instance);
}

public class DisabledRankFeatureCreator : IFeatureCreator<IRankFeature> {
    public IRankFeature Create(InstanceData instance) => new RankFeature.Disabled();
}

}