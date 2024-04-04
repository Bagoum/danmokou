using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Functional;
using Danmokou.ADV;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Player;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.VN;
using JetBrains.Annotations;
using UnityEngine;
using Observable = System.Reactive.Linq.Observable;

namespace Danmokou.GameInstance {
/// <summary>
/// Contains information about the currently executing danmaku game.
/// </summary>
public class InstanceData {
    #region StaticEvents
    
    public readonly Event<Unit> UselessPowerupCollected = new();
    public readonly Event<Unit> TeamUpdated = new();
    public readonly Event<CardRecord> CardHistoryUpdated = new();
    public readonly Event<ExtendType> ExtendAcquired = new();
    public readonly Event<PhaseCompletion> PhaseCompleted = new();
    public readonly Event<Unit> LifeSwappedForScore = new();

    public readonly Event<Unit> GameOver = new();
    public readonly Event<ILowInstanceRequest> PracticeSuccess = new();
    
    #endregion
    public DifficultySettings Difficulty { get; }

    /* "Features" is an architecture by which game mechanics
     *  can be slotted in or out of InstanceData depending on which game
     *  is constructing it (via GameDef.MakeFeatures).
     * For example, we might use PowerFeature (Touhou-like 1 to 4 power)
     *  for one game, and DisabledPowerFeature (power is always 4)
     *  for a game in which we do not want power item support.
     * Furthermore, we can tweak handling between games by writing
     *  different interface implementations of eg. IPowerFeature.
     * Features may generically listen to events (see the methods on
     *  IInstanceFeature), or they may have specialized methods called
     *  by consuming code (eg. see the methods on IPowerFeature).
     * 
     * Optional features are handled via a "disabled"
     *  variant (such as PowerFeature.Disabled).
     */
    public List<IInstanceFeature> Features { get; } = new();
    
    /// <inheritdoc cref="IBasicFeature"/>
    public IBasicFeature BasicF { get; }
    
    /// <inheritdoc cref="IConfigurationFeature"/>
    public IConfigurationFeature ConfigurationF { get; }
    
    /// <inheritdoc cref="IScoreFeature"/>
    public IScoreFeature ScoreF { get; }
    
    /// <inheritdoc cref="IPowerFeature"/>
    public IPowerFeature PowerF { get; }
    
    /// <inheritdoc cref="ILifeItemFeature"/>
    public ILifeItemFeature LifeItemF { get; }
    
    /// <inheritdoc cref="IScoreExtendFeature"/>
    public IScoreExtendFeature ScoreExtendF { get; }
    
    /// <inheritdoc cref="IRankFeature"/>
    public IRankFeature RankF { get; }
    
    /// <inheritdoc cref="IFaithFeature"/>
    public IFaithFeature FaithF { get; }
    
    /// <inheritdoc cref="IMeterFeature"/>
    public IMeterFeature MeterF { get; }
    
    /// <inheritdoc cref="ICustomDataFeature"/>
    public ICustomDataFeature CustomDataF { get; }
    
    
    public readonly InstanceMode mode;
    
    /// <summary>
    /// Set to false after eg. a game is completed, but before starting a new game
    /// If the mode is null or modeActive is false, the instance will not update
    /// </summary>
    public bool InstanceActive { get; private set; } = true;
    public void Deactivate(bool isProperCompletion) {
        if (InstanceActive) {
            InstanceActive = false;
            Request?.Cancel();
            if (isProperCompletion) {
                Replay?.TryFinish();
            } else
                Replay?.Cancel();
        }
    }
    
    public ActiveTeamConfig? TeamCfg { get; private set; }

    public ActiveTeamConfig GetOrSetTeam(ActiveTeamConfig deflt) {
        if (TeamCfg is null) {
            TeamCfg = deflt;
            TeamUpdated.OnNext(default);
        }
        return TeamCfg;
    }

    /// <summary>
    /// If this is true, then time-based features (like faith or combo) should not tick.
    /// </summary>
    public readonly DisturbedOr Lenient = new();
    public BehaviorEntity? CurrentBoss { get; private set; }
    private ICancellee? CurrentBossCT { get; set; }

    /// <summary>
    /// Present for all games, including "null_campaign" default for unscoped games
    /// </summary>
    public readonly string campaignKey;
    public InstanceRequest? Request { get; }
    private readonly Dictionary<BossPracticeRequestKey, (int, int)> PreviousSpellHistory;
    
    #region DynamicData

