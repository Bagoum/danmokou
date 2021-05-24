using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using Danmokou.Achievements;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Graphics.Backgrounds;
using Danmokou.Player;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using ProtoBuf;
using Danmokou.SM;
using InstanceLowRequest = Danmokou.Core.DU<Danmokou.GameInstance.CampaignRequest, Danmokou.GameInstance.BossPracticeRequest, 
    Danmokou.GameInstance.PhaseChallengeRequest, Danmokou.GameInstance.StagePracticeRequest>;
using static Danmokou.Core.GameManagement;
using static Danmokou.Scenes.SceneIntermediary;
using InstanceLowRequestKey = Danmokou.Core.DU<string, ((string, string), int), ((((string, int), string), int), int), ((string, int), int)>;


namespace Danmokou.GameInstance {

public readonly struct BossPracticeRequest {
    public readonly SMAnalysis.AnalyzedBoss boss;
    public readonly SMAnalysis.Phase phase;
    public PhaseType PhaseType => phase.type;

    public BossPracticeRequest(SMAnalysis.AnalyzedBoss boss, SMAnalysis.Phase? phase = null) {
        this.boss = boss;
        //the array boss.phases contains nontrivial phases only
        this.phase = phase ?? boss.Phases[0];
    }

    public ((string, string bossName), int) Key => (boss.Key, phase.index);
    public static BossPracticeRequest Reconstruct(((string campaign, string bossKey), int phase) key) {
        var boss = SMAnalysis.AnalyzedBoss.Reconstruct(key.Item1);
        return new BossPracticeRequest(boss, boss.Phases.First(p => p.index == key.Item2));
    }
}

public readonly struct StagePracticeRequest {
    public readonly SMAnalysis.AnalyzedStage stage;
    public readonly int phase;
    public readonly LevelController.LevelRunMethod method;

    public StagePracticeRequest(SMAnalysis.AnalyzedStage stage, int phase, LevelController.LevelRunMethod method = LevelController.LevelRunMethod.CONTINUE) {
        this.stage = stage;
        this.phase = phase;
        this.method = method;
    }
    
    public ((string, int), int) Key => (stage.Key, phase);
    public static StagePracticeRequest Reconstruct(((string, int), int) key) =>
        new StagePracticeRequest(SMAnalysis.AnalyzedStage.Reconstruct(key.Item1), key.Item2);
}

public readonly struct CampaignRequest {
    public readonly SMAnalysis.AnalyzedCampaign campaign;

    public CampaignRequest(SMAnalysis.AnalyzedCampaign campaign) {
        this.campaign = campaign;
    }
    
    public string Key => campaign.Key;
    public static CampaignRequest Reconstruct(string key) =>
        new CampaignRequest(SMAnalysis.AnalyzedCampaign.Reconstruct(key));
    
}

public readonly struct SharedInstanceMetadata {
    public readonly TeamConfig team;
    public readonly DifficultySettings difficulty;
    
    public SharedInstanceMetadata(TeamConfig team, DifficultySettings difficulty) {
        this.team = team;
        this.difficulty = difficulty;
    }
    
    public SharedInstanceMetadata(Saveable saved) : this(new TeamConfig(saved.team), saved.difficulty) { }

    [Serializable]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct Saveable {
        public TeamConfig.Saveable team { get; set; }
        public DifficultySettings difficulty { get; set; }

        public Saveable(SharedInstanceMetadata gm) {
            this.team = new TeamConfig.Saveable(gm.team);
            this.difficulty = gm.difficulty;
        }
    }
}


public class InstanceRequest {
    private readonly List<Cancellable> gameTrackers = new List<Cancellable>();
    public readonly Func<InstanceData, bool>? cb;
    public readonly SharedInstanceMetadata metadata;
    public readonly Replay? replay;
    public bool Saveable => replay == null;
    public readonly InstanceLowRequest lowerRequest;
    public readonly int seed;
    public InstanceMode Mode => lowerRequest.Resolve(
        _ => InstanceMode.CAMPAIGN,
        _ => InstanceMode.CARD_PRACTICE,
        _ => InstanceMode.SCENE_CHALLENGE,
        _ => InstanceMode.STAGE_PRACTICE);

