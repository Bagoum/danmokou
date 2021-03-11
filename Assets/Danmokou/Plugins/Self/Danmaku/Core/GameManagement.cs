using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DMK.Achievements;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku;
using DMK.DataHoist;
using DMK.DMath;
using DMK.Expressions;
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
using FastExpressionCompiler;
using JetBrains.Annotations;
using UnityEditor;
using static DMK.SM.SMAnalysis;
using GameLowRequest = DMK.Core.DU<DMK.GameInstance.CampaignRequest, DMK.GameInstance.BossPracticeRequest, 
    DMK.GameInstance.PhaseChallengeRequest, DMK.GameInstance.StagePracticeRequest>;

namespace DMK.Core {
/// <summary>
/// A singleton manager for persistent game data.
/// This is the only scene-persistent object in the game.
/// </summary>
public class GameManagement : CoroutineRegularUpdater {
    public static readonly Version EngineVersion = new Version(7, 0, 0);
    public static bool Initialized { get; private set; } = false;
    public static DifficultySettings Difficulty => Instance.Difficulty;

    public static DifficultySettings defaultDifficulty { get; private set; } =
#if UNITY_EDITOR
        new DifficultySettings(FixedDifficulty.Lunatic);
#else
        new DifficultySettings(FixedDifficulty.Normal);
#endif

    public static InstanceData Instance { get; private set; } = new InstanceData(InstanceMode.NULL);
    [UsedImplicitly] public static bool Continued => Instance.Continued;

    public static void NewInstance(InstanceMode mode, long? highScore = null, InstanceRequest? req = null) =>
        Instance = new InstanceData(mode, req, highScore);

#if UNITY_EDITOR
    [ContextMenu("Add 1000 value")]
    public void YeetScore() => Instance.AddValueItems(1000, 1);

    [ContextMenu("Add 10 PIV+")]
    public void YeetPIV() => Instance.AddPointPlusItems(10);

    [ContextMenu("Add 40 life")]
    public void YeetLife() => Instance.AddLifeItems(40);

    [ContextMenu("Set Power to 1")]
    public void SetPower1() => Instance.SetPower(1);

    [ContextMenu("Set Power to 2")]
    public void SetPower2() => Instance.SetPower(2);

    [ContextMenu("Set Power to 3")]
    public void SetPower3() => Instance.SetPower(3);

    [ContextMenu("Set Power to 4")]
    public void SetPower4() => Instance.SetPower(4);

    [ContextMenu("Set Subshot D")]
    public void SetSubshotD() => Instance.SetSubshot(Subshot.TYPE_D);

    [ContextMenu("Set Subshot M")]
    public void SetSubshotM() => Instance.SetSubshot(Subshot.TYPE_M);

    [ContextMenu("Set Subshot K")]
    public void SetSubshotK() => Instance.SetSubshot(Subshot.TYPE_K);

    //[ContextMenu("Save AoT Helpers")] 
    //public void GenerateAoT() => Reflector.GenerateAoT();

    [ContextMenu("Bake Expressions")]
    public void BakeExpressions() {
        BakeCodeGenerator.BakeExpressions();
        Reflector.GenerateAoT();
        EditorApplication.ExitPlaymode();
    }
#endif


    public static IEnumerable<FixedDifficulty> VisibleDifficulties => new[] {
        FixedDifficulty.Easy, FixedDifficulty.Normal, FixedDifficulty.Hard,
        FixedDifficulty.Lunatic
    };
    public static IEnumerable<FixedDifficulty?> CustomAndVisibleDifficulties =>
        VisibleDifficulties.Select(fd => (FixedDifficulty?) fd).Prepend(null);

    private static GameManagement gm = null!;
    public GameUniqueReferences references = null!;
    public static GameUniqueReferences References => gm.references;
    public static PrefabReferences Prefabs => References.prefabReferences;
    public static AchievementManager? Achievements { get; private set; }

    public SOPlayerHitbox visiblePlayer = null!;
    public static Vector2 VisiblePlayerLocation => gm.visiblePlayer.location;

    private void Awake() {
        if (gm != null) {
            DestroyImmediate(gameObject);
            return;
        }
        Initialized = true;
        gm = this;
        DontDestroyOnLoad(this);

        //This looks silly, but the static initializer needs to be actively run to ensure that the locale is set correctly.
        _ = SaveData.s;
        
        Log.Unity($"Danmokou {EngineVersion}, {References.gameIdentifier} {References.gameVersion}");
        SceneIntermediary.Setup(References.defaultTransition);
        ParticlePooler.Prepare();
        GhostPooler.Prepare(Prefabs.cutinGhost);
        BEHPooler.Prepare(Prefabs.inode);
        ItemPooler.Prepare(References.items);
        ETime.RegisterPersistentSOFInvoke(Replayer.BeginFrame);
        ETime.RegisterPersistentSOFInvoke(Enemy.FreezeEnemies);
        ETime.RegisterPersistentEOFInvoke(BehaviorEntity.PrunePoolControls);
        ETime.RegisterPersistentEOFInvoke(CurvedTileRenderLaser.PrunePoolControls);
        SceneIntermediary.RegisterSceneUnload(ClearForScene);
        SceneIntermediary.RegisterSceneLoad(OnSceneLoad);

        //The reason we do this instead of Awake is that we want all resources to be
        //loaded before any State Machines are constructed, which may occur in other entities' Awake calls.
        GetComponent<ResourceManager>().Setup();
        GetComponent<BulletManager>().Setup();
        GetComponentInChildren<SFXService>().Setup();
        GetComponentInChildren<AudioTrackService>().Setup();

        if (References.achievements != null)
            Achievements = References.achievements.MakeRepo().Construct();
        
        RunDroppableRIEnumerator(DelayedInitialAchievementsCheck());
    }

