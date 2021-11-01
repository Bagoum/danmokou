using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using BagoumLib.Events;
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

namespace Danmokou.Services {
/// <summary>
/// A singleton manager for persistent game data.
/// This is the only scene-persistent object in the game.
/// </summary>
public class GameManagement : CoroutineRegularUpdater {
    public static readonly Version EngineVersion = new Version(8, 0, 0);
    public static bool Initialized { get; private set; } = false;
    public static DifficultySettings Difficulty => Instance.Difficulty;

    public static DifficultySettings defaultDifficulty { get; private set; } =
#if UNITY_EDITOR
        new DifficultySettings(FixedDifficulty.Lunatic);
#else
        new DifficultySettings(FixedDifficulty.Normal);
#endif

    public static InstanceData Instance => _evInstance.Value;
    private static Evented<InstanceData> _evInstance { get; } = new Evented<InstanceData>(null!);
    public static EventProxy<InstanceData> EvInstance { get; } = new EventProxy<InstanceData>(_evInstance);
    [UsedImplicitly] public static bool Continued => Instance.Continued;

    public static void DeactivateInstance() {
        //Actually null on startup
        // ReSharper disable once ConstantConditionalAccessQualifier
        if (Instance?.InstanceActive == true) {
            Logs.Log("Deactivating game instance");
            Instance.Deactivate();
            Instance.Dispose();
        }
    }

    public static void NewInstance(InstanceMode mode, long? highScore = null, InstanceRequest? req = null, ReplayActor? replay = null) {
        DeactivateInstance();
        Logs.Log($"Creating new game instance with mode {mode}");
        _evInstance.OnNext(new InstanceData(mode, req, highScore, replay));
    }

    public static IEnumerable<FixedDifficulty> VisibleDifficulties => new[] {
        FixedDifficulty.Easy, FixedDifficulty.Normal, FixedDifficulty.Hard,
        FixedDifficulty.Lunatic
    };
    public static IEnumerable<FixedDifficulty?> CustomAndVisibleDifficulties =>
        VisibleDifficulties.Select(fd => (FixedDifficulty?) fd).Prepend(null);

    public static GameManagement Main { get; private set; } = null!;
    public GameUniqueReferences references = null!;
    public static GameUniqueReferences References => Main.references;
    public static PrefabReferences Prefabs => References.prefabReferences;
    public static AchievementManager? Achievements { get; private set; }

    public SOPlayerHitbox visiblePlayer = null!;
    public bool OpenAsDebugMode = false;
    public static Vector2 VisiblePlayerLocation => Main.visiblePlayer.location;

