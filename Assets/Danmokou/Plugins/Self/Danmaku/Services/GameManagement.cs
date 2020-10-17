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

public struct CampaignData {
    public static bool PowerMechanicEnabled { get; } = false;
    public static bool MeterMechanicEnabled { get; } = true;
    private static int StartLives(CampaignMode mode) {
        if (mode == CampaignMode.MAIN || mode == CampaignMode.TUTORIAL || mode == CampaignMode.STAGE_PRACTICE) return 7;
        else if (mode.OneLife()) return 1;
        else if (mode == CampaignMode.NULL) return 7;
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
    public double PIVDecay { get; private set; }
    private double pivDecayLenience;
    public double UIVisiblePIVDecayLenienceRatio { get; private set; }
    private const double pivDecayRate = 0.13;
    private double pivDecayRateMultiplier;
    private const double pivDecayRateMultiplierBoss = 0.666;
    private const double pivDecayLenienceFall = 5;
    private const double pivDecayLenienceValue = 0.5;
    private const double pivDecayLeniencePointPP = 0.7;
    private const double pivDecayLenienceGraze = 0.4;
    private const double pivDecayLenienceEnemyDestroy = 0.2;
    private const double pivDecayBoostValue = 0.014;
    private const double pivDecayBoostPointPP = 0.36;
    private const double pivDecayBoostGraze = 0.03;
    
    private const double pivDecayLeniencePhase = 4;
    
    public double Meter { get; private set; } 
    private const double meterBoostGraze = 0.008;
    private const double meterBoostGem = 0.02;
    private const double meterRefillRate = 0.015;
    private const double meterUseRate = 0.25;
    public const double meterUseThreshold = 0.4;
    private const double meterUseInstantCost = 0.03;
    
    public bool MeterInUse { get; set; }
    private double MeterPivPerPPPMultiplier => MeterInUse ? 2 : 1;
    private double MeterScorePerValueMultiplier => MeterInUse ? 1.69 : 1;
    
    public bool Reloaded { get; set; }
    
    public int Continues { get; private set; }
    public int HitsTaken { get; private set; }
    public int EnemiesDestroyed { get; private set; }

    private int nextScoreLifeIndex;
    private int nextItemLifeIndex;
    public readonly CampaignMode mode;
    public bool Continued { get; private set; }
    private PlayerTeam team;
    [CanBeNull] public PlayerConfig Player => team.Player;
    [CanBeNull] public ShotConfig Shot => team.Shot;
    public Subshot Subshot => team.Subshot;

    public void SetSubshot(Subshot newSubshot) {
        team.Subshot = newSubshot;
    }
    
    //TODO: this can cause problems if multiple phases are declared lenient at the same time, but that's not a current use case
    public bool Lenience { get; set; }
    [CanBeNull] public BehaviorEntity ExecutingBoss { get; set; }

    private static readonly long[] scoreLives = {
         1000000,
         2000000,
         5000000,
         7500000,
        10000000,
        15000000,
        20000000,
        25000000,
        30000000,
        40000000,
        50000000,
        70000000,
        100000000,
        long.MaxValue
    };
    private static readonly int[] pointLives = {
        69,
        141,
        314,
        420,
        666,
        999,
        1337,
        1667,
        2048,
        2718,
        3142,
        4200,
        6666,
        9001,
        int.MaxValue
    };

    private readonly CampaignConfig campaign;

    public CampaignData(CampaignMode mode, GameRequest? req = null, long? maxScore = null) {
        this.mode = mode;
        this.MaxScore = maxScore ?? 9001;
        campaign = req?.lowerRequest.Resolve(cr => cr.campaign.campaign, _ => null, _ => null, _ => null);
        team = req?.metadata.team ?? PlayerTeam.Empty;
        if (campaign != null) {
            Lives = campaign.startLives > 0 ? campaign.startLives : StartLives(mode);
        } else {
            Lives = StartLives(mode);
        }
        Bombs = StartBombs(mode);
        Power = StartPower(mode, team.Shot);
        this.Score = 0;
        this.PIV = 1;
        Meter = 1;
        nextScoreLifeIndex = 0;
        nextItemLifeIndex = 0;
        remVisibleScoreLerpTime = 0;
        lastScore = 0;
        UIVisibleScore = 0;
        LifeItems = 0;
        PIVDecay = 1f;
        pivDecayLenience = 0f;
        UIVisiblePIVDecayLenienceRatio = 0f;
        Continues = mode.OneLife() ? 0 : defltContinues;
        Continued = false;
        Reloaded = false;
        HitsTaken = 0;
        pivDecayRateMultiplier = 1f;
        EnemiesDestroyed = 0;
        Lenience = false;
        Graze = 0;
        ExecutingBoss = null;
        MeterInUse = false;
    }

    public bool TryContinue() {
        if (Continues > 0) {
            Continued = true;
            Replayer.Cancel();
            --Continues;
            Score = lastScore = UIVisibleScore = nextItemLifeIndex = nextScoreLifeIndex = LifeItems = 0;
            PIV = 1;
            Meter = 1;
            if (campaign != null) {
                Lives = campaign.startLives > 0 ? campaign.startLives : StartLives(mode);
            } else {
                Lives = StartLives(mode);
            }
            Bombs = StartBombs(mode);
            remVisibleScoreLerpTime = PIVDecay = pivDecayLenience = 0;
            UIManager.UpdatePlayerUI();
            return true;
        } else return false;
    }


    /// <summary>
    /// Delta should be negative.
    /// </summary>
    public bool TryConsumeBombs(int delta) {
        if (Bombs + delta >= 0) {
            Bombs += delta;
            UIManager.UpdatePlayerUI();
            return true;
        }
        return false;
    }
    public void AddLives(int delta) {
        //if (mode == CampaignMode.NULL) return;
        Log.Unity($"Adding player lives: {delta}");
        if (delta < 0) {
            ++HitsTaken;
            Bombs = Math.Max(Bombs, StartBombs(mode));
            AddPower(powerDeathLoss);
            Meter = 1;
        }
        if (delta < 0 && mode.OneLife()) Lives = 0;
        else Lives = Math.Max(0, Lives + delta);
        if (Lives == 0) GameStateManager.HandlePlayerDeath();
        UIManager.UpdatePlayerUI();
    }

    /// <summary>
    /// Don't use this in the main campaign-- it will interfere with stats
    /// </summary>
    public void SetLives(int to) => AddLives(to - Lives);

    private void AddPIVDecay(double delta) => PIVDecay = M.Clamp(0, 1, PIVDecay + delta);
    private void AddPIVDecayLenience(double time) => pivDecayLenience = Math.Max(pivDecayLenience, time);
    public void ExternalLenience(double time) => AddPIVDecayLenience(time);
    private void AddMeter(double delta) {
        var belowThreshold = Meter < meterUseThreshold;
        Meter = M.Clamp(0, 1, Meter + delta);
        if (belowThreshold && Meter >= meterUseThreshold) {
            SFXService.MeterUsable();
        }
    }

    public void RefillMeterFrame(PlayerInput.PlayerState state) {
        double rate = 0;
        if (state == PlayerInput.PlayerState.NORMAL) rate = meterRefillRate;
        //meter use handled under TryUseMeterFrame
        AddMeter(rate * ETime.FRAME_TIME);
    }

    public bool TryStartMeter() {
        if (Meter >= meterUseThreshold) {
            Meter -= meterUseInstantCost;
            return true;
        } else return false;
    }

    public bool TryUseMeterFrame() {
        var consume = meterUseRate * ETime.FRAME_TIME;
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
        if (Power < prevFloor) SFXService.PowerLost();
        if (prevPower < prevCeil && Power >= prevCeil) {
            if (Power >= powerMax) SFXService.PowerFull();
            else SFXService.PowerGained();
        }
        UIManager.UpdatePlayerUI();
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
        SFXService.PowerFull();
    }
    public void AddPowerItems(int delta) {
        if (!PowerMechanicEnabled || Power >= powerMax) {
            AddValueItems((int)(delta * powerToValueConversion));
        } else AddPower(delta * powerItemValue);
    }

    public void AddFullPowerItems(int _) {
        FullPower();
    }
    public void AddValueItems(int delta) {
        AddPIVDecay(delta * pivDecayBoostValue);
        AddPIVDecayLenience(pivDecayLenienceValue);
        AddScore((long)Math.Round(delta * valueItemPoints * MeterScorePerValueMultiplier * EffectivePIV));
    }
    public void AddGraze(int delta) {
        Graze += delta;
        AddPIVDecay(delta * pivDecayBoostGraze);
        AddPIVDecayLenience(pivDecayLenienceGraze);
        AddMeter(delta * meterBoostGraze);
        Counter.GrazeProc(delta);
        UIManager.UpdatePlayerUI();
    }

    public void AddPointPlusItems(int delta) {
        PIV += pivPerPPP * MeterPivPerPPPMultiplier * delta;
        AddPIVDecay(delta * pivDecayBoostPointPP);
        AddPIVDecayLenience(pivDecayLeniencePointPP);
        UIManager.UpdatePlayerUI();
    }

    public void AddGems(int delta) {
        AddMeter(delta * meterBoostGem);
    }

    public void LifeExtend() {
        ++Lives;
        SFXService.LifeExtend();
        UIManager.UpdatePlayerUI();
    }

    public void PhaseEnd(PhaseCompletion pc) {
        if (pc.props.phaseType?.IsPattern() ?? false) AddPIVDecayLenience(pivDecayLeniencePhase);
        SFXService.PhaseEndSound(pc.Captured);
        if (pc.props.phaseType == PhaseType.STAGE) SFXService.StageSectionEndSound();
        UIManager.CardCapture(pc);
        ChallengeManager.ReceivePhaseCompletion(pc);
    }
    
    private void AddScore(long delta) {
        lastScore = UIVisibleScore;
        Score += delta;
        MaxScore = Math.Max(MaxScore, Score);
        if (nextScoreLifeIndex < scoreLives.Length && Score >= scoreLives[nextScoreLifeIndex]) {
            ++nextScoreLifeIndex;
            LifeExtend();
            UIManager.LifeExtendScore();
        }
        remVisibleScoreLerpTime = visibleScoreLerpTime;
        //updated in RegUpd
    }
    public void AddLifeItems(int delta) {
        LifeItems += delta;
        if (nextItemLifeIndex < pointLives.Length && LifeItems >= pointLives[nextItemLifeIndex]) {
            ++nextItemLifeIndex;
            LifeExtend();
            UIManager.LifeExtendItems();
        }
        UIManager.UpdatePlayerUI();
    }

    public void DestroyNormalEnemy() {
        ++EnemiesDestroyed;
        AddPIVDecayLenience(pivDecayLenienceEnemyDestroy);
    }

    public void RegularUpdate() {
        if (remVisibleScoreLerpTime > 0) {
            remVisibleScoreLerpTime -= ETime.FRAME_TIME;
            if (remVisibleScoreLerpTime <= 0) UIVisibleScore = Score;
            else UIVisibleScore = (long) M.Lerp(lastScore, Score, 1 - remVisibleScoreLerpTime / visibleScoreLerpTime);
            UIManager.UpdatePlayerUI();
        }
        UIVisiblePIVDecayLenienceRatio = M.Lerp(UIVisiblePIVDecayLenienceRatio, 
            Math.Min(1f, pivDecayLenience / 3f), 6f * ETime.FRAME_TIME);
        if (PlayerInput.PlayerActive && !Lenience && GameStateManager.IsRunning) {
            if (pivDecayLenience > 0) {
                pivDecayLenience = Math.Max(0, pivDecayLenience - ETime.FRAME_TIME);
            } else if (PIVDecay > 0) {
                PIVDecay = Math.Max(0, PIVDecay - ETime.FRAME_TIME * pivDecayRate * pivDecayRateMultiplier);
            } else if (PIV > 1) {
                PIV = Math.Max(1, PIV - pivFallStep);
                PIVDecay = 0.5f;
                pivDecayLenience = pivDecayLenienceFall;
                UIManager.UpdatePlayerUI();
            }
        }
    }

    public void OpenBoss(BehaviorEntity boss) {
        if (ExecutingBoss != null) CloseBoss();
        ExecutingBoss = boss;
        pivDecayRateMultiplier *= pivDecayRateMultiplierBoss;
    }

    public void CloseBoss() {
        if (ExecutingBoss != null) {
            ExecutingBoss = null;
            pivDecayRateMultiplier /= pivDecayRateMultiplierBoss;
        } else Log.UnityError("You tried to close a boss section when no boss exists.");
    }

    public void AddDecayRateMultiplier_Tutorial(double m) {
        pivDecayRateMultiplier *= m;
    }

    public void SaveCampaign(string gameIdentifier) {
        SaveData.r.TrySetHighScore(gameIdentifier, Score);
    }
    
    #if UNITY_EDITOR
    public void SetPower(double x) => Power = x;
    #endif
}

/// <summary>
/// A singleton manager for persistent game data.
/// This is the only scene-persistent object in the game.
/// </summary>
public class GameManagement : RegularUpdater {
    public static readonly Version EngineVersion = new Version(4, 2, 0);
    public static bool Initialized { get; private set; } = false;
    public static DifficultySettings Difficulty { get; set; } = 
#if UNITY_EDITOR
        new DifficultySettings(FixedDifficulty.Ultra);
#else
        new DifficultySettings(FixedDifficulty.Normal);
#endif
    public static float FRAME_TIME_BULLET => Difficulty.FRAME_TIME_BULLET;
    

