using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using Danmaku;
using DMath;
using JetBrains.Annotations;
using SM;
using UnityEditor;
using static Danmaku.Enums;
using static SM.SMAnalysis;
using GameLowRequest = DU<Danmaku.CampaignRequest, Danmaku.BossPracticeRequest, 
    PhaseChallengeRequest, Danmaku.StagePracticeRequest>;

public class CampaignData {
    public static bool PowerMechanicEnabled { get; } = false;
    private static int StartLives(CampaignMode mode) {
        if (mode == CampaignMode.MAIN || mode == CampaignMode.TUTORIAL || mode == CampaignMode.STAGE_PRACTICE) return 7;
        else if (mode.OneLife()) return 1;
        else if (mode == CampaignMode.NULL) return 14;
        else return 1;
    }

    private static int StartBombs(CampaignMode mode) {
        if (mode == CampaignMode.MAIN || mode == CampaignMode.TUTORIAL || mode == CampaignMode.STAGE_PRACTICE) return 2;
        else if (mode.OneLife()) return 0;
        else return 3;
    }

    private static double StartPower(CampaignMode mode, [CanBeNull] ShotConfig shot) {
        if (mode.OneLife() || !PowerMechanicEnabled) return powerMax;
        else if (shot != null) return M.Clamp(powerMin, powerMax, shot.defaultPower);
        else return M.Clamp(powerMin, powerMax, powerDefault);
    }

    private static double StartMeter(CampaignMode mode) {
        if (mode.IsOneCard()) return 0;
        return 0.7;
    }

    public DifficultySettings Difficulty { get; }
    
    private const int defltContinues = 42;
    public const long valueItemPoints = 3142;
    public long MaxScore { get; private set; }
    public long Score { get; private set; }
    private long lastScore;
    public long UIVisibleScore { get; private set; }
    private double remVisibleScoreLerpTime;
    public const double visibleScoreLerpTime = 1f;
    public int Lives { get; private set; }
    public int Bombs { get; private set; }
    public int LifeItems { get; private set; }
    public int NextLifeItems => pointLives.Try(nextItemLifeIndex, 9001);
    public long Graze { get; private set; }
    public const double powerMax = 4;
    public const double powerMin = 1;
#if UNITY_EDITOR
    private const double powerDefault = 1000;
#else
    private const double powerDefault = 1;
#endif
    private const double powerDeathLoss = -1;
    private const double powerItemValue = 0.05;
    private const double powerToValueConversion = 2;
    public double Power { get; private set; }
    public int PowerF => (int)Math.Floor(Power);
    public int PowerIndex => PowerF - (int) powerMin;
    public double PIV { get; private set; }
    private double EffectivePIV => PIV + 0.01 * (long)(Graze / 42);
    private const double pivPerPPP = 0.01;
    public const double pivFallStep = 0.1;
    public double Faith { get; private set; }
    private double faithLenience;
    public double UIVisibleFaithDecayLenienceRatio { get; private set; }
    private const double faithDecayRate = 0.14;
    private double faithDecayRateMultiplier;
    private const double faithDecayRateMultiplierBoss = 0.666;
    private const double faithLenienceFall = 5;
    private const double faithLenienceValue = 0.2;
    private const double faithLeniencePointPP = 0.3;
    private double FaithLenienceGraze => M.Lerp(0, 3, Difficulty.Counter, 0.4, 0.3);
    private const double faithLenienceEnemyDestroy = 0.1;
    private const double faithBoostValue = 0.02;
    private const double faithBoostPointPP = 0.09;
    private double FaithBoostGraze => M.Lerp(0, 3, Difficulty.Counter, 0.03, 0.02);
    
    private const double faithLeniencePhase = 4;
    
    public double Meter { get; private set; }
    public bool EnoughMeterToUse => Meter >= meterUseThreshold;
    private double MeterBoostGraze => M.Lerp(0, 3, Difficulty.Counter, 0.006, 0.004);
    private const double meterBoostGem = 0.02;
    private const double meterRefillRate = 0.002;
    private const double meterUseRate = 0.314;
    public const double meterUseThreshold = 0.42;
    private const double meterUseInstantCost = 0.042;
    
    public bool MeterInUse { get; set; }
    private double MeterPivPerPPPMultiplier => MeterInUse ? 2 : 1;
    private double MeterScorePerValueMultiplier => MeterInUse ? 2 : 1;
    