    public InstanceRequest(Func<InstanceData, bool>? cb, InstanceLowRequest lowerRequest, 
        Replay replay) : this(cb, replay.metadata.Record.SharedInstanceMetadata, lowerRequest, replay) {}

    public InstanceRequest(Func<InstanceData, bool>? cb, SharedInstanceMetadata metadata, CampaignRequest? campaign = null,
        BossPracticeRequest? boss = null, PhaseChallengeRequest? challenge = null, StagePracticeRequest? stage = null,
        Replay? replay = null) : 
        this(cb, metadata, InstanceLowRequest.FromNullable(
            campaign, boss, challenge, stage) ?? throw new Exception("No valid request type made of GameReq"), 
            replay) { }

    public InstanceRequest(Func<InstanceData, bool>? cb, SharedInstanceMetadata metadata, InstanceLowRequest lowerRequest, Replay? replay) {
        this.metadata = metadata;
        this.cb = cb;
        this.replay = replay;
        this.lowerRequest = lowerRequest;
        this.seed = replay?.metadata.Record.Seed ?? new Random().Next();
    }

    public void SetupInstance() {
        Log.Unity(
            $"Starting game with mode {Mode} on difficulty {metadata.difficulty.Describe()}.");
        var actor = (replay == null) ? 
            Replayer.BeginRecording() :
            Replayer.BeginReplaying(
                new Replayer.ReplayerConfig(
                    replay.Value.metadata.Debug ? 
                        Replayer.ReplayerConfig.FinishMethod.STOP :
                        Replayer.ReplayerConfig.FinishMethod.ERROR, replay.Value.frames));
        GameManagement.NewInstance(Mode, SaveData.r.GetHighScore(this), this, actor);
    }

    public InstanceRecord MakeGameRecord(AyaPhoto[]? photos = null, string? ending = null) {
        var record = new InstanceRecord(this, GameManagement.Instance, true) {
            Photos = photos ?? new AyaPhoto[0],
            Ending = ending
        };
        return record;
    }

    public void TrySave(InstanceRecord record) {
        if (Saveable)
            SaveData.r.RecordGame(record);
    }
    
    /// <summary>
    /// If the record is not already created, it will be created here.
    /// </summary>
    public bool FinishAndPostReplay(InstanceRecord? record = null) {
        var d = GameManagement.Instance;
        if (cb?.Invoke(d) ?? true) {
            record?.Update(d);
            record ??= MakeGameRecord();
            GameManagement.DeactivateInstance(); //Also stops the replay
            TrySave(record);
            InstanceCompleted.Publish((d, record));
            return true;
        } else {
            if (record != null && Saveable) {
                Log.Unity($"Invalidating record with UUID {record.Uuid}", level: LogLevel.INFO);
                SaveData.r.InvalidateRecord(record.Uuid);
            }
            return false;
        }
    }
    
    public static InstanceLowRequestKey CampaignIdentifier(InstanceLowRequest lowerRequest) => lowerRequest.Resolve(
        c => new InstanceLowRequestKey(0, c.Key, default, default, default),
        b => new InstanceLowRequestKey(1, default!, b.Key, default, default),
        c => new InstanceLowRequestKey(2, default!, default, c.Key, default),
        s => new InstanceLowRequestKey(3, default!, default, default, s.Key));

    private void WaitThenFinishAndPostReplay(InstanceRecord? record = null) => 
        SceneLocalCRU.Main.RunDroppableRIEnumerator(WaitingUtils.WaitFor(
            WaitBeforeReturn, Cancellable.Null, () => FinishAndPostReplay(record)));

    public bool Run() {
        Cancel();
        RNG.Seed(seed);
        replay?.metadata.ApplySettings();
        InstancedRequested.Publish(this);
            
        return lowerRequest.Resolve(
            SelectCampaign,
            SelectBoss,
            SelectChallenge,
            SelectStage);
    }