    public static CampaignData campaign = new CampaignData(CampaignMode.NULL);
    private static CampaignData lastinfo = campaign;
    [UsedImplicitly]
    public static bool Continued => campaign.Continued;

    public static void NewCampaign(CampaignMode mode, long? highScore, GameRequest? req = null) => 
        lastinfo = campaign = new CampaignData(mode, req, highScore);
    public static void CheckpointCampaignData() => lastinfo = campaign;
    public static void ReloadCampaignData() {
        Debug.Log("Reloading campaign from last stage.");
        lastinfo.Reloaded = true;
        Replayer.Cancel();
        campaign = lastinfo;
        UIManager.UpdatePlayerUI();
    }

#if UNITY_EDITOR
    [ContextMenu("Add 1000 value")]
    public void YeetScore() => campaign.AddValueItems(1000);
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

    [ContextMenu("Set bullet speed to 2")]
    public void SetBulletSpeed2() => Difficulty = 
        new DifficultySettings(FixedDifficulty.Ultra, bulletSpeedMod: 2f);
#endif
    public static IEnumerable<FixedDifficulty> VisibleDifficulties => new[] {
        FixedDifficulty.Easier, FixedDifficulty.Easy, FixedDifficulty.Normal, FixedDifficulty.Hard,
        FixedDifficulty.Lunatic, FixedDifficulty.Ultra, 
        //DifficultySet.Abex, DifficultySet.Assembly
    };
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
        ETime.RegisterPersistentEOFInvoke(BehaviorEntity.PruneControls);
        ETime.RegisterPersistentEOFInvoke(CurvedTileRenderLaser.PruneControls);
        SceneIntermediary.RegisterSceneUnload(ClearForScene);
        SceneIntermediary.RegisterSceneLoad(Replayer.LoadLazy);
    #if UNITY_EDITOR
        Log.Unity($"Graphics Jobs: {PlayerSettings.graphicsJobs} {PlayerSettings.graphicsJobMode}; MTR {PlayerSettings.MTRendering}");
    #endif
        Log.Unity($"Graphics Render mode {SystemInfo.renderingThreadingMode}");
        Log.Unity($"Danmokou {EngineVersion}, {References.gameIdentifier} {References.gameVersion}");