    public bool Reloaded { get; set; }
    
    public int Continues { get; private set; }
    public int HitsTaken { get; private set; }
    public int EnemiesDestroyed { get; private set; }

    private int nextScoreLifeIndex;
    public long? NextScoreLife => mode.OneLife() ? null : scoreLives.TryN(nextScoreLifeIndex);
    private int nextItemLifeIndex;
    public readonly CampaignMode mode;
    public bool Continued { get; private set; }
    private PlayerTeam team;
    [CanBeNull] public PlayerConfig Player => team.Player;
    [CanBeNull] public ShotConfig Shot => team.Shot;
    public Subshot Subshot => team.Subshot;
    public string MultishotString => (Shot != null && Shot.isMultiShot) ? Subshot.Describe() : "";
    public void SetSubshot(Subshot newSubshot) {
        team.Subshot = newSubshot;
        Events.CampaignDataHasChanged.Proc();
    }
    //This uses boss key instead of boss index since phaseSM doesn't have trivial access to boss index
    public List<CardHistory> CardCaptures { get; }

    //TODO: this can cause problems if multiple phases are declared lenient at the same time, but that's not a current use case
    public bool Lenience { get; set; }
    [CanBeNull] public BehaviorEntity ExecutingBoss { get; private set; }

    private static readonly long[] scoreLives = {
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
    private static readonly int[] pointLives = {
        69,
        141,
        224,
        314,
        420,
        618,
        840,
        1084,
        1337,
        1618,
        2048,
        2718,
        3142,
        9001,
        int.MaxValue
    };

    /// <summary>
    /// Only present for campaign-type games
    /// </summary>
    [CanBeNull] private readonly CampaignConfig campaign;
    /// <summary>
    /// Present for all games, including "null_campaign" default for unscoped games
    /// </summary>
    private readonly string campaignKey;
    public GameRequest? Request { get; }

    public CampaignData(CampaignMode mode, GameRequest? req = null, long? maxScore = null) {
        this.Request = req;
        this.mode = mode;
        this.Difficulty = req?.metadata.difficulty ?? GameManagement.defaultDifficulty;
        this.MaxScore = maxScore ?? 9001;
        campaign = req?.lowerRequest.Resolve(cr => cr.campaign.campaign, _ => null, _ => null, _ => null);
        campaignKey = req?.lowerRequest.Resolve(cr => cr.Key, b => b.boss.campaign.Key, s => s.Campaign.key,
            s => s.stage.campaign.Key) ?? "null_campaign";
        team = req?.metadata.team ?? PlayerTeam.Empty;
        if (campaign != null) {
            Lives = campaign.startLives > 0 ? campaign.startLives : StartLives(mode);
        } else {
            Lives = StartLives(mode);
        }
        Lives = Difficulty.startingLives ?? Lives;
        Bombs = StartBombs(mode);
        Power = StartPower(mode, team.Shot);
        CardCaptures = new List<CardHistory>();
        this.Score = 0;
        this.PIV = 1;
        Meter = StartMeter(mode);
        nextScoreLifeIndex = 0;
        nextItemLifeIndex = 0;
        remVisibleScoreLerpTime = 0;
        lastScore = 0;
        UIVisibleScore = 0;
        LifeItems = 0;
        Faith = 1f;
        faithLenience = 0f;
        UIVisibleFaithDecayLenienceRatio = 0f;
        Continues = mode.OneLife() ? 0 : defltContinues;
        Continued = false;
        Reloaded = false;
        HitsTaken = 0;
        faithDecayRateMultiplier = 1f;
        EnemiesDestroyed = 0;
        Lenience = false;
        Graze = 0;
        ExecutingBoss = null;
        MeterInUse = false;
    }

    public bool TryContinue() {
        if (Continues > 0) {
            Continued = true;
            //We can allow continues in replays! But in the current impl, the watcher will have to press continue.
            //Replayer.Cancel();
            --Continues;
            Score = lastScore = UIVisibleScore = nextItemLifeIndex = nextScoreLifeIndex = LifeItems = 0;
            CardCaptures.Clear(); //Partial game is saved when lives=0. Don't double on captures.
            PIV = 1;
            Meter = StartMeter(mode);
            if (campaign != null) {
                Lives = campaign.startLives > 0 ? campaign.startLives : StartLives(mode);
            } else {
                Lives = StartLives(mode);
            }
            Bombs = StartBombs(mode);
            remVisibleScoreLerpTime = Faith = faithLenience = 0;
            Events.CampaignDataHasChanged.Proc();
            return true;
        } else return false;
    }


    /// <summary>
    /// Delta should be negative.
    /// </summary>
    public bool TryConsumeBombs(int delta) {
        if (Bombs + delta >= 0) {
            Bombs += delta;
            Events.CampaignDataHasChanged.Proc();
            return true;
        }
        return false;
    }

    public void SwapLifeScore(int score) {
        AddLives(-1, false);
        AddScore(score);
        LifeSwappedForScore.Proc();
        Events.CampaignDataHasChanged.Proc();
    }
    public void AddLives(int delta, bool asHit = true) {
        //if (mode == CampaignMode.NULL) return;
        Log.Unity($"Adding player lives: {delta}");
        if (delta < 0 && asHit) {
            ++HitsTaken;
            Bombs = Math.Max(Bombs, StartBombs(mode));
            AddPower(powerDeathLoss);
            Meter = 1;
        }
        if (delta < 0 && mode.OneLife()) Lives = 0;
        else Lives = Math.Max(0, Lives + delta);
        if (Lives == 0) {
            //Record failure
            if (Request.Try(out var req) && req.Saveable) {
                //Special-case boss practice handling
                if (req.lowerRequest.Resolve(_ => null, b => (BossPracticeRequest?) b, _ => null, _ => null)
                    .Try(out var bpr)) {
                    CardCaptures.Add(new CardHistory() {
                        campaign = bpr.boss.campaign.Key,
                        boss = bpr.boss.boss.key,
                        phase = bpr.phase.index,
                        captured = false
                    });
                }
                SaveData.r.RecordGame(new GameRecord(req, this, false));
            }
            GameStateManager.HandlePlayerDeath();
        }
        Events.CampaignDataHasChanged.Proc();
    }

    /// <summary>
    /// Don't use this in the main campaign-- it will interfere with stats
    /// </summary>
    public void SetLives(int to) => AddLives(to - Lives, false);

    private void AddFaith(double delta) => Faith = M.Clamp(0, 1, Faith + delta * Difficulty.faithAcquireMultiplier);
    private void AddFaithLenience(double time) => faithLenience = Math.Max(faithLenience, time);
    public void ExternalLenience(double time) => AddFaithLenience(time);
    private void AddMeter(double delta) {
        var belowThreshold = !EnoughMeterToUse;
        Meter = M.Clamp(0, 1, Meter + delta * Difficulty.meterAcquireMultiplier);
        if (belowThreshold && EnoughMeterToUse && !MeterInUse) {
            MeterNowUsable.Proc();
        }
    }

    public void RefillMeterFrame(PlayerInput.PlayerState state) {
        double rate = 0;
        if (state == PlayerInput.PlayerState.NORMAL) rate = meterRefillRate;
        //meter use handled under TryUseMeterFrame
        AddMeter(rate * ETime.FRAME_TIME);
    }

    public bool TryStartMeter() {
        if (Difficulty.meterEnabled && EnoughMeterToUse) {
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

    private void AddPower(double delta) {
        if (!PowerMechanicEnabled) return;
        var prevFloor = Math.Floor(Power);
        var prevCeil = Math.Ceiling(Power);
        var prevPower = Power;
        Power = M.Clamp(powerMin, powerMax, Power + delta);
        //1.95 is effectively 1, 2.00 is effectively 2
        if (Power < prevFloor) PowerLost.Proc();
        if (prevPower < prevCeil && Power >= prevCeil) {
            if (Power >= powerMax) PowerFull.Proc();
            else PowerGained.Proc();
        }
        Events.CampaignDataHasChanged.Proc();
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

    private void FullPower() {
        Power = powerMax;
        PowerFull.Proc();
    }
    public void AddPowerItems(int delta) {
        if (!PowerMechanicEnabled || Power >= powerMax) {
            AddValueItems((int)(delta * powerToValueConversion), 1);
        } else AddPower(delta * powerItemValue);
    }

    public void AddFullPowerItems(int _) {
        FullPower();
    }
    public void AddValueItems(int delta, double multiplier) {
        AddFaith(delta * faithBoostValue);
        AddFaithLenience(faithLenienceValue);
        double bonus = MeterScorePerValueMultiplier;
        long scoreDelta = (long) Math.Round(delta * valueItemPoints * bonus * EffectivePIV * multiplier);
        AddScore(scoreDelta);
        Events.ScoreItemHasReceived.Publish((scoreDelta, bonus > 1));
    }
    public void AddGraze(int delta) {
        Graze += delta;
        AddFaith(delta * FaithBoostGraze);
        AddFaithLenience(FaithLenienceGraze);
        AddMeter(delta * MeterBoostGraze);
        Counter.GrazeProc(delta);
        Events.CampaignDataHasChanged.Proc();
    }

    public void AddPointPlusItems(int delta) {
        PIV += pivPerPPP * MeterPivPerPPPMultiplier * delta;
        AddFaith(delta * faithBoostPointPP);
        AddFaithLenience(faithLeniencePointPP);
        Events.CampaignDataHasChanged.Proc();
    }

    public void AddGems(int delta) {
        AddMeter(delta * meterBoostGem);
    }

    public void LifeExtend() {
        ++Lives;
        AnyExtendAcquired.Proc();
        Events.CampaignDataHasChanged.Proc();
    }

    public void PhaseEnd(PhaseCompletion pc) {
        if (pc.props.phaseType?.IsCard() == true && pc.props.Boss != null && pc.Captured.Try(out var captured)) {
            CardCaptures.Add(new CardHistory() {
                campaign = campaignKey,
                boss = pc.props.Boss.key,
                phase = pc.props.Index,
                captured = captured
            });
        }
        if (pc.props.phaseType?.IsPattern() ?? false) AddFaithLenience(faithLeniencePhase);

        PhaseCompleted.Publish(pc);
    }
    
    private void AddScore(long delta) {
        lastScore = UIVisibleScore;
        Score += delta;
        MaxScore = Math.Max(MaxScore, Score);
        if (NextScoreLife.Try(out var next) && Score >= next) {
            ++nextScoreLifeIndex;
            LifeExtend();
            ScoreExtendAcquired.Proc();
            Events.CampaignDataHasChanged.Proc();
        }
        remVisibleScoreLerpTime = visibleScoreLerpTime;
        //updated in RegUpd
    }
    public void AddLifeItems(int delta) {
        LifeItems += delta;
        if (nextItemLifeIndex < pointLives.Length && LifeItems >= pointLives[nextItemLifeIndex]) {
            ++nextItemLifeIndex;
            LifeExtend();
            ItemExtendAcquired.Proc();
        }
        Events.CampaignDataHasChanged.Proc();
    }

    public void DestroyNormalEnemy() {
        ++EnemiesDestroyed;
        AddFaithLenience(faithLenienceEnemyDestroy);
    }

    public void RegularUpdate() {
        if (remVisibleScoreLerpTime > 0) {
            remVisibleScoreLerpTime -= ETime.FRAME_TIME;
            if (remVisibleScoreLerpTime <= 0) UIVisibleScore = Score;
            else UIVisibleScore = (long) M.LerpU(lastScore, Score, 1 - remVisibleScoreLerpTime / visibleScoreLerpTime);
            Events.CampaignDataHasChanged.Proc();
        }
        UIVisibleFaithDecayLenienceRatio = M.LerpU(UIVisibleFaithDecayLenienceRatio, 
            Math.Min(1f, faithLenience / 3f), 6f * ETime.FRAME_TIME);
        if (PlayerInput.PlayerActive && !Lenience && GameStateManager.IsRunning) {
            if (faithLenience > 0) {
                faithLenience = Math.Max(0, faithLenience - ETime.FRAME_TIME);
            } else if (Faith > 0) {
                Faith = Math.Max(0, Faith - ETime.FRAME_TIME * faithDecayRate * faithDecayRateMultiplier * Difficulty.faithDecayMultiplier);
            } else if (PIV > 1) {
                PIV = Math.Max(1, PIV - pivFallStep);
                Faith = 0.5f;
                faithLenience = faithLenienceFall;
                Events.CampaignDataHasChanged.Proc();
            }
        }
    }

    public void OpenBoss(BehaviorEntity boss) {
        if (ExecutingBoss != null) CloseBoss();
        ExecutingBoss = boss;
        faithDecayRateMultiplier *= faithDecayRateMultiplierBoss;
    }

    public void CloseBoss() {
        if (ExecutingBoss != null) {
            ExecutingBoss = null;
            faithDecayRateMultiplier /= faithDecayRateMultiplierBoss;
        } else Log.UnityError("You tried to close a boss section when no boss exists.");
    }

    public void AddDecayRateMultiplier_Tutorial(double m) {
        faithDecayRateMultiplier *= m;
    }
    
    public static readonly Events.Event0 MeterNowUsable = new Events.Event0();
    public static readonly Events.Event0 PowerLost = new Events.Event0();
    public static readonly Events.Event0 PowerGained = new Events.Event0();
    public static readonly Events.Event0 PowerFull = new Events.Event0();
    public static readonly Events.Event0 AnyExtendAcquired = new Events.Event0();
    public static readonly Events.Event0 ItemExtendAcquired = new Events.Event0();
    public static readonly Events.Event0 ScoreExtendAcquired = new Events.Event0();
    public static readonly Events.IEvent<PhaseCompletion> PhaseCompleted = new Events.Event<PhaseCompletion>();
    public static readonly Events.Event0 LifeSwappedForScore = new Events.Event0();

#if UNITY_EDITOR
    public void SetPower(double x) => Power = x;
    #endif
}

/// <summary>
/// A singleton manager for persistent game data.
/// This is the only scene-persistent object in the game.
/// </summary>
public class GameManagement : RegularUpdater {
    public static readonly Version EngineVersion = new Version(5, 0, 1);
    public static bool Initialized { get; private set; } = false;
    public static DifficultySettings Difficulty => campaign.Difficulty;
    
    public static DifficultySettings defaultDifficulty { get; private set; } = 
#if UNITY_EDITOR
        new DifficultySettings(FixedDifficulty.Lunatic);
#else
        new DifficultySettings(FixedDifficulty.Normal);
#endif

    public static CampaignData campaign = new CampaignData(CampaignMode.NULL);
    [UsedImplicitly]
    public static bool Continued => campaign.Continued;

    public static void NewCampaign(CampaignMode mode, long? highScore, GameRequest? req = null) => 
        campaign = new CampaignData(mode, req, highScore);

#if UNITY_EDITOR
    [ContextMenu("Add 1000 value")]
    public void YeetScore() => campaign.AddValueItems(1000, 1);
    [ContextMenu("Add 10 PIV+")]
    public void YeetPIV() => campaign.AddPointPlusItems(10);
    [ContextMenu("Add 40 life")]
    public void YeetLife() => campaign.AddLifeItems(40);

    [ContextMenu("Set Power to 1")]
    public void SetPower1() => campaign.SetPower(1);
    [ContextMenu("Set Power to 2")]
    public void SetPower2() => campaign.SetPower(2);
    [ContextMenu("Set Power to 3")]
    public void SetPower3() => campaign.SetPower(3);
    [ContextMenu("Set Power to 4")]
    public void SetPower4() => campaign.SetPower(4);
    [ContextMenu("Set Subshot D")]
    public void SetSubshotD() => campaign.SetSubshot(Subshot.TYPE_D);
    [ContextMenu("Set Subshot M")]
    public void SetSubshotM() => campaign.SetSubshot(Subshot.TYPE_M);
    [ContextMenu("Set Subshot K")]
    public void SetSubshotK() => campaign.SetSubshot(Subshot.TYPE_K);

#endif
    public static IEnumerable<FixedDifficulty> VisibleDifficulties => new[] {
        FixedDifficulty.Easy, FixedDifficulty.Normal, FixedDifficulty.Hard,
        FixedDifficulty.Lunatic
    };
    public static IEnumerable<FixedDifficulty?> CustomAndVisibleDifficulties =>
        VisibleDifficulties.Select(fd => (FixedDifficulty?) fd).Prepend(null);
    public static IEnumerable<FixedDifficulty?> VisibleDifficultiesAndCustom =>
        VisibleDifficulties.Select(fd => (FixedDifficulty?) fd).Append(null);
    public static IEnumerable<(string, FixedDifficulty)> VisibleDifficultiesDescribed =>
        VisibleDifficulties.Select(d => (d.Describe(), d));
    public static IEnumerable<(string, FixedDifficulty?)> VisibleDifficultiesAndCustomDescribed =>
        VisibleDifficultiesAndCustom.Select(d => (d?.Describe() ?? "Custom", d));
    
    private static GameManagement gm;
    public GameUniqueReferences references;
    public static GameUniqueReferences References => gm.references;
    public GameObject ghostPrefab;
    public GameObject inodePrefab;
    public GameObject arbitraryCapturer;
    public static GameObject ArbitraryCapturer => gm.arbitraryCapturer;
    public SceneConfig defaultSceneConfig;
    public SOPlayerHitbox playerHitbox;
    public SOPlayerHitbox visiblePlayer;
    public static Vector2 VisiblePlayerLocation => gm.visiblePlayer.location;

    private void Awake() {
        if (gm != null) {
            DestroyImmediate(gameObject);
            return;
        }
        Initialized = true;
        gm = this;
        DontDestroyOnLoad(this);
        SceneIntermediary.Setup(defaultSceneConfig, References.defaultTransition);
        ParticlePooler.Prepare();
        GhostPooler.Prepare(ghostPrefab);
        BEHPooler.Prepare(inodePrefab);
        ItemPooler.Prepare(References.items);
        ETime.RegisterPersistentSOFInvoke(Replayer.BeginFrame);
        ETime.RegisterPersistentSOFInvoke(Enemy.FreezeEnemies);
        ETime.RegisterPersistentEOFInvoke(BehaviorEntity.PrunePoolControls);
        ETime.RegisterPersistentEOFInvoke(CurvedTileRenderLaser.PrunePoolControls);
        SceneIntermediary.RegisterSceneUnload(ClearForScene);
        SceneIntermediary.RegisterSceneLoad(OnSceneLoad);
#if UNITY_EDITOR
        Log.Unity($"Graphics Jobs: {PlayerSettings.graphicsJobs} {PlayerSettings.graphicsJobMode}; MTR {PlayerSettings.MTRendering}");
    #endif
        Log.Unity($"Graphics Render mode {SystemInfo.renderingThreadingMode}");
        Log.Unity($"Danmokou {EngineVersion}, {References.gameIdentifier} {References.gameVersion}");

        //The reason we do this instead of Awake is that we want all resources to be
        //loaded before any State Machines are constructed, which may occur in other entities' Awake calls.
        GetComponent<ResourceManager>().Setup();
        GetComponent<BulletManager>().Setup();
        GetComponentInChildren<SFXService>().Setup();
        GetComponentInChildren<AudioTrackService>().Setup();
    }

    private void OnSceneLoad() {
        Replayer.LoadLazy();
        (new GameObject("Scene-Local CRU")).AddComponent<SceneLocalCRU>();
    }

    public static bool MainMenuExists => References.mainMenu != null;
    public static bool GoToMainMenu() => SceneIntermediary.LoadScene(
            new SceneIntermediary.SceneRequest(References.mainMenu, 
                SceneIntermediary.SceneRequest.Reason.ABORT_RETURN, Replayer.Cancel));
    public static bool GoToReplayScreen() => SceneIntermediary.LoadScene(
        new SceneIntermediary.SceneRequest(References.replaySaveMenu, 
            SceneIntermediary.SceneRequest.Reason.FINISH_RETURN, 
            () => BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground));

    /// <summary>
    /// Restarts the existing game if it exists, or reloads the specific level.
    /// </summary>
    /// <returns></returns>
    public static bool Restart() => GameRequest.Rerun() ?? throw new Exception("Couldn't find a game to reload");
    
    public static void ClearForScene() {
        AudioTrackService.ClearAllAudio(false);
        SFXService.ClearConstructed();
        BulletManager.ClearPoolControls();
        Events.Event0.DestroyAll();
        ETime.Slowdown.RevokeAll(MultiMultiplier.Priority.CLEAR_SCENE);
        ETime.Timer.DestroyAll();
        BulletManager.OrphanAll();
        DataHoisting.DestroyAll();
        //SMs may have links to data hoisting, so we destroy both of them on phase end.
        ReflWrap.ClearWrappers();
        StateMachineManager.ClearCachedSMs();
        BehaviorEntity.ClearPointers();
        AyaPhoto.ClearTextures();
    }

    public static void LocalReset() {
        //AudioTrackService.ClearAllAudio();
        SFXService.ClearConstructed();
        Events.Event0.DestroyAll();
        ETime.Slowdown.RevokeAll(MultiMultiplier.Priority.CLEAR_SCENE);
        ETime.Timer.DestroyAll();
        BehaviorEntity.DestroyAllSummons();
        DataHoisting.DestroyAll();
        ReflWrap.ClearWrappers();
        StateMachineManager.ClearCachedSMs();
        BulletManager.ClearPoolControls();
        BulletManager.ClearEmpty();
        BulletManager.ClearAllBullets();
        BulletManager.DestroyCopiedPools();
        Events.CampaignDataHasChanged.Proc();
#if UNITY_EDITOR || ALLOW_RELOAD
        Events.LocalReset.Proc();
#endif
        //Ordered last so cancellations from HardCancel will occur under old data
        campaign = new CampaignData(CampaignMode.NULL);
        Debug.Log($"Reloading level: {Difficulty.Describe()} is the current difficulty");
        UIManager.UpdateTags();
    }
    
#if UNITY_EDITOR || ALLOW_RELOAD
    private void Update() {
        TryTriggerLocalReset();
    }
    
    private static bool TryTriggerLocalReset() {
        if (!SceneIntermediary.IsFirstScene) return false;
        if (Input.GetKeyDown(KeyCode.R)) {
        } else if (Input.GetKeyDown(KeyCode.T)) {
            defaultDifficulty = new DifficultySettings(FixedDifficulty.Easy);
        } else if (Input.GetKeyDown(KeyCode.Y)) {
            defaultDifficulty = new DifficultySettings(FixedDifficulty.Normal);
        } else if (Input.GetKeyDown(KeyCode.U)) {
            defaultDifficulty = new DifficultySettings(FixedDifficulty.Hard);
        } else if (Input.GetKeyDown(KeyCode.I)) {
            defaultDifficulty = new DifficultySettings(FixedDifficulty.Lunatic);
        } else return false;
        LocalReset();
        return true;
    }
#endif

    private static void ClearPhase() {
        BulletManager.ClearPoolControls(false);
        BulletManager.ClearEmpty();
        Events.Event0.Reset();
        ETime.Slowdown.RevokeAll(MultiMultiplier.Priority.CLEAR_PHASE);
        ETime.Timer.ResetAll();
        Events.ClearPhase.Proc();
        //Delay this so copy pools can be softculled correctly
        ETime.QueueDelayedEOFInvoke(1, BulletManager.DestroyCopiedPools);
        //Delay this so that bullets referencing hosting data don't break down before
        //converting into softcull (note softcull bullets don't run velocity)
        ETime.QueueDelayedEOFInvoke(1, DataHoisting.ClearValues);
    }

    public static void ClearPhaseAutocull(string cullPool, string defaulter) {
        ClearPhase();
        BulletManager.Autocull(cullPool, defaulter);
        BehaviorEntity.Autocull(cullPool, defaulter);
    }
    


    [ContextMenu("Unload unused")]
    public void UnloadUnused() {
        Resources.UnloadUnusedAssets();
    }

    public override int UpdatePriority => UpdatePriorities.SYSTEM;

    public override void RegularUpdate() {
        campaign.RegularUpdate();
    }

    

    [CanBeNull] private static AnalyzedDayCampaign _dayCampaign;
    public static AnalyzedDayCampaign DayCampaign => 
        _dayCampaign = _dayCampaign ?? new AnalyzedDayCampaign(References.dayCampaign);

    [CanBeNull] private static AnalyzedCampaign[] _campaigns;
    public static AnalyzedCampaign[] Campaigns => _campaigns =
        _campaigns ?? References.Campaigns.Select(c => new AnalyzedCampaign(c)).ToArray();

    public static IEnumerable<AnalyzedCampaign> FinishedCampaigns =>
        Campaigns.Where(c => SaveData.r.EndingsAchieved.ContainsKey(c.campaign.key));
    
    [CanBeNull]
    public static AnalyzedCampaign MainCampaign => Campaigns.First(c => c.campaign.key == References.campaign.key);
    [CanBeNull]
    public static AnalyzedCampaign ExtraCampaign => Campaigns.First(c => c.campaign.key == References.exCampaign.key);
    public static AnalyzedBoss[] PBosses => FinishedCampaigns.SelectMany(c => c.bosses).ToArray();
    public static AnalyzedStage[] PStages => FinishedCampaigns.SelectMany(c => c.practiceStages).ToArray();
    
    #if UNITY_EDITOR
    public static AnalyzedBoss[] AllPBosses => Campaigns.SelectMany(c => c.bosses).ToArray();
    
    #endif
    
}
