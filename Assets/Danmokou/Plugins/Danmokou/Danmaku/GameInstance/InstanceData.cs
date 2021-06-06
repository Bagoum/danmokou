using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Player;
using Danmokou.Scriptables;
using Danmokou.Services;
using static Danmokou.GameInstance.InstanceConsts;
using JetBrains.Annotations;
using UnityEngine;
using Observable = System.Reactive.Linq.Observable;

namespace Danmokou.GameInstance {
public class InstanceData {
    #region StaticEvents
    
    public static readonly Events.Event0 UselessPowerupCollected = new Events.Event0();
    public static readonly Events.Event0 TeamUpdated = new Events.Event0();
    public static readonly Events.Event0 PlayerTookHit = new Events.Event0();
    public static readonly IBSubject<CardRecord> CardHistoryUpdated = new Event<CardRecord>();
    public static readonly Events.Event0 MeterNowUsable = new Events.Event0();
    public static readonly Events.Event0 PowerLost = new Events.Event0();
    public static readonly Events.Event0 PowerGained = new Events.Event0();
    public static readonly Events.Event0 PowerFull = new Events.Event0();
    public static readonly Events.Event0 AnyExtendAcquired = new Events.Event0();
    public static readonly Events.Event0 ItemExtendAcquired = new Events.Event0();
    public static readonly Events.Event0 ScoreExtendAcquired = new Events.Event0();
    public static readonly IBSubject<PhaseCompletion> PhaseCompleted = new Event<PhaseCompletion>();
    public static readonly Events.Event0 LifeSwappedForScore = new Events.Event0();

    public static readonly Events.Event0 GameOver = new Events.Event0();
    public static readonly Events.Event0 PracticeSuccess = new Events.Event0();
    
    #endregion
    
    public DifficultySettings Difficulty { get; }
    public int RankLevel { get; set; }
    public double RankPoints { get; set; }
    public Evented<long> MaxScore { get; }
    public Evented<long> Score { get; }
    public Evented<int> Lives { get; }
    public Evented<int> Bombs { get; }
    public Evented<int> LifeItems { get; }
    public Evented<long> Graze { get; }
    public Evented<double> Power { get; }
    public Evented<double> PIV { get; }
    public double Faith { get; private set; }
    private double faithLenience;
    public readonly MultiMultiplierD externalFaithDecayMultiplier = new MultiMultiplierD(1, null);
    public double Meter { get; private set; }
    public int Continues { get; private set; }
    public int ContinuesUsed { get; private set; } = 0;
    public int HitsTaken { get; private set; }
    private int nextScoreLifeIndex;
    private int nextItemLifeIndex;
    public readonly InstanceMode mode;
    /// <summary>
    /// Set to false after eg. a game is completed, but before starting a new game
    /// If the mode is null or modeActive is false, the instance will not update
    /// </summary>
    public bool InstanceActive { get; private set; }= true;
    public void Deactivate() {
        if (InstanceActive) {
            InstanceActive = false;
            Replay?.Cancel();
        }
    }

    private bool InstanceActiveGuard() => mode != InstanceMode.NULL && InstanceActive;
    
    public ActiveTeamConfig? TeamCfg { get; }
    
    public CardHistory CardHistory { get; }

    public readonly MultiAdder Lenience = new MultiAdder(0, null);
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
    private readonly Dictionary<((string, string), int), (int, int)> PreviousSpellHistory;
    public ReplayActor? Replay { get; }
    
    //Miscellaneous stats
    public List<BossConfig> BossesEncountered { get; } = new List<BossConfig>();
    public int EnemiesDestroyed { get; private set; }
    public int TotalFrames { get; private set; }
    public int PlayerActiveFrames { get; private set; }
    public int LastMeterStartFrame { get; set; }
    public int LastTookHitFrame { get; private set; }
    public int MeterFrames { get; set; }
    public int BombsUsed { get; set; }
    public int SubshotSwitches { get; set; }
    public int OneUpItemsCollected { get; private set; }
    