    private IEnumerator DelayedInitialAchievementsCheck() {
        for (int ii = 0; ii < 10; ++ii)
            yield return null; //just in case of initial frame wonkiness
        
        for (float t = 0; t < 2f; t += ETime.FRAME_TIME)
            yield return null;
        
        Achievements?.UpdateAll();
    }

    private void OnSceneLoad() {
        Replayer.LoadLazy();
        (new GameObject("Scene-Local CRU")).AddComponent<SceneLocalCRU>();
    }

    public static bool GoToMainMenu() => SceneIntermediary.LoadScene(
        new SceneIntermediary.SceneRequest(References.mainMenu,
            SceneIntermediary.SceneRequest.Reason.ABORT_RETURN, () => {
                Instance.Request?.Cancel();
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
        if (Instance.Request == null) throw new Exception("No game instance found to restart");
        if (Instance.Request.Mode.PreserveReloadAudio()) AudioTrackService.PreserveBGM();
        return Instance.Request.Run();
    }

    public static bool CanRestart => Instance.Request != null;

    public static void ClearForScene() {
        AudioTrackService.ClearAllAudio(false);
        SFXService.ClearConstructed();
        BulletManager.ClearPoolControls();
        Events.Event0.DestroyAll();
        ETime.Slowdown.RevokeAll(MultiOp.Priority.CLEAR_SCENE);
        ETime.Timer.DestroyAll();
        BulletManager.OrphanAll();
        PublicDataHoisting.DestroyAll();
        FiringCtx.ClearNames();
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
        ETime.Slowdown.RevokeAll(MultiOp.Priority.CLEAR_SCENE);
        ETime.Timer.DestroyAll();
        BehaviorEntity.DestroyAllSummons();
        PublicDataHoisting.DestroyAll();
        FiringCtx.ClearNames();
        ReflWrap.ClearWrappers();
        StateMachineManager.ClearCachedSMs();
        BulletManager.ClearPoolControls();
        BulletManager.ClearEmpty();
        BulletManager.ClearAllBullets();
        BulletManager.DestroyCopiedPools();
        InstanceData.CampaignDataUpdated.Proc();
#if UNITY_EDITOR || ALLOW_RELOAD
        Events.LocalReset.Proc();
#endif
        //Ordered last so cancellations from HardCancel will occur under old data
        Instance = new InstanceData(InstanceMode.NULL);
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
        ETime.Slowdown.RevokeAll(MultiOp.Priority.CLEAR_PHASE);
        ETime.Timer.ResetPhase();
        Events.ClearPhase.Proc();
        //Delay this so copy pools can be softculled correctly
        //TODO: can I remove this? might be permissible to keep copied pools throughout the scene
        ETime.QueueDelayedEOFInvoke(1, BulletManager.DestroyCopiedPools);
        //Delay this so that bullets referencing hosting data don't break down before
        //converting into softcull (note softcull bullets don't run velocity)
        ETime.QueueDelayedEOFInvoke(1, PublicDataHoisting.ClearValues);
    }

    public static void ClearPhaseAutocull(SoftcullProperties props) {
        ClearPhase();
        BulletManager.Autocull(props);
        BehaviorEntity.Autocull(props);
    }



    [ContextMenu("Unload unused")]
    public void UnloadUnused() {
        Resources.UnloadUnusedAssets();
    }

    public override int UpdatePriority => UpdatePriorities.SYSTEM;

    public override void RegularUpdate() {
        Instance._RegularUpdate();
        base.RegularUpdate();
    }



    private static AnalyzedDayCampaign? _dayCampaign;
    public static AnalyzedDayCampaign DayCampaign =>
        _dayCampaign ??= new AnalyzedDayCampaign(References.dayCampaign != null ? 
            References.dayCampaign : 
            throw new Exception("No day campaign exists."));

    private static AnalyzedCampaign[]? _campaigns;
    public static AnalyzedCampaign[] Campaigns =>
        _campaigns ??= References.Campaigns.Select(c => new AnalyzedCampaign(c)).ToArray();

    public static IEnumerable<AnalyzedCampaign> FinishedCampaigns =>
        Campaigns.Where(c => SaveData.r.CompletedCampaigns.Contains(c.campaign.key));

    public static AnalyzedCampaign MainCampaign =>
        Campaigns.FirstOrDefault(c => c.campaign.key == References.campaign.key)!;
    
    public static AnalyzedCampaign? ExtraCampaign =>
        References.exCampaign == null ? null :
        Campaigns.FirstOrDefault(c => c.campaign.key == References.exCampaign.key);
    public static AnalyzedBoss[] PBosses => FinishedCampaigns.SelectMany(c => c.bosses).ToArray();
    public static AnalyzedStage[] PStages => FinishedCampaigns.SelectMany(c => c.practiceStages).ToArray();

    public static bool PracticeBossesExist => Campaigns.SelectMany(c => c.bosses).Any();
    public static bool PracticeStagesExist => Campaigns.SelectMany(c => c.practiceStages).Any();

#if UNITY_EDITOR
    public static AnalyzedBoss[] AllPBosses => Campaigns.SelectMany(c => c.bosses).ToArray();

#endif

}
}