        //The reason we do this instead of Awake is that we want all resources to be
        //loaded before any State Machines are constructed, which may occur in other entities' Awake calls.
        GetComponent<BulletManager>().Setup();
        GetComponent<ResourceManager>().Setup();
        GetComponentInChildren<SFXService>().Setup();
        GetComponentInChildren<AudioTrackService>().Setup();
    }

    public static bool MainMenuExists => References.mainMenu != null;
    public static bool GoToMainMenu() => SceneIntermediary.LoadScene(
            new SceneIntermediary.SceneRequest(References.mainMenu, 
                SceneIntermediary.SceneRequest.Reason.ABORT_RETURN, Replayer.Cancel));
    public static bool GoToReplayScreen() => SceneIntermediary.LoadScene(
        new SceneIntermediary.SceneRequest(References.replaySaveMenu, 
            SceneIntermediary.SceneRequest.Reason.FINISH_RETURN));

    /// <summary>
    /// Reloads the specific level that is being run.
    /// This is for single-scene mini projects and is not generally exposed. Deprecate in the future.
    /// </summary>
    /// <returns></returns>
    public static bool ReloadLevel() => SceneIntermediary._ReloadScene(ReloadCampaignData);
    /// <summary>
    /// Restarts the existing game if it exists, or reloads the specific level.
    /// </summary>
    /// <returns></returns>
    public static bool Restart() => GameRequest.Rerun() ?? ReloadLevel();
    
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
        Debug.Log($"Reloading level: {Difficulty.Describe} is the current difficulty");
        UIManager.UpdateTags();
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
        campaign = new CampaignData(CampaignMode.NULL);
        UIManager.UpdatePlayerUI();
        SeijaCamera.ResetTargetFlip(0.2f);
#if UNITY_EDITOR || ALLOW_RELOAD
        Events.LocalReset.InvokeIfNotRefractory();
#endif
    }
    