    #region ComputedProperties
    public double RankPointsRequired => RankManager.RankPointsRequiredForLevel(RankLevel);
    public double RankRatio => (RankLevel - RankManager.minRankLevel) / (double)(RankManager.maxRankLevel - RankManager.minRankLevel);
    public int NextLifeItems => pointLives.Try(nextItemLifeIndex, 9001);
    public double PlayerDamageMultiplier => M.Lerp(0, 3, Difficulty.Counter, 1.20, 1);
    public int PowerF => (int)Math.Floor(Power);
    public int PowerIndex => PowerF - (int) powerMin;
    public double EffectivePIV => PIV + Graze / (double)1337;
    private double FaithDecayRateMultiplier => (CurrentBoss != null ? 0.666f : 1f) * externalFaithDecayMultiplier.Value;
    private double FaithLenienceGraze => M.Lerp(0, 3, Difficulty.Counter, 0.42, 0.3);
    private double FaithBoostGraze => M.Lerp(0, 3, Difficulty.Counter, 0.033, 0.02);
    public bool Lenient => Lenience.Value > 0;
    public bool MeterEnabled => MeterMechanicEnabled && Difficulty.meterEnabled;
    public bool EnoughMeterToUse => MeterEnabled && Meter >= meterUseThreshold;
    private double MeterBoostGraze => M.Lerp(0, 3, Difficulty.Counter, 0.010, 0.006);
    public long? NextScoreLife => mode.OneLife() ? null : scoreLives.TryN(nextScoreLifeIndex);
    public ShipConfig? Player => TeamCfg?.Ship;
    public Subshot? Subshot => TeamCfg?.Subshot;
    public string MultishotString => (TeamCfg?.HasMultishot == true) ? (Subshot?.Describe() ?? "") : "";
    public bool Continued => ContinuesUsed > 0;
    public bool IsCampaign => mode == InstanceMode.CAMPAIGN;
    public bool IsAtleastNormalCampaign => IsCampaign && Difficulty.standard >= FixedDifficulty.Normal;
    
    #endregion

    #region UILerpers

    //The reason for the static duplication is that the variables are instanced, but the events are static.
    // It's regrettable but I'm not exactly sure how to resolve it.
    public static readonly IBSubject<long> sMaxScore = new Evented<long>(0);
    public static readonly IBSubject<long> sScore = new Evented<long>(0);
    public static readonly IBSubject<long> sGraze = new Evented<long>(0);
    public static readonly IBSubject<int> sLives = new Evented<int>(0);
    public static readonly IBSubject<int> sLifeItems = new Evented<int>(0);
    public static readonly IBSubject<int> sBombs = new Evented<int>(0);
    public static readonly IBSubject<double> sPower = new Evented<double>(0);
    public static readonly IBSubject<double> sPIV = new Evented<double>(0);
    
    private readonly Lerpifier<long> VisibleScore;
    private readonly Lerpifier<float> VisibleMeter;
    private readonly Lerpifier<float> VisibleFaith;
    private readonly Lerpifier<float> VisibleFaithLenience;
    private readonly Lerpifier<float> VisibleRankPointFill;
    public static readonly Evented<long> sVisibleScore = new Evented<long>(0);
    public static readonly Evented<float> sVisibleMeter = new Evented<float>(0);
    public static readonly Evented<float> sVisibleFaith = new Evented<float>(0);
    public static readonly Evented<float> sVisibleFaithLenience = new Evented<float>(0);
    public static readonly Evented<float> sVisibleRankPointFill = new Evented<float>(0);

    private readonly List<IDisposable> Tokens = new List<IDisposable>();
    #endregion
    