    public Suzunoya.Data.InstanceData VNData { get; }
    public CardHistory CardHistory { get; }
    public List<BossConfig> BossesEncountered { get; } = new();
    public ReplayActor? Replay { get; }
    public Evented<long> Graze { get; }
    public int EnemiesDestroyed { get; private set; }
    public int TotalFrames { get; private set; }
    public int PlayerActiveFrames { get; private set; }
    public int LastMeterStartFrame { get; set; }
    public int BombsUsed { get; set; }
    public int SubshotSwitches { get; set; }
    public int OneUpItemsCollected { get; private set; }
    
    #endregion
    
    #region Checkpoint

    public Checkpoint? LastCheckpoint { get; private set; }
    private readonly OverrideEvented<(IStageConfig, Action<Checkpoint>)?> checkpointHandler = new(null);
    public bool CanRestartCheckpoint => LastCheckpoint != null && checkpointHandler.Value != null;
    
    #endregion
    
    #region ComputedProperties

    public bool InstanceActiveGuard => mode != InstanceMode.NULL && InstanceActive;
    public double PlayerDamageMultiplier => M.Lerp(0, 3, Difficulty.Counter, 1.20, 1);
    public ShipConfig? Player => TeamCfg?.Ship;
    public Subshot? Subshot => TeamCfg?.Subshot;
    public string MultishotString => (TeamCfg?.HasMultishot == true) ? (Subshot?.Describe() ?? "") : "";
    public bool Continued => BasicF.ContinuesUsed > 0;
    public bool IsCampaign => mode == InstanceMode.CAMPAIGN;
    public bool IsAtleastNormalCampaign => IsCampaign && Difficulty.standard >= FixedDifficulty.Normal;
    
    #endregion

    public InstanceData(InstanceMode mode, InstanceFeatures features, InstanceRequest? req, ReplayActor? replay) {
        VNData = new(SaveData.r.GlobalVNData);
        this.Request = req;
        this.Replay = replay;
        //Minor hack to avoid running the SaveData static constructor in the editor during type initialization
        PreviousSpellHistory = (req == null) ? 
            new Dictionary<BossPracticeRequestKey, (int, int)>() :
            SaveData.r.GetCampaignSpellHistory();
        
        this.mode = mode;
        this.Difficulty = req?.metadata.difficulty ?? GameManagement.defaultDifficulty;
        Difficulty.FixVariables();
        campaignKey = req?.lowerRequest.Campaign.Key ?? "null_campaign";
        TeamCfg = req?.metadata.team != null ? new ActiveTeamConfig(req.metadata.team) : null;
        Graze = new Evented<long>(0);
        CardHistory = new CardHistory();
        EnemiesDestroyed = 0;
        CurrentBoss = null;

        Features.Add(BasicF = features.Basic.Create(this));
        Features.Add(ConfigurationF = features.Configuration.Create(this));
        Features.Add(ScoreF = features.Score.Create(this));
        Features.Add(PowerF = features.Power.Create(this));
        Features.Add(LifeItemF = features.ItemExt.Create(this));
        Features.Add(ScoreExtendF = features.ScoreExt.Create(this));
        Features.Add(RankF = features.Rank.Create(this));
        Features.Add(FaithF = features.Faith.Create(this));
        Features.Add(MeterF = features.Meter.Create(this));
        Features.Add(CustomDataF = features.CustomData.Create(this));
    }

    public (int success, int total)? LookForSpellHistory(string bossKey, int phaseIndex) {
        var key = new BossPracticeRequestKey {
            Campaign = campaignKey,
            Boss = bossKey,
            PhaseIndex = phaseIndex
        };
        return PreviousSpellHistory.TryGetValue(key, out var rate) ? rate : ((int, int)?)null;
    }

    public void SwapLifeScore(long score, bool usePIVMultiplier) {
        BasicF.AddLives(-1, false);
        if (usePIVMultiplier) score = (long) (score * ScoreF.Multiplier);
        ScoreF.AddScore(score);
        LifeSwappedForScore.OnNext(default);
    }

    /// <summary>
    /// Don't use this in the main campaign-- it will interfere with stats
    /// </summary>
    public void SetLives(int to) => BasicF.AddLives(to - BasicF.Lives, false);

    public void UpdatePlayerFrame(PlayerController.PlayerState state) {
        ++PlayerActiveFrames;
        foreach (var f in Features)
            f.OnPlayerFrame(Lenient, state);
    }

    public void AddGraze(int delta) {
        Graze.Value += delta;
        foreach (var f in Features)
            f.OnGraze(delta);
        Counter.GrazeProc(delta);
    }

    /// <summary>
    /// Add a lenience period (ie. do not count down in a way that hurts the player) for time-based mechanics.
    /// </summary>
    /// <param name="time">Time in seconds</param>
    public void AddLenience(double time) {
        foreach (var f in Features)
            f.AddLenience(time);
    }