    public void Cancel() {
        GameManagement.DeactivateInstance();
        foreach (var c in gameTrackers) c.Cancel();
        gameTrackers.Clear();
    }

    private ICancellee NewTracker() {
        var c = new Cancellable();
        gameTrackers.Add(c);
        return c;
    }

    private bool SelectCampaign(CampaignRequest c) {
        ICancellee tracker = NewTracker();
        bool _Finalize(string? endingKey = null) {
            if (!tracker.Cancelled && FinishAndPostReplay(MakeGameRecord(null, endingKey))) {
                Log.Unity($"Campaign complete for {c.campaign.campaign.key}. Returning to replay save screen.");
                return true;
            } else return false;
        }
        bool ExecuteEndcard() {
            Log.Unity($"Game stages for {c.campaign.campaign.key} are tentatively finished." +
                      " Moving to endcard, if it exists." +
                      "\nIf you see a REJECTED message below this, then a reload or the like prevented completion.");
            if (c.campaign.campaign.TryGetEnding(out var ed)) {
                return SceneIntermediary.LoadScene(new SceneRequest(References.endcard, SceneRequest.Reason.ENDCARD,
                    null,
                    null,
                    () => DependencyInjection.Find<LevelController>()
                        .Request(new LevelController.LevelRunRequest(1, tracker.Guard(() => _Finalize(ed.key)),
                            LevelController.LevelRunMethod.CONTINUE, new EndcardStageConfig(ed.dialogueKey)))
                ));
            } else return _Finalize();
        }
        bool ExecuteStage(int index) {
            if (index < c.campaign.stages.Length) {
                var s = c.campaign.stages[index];
                return SceneIntermediary.LoadScene(new SceneRequest(s.stage.sceneConfig,
                    SceneRequest.Reason.RUN_SEQUENCE,
                    (index == 0) ? SetupInstance : (Action?) null,
                    //Note: this load during onHalfway is for the express purpose of preventing load lag
                    () => StateMachineManager.FromText(s.stage.stateMachine),
                    () => DependencyInjection.Find<LevelController>()
                        .Request(new LevelController.LevelRunRequest(1, tracker.Guard(() => {
                                if (ExecuteStage(index + 1))
                                    StageCompleted.Publish((c.campaign.Key, index));
                            }),
                        LevelController.LevelRunMethod.CONTINUE, s.stage))));
            } else return ExecuteEndcard();
        }
        return ExecuteStage(0);
    }
    
    private bool SelectStage(StagePracticeRequest s) {
        var tracker = NewTracker();
        return SceneIntermediary.LoadScene(new SceneRequest(s.stage.stage.sceneConfig,
            SceneRequest.Reason.START_ONE,
            SetupInstance,
            //Note: this load during onHalfway is for the express purpose of preventing load lag
            () => StateMachineManager.FromText(s.stage.stage.stateMachine),
            () => DependencyInjection.Find<LevelController>().Request(
                new LevelController.LevelRunRequest(s.phase,
                    tracker.Guard(() => FinishAndPostReplay()), s.method, s.stage.stage))));
    }


    private bool SelectBoss(BossPracticeRequest ab) {
        var tracker = NewTracker();
        var b = ab.boss.boss;
        BackgroundOrchestrator.NextSceneStartupBGC = b.Background(ab.PhaseType);
        return SceneIntermediary.LoadScene(new SceneRequest(References.unitScene,
            SceneRequest.Reason.START_ONE,
            SetupInstance,
            //Note: this load during onHalfway is for the express purpose of preventing load lag
            () => StateMachineManager.FromText(b.stateMachine),
            () => {
                var beh = UnityEngine.Object.Instantiate(b.boss).GetComponent<BehaviorEntity>();
                beh.phaseController.Override(ab.phase.index, tracker.Guard(() => WaitThenFinishAndPostReplay()));
                beh.RunSMFromScript(b.stateMachine);
            }));
    }