    public InstanceData(InstanceMode mode, InstanceRequest? req, long? maxScore, ReplayActor? replay) {
        this.Request = req;
        this.Replay = replay;
        //Minor hack to avoid running the SaveData static constructor in the editor during type initialization
        PreviousSpellHistory = (req == null) ? 
            new Dictionary<((string, string), int), (int, int)>() :
            SaveData.r.GetCampaignSpellHistory();
        
        this.mode = mode;
        this.Difficulty = req?.metadata.difficulty ?? GameManagement.defaultDifficulty;
        this.RankLevel = Difficulty.customRank ?? Difficulty.ApproximateStandard.DefaultRank();
        this.RankPoints = RankManager.DefaultRankPointsForLevel(RankLevel);
        campaign = req?.lowerRequest.Resolve(cr => cr.campaign.campaign, _ => null!, _ => null!, _ => null!);
        campaignKey = req?.lowerRequest.Resolve(cr => cr.Key, b => b.boss.campaign.Key, s => s.Campaign.key,
            s => s.stage.campaign.Key) ?? "null_campaign";
        TeamCfg = req?.metadata.team != null ? new ActiveTeamConfig(req.metadata.team) : null;
        var dfltLives = campaign != null ?
            (campaign.startLives > 0 ? campaign.startLives : StartLives(mode)) :
            StartLives(mode);
        MaxScore = new Evented<long>(maxScore ?? 9001);
        Tokens.Add(MaxScore.Subscribe(sMaxScore.OnNext));
        Score = new Evented<long>(0);
        Tokens.Add(Score.Subscribe(sScore.OnNext));
        Lives = new Evented<int>(Difficulty.startingLives ?? dfltLives);
        Tokens.Add(Lives.Subscribe(sLives.OnNext));
        Bombs = new Evented<int>(StartBombs(mode));
        Tokens.Add(Bombs.Subscribe(sBombs.OnNext));
        Power = new Evented<double>(StartPower(mode));
        Tokens.Add(Power.Subscribe(sPower.OnNext));
        PIV = new Evented<double>(1);
        Tokens.Add(PIV.Subscribe(sPIV.OnNext));
        LifeItems = new Evented<int>(0);
        Tokens.Add(LifeItems.Subscribe(sLifeItems.OnNext));
        Graze = new Evented<long>(0);
        Tokens.Add(Graze.Subscribe(sGraze.OnNext));
        CardHistory = new CardHistory();
        Meter = StartMeter(mode);
        nextScoreLifeIndex = 0;
        nextItemLifeIndex = 0;
        Faith = 1f;
        faithLenience = 0f;
        Continues = mode.OneLife() ? 0 : defltContinues;
        HitsTaken = 0;
        EnemiesDestroyed = 0;
        CurrentBoss = null;
        
        VisibleScore = new Lerpifier<long>((a, b, t) => (long)M.Lerp(a, b, (double)M.EOutSine(t)), 
            () => Score, 1.3f);
        Tokens.Add(VisibleScore.Subscribe(sVisibleScore.OnNext));
        VisibleMeter = new Lerpifier<float>((a, b, t) => M.Lerp(a, b, M.EOutPow(t, 3f)), 
            () => (float)Meter, 0.2f);
        Tokens.Add(VisibleMeter.Subscribe(sVisibleMeter.OnNext));
        VisibleFaith = new Lerpifier<float>((a, b, t) => M.Lerp(a, b, M.EOutPow(t, 4f)), 
            () => (float)Faith, 0.2f);
        Tokens.Add(VisibleFaith.Subscribe(sVisibleFaith.OnNext));
        VisibleFaithLenience = new Lerpifier<float>((a, b, t) => M.Lerp(a, b, M.EOutPow(t, 3f)), 
            () => (float)Math.Min(1, faithLenience / 3), 0.2f);
        Tokens.Add(VisibleFaithLenience.Subscribe(sVisibleFaithLenience.OnNext));
        VisibleRankPointFill = new Lerpifier<float>((a, b, t) => M.Lerp(a, b, M.EOutPow(t, 2f)),
            () => (float) (RankPoints / RankPointsRequired), 0.3f);
        Tokens.Add(VisibleRankPointFill.Subscribe(sVisibleRankPointFill.OnNext));
    }

    public bool TryContinue() {
        if (Continues > 0) {
            //We can allow continues in replays! But in the current impl, the watcher will have to press continue.
            //Replayer.Cancel();
            --Continues;
            ++ContinuesUsed;
            CardHistory.Clear();//Partial game is saved when lives=0. Don't double on captures.
            Score.Value = nextItemLifeIndex = nextScoreLifeIndex = LifeItems.Value = 0;
            VisibleScore.HardReset();
            PIV.Value = 1;
            Meter = StartMeter(mode);
            if (campaign != null) {
                Lives.Value = campaign.startLives > 0 ? campaign.startLives : StartLives(mode);
            } else {
                Lives.Value = StartLives(mode);
            }
            Bombs.Value = StartBombs(mode);
            Faith = faithLenience = 0;
            SetRankLevel(Difficulty.RankLevelBounds.min);
            return true;
        } else return false;
    }