#if UNITY_EDITOR || ALLOW_RELOAD
    private void Update() {
        TryTriggerLocalReset();
    }
    
    private static bool TryTriggerLocalReset() {
        if (!SceneIntermediary.IsFirstScene) return false;
        if (Input.GetKeyDown(KeyCode.R)) {
        } else if (Input.GetKeyDown(KeyCode.Alpha5)) {
            GameManagement.Difficulty = new DifficultySettings(FixedDifficulty.Easier);
        } else if (Input.GetKeyDown(KeyCode.T)) {
            GameManagement.Difficulty = new DifficultySettings(FixedDifficulty.Easy);
        } else if (Input.GetKeyDown(KeyCode.Y)) {
            GameManagement.Difficulty = new DifficultySettings(FixedDifficulty.Normal);
        } else if (Input.GetKeyDown(KeyCode.U)) {
            GameManagement.Difficulty = new DifficultySettings(FixedDifficulty.Hard);
        } else if (Input.GetKeyDown(KeyCode.I)) {
            GameManagement.Difficulty = new DifficultySettings(FixedDifficulty.Lunatic);
        } else if (Input.GetKeyDown(KeyCode.O)) {
            GameManagement.Difficulty = new DifficultySettings(FixedDifficulty.Ultra);
        } else if (Input.GetKeyDown(KeyCode.P)) {
            GameManagement.Difficulty = new DifficultySettings(FixedDifficulty.Abex);
        } else if (Input.GetKeyDown(KeyCode.LeftBracket)) {
            GameManagement.Difficulty = new DifficultySettings(FixedDifficulty.Assembly);
        } else return false;
        LocalReset();
        return true;
    }
#endif

    private static void ClearPhase() {
        BulletManager.ClearPoolControls();
        BulletManager.ClearEmpty();
        Events.Event0.Reset();
        ETime.Slowdown.RevokeAll(MultiMultiplier.Priority.CLEAR_PHASE);
        SeijaCamera.ResetTargetFlip(1f);
        ETime.Timer.ResetAll();
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
