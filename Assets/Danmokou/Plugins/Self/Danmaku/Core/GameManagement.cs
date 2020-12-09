using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku;
using DMK.DataHoist;
using DMK.DMath;
using DMK.GameInstance;
using DMK.Graphics;
using DMK.Graphics.Backgrounds;
using DMK.Player;
using DMK.Pooling;
using DMK.Reflection;
using DMK.Scenes;
using DMK.Scriptables;
using DMK.Services;
using DMK.SM;
using DMK.UI;
using JetBrains.Annotations;
using static DMK.SM.SMAnalysis;
using GameLowRequest = DMK.Core.DU<DMK.GameInstance.CampaignRequest, DMK.GameInstance.BossPracticeRequest, 
    DMK.GameInstance.PhaseChallengeRequest, DMK.GameInstance.StagePracticeRequest>;

namespace DMK.Core {
/// <summary>
/// A singleton manager for persistent game data.
/// This is the only scene-persistent object in the game.
/// </summary>
public class GameManagement : RegularUpdater {
    public static readonly Version EngineVersion = new Version(6, 0, 0);
    public static bool Initialized { get; private set; } = false;
    public static DifficultySettings Difficulty => instance.Difficulty;

    public static DifficultySettings defaultDifficulty { get; private set; } =
#if UNITY_EDITOR
        new DifficultySettings(FixedDifficulty.Lunatic);
#else
        new DifficultySettings(FixedDifficulty.Normal);
#endif

    public static InstanceData instance = new InstanceData(InstanceMode.NULL);
    [UsedImplicitly] public static bool Continued => instance.Continued;

    public static void NewCampaign(InstanceMode mode, long? highScore, [CanBeNull] InstanceRequest req = null) =>
        instance = new InstanceData(mode, req, highScore);

#if UNITY_EDITOR
    [ContextMenu("Add 1000 value")]
    public void YeetScore() => instance.AddValueItems(1000, 1);

    [ContextMenu("Add 10 PIV+")]
    public void YeetPIV() => instance.AddPointPlusItems(10);

    [ContextMenu("Add 40 life")]
    public void YeetLife() => instance.AddLifeItems(40);

    [ContextMenu("Set Power to 1")]
    public void SetPower1() => instance.SetPower(1);

    [ContextMenu("Set Power to 2")]
    public void SetPower2() => instance.SetPower(2);

    [ContextMenu("Set Power to 3")]
    public void SetPower3() => instance.SetPower(3);

    [ContextMenu("Set Power to 4")]
    public void SetPower4() => instance.SetPower(4);

    [ContextMenu("Set Subshot D")]
    public void SetSubshotD() => instance.SetSubshot(Subshot.TYPE_D);

    [ContextMenu("Set Subshot M")]
    public void SetSubshotM() => instance.SetSubshot(Subshot.TYPE_M);

    [ContextMenu("Set Subshot K")]
    public void SetSubshotK() => instance.SetSubshot(Subshot.TYPE_K);

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
            SceneIntermediary.SceneRequest.Reason.ABORT_RETURN, () => {
                instance.Request?.Cancel();
                Replayer.Cancel();
            }));

    public static bool GoToReplayScreen() => SceneIntermediary.LoadScene(
        new SceneIntermediary.SceneRequest(References.replaySaveMenu,
            SceneIntermediary.SceneRequest.Reason.FINISH_RETURN,
            () => BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground));

    /// <summary>
    /// Restarts the game instance.
    /// </summary>
    public static bool Restart() {
        if (instance.Request == null) throw new Exception("No game instance found to restart");
        if (instance.Request.Mode.PreserveReloadAudio()) AudioTrackService.PreserveBGM();
        return instance.Request.Run();
    }

    public static bool CanRestart => instance.Request != null;

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
        instance = new InstanceData(InstanceMode.NULL);
        Debug.Log($"Reloading level: {Difficulty.Describe()} is the current difficulty");
        UIManager.UpdateTags();
    }

#if UNITY_EDITOR || ALLOW_RELOAD
    private void Update() {
        TryTriggerLocalReset();
    }

    private static bool TryTriggerLocalReset() {
        if (!SceneIntermediary.IsFirstScene) return false;
        if (Input.GetKeyDown(KeyCode.R)) { } else if (Input.GetKeyDown(KeyCode.T)) {
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
        instance.RegularUpdate();
    }



    [CanBeNull] private static AnalyzedDayCampaign _dayCampaign;
    public static AnalyzedDayCampaign DayCampaign =>
        _dayCampaign = _dayCampaign ?? new AnalyzedDayCampaign(References.dayCampaign);

    [CanBeNull] private static AnalyzedCampaign[] _campaigns;
    public static AnalyzedCampaign[] Campaigns =>
        _campaigns = _campaigns ?? References.Campaigns.Select(c => new AnalyzedCampaign(c)).ToArray();

    public static IEnumerable<AnalyzedCampaign> FinishedCampaigns =>
        Campaigns.Where(c => SaveData.r.CompletedCampaigns.Contains(c.campaign.key));

    [CanBeNull]
    public static AnalyzedCampaign MainCampaign =>
        Campaigns.First(c => c.campaign.key == References.campaign.key);
    [CanBeNull]
    public static AnalyzedCampaign ExtraCampaign =>
        Campaigns.First(c => c.campaign.key == References.exCampaign.key);
    public static AnalyzedBoss[] PBosses => FinishedCampaigns.SelectMany(c => c.bosses).ToArray();
    public static AnalyzedStage[] PStages => FinishedCampaigns.SelectMany(c => c.practiceStages).ToArray();

#if UNITY_EDITOR
    public static AnalyzedBoss[] AllPBosses => Campaigns.SelectMany(c => c.bosses).ToArray();

#endif

}
}