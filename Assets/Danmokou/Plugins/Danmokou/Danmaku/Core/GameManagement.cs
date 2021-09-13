using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using Danmokou.Achievements;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Graphics;
using Danmokou.Graphics.Backgrounds;
using Danmokou.Player;
using Danmokou.Pooling;
using Danmokou.Reflection;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
using Danmokou.UI;
using JetBrains.Annotations;
using UnityEditor;
using static Danmokou.SM.SMAnalysis;
using GameLowRequest = Danmokou.Core.DU<Danmokou.GameInstance.CampaignRequest, Danmokou.GameInstance.BossPracticeRequest, 
    Danmokou.GameInstance.PhaseChallengeRequest, Danmokou.GameInstance.StagePracticeRequest>;

namespace Danmokou.Core {
/// <summary>
/// A singleton manager for persistent game data.
/// This is the only scene-persistent object in the game.
/// </summary>
public class GameManagement : CoroutineRegularUpdater {
    public static readonly Version EngineVersion = new Version(7, 0, 3);
    public static bool Initialized { get; private set; } = false;
    public static DifficultySettings Difficulty => Instance.Difficulty;

    public static DifficultySettings defaultDifficulty { get; private set; } =
#if UNITY_EDITOR
        new DifficultySettings(FixedDifficulty.Lunatic);
#else
        new DifficultySettings(FixedDifficulty.Normal);
#endif

    public static InstanceData Instance { get; private set; } = null!;
    [UsedImplicitly] public static bool Continued => Instance.Continued;

    public static void DeactivateInstance() {
        if (Instance?.InstanceActive == true) {
            Log.Unity("Deactivating game instance");
            Instance.Deactivate();
            Instance.Dispose();
        }
    }

    public static void NewInstance(InstanceMode mode, long? highScore = null, InstanceRequest? req = null, ReplayActor? replay = null) {
        DeactivateInstance();
        Log.Unity($"Creating new game instance with mode {mode}");
        Instance = new InstanceData(mode, req, highScore, replay);
    }

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
    public bool OpenAsDebugMode = false;
    public static Vector2 VisiblePlayerLocation => gm.visiblePlayer.location;

    private void Awake() {
        if (gm != null) {
            DestroyImmediate(gameObject);
            return;
        }
        NewInstance(
#if UNITY_EDITOR || ALLOW_RELOAD
            OpenAsDebugMode ? InstanceMode.DEBUG : 
#endif
                InstanceMode.NULL);
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
            //This cancels the replay and deactivates the isntance as well
            SceneIntermediary.SceneRequest.Reason.ABORT_RETURN, () => Instance.Request?.Cancel()));

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
        BulletManager.ClearAllBullets();
        BulletManager.DestroyCopiedPools();
#if UNITY_EDITOR || ALLOW_RELOAD
        Events.LocalReset.Proc();
        //Ordered last so cancellations from HardCancel will occur under old data
        NewInstance(InstanceMode.DEBUG);
#endif
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
        BulletManager.ClearEmptyBullets(false);
        Events.Event0.Reset();
        ETime.Slowdown.RevokeAll(MultiOp.Priority.CLEAR_PHASE);
        ETime.Timer.ResetPhase();
        Events.PhaseCleared.Proc();
    }

    public static void ClearPhaseAutocull(SoftcullProperties props) {
        ClearPhase();
        BulletManager.Autocull(props);
        BehaviorEntity.Autocull(props);
    }

    public static void ClearPhaseAutocullOverTime_Initial(SoftcullProperties props) {
        BulletManager.AutocullCircleOverTime(props);
        BehaviorEntity.Autocull(props);
    }
    public static void ClearPhaseAutocullOverTime_Final() {
        ClearPhase();
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
        Campaigns.Where(c => SaveData.r.CampaignCompleted(c.campaign.key));

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

    private PlayerController Player => ServiceLocator.Find<PlayerController>();

    [ContextMenu("Add 1000 value")]
    public void YeetScore() => Player.AddValueItems(1000, 1);

    [ContextMenu("Add 10 PIV+")]
    public void YeetPIV() => Player.AddPointPlusItems(10);

    [ContextMenu("Add 40 life")]
    public void YeetLife() => Player.AddLifeItems(40);

    [ContextMenu("Set Power to 1")]
    public void SetPower1() => Instance.SetPower(1);

    [ContextMenu("Set Power to 2")]
    public void SetPower2() => Instance.SetPower(2);

    [ContextMenu("Set Power to 3")]
    public void SetPower3() => Instance.SetPower(3);

    [ContextMenu("Set Power to 4")]
    public void SetPower4() => Instance.SetPower(4);

    [ContextMenu("Set Subshot D")]
    public void SetSubshotD() => Player.SetSubshot(Subshot.TYPE_D);

    [ContextMenu("Set Subshot M")]
    public void SetSubshotM() => Player.SetSubshot(Subshot.TYPE_M);

    [ContextMenu("Set Subshot K")]
    public void SetSubshotK() => Player.SetSubshot(Subshot.TYPE_K);


    [ContextMenu("Lower Rank Level")]
    public void LowerRankLevel() => Instance.SetRankLevel(Instance.RankLevel - 1);
    [ContextMenu("Up Rank Level")]
    public void UpRankLevel() => Instance.SetRankLevel(Instance.RankLevel + 1);
    [ContextMenu("Add Rank Points")]
    public void AddRankPoints() => Instance.AddRankPoints(10000);
    [ContextMenu("Sub Rank Points")]
    public void SubRankPoints() => Instance.AddRankPoints(-10000);

    [ContextMenu("Debug Game Mode")]
    public void DebugGameMode() => Log.Unity(Instance.mode.ToString());

    [ContextMenu("Add Lenience")]
    public void AddLenience() => Instance.AddFaithLenience(2);

    //[ContextMenu("Save AoT Helpers")] 
    //public void GenerateAoT() => Reflector.GenerateAoT();

    [ContextMenu("Bake Expressions")]
    public void BakeExpressions() {
        BakeCodeGenerator.BakeExpressions();
        Reflector.GenerateAoT();
        EditorApplication.ExitPlaymode();
    }
#endif

}
}