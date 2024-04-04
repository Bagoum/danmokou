using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Events;
using Danmokou.Achievements;
using Danmokou.ADV;
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
using Danmokou.VN;
using JetBrains.Annotations;
using SuzunoyaUnity;
using UnityEditor;
using static Danmokou.SM.SMAnalysis;
using Danmokou.Core.DInput;
using Danmokou.UI.XML;

namespace Danmokou.Services {
/// <summary>
/// A singleton manager for persistent game data.
/// This is the only scene-persistent object in the game.
/// </summary>
public class GameManagement : CoroutineRegularUpdater {
    public static readonly Version EngineVersion = new(11, 0, 0);
    public static readonly int ExecutionNumber = new System.Random().Next();
    public static DifficultySettings Difficulty => Instance.Difficulty;

    public static DifficultySettings defaultDifficulty { get; private set; } =
#if UNITY_EDITOR
        new(FixedDifficulty.Lunatic);
#else
        new DifficultySettings(FixedDifficulty.Normal);
#endif

    public static InstanceData Instance { get; private set; } = null!;
    public static Evented<InstanceData> EvInstance { get; } = new(null!);

    static GameManagement() {
        //This is somewhat faster for reflection consumption than Instance => EvInstance.Value
        _ = EvInstance.Subscribe(x => Instance = x);
    }
    
    public static void DeactivateInstance() {
        //Actually null on startup
        // ReSharper disable once ConstantConditionalAccessQualifier
        if (Instance?.InstanceActive == true) {
            Logs.Log("Deactivating game instance.");
            Instance.Deactivate(false);
            Instance.Dispose();
        }
    }

    public static void NewInstance(InstanceMode mode, InstanceFeatures features, InstanceRequest? req = null, ReplayActor? replay = null) {
        DeactivateInstance();
        var inst = new InstanceData(mode, features, req, replay);
#if UNITY_EDITOR
        if (mode == InstanceMode.DEBUG)
            inst.GetOrSetTeam(EvInstance.Value?.TeamCfg);
#endif
        Logs.Log($"Creating new game instance with mode {mode} on difficulty {inst.Difficulty.Describe()}.", true);
        EvInstance.OnNext(inst);
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
    public static UXMLReferences UXMLPrefabs => References.uxmlDefaults;
    public static ADVReferences ADVReferences => References.advReferences!;
    public static AchievementManager? Achievements { get; private set; }

    public bool OpenAsDebugMode = false;

    private static InstanceFeatures DefaultFeatures => 
        References.gameDefinition is IDanmakuGameDef g ?
            g.MakeFeatures(defaultDifficulty, 
#if UNITY_EDITOR
                Main.OpenAsDebugMode ? InstanceMode.DEBUG : 
#endif
                    InstanceMode.NULL, null) :
            InstanceFeatures.InactiveFeatures;

    private void Awake() {
        if (Main != null) {
            DestroyImmediate(gameObject);
            return;
        }
        Main = this;
        DontDestroyOnLoad(this);
        NewInstance(
#if UNITY_EDITOR || ALLOW_RELOAD
            OpenAsDebugMode ? InstanceMode.DEBUG : 
#endif
                InstanceMode.NULL, 
#if UNITY_EDITOR || ALLOW_RELOAD
            OpenAsDebugMode ? DefaultFeatures :
#endif
        InstanceFeatures.InactiveFeatures
            );

        Logs.Log($"Danmokou {EngineVersion}, {References.gameDefinition.Key} {References.gameDefinition.Version}, exec {ExecutionNumber}");
        gameObject.AddComponent<SceneIntermediary>().defaultTransition = References.defaultTransition;
        gameObject.AddComponent<FreezeFrameHelper>();
        ETime.RegisterPersistentSOFInvoke(Replayer.BeginFrame);
        ETime.RegisterPersistentSOFInvoke(Enemy.FreezeEnemies);
        ETime.RegisterPersistentEOFInvoke(BehaviorEntity.PrunePoolControls);
        ETime.RegisterPersistentEOFInvoke(CurvedTileRenderLaser.PrunePoolControls);
        SceneIntermediary.SceneUnloaded.Subscribe(_ => ClearScene());
        
        RegisterService<IUXMLReferences>(UXMLPrefabs);

        EnumHelpers2.SetupUnityContextDependencies();
        //The reason we do this instead of Awake is that we want all resources to be
        //loaded before any State Machines are constructed, which may occur in other entities' Awake calls.
        // I tried to get rid of those constructions, but with the presence of ResetValues, it's not easy.
        GetComponent<ResourceManager>().Setup();
        GetComponent<BulletManager>().Setup();
        GetComponentInChildren<SFXService>().Setup();
        GetComponentInChildren<AudioTrackService>().Setup();

        Achievements = References.gameDefinition.MakeAchievements()?.Construct();
        
        References.gameDefinition.ApplyConfigurations();

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
            SceneRequest.Reason.ABORT_RETURN, () => Instance.Request?.Cancel())) is { };
    
    public static bool QuickFadeToMainMenu() => ServiceLocator.Find<ISceneIntermediary>().LoadScene(
        new SceneRequest(References.mainMenu,
            SceneRequest.Reason.ABORT_RETURN, () => Instance.Request?.Cancel())
                { Transition = GameManagement.References.defaultTransition.AsQuickFade(false) }) is { };

    public static bool CanRestart => Instance.Request is { CanRestart: true };

    public static void ClearScene() {
        //TODO: this is necessary because PlayerController/AyaCamera can add tokens to Slowdown in droppable coroutines.
        // Ideally we shouldn't allow that.
        ETime.Slowdown.ClearDisturbances();
        ETime.Timer.StopAll();
        BulletManager.OrphanAll(); //Also clears pool controls
        PublicDataHoisting.ClearAllValues();
        //PICustomData.ClearNames();
        ReflWrap.ClearWrappers();
        StateMachineManager.ClearCachedSMs();
        Events.SceneCleared.OnNext(default);
    }

#if UNITY_EDITOR || ALLOW_RELOAD