    public void AddOneUpItem() {
        ++OneUpItemsCollected;
        BasicF.LifeExtend(ExtendType.ONEUP_ITEM);
        foreach (var f in Features)
            f.OnItemOneUp();
    }

    public void PhaseEnd(PhaseCompletion pc) {
        CardRecord? ncrec = null;
        if (pc.phase.Props.phaseType?.IsCard() == true && pc.phase.Boss != null && 
            pc.CaptureStars.Try(out var captured)) {
            var crec = new CardRecord() {
                campaign = campaignKey,
                boss = pc.phase.Boss.key,
                phase = pc.phase.Index,
                stars = captured,
                hits = pc.hits,
                method = pc.clear
            };
            ncrec = crec;
            CardHistory.Add(crec);
            CardHistoryUpdated.OnNext(crec);
        }
        foreach (var f in Features)
            f.OnPhaseEnd(in pc, in ncrec);
        PhaseCompleted.OnNext(pc);
    }

    public void NormalEnemyDestroyed() {
        ++EnemiesDestroyed;
        foreach (var f in Features)
            f.OnEnemyDestroyed();
    }

    public void _RegularUpdate() {
        if (!InstanceActiveGuard) return;
        ++TotalFrames;
        if (CurrentBossCT?.Cancelled == true) {
            CloseBoss();
        }
        foreach (var f in Features)
            f.OnRegularUpdate();
    }


    public void SetCurrentBoss(BossConfig cfg, BehaviorEntity boss, ICancellee bossCT) {
        if (CurrentBossCT != null) CloseBoss();
        BossesEncountered.Add(cfg);
        CurrentBoss = boss;
        CurrentBossCT = bossCT;
    }

    private void CloseBoss() {
        if (CurrentBossCT != null) {
            CurrentBoss = null;
            CurrentBossCT = null;
        } else Logs.UnityError("You tried to close a boss section when no boss exists.");
    }

    /// <summary>
    /// Restarts the game instance from the beginning (same information, new random seed).
    /// </summary>
    public bool Restart() {
        if (Request == null) throw new Exception("No game instance found to restart");
        Request.Cancel();
        InstanceRequest.InstanceRestarted.OnNext(Request);
        return Request.Copy().Run();
    }

    public IDisposable SetStageCheckpointCancel(IStageConfig stage, Action<Checkpoint> canceller) {
        if (Replay is ReplayPlayer) return NullDisposable.Default;
        return checkpointHandler.AddConst((stage, canceller));
    }

    public void UpdateStageCheckpoint(int checkpointPhase, bool allowBossCheckpoints) {
        if (Replay is ReplayPlayer) return;
        if (checkpointHandler.Value is { Item1: { } stage }) {
            Logs.Log($"Setting a checkpoint at phase {checkpointPhase} (this is a stageboss: {allowBossCheckpoints})");
            LastCheckpoint = new(stage, checkpointPhase, allowBossCheckpoints);
        } else
            throw new Exception("There is no active stage handler on which to update a checkpoint.");
    }

    public void UpdateStageNoCheckpoint() {
        if (Replay is ReplayPlayer) return;
        if (LastCheckpoint != null)
            LastCheckpoint = LastCheckpoint with { AllowBossCheckpoint = false };
    }

    public void UpdateBossCheckpoint(BossConfig boss, int checkpointPhase) {
        if (Replay is ReplayPlayer) return;
        if (LastCheckpoint != null) {
            Logs.Log($"Setting a boss checkpoint ({boss.key}) at phase {checkpointPhase} " +
                     $"(stage phase: {LastCheckpoint.StagePhase})");
            LastCheckpoint = LastCheckpoint.SetBossCheckpoint(boss, checkpointPhase);
        } else
            throw new Exception("Could not set a boss checkpoint because a level checkpoint has not yet been set.");
    }

    public void RestartFromCheckpoint() {
        if (CanRestartCheckpoint && checkpointHandler.Value is { Item2: {} cb}) {
            Logs.Log($"Restarting from checkpoint: {LastCheckpoint}");
            Replay?.Cancel();
            foreach (var f in Features)
                f.OnContinueOrCheckpoint();
            cb(LastCheckpoint!);
        } else Logs.UnityError("No checkpoint is recorded, but you tried to restart from a checkpoint.");
    }

    public void Dispose() {
        foreach (var f in Features)
            f.Dispose();
    }
    
#if UNITY_EDITOR
    public void SetPower(double x) => PowerF.Power.Value = x;
    #endif
}

/// <summary>
/// Methods by which a player can gain a life.
/// </summary>
public enum ExtendType {
    SCORE,
    LIFE_ITEM,
    ONEUP_ITEM
}

}