    private void Awake() {
        if (Main != null) {
            DestroyImmediate(gameObject);
            return;
        }
        NewInstance(
#if UNITY_EDITOR || ALLOW_RELOAD
            OpenAsDebugMode ? InstanceMode.DEBUG : 
#endif
                InstanceMode.NULL);
        Initialized = true;
        Main = this;
        DontDestroyOnLoad(this);

        //This looks silly, but the static initializer needs to be actively run to ensure that the locale is set correctly.
        _ = SaveData.s;
        
        Logs.Log($"Danmokou {EngineVersion}, {References.gameIdentifier} {References.gameVersion}");
        gameObject.AddComponent<SceneIntermediary>().defaultTransition = References.defaultTransition;
        gameObject.AddComponent<FreezeFrameHelper>();
        ETime.RegisterPersistentSOFInvoke(Replayer.BeginFrame);
        ETime.RegisterPersistentSOFInvoke(Enemy.FreezeEnemies);
        ETime.RegisterPersistentEOFInvoke(BehaviorEntity.PrunePoolControls);
        ETime.RegisterPersistentEOFInvoke(CurvedTileRenderLaser.PrunePoolControls);
        SceneIntermediary.SceneUnloaded.Subscribe(_ => ClearScene());
        
        //The reason we do this instead of Awake is that we want all resources to be
        //loaded before any State Machines are constructed, which may occur in other entities' Awake calls.
        // I tried to get rid of those constructions, but with the presence of ResetValues, it's not easy.
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

    public static bool GoToMainMenu() => ServiceLocator.Find<ISceneIntermediary>().LoadScene(
        new SceneRequest(References.mainMenu,
            //This cancels the replay and deactivates the instance as well
            SceneRequest.Reason.ABORT_RETURN, () => Instance.Request?.Cancel()));

    public static bool GoToReplayScreen() => ServiceLocator.Find<ISceneIntermediary>().LoadScene(
        new SceneRequest(References.replaySaveMenu,
            SceneRequest.Reason.FINISH_RETURN,
            () => BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground));

    /// <summary>
    /// Restarts the game instance.
    /// </summary>
    public static bool Restart() {
        if (Instance.Request == null) throw new Exception("No game instance found to restart");
        InstanceRequest.InstanceRestarted.OnNext(Instance.Request);
        return Instance.Request.Run();
    }

    public static bool CanRestart => Instance.Request != null;

    public static void ClearScene() {
        //TODO: is this required? or can we assume that disposables are handled correctly on scene change?
        ETime.Slowdown.ClearDisturbances();
        ETime.Timer.DestroyAll();
        BulletManager.OrphanAll(); //Also clears pool controls
        PublicDataHoisting.DestroyAll();
        FiringCtx.ClearNames();
        //SMs may have links to data hoisting, so we destroy both of them on phase end.
        ReflWrap.ClearWrappers();
        StateMachineManager.ClearCachedSMs();
        BehaviorEntity.ClearPointers();
        AyaPhoto.ClearTextures();
        Events.SceneCleared.OnNext(default);
    }

#if UNITY_EDITOR || ALLOW_RELOAD

    private void Update() {
        TryTriggerLocalReset();
    }
    
    public static void LocalReset() {
        ETime.Slowdown.ClearDisturbances();
        ETime.Timer.DestroyAll();
        BehaviorEntity.DestroyAllSummons();
        PublicDataHoisting.DestroyAll();
        FiringCtx.ClearNames();
        ReflWrap.ClearWrappers();
        StateMachineManager.ClearCachedSMs();
        BulletManager.ClearPoolControls();
        BulletManager.ClearAllBullets();
        BulletManager.DestroyCopiedPools();
        //Ordered last so cancellations from HardCancel will occur under old data
        NewInstance(InstanceMode.DEBUG);
        Debug.Log($"Reloading level: {Difficulty.Describe()} is the current difficulty");
        Events.LocalReset.OnNext(default);
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

    public static void ClearPhaseAutocull(SoftcullProperties propsSimple, SoftcullProperties propsBeh) {
        BulletManager.ClearEmptyBullets(false);
        BulletManager.Autocull(propsSimple);
        BehaviorEntity.Autocull(propsBeh);
    }

    public static void ClearPhaseAutocullOverTime_Initial(SoftcullProperties propsSimple, SoftcullProperties propsBeh) {
        BulletManager.AutocullCircleOverTime(propsSimple);
        BehaviorEntity.Autocull(propsBeh);
    }
    public static void ClearPhaseAutocullOverTime_Final() {
        BulletManager.ClearEmptyBullets(false);
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
    public void DebugGameMode() => Logs.Log(Instance.mode.ToString());

    [ContextMenu("Add Lenience")]
    public void AddLenience() => Instance.AddFaithLenience(2);

    /*
    [ContextMenu("Debug GCX stats")]
    public void DebugGCXStats() {
        Logs.Log(GenCtx.DebugState());
    }
    */

    //[ContextMenu("Save AoT Helpers")] 
    //public void GenerateAoT() => Reflector.GenerateAoT();

#if EXBAKE_SAVE
    [ContextMenu("Bake Expressions")]
    public void BakeExpressions() {
        BakeCodeGenerator.BakeExpressions();
        Reflector.GenerateAoT();
        EditorApplication.ExitPlaymode();
    }
#endif
#endif

}
}