    public static void LocalReset() {
        ETime.Slowdown.ClearDisturbances();
        ETime.Timer.StopAll();
        BehaviorEntity.DestroyAllSummons();
        PublicDataHoisting.ClearAllValues();
        //PICustomData.ClearNames();
        ReflWrap.ClearWrappers();
        StateMachineManager.ClearCachedSMs();
        BulletManager.ClearPoolControls();
        BulletManager.ClearAllBullets();
        BulletManager.DestroyCopiedPools();
        GC.Collect();
        //Ordered last so cancellations from HardCancel will occur under old data
        NewInstance(InstanceMode.DEBUG, DefaultFeatures);
        Debug.Log($"Reloading level: {Difficulty.Describe()} is the current difficulty");
        Events.LocalReset.OnNext(default);
    }

    private static bool TryTriggerLocalReset() {
        if (!SceneIntermediary.IsFirstScene) return false;
        if (InputManager.GetKeyTrigger(KeyCode.R).Active) {
        } else if (InputManager.GetKeyTrigger(KeyCode.T).Active) {
            defaultDifficulty = new DifficultySettings(FixedDifficulty.Easy);
        } else if (InputManager.GetKeyTrigger(KeyCode.Y).Active) {
            defaultDifficulty = new DifficultySettings(FixedDifficulty.Normal);
        } else if (InputManager.GetKeyTrigger(KeyCode.U).Active) {
            defaultDifficulty = new DifficultySettings(FixedDifficulty.Hard);
        } else if (InputManager.GetKeyTrigger(KeyCode.I).Active) {
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
#if UNITY_EDITOR || ALLOW_RELOAD
        TryTriggerLocalReset();
#endif
        Instance._RegularUpdate();
        base.RegularUpdate();
    }



    private static AnalyzedDayCampaign? _dayCampaign;
    public static AnalyzedDayCampaign DayCampaign =>
        References.gameDefinition is ISceneDanmakuGameDef g ?
            _dayCampaign ??= new AnalyzedDayCampaign(g.DayCampaign, g) :
            throw new Exception($"Game {References.gameDefinition.Key} is not {nameof(ISceneDanmakuGameDef)}");

    private static AnalyzedCampaign[]? _campaigns;
    public static AnalyzedCampaign[] Campaigns =>
        References.gameDefinition is ICampaignDanmakuGameDef g ?
            _campaigns ??= g.Campaigns.Select(c => new AnalyzedCampaign(c, g)).ToArray() :
            throw new Exception($"Game {References.gameDefinition.Key} is not {nameof(ICampaignDanmakuGameDef)}");

    public static IEnumerable<AnalyzedCampaign> FinishedCampaigns =>
        Campaigns.Where(c => SaveData.r.CampaignCompleted(c.campaign.key));

    public static AnalyzedCampaign MainCampaign {
        get {
            var key = References.CampaignGameDef.Campaign.Key;
            return Campaigns.FirstOrDefault(c => c.campaign.key == key)!;
        }
    }

    public static AnalyzedCampaign? ExtraCampaign {
        get {
            var c = References.CampaignGameDef;
            return c.ExCampaign is {Key: { } key} ? 
                Campaigns.FirstOrDefault(c => c.campaign.key == key) : 
                null;
        }
    }
    public static AnalyzedBoss[] PBosses => FinishedCampaigns.SelectMany(c => c.PracticeBosses).ToArray();
    public static AnalyzedStage[] PStages => FinishedCampaigns.SelectMany(c => c.PracticeStages).ToArray();

    public static bool PracticeBossesExist => Campaigns.SelectMany(c => c.PracticeBosses).Any();
    public static bool PracticeStagesExist => Campaigns.SelectMany(c => c.PracticeStages).Any();

#if UNITY_EDITOR
    public static AnalyzedBoss[] AllPBosses => Campaigns.SelectMany(c => c.PracticeBosses).ToArray();

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
    public void LowerRankLevel() => (Instance.RankF as RankFeature)?.SetRankLevel(Instance.RankF!.RankLevel - 1);
    [ContextMenu("Up Rank Level")]
    public void UpRankLevel() => (Instance.RankF as RankFeature)?.SetRankLevel(Instance.RankF!.RankLevel + 1);
    [ContextMenu("Add Rank Points")]
    public void AddRankPoints() => (Instance.RankF as RankFeature)?.AddRankPoints(10000);
    [ContextMenu("Sub Rank Points")]
    public void SubRankPoints() => (Instance.RankF as RankFeature)?.AddRankPoints(-10000);

    [ContextMenu("Debug Game Mode")]
    public void DebugGameMode() => Logs.Log(Instance.mode.ToString());

    [ContextMenu("Add Lenience")]
    public void AddLenience() => Instance.AddLenience(2);

    /*
    [ContextMenu("Debug GCX stats")]
    public void DebugGCXStats() {
        Logs.Log(GenCtx.DebugState());
    }
    */

    //[ContextMenu("Save AoT Helpers")] 
    //public void GenerateAoT() => Reflector.GenerateAoT();

//#if EXBAKE_SAVE
    [ContextMenu("Bake Expressions")]
    public void BakeExpressions() {
        BakeCodeGenerator.BakeExpressions(false);
        Reflector.GenerateAoT();
        EditorApplication.ExitPlaymode();
    }
    
    [ContextMenu("Verify Expressions")]
    public void VerifyExpressions() {
        BakeCodeGenerator.BakeExpressions(true);
    }
    
//#endif
#endif

}
}