    public (int success, int total)? LookForSpellHistory(string bossKey, int phaseIndex) {
        var key = ((campaignKey, bossKey), phaseIndex);
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
        if (usePIVMultiplier) score = (long) (score * PIV);
        AddScore(score);
        LifeSwappedForScore.Proc();
    }
    public void AddLives(int delta, bool asHit = true) {
        //if (mode == CampaignMode.NULL) return;
        Log.Unity($"Adding player lives: {delta}");
        if (delta < 0 && asHit) {
            ++HitsTaken;
            LastTookHitFrame = ETime.FrameNumber;
            Bombs.Value = Math.Max(Bombs, StartBombs(mode));
            AddPower(powerDeathLoss);
            Meter = 1;
            AddRankPoints(RankManager.RankPointsDeath);
            PlayerTookHit.Proc();
        }
        if (delta < 0 && mode.OneLife()) 
            Lives.Value = 0;
        else 
            Lives.Value = Math.Max(0, Lives + delta);
        if (Lives == 0) {
            //Record failure
            if (Request?.Saveable == true) {
                //Special-case boss practice handling
                if (Request.lowerRequest.Resolve(_ => null, 
                        b => (BossPracticeRequest?) b, _ => null, _ => null).Try(out var bpr)) {
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
            GameOver.Proc();
        }
    }

    /// <summary>
    /// Don't use this in the main campaign-- it will interfere with stats
    /// </summary>
    public void SetLives(int to) => AddLives(to - Lives, false);

    public void AddFaith(double delta) => Faith = M.Clamp(0, 1, Faith + delta * Difficulty.faithAcquireMultiplier);
    public void AddFaithLenience(double time) => faithLenience = Math.Max(faithLenience, time);

    public void UpdateFaithFrame() {
        if (!Lenient) {
            if (faithLenience > 0) {
                faithLenience = Math.Max(0, faithLenience - ETime.FRAME_TIME);
            } else if (Faith > 0) {
                Faith = Math.Max(0, Faith - ETime.FRAME_TIME *
                    faithDecayRate * FaithDecayRateMultiplier * Difficulty.faithDecayMultiplier);
            } else if (PIV > 1) {
                PIV.Value = Math.Max(1, PIV - pivFallStep);
                Faith = 0.5f;
                faithLenience = faithLenienceFall;
            }
        }
    }
    
    #region Meter

    public void AddMeter(double delta, PlayerController source) {
        var belowThreshold = !EnoughMeterToUse;
        Meter = M.Clamp(0, 1, Meter + delta * Difficulty.meterAcquireMultiplier);
        if (belowThreshold && EnoughMeterToUse && source.State != PlayerController.PlayerState.WITCHTIME) {
            MeterNowUsable.Proc();
        }
    }


    public bool TryStartMeter() {
        if (EnoughMeterToUse) {
            Meter -= meterUseInstantCost;
            return true;
        } else return false;
    }

    public bool TryUseMeterFrame() {
        var consume = meterUseRate * Difficulty.meterUsageMultiplier * ETime.FRAME_TIME;
        if (Meter >= consume) {
            Meter -= consume;
            return true;
        } else {
            Meter = 0;
            return false;
        }
    }
    
    #endregion

    private void AddPower(double delta) {
        if (!PowerMechanicEnabled) return;
        var prevFloor = Math.Floor(Power);
        var prevCeil = Math.Ceiling(Power);
        var prevPower = Power;
        Power.Value = M.Clamp(powerMin, powerMax, Power + delta);
        //1.95 is effectively 1, 2.00 is effectively 2
        if (Power < prevFloor) PowerLost.Proc();
        if (prevPower < prevCeil && Power >= prevCeil) {
            if (Power >= powerMax) PowerFull.Proc();
            else PowerGained.Proc();
        }
    }

    /// <summary>
    /// Delta should be negative.
    /// </summary>
    public bool TryConsumePower(double delta) {
        if (!PowerMechanicEnabled) return false;
        if (Power + delta >= powerMin) {
            AddPower(delta);
            return true;
        } else return false;
    }

    public void FullPower() {
        Power.Value = powerMax;
        PowerFull.Proc();
    }
    public void AddGraze(int delta, PlayerController source) {
        Graze.Value += delta;
        AddFaith(delta * FaithBoostGraze);
        AddFaithLenience(FaithLenienceGraze);
        AddMeter(delta * MeterBoostGraze, source);
        AddRankPoints(delta * RankManager.RankPointsGraze);
        Counter.GrazeProc(delta);
    }

    #region ItemMethods

    public void AddPowerItems(int delta) {
        if (!PowerMechanicEnabled || Power >= powerMax) {
            AddValueItems((int)(delta * powerToValueConversion), 1);
        } else {
            AddPower(delta * powerItemValue);
        }
    }
    
    /// <summary>
    /// Returns score delta.
    /// </summary>
    public long AddValueItems(int delta, double multiplier) {
        AddFaith(delta * faithBoostValue);
        AddFaithLenience(faithLenienceValue);
        long scoreDelta = (long) Math.Round(delta * valueItemPoints * EffectivePIV * multiplier);
        AddScore(scoreDelta);
        return scoreDelta;
    }
    public long AddSmallValueItems(int delta, double multiplier) {
        AddFaith(delta * faithBoostValue * 0.1);
        AddFaithLenience(faithLenienceValue * 0.1);
        long scoreDelta = (long) Math.Round(delta * smallValueItemPoints * EffectivePIV * multiplier);
        AddScore(scoreDelta);
        return scoreDelta;
    }
    public void AddPointPlusItems(int delta, double multiplier) {
        PIV.Value += delta * pivPerPPP * multiplier;
        AddFaith(delta * faithBoostPointPP);
        AddFaithLenience(faithLeniencePointPP);
    }
    public void AddGems(int delta, PlayerController source) {
        AddMeter(delta * meterBoostGem, source);
    }
    public void AddOneUpItem() {
        ++OneUpItemsCollected;
        LifeExtend();
    }
    public void AddLifeItems(int delta) {
        LifeItems.Value += delta;
        if (nextItemLifeIndex < pointLives.Length && LifeItems >= pointLives[nextItemLifeIndex]) {
            ++nextItemLifeIndex;
            LifeExtend();
            ItemExtendAcquired.Proc();
        }
    }
    
    #endregion

    #region Rank
    
    
    public bool SetRankLevel(int level, double? points = null) {
        if (!InstanceActiveGuard()) return false;
        var (min, max) = Difficulty.RankLevelBounds;
        level = M.Clamp(min, max, level);
        if (RankLevel == level) return false;
        bool increaseRank = level > RankLevel;
        RankLevel = level;
        RankPoints = M.Clamp(0, RankPointsRequired - 1, points ?? RankManager.DefaultRankPointsForLevel(level));
        RankManager.RankLevelChanged.OnNext(increaseRank);
        return true;
    }
    public void AddRankPoints(double delta) {
        if (!InstanceActiveGuard()) return;
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
    
    #endregion
    

    public void LifeExtend() {
        ++Lives.Value;
        AnyExtendAcquired.Proc();
    }

    public void PhaseEnd(PhaseCompletion pc) {
        if (pc.props.phaseType?.IsCard() == true && pc.props.Boss != null && pc.CaptureStars.Try(out var captured)) {
            var crec = new CardRecord() {
                campaign = campaignKey,
                boss = pc.props.Boss.key,
                phase = pc.props.Index,
                stars = captured,
                hits = pc.hits,
                method = pc.clear
            };
            CardHistory.Add(crec);
            AddRankPoints(RankManager.RankPointsForCard(crec));
            CardHistoryUpdated.OnNext(crec);
        }
        if (pc.props.phaseType?.IsPattern() ?? false) AddFaithLenience(faithLeniencePhase);

        PhaseCompleted.OnNext(pc);
    }

    private void AddScore(long delta) {
        Score.Value += delta;
        MaxScore.Value = Math.Max(MaxScore, Score);
        if (NextScoreLife.Try(out var next) && Score >= next) {
            ++nextScoreLifeIndex;
            LifeExtend();
            AddRankPoints(RankManager.RankPointsScoreExtend);
            ScoreExtendAcquired.Proc();
        }
    }

    public void DestroyNormalEnemy() {
        ++EnemiesDestroyed;
        AddFaithLenience(faithLenienceEnemyDestroy);
    }

    public void _RegularUpdate() {
        if (!InstanceActiveGuard()) return;
        
        ++TotalFrames;
        if (CurrentBossCT?.Cancelled == true) {
            CloseBoss();
        }
        if (PlayerController.PlayerActive) {
            if (++PlayerActiveFrames % ETime.ENGINEFPS == 0) {
                //note that this needs to be guarded by the if mode == .NULL check to avoid rank increases on eg. the main menu...
                AddRankPoints(RankManager.RankPointsPerSecond);
            }
        }

        VisibleScore.Update(ETime.FRAME_TIME);
        VisibleMeter.Update(ETime.FRAME_TIME);
        VisibleFaith.Update(ETime.FRAME_TIME);
        VisibleFaithLenience.Update(ETime.FRAME_TIME);
        VisibleRankPointFill.Update(ETime.FRAME_TIME);
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
        } else Log.UnityError("You tried to close a boss section when no boss exists.");
    }

    public void Dispose() {
        foreach (var t in Tokens)
            t.Dispose();
    }
    
#if UNITY_EDITOR
    public void SetPower(double x) => Power.Value = x;
    #endif
}


}