    private bool SelectChallenge(PhaseChallengeRequest cr) {
        var tracker = NewTracker();
        BackgroundOrchestrator.NextSceneStartupBGC = cr.Boss.Background(cr.phase.phase.type);
        return SceneIntermediary.LoadScene(new SceneRequest(References.unitScene,
            SceneRequest.Reason.START_ONE,
            SetupInstance,
            () => {
                StateMachineManager.FromText(cr.Boss.stateMachine);
                DependencyInjection.Find<IChallengeManager>().TrackChallenge(new SceneChallengeReqest(this, cr), 
                    tracker.Guard<InstanceRecord>(WaitThenFinishAndPostReplay));
            },
            () => {
                var beh = UnityEngine.Object.Instantiate(cr.Boss.boss).GetComponent<BehaviorEntity>();
                DependencyInjection.Find<IChallengeManager>().LinkBoss(beh);
            }));
    }
    


    private static SceneConfig MaybeSaveReplayScene(InstanceData d) => 
        (References.replaySaveMenu != null && d.Replay is ReplayRecorder rr) ?
        References.replaySaveMenu : References.mainMenu;

    public const float WaitBeforeReturn = 2f;

    public static bool DefaultReturn(InstanceData d) => LoadScene(new SceneRequest(MaybeSaveReplayScene(d),
        SceneRequest.Reason.FINISH_RETURN,
        () => BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground));
    
    public static bool PracticeSuccess(InstanceData d) {
        if (SceneIntermediary.LOADING) return false;
        InstanceData.PracticeSuccess.Proc();
        return true;
    }
    public static bool ViewReplay(Replay r) {
        return new InstanceRequest(DefaultReturn, r.metadata.Record.ReconstructedRequest, r).Run();
    }

    public static bool ViewReplay(Replay? r) => r != null && ViewReplay(r.Value);


    public static bool RunCampaign(SMAnalysis.AnalyzedCampaign? campaign, Action? cb, 
        SharedInstanceMetadata metadata) {
        if (campaign == null) return false;
        var req = new InstanceRequest(d => LoadScene(new SceneRequest(MaybeSaveReplayScene(d), 
            SceneRequest.Reason.FINISH_RETURN, () => {
                cb?.Invoke();
                BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground;
            })), metadata, campaign: new CampaignRequest(campaign));


        if (SaveData.r.TutorialDone || References.miniTutorial == null) return req.Run();
        //Note: if you Restart within the mini-tutorial, it will send you to stage 1.
        // This is because Restart calls campaign.Request.Run().
        else return LoadScene(new SceneRequest(References.miniTutorial,
            SceneRequest.Reason.START_ONE,
            //Prevents hangover information from previous campaign, will be overriden anyways
            req.SetupInstance,
            null, 
            () => MiniTutorial.RunMiniTutorial(() => req.Run())));
    }

    public static bool RunTutorial() {
        if (References.tutorial == null) return false;
        return SceneIntermediary.LoadScene(new SceneRequest(References.tutorial, SceneRequest.Reason.START_ONE,
            () => GameManagement.NewInstance(InstanceMode.TUTORIAL, null)));
    }
    
    /// <summary>
    /// Sent before the instance is run. Sent even if the instance was a replay.
    /// </summary>
    public static readonly Event<InstanceRequest> InstancedRequested = new Event<InstanceRequest>();
    /// <summary>
    /// Sent once the stage is completed (during a campaign only), before the next stage is loaded.
    /// </summary>
    public static readonly Event<(string campaign, int stage)> StageCompleted =
        new Event<(string, int)>();
    /// <summary>
    /// Sent once the instance is completed (during any mode), before returning to the main menu (or wherever).
    /// Sent even if the instance was a replay. 
    /// </summary>
    public static readonly Event<(InstanceData data, InstanceRecord record)> InstanceCompleted = 
        new Event<(InstanceData, InstanceRecord)>();
}
}
