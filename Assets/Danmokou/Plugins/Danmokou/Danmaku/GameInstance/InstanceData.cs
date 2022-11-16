using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
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
using static Danmokou.GameInstance.InstanceConsts;
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
    public readonly Event<Unit> PlayerTookHit = new();
    public readonly Event<CardRecord> CardHistoryUpdated = new();
    public readonly Event<ExtendType> ExtendAcquired = new();
    public readonly Event<PhaseCompletion> PhaseCompleted = new();
    public readonly Event<Unit> LifeSwappedForScore = new();

    public readonly Event<Unit> GameOver = new();
    public readonly Event<ILowInstanceRequest> PracticeSuccess = new();
    
    #endregion
    public Suzunoya.Data.InstanceData VNData { get; }
    public DifficultySettings Difficulty { get; }

    /* "Features" is an architecture by which game mechanics
     *  can be slotted in or out of InstanceData depending on which game
     *  is constructing it (depending on GameDef.MakeFeatures).
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
    public IScoreFeature ScoreF { get; }
    public IPowerFeature PowerF { get; }
    public ILifeItemFeature LifeItemF { get; }
    public IScoreExtendFeature ScoreExtendF { get; }
    public IRankFeature RankF { get; }
    public IFaithFeature FaithF { get; }
    public IMeterFeature MeterF { get; }
    public Evented<int> Lives { get; }
    public Evented<int> Bombs { get; }
    public Evented<long> Graze { get; }
    public int Continues { get; private set; }
    public int ContinuesUsed { get; private set; } = 0;
    public int HitsTaken { get; private set; }
    public readonly InstanceMode mode;
    /// <summary>
    /// Set to false after eg. a game is completed, but before starting a new game
    /// If the mode is null or modeActive is false, the instance will not update
    /// </summary>
    public bool InstanceActive { get; private set; } = true;
    public void Deactivate() {
        if (InstanceActive) {
            InstanceActive = false;
            Replay?.Cancel();
        }
    }
    
    public ActiveTeamConfig? TeamCfg { get; private set; }

    public ActiveTeamConfig GetOrSetTeam(ActiveTeamConfig deflt) => TeamCfg ??= deflt;

    public CardHistory CardHistory { get; }

    /// <summary>
    /// If this is true, then time-based features (like faith or combo) should not tick.
    /// </summary>
    public readonly DisturbedOr Lenient = new();
    public BehaviorEntity? CurrentBoss { get; private set; }
    private ICancellee? CurrentBossCT { get; set; }

    /// <summary>
    /// Only present for campaign-type games
    /// </summary>
    private readonly CampaignConfig? campaign;
    /// <summary>
    /// Present for all games, including "null_campaign" default for unscoped games
    /// </summary>
    public readonly string campaignKey;
    public InstanceRequest? Request { get; }
    private readonly Dictionary<BossPracticeRequestKey, (int, int)> PreviousSpellHistory;
    public ReplayActor? Replay { get; }
    
    //Miscellaneous stats
    public List<BossConfig> BossesEncountered { get; } = new();
    public int EnemiesDestroyed { get; private set; }
    public int TotalFrames { get; private set; }
    public int PlayerActiveFrames { get; private set; }
    public int LastMeterStartFrame { get; set; }
    public int LastTookHitFrame { get; private set; }
    public int BombsUsed { get; set; }
    public int SubshotSwitches { get; set; }
    public int OneUpItemsCollected { get; private set; }
    
    #region ComputedProperties

    public bool InstanceActiveGuard => mode != InstanceMode.NULL && InstanceActive;
    public double PlayerDamageMultiplier => M.Lerp(0, 3, Difficulty.Counter, 1.20, 1);
    public ShipConfig? Player => TeamCfg?.Ship;
    public Subshot? Subshot => TeamCfg?.Subshot;
    public string MultishotString => (TeamCfg?.HasMultishot == true) ? (Subshot?.Describe() ?? "") : "";
    public bool Continued => ContinuesUsed > 0;
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
        campaign = req?.lowerRequest is CampaignRequest cr ? cr.campaign.campaign : null;
        campaignKey = req?.lowerRequest.Campaign.Key ?? "null_campaign";
        TeamCfg = req?.metadata.team != null ? new ActiveTeamConfig(req.metadata.team) : null;
        var dfltLives = campaign != null ?
            (campaign.startLives > 0 ? campaign.startLives : StartLives(mode)) :
            StartLives(mode);
        Lives = new Evented<int>(Difficulty.startingLives ?? dfltLives);
        Bombs = new Evented<int>(StartBombs(mode));
        Graze = new Evented<long>(0);
        CardHistory = new CardHistory();
        Continues = mode.OneLife() ? 0 : defltContinues;
        HitsTaken = 0;
        EnemiesDestroyed = 0;
        CurrentBoss = null;

        Features.Add(ScoreF = features.Score.Create(this));
        Features.Add(PowerF = features.Power.Create(this));
        Features.Add(LifeItemF = features.ItemExt.Create(this));
        Features.Add(ScoreExtendF = features.ScoreExt.Create(this));
        Features.Add(RankF = features.Rank.Create(this));
        Features.Add(FaithF = features.Faith.Create(this));
        Features.Add(MeterF = features.Meter.Create(this));
    }

    public bool TryContinue() {
        if (Continues > 0) {
            //We can allow continues in replays! But in the current impl, the watcher will have to press continue.
            //Replayer.Cancel();
            --Continues;
            ++ContinuesUsed;
            CardHistory.Clear();//Partial game is saved when lives=0. Don't double on captures.
            if (campaign != null) {
                Lives.Value = campaign.startLives > 0 ? campaign.startLives : StartLives(mode);
            } else {
                Lives.Value = StartLives(mode);
            }
            Bombs.Value = StartBombs(mode);
            foreach (var f in Features)
                f.OnContinue();
            return true;
        } else return false;
    }

    public (int success, int total)? LookForSpellHistory(string bossKey, int phaseIndex) {
        var key = new BossPracticeRequestKey {
            Campaign = campaignKey,
            Boss = bossKey,
            PhaseIndex = phaseIndex
        };
        return PreviousSpellHistory.TryGetValue(key, out var rate) ? rate : ((int, int)?)null;
    }


    /// <summary>
    /// Delta should be negative.
    /// Note: powerbombs do not call this routine.
    /// </summary>
    public bool TryConsumeBombs(int delta) {
        if (Bombs + delta >= 0) {
            Bombs.Value += delta;
            return true;
        }
        return false;
    }

    public void SwapLifeScore(long score, bool usePIVMultiplier) {
        AddLives(-1, false);
        if (usePIVMultiplier) score = (long) (score * ScoreF.PIV);
        ScoreF.AddScore(score);
        LifeSwappedForScore.OnNext(default);
    }
    public void AddLives(int delta, bool asHit = true) {
        //if (mode == CampaignMode.NULL) return;
        Logs.Log($"Adding player lives: {delta}");
        if (delta < 0 && asHit) {
            ++HitsTaken;
            LastTookHitFrame = ETime.FrameNumber;
            Bombs.Value = Math.Max(Bombs, StartBombs(mode));
            foreach (var f in Features)
                f.OnDied();
            PlayerTookHit.OnNext(default);
        }
        if (delta < 0 && mode.OneLife()) 
            Lives.Value = 0;
        else 
            Lives.Value = Math.Max(0, Lives + delta);
        if (Lives == 0) {
            //Record failure
            if (Request?.Saveable == true) {
                //Special-case boss practice handling
                if (Request.lowerRequest is BossPracticeRequest bpr) {
                    CardHistory.Add(new CardRecord() {
                        campaign = bpr.boss.campaign.Key,
                        boss = bpr.boss.boss.key,
                        phase = bpr.phase.index,
                        stars = 0,
                        hits = 1,
                        method = null
                    });
                }
                SaveData.r.RecordGame(new InstanceRecord(Request, this, false));
            }
            GameOver.OnNext(default);
        }
    }

    /// <summary>
    /// Don't use this in the main campaign-- it will interfere with stats
    /// </summary>
    public void SetLives(int to) => AddLives(to - Lives, false);

    public void UpdatePlayerFrame(PlayerController.PlayerState state) {
        ++PlayerActiveFrames;
        foreach (var f in Features)
            f.OnPlayerFrame(Lenient, state);
    }
    
    #region Meter

    
    #endregion

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
        LifeExtend(ExtendType.ONEUP_ITEM);
        foreach (var f in Features)
            f.OnItemOneUp();
    }

    public void LifeExtend(ExtendType method) {
        ++Lives.Value;
        ExtendAcquired.OnNext(method);
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