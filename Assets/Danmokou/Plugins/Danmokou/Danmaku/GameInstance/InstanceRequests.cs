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
using static Danmokou.Services.GameManagement;


namespace Danmokou.GameInstance {

public interface ILowInstanceRequest {
    public ILowInstanceRequestKey Key { get; }
    public InstanceMode Mode { get; }
    public ICampaignMeta Campaign { get; }
}

public class CampaignRequest : ILowInstanceRequest {
    public readonly SMAnalysis.AnalyzedCampaign campaign;

    public CampaignRequest(SMAnalysis.AnalyzedCampaign campaign) {
        this.campaign = campaign;
    }

    public ILowInstanceRequestKey Key => new CampaignRequestKey() {
        Campaign = campaign.Key
    };
    public InstanceMode Mode => InstanceMode.CAMPAIGN;
    public ICampaignMeta Campaign => campaign.campaign;
}
public class BossPracticeRequest : ILowInstanceRequest {
    public readonly SMAnalysis.AnalyzedBoss boss;
    public readonly SMAnalysis.Phase phase;
    public PhaseType PhaseType => phase.type;

    public BossPracticeRequest(SMAnalysis.AnalyzedBoss boss, SMAnalysis.Phase? phase = null) {
        this.boss = boss;
        //the array boss.phases contains nontrivial phases only
        this.phase = phase ?? boss.Phases[0];
    }

    public ILowInstanceRequestKey Key => new BossPracticeRequestKey() {
        Campaign = boss.campaign.Key,
        Boss = boss.boss.key,
        PhaseIndex = phase.index
    };
    public InstanceMode Mode => InstanceMode.BOSS_PRACTICE;
    public ICampaignMeta Campaign => boss.campaign.campaign;
}

public class StagePracticeRequest : ILowInstanceRequest {
    public readonly SMAnalysis.AnalyzedStage stage;
    public readonly int phase;
    public readonly LevelController.LevelRunMethod method;

    public StagePracticeRequest(SMAnalysis.AnalyzedStage stage, int phase, LevelController.LevelRunMethod method = LevelController.LevelRunMethod.CONTINUE) {
        this.stage = stage;
        this.phase = phase;
        this.method = method;
    }

    public ILowInstanceRequestKey Key => new StagePracticeRequestKey() {
        Campaign = stage.campaign.Key,
        StageIndex = stage.stageIndex,
        PhaseIndex = phase
    };
    public InstanceMode Mode => InstanceMode.STAGE_PRACTICE;
    public ICampaignMeta Campaign => stage.campaign.campaign;
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

public abstract class ReplayMode {
    public class Replaying : ReplayMode {
        public readonly Replay replay;
        public Replaying(Replay replay) {
            this.replay = replay;
        }
    }
    /// <summary>
    /// Disables functionality such as VNState backlogging, and allows a replay to be saved at the end.
    /// </summary>
    public class RecordingReplay : ReplayMode { }
    /// <summary>
    /// Enables functionality such as VNState backlogging and runtime dialogue speed/language modification,
    ///  and disallows saving a replay.
    /// </summary>
    public class NotRecordingReplay : ReplayMode { }
}

public class InstanceRequest {
    private readonly List<Cancellable> gameTrackers = new List<Cancellable>();
    public readonly Func<InstanceData, bool>? cb;
    public readonly SharedInstanceMetadata metadata;
    public readonly ReplayMode replay;
    public bool Saveable => replay is ReplayMode.RecordingReplay;
    public readonly ILowInstanceRequest lowerRequest;
    public readonly int seed;
    public InstanceMode Mode => lowerRequest.Mode;

    public InstanceRequest(Func<InstanceData, bool>? cb, ILowInstanceRequest lowerRequest, Replay replay) : 
        this(cb, replay.metadata.Record.SharedInstanceMetadata, lowerRequest, new ReplayMode.Replaying(replay)) {}

    public InstanceRequest(Func<InstanceData, bool>? cb, SharedInstanceMetadata metadata, ILowInstanceRequest lowReq, bool? recording = null) : 
        this(cb, metadata, lowReq, recording switch {
                null => null,
                true => new ReplayMode.RecordingReplay(),
                false => new ReplayMode.NotRecordingReplay()
            }) { }

    public InstanceRequest(Func<InstanceData, bool>? cb, SharedInstanceMetadata metadata, ILowInstanceRequest lowerRequest, ReplayMode? replay) {
        this.metadata = metadata;
        this.cb = cb;
        this.replay = replay ?? (lowerRequest.Campaign.Replayable ? 
            new ReplayMode.RecordingReplay() : 
            (ReplayMode)new ReplayMode.NotRecordingReplay());
        this.lowerRequest = lowerRequest;
        this.seed = replay is ReplayMode.Replaying r ?  r.replay.metadata.Record.Seed : new Random().Next();
    }

    public void SetupInstance() {
        Logs.Log(
            $"Starting game with mode {Mode} on difficulty {metadata.difficulty.Describe()}.");
        var actor = replay switch {
            ReplayMode.NotRecordingReplay _ => null,
            ReplayMode.RecordingReplay _ => Replayer.BeginRecording(),
            ReplayMode.Replaying r =>
                Replayer.BeginReplaying(
                    new Replayer.ReplayerConfig(
                        r.replay.metadata.Debug ?
                            Replayer.ReplayerConfig.FinishMethod.STOP :
                            Replayer.ReplayerConfig.FinishMethod.ERROR, r.replay.frames)),
            _ => throw new Exception($"Unhandled replay type: {replay}")
        };
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
            InstanceCompleted.OnNext((d, record));
            return true;
        } else {
            if (record != null && Saveable) {
                Logs.Log($"Invalidating record with UUID {record.Uuid}", level: LogLevel.INFO);
                SaveData.r.InvalidateRecord(record.Uuid);
            }
            return false;
        }
    }

    private void WaitThenFinishAndPostReplay(InstanceRecord? record = null) => 
        WaitingUtils.WaitThenCB(GameManagement.Main, ServiceLocator.Find<ISceneIntermediary>().SceneBoundedToken, 
            WaitBeforeReturn, false, () => FinishAndPostReplay(record));

    public bool Run() {
        Cancel();
        RNG.Seed(seed);
        if (replay is ReplayMode.Replaying r)
            r.replay.metadata.ApplySettings();
        InstancedRequested.OnNext(this);

        return lowerRequest switch {
            CampaignRequest cr => SelectCampaign(cr),
            BossPracticeRequest br => SelectBoss(br),
            PhaseChallengeRequest sc => SelectChallenge(sc),
            StagePracticeRequest sr => SelectStage(sr),
            _ => throw new Exception($"No instance run handling for request type {lowerRequest.GetType()}")
        };
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
                Logs.Log($"Campaign complete for {c.campaign.campaign.key}. Returning to replay save screen.");
                return true;
            } else return false;
        }
        bool ExecuteEndcard() {
            Logs.Log($"Game stages for {c.campaign.campaign.key} are tentatively finished." +
                      " Moving to endcard, if it exists." +
                      "\nIf you see a REJECTED message below this, then a reload or the like prevented completion.");
            if (c.campaign.campaign.TryGetEnding(out var ed)) {
                return ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(
                    References.endcard, 
                    SceneRequest.Reason.ENDCARD,
                    null,
                    null,
                    () => ServiceLocator.Find<LevelController>()
                        .Request(new LevelController.LevelRunRequest(1, tracker.Guard(() => _Finalize(ed.key)),
                            LevelController.LevelRunMethod.CONTINUE, new EndcardStageConfig(ed.dialogueKey)))
                ));
            } else return _Finalize();
        }
        bool ExecuteStage(int index) {
            if (index < c.campaign.stages.Length) {
                var s = c.campaign.stages[index];
                return ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(
                    s.stage.sceneConfig,
                    SceneRequest.Reason.RUN_SEQUENCE,
                    (index == 0) ? SetupInstance : (Action?) null,
                    //Note: this load during onHalfway is for the express purpose of preventing load lag
                    () => StateMachineManager.FromText(s.stage.stateMachine),
                    () => ServiceLocator.Find<LevelController>()
                        .Request(new LevelController.LevelRunRequest(1, tracker.Guard(() => {
                                if (ExecuteStage(index + 1))
                                    StageCompleted.OnNext((c.campaign.Key, index));
                            }),
                        LevelController.LevelRunMethod.CONTINUE, s.stage))));
            } else return ExecuteEndcard();
        }
        return ExecuteStage(0);
    }
    
    private bool SelectStage(StagePracticeRequest s) {
        var tracker = NewTracker();
        return ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(
            s.stage.stage.sceneConfig,
            SceneRequest.Reason.START_ONE,
            SetupInstance,
            //Note: this load during onHalfway is for the express purpose of preventing load lag
            () => StateMachineManager.FromText(s.stage.stage.stateMachine),
            () => ServiceLocator.Find<LevelController>().Request(
                new LevelController.LevelRunRequest(s.phase,
                    tracker.Guard(() => FinishAndPostReplay()), s.method, s.stage.stage))));
    }


    private bool SelectBoss(BossPracticeRequest ab) {
        var tracker = NewTracker();
        var b = ab.boss.boss;
        BackgroundOrchestrator.NextSceneStartupBGC = b.Background(ab.PhaseType);
        return ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(References.unitScene,
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
        return ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(References.unitScene,
            SceneRequest.Reason.START_ONE,
            SetupInstance,
            () => {
                StateMachineManager.FromText(cr.Boss.stateMachine);
                ServiceLocator.Find<IChallengeManager>().TrackChallenge(new SceneChallengeReqest(this, cr), 
                    tracker.Guard<InstanceRecord>(WaitThenFinishAndPostReplay));
            },
            () => {
                var beh = UnityEngine.Object.Instantiate(cr.Boss.boss).GetComponent<BehaviorEntity>();
                ServiceLocator.Find<IChallengeManager>().LinkBoss(beh);
            }));
    }
    


    private static SceneConfig MaybeSaveReplayScene(InstanceData d) => 
        (References.replaySaveMenu != null && d.Replay is ReplayRecorder) ?
        References.replaySaveMenu : References.mainMenu;

    public const float WaitBeforeReturn = 2f;

    public static bool DefaultReturn(InstanceData d) => ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(MaybeSaveReplayScene(d),
        SceneRequest.Reason.FINISH_RETURN,
        () => BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground));
    
    public static bool PracticeSuccess(InstanceData d) {
        if (SceneIntermediary.LOADING) return false;
        d.PracticeSuccess.OnNext(default);
        return true;
    }
    public static bool ViewReplay(Replay r) {
        return new InstanceRequest(DefaultReturn, r.metadata.Record.ReconstructedRequest, r).Run();
    }

    public static bool ViewReplay(Replay? r) => r != null && ViewReplay(r.Value);


    public static bool RunCampaign(SMAnalysis.AnalyzedCampaign? campaign, Action? cb, 
        SharedInstanceMetadata metadata) {
        if (campaign == null) return false;
        var req = new InstanceRequest(d => 
            ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(MaybeSaveReplayScene(d), 
            SceneRequest.Reason.FINISH_RETURN, () => {
                cb?.Invoke();
                BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground;
            })), metadata, new CampaignRequest(campaign));


        if (SaveData.r.TutorialDone || References.miniTutorial == null) return req.Run();
        //Note: if you Restart within the mini-tutorial, it will send you to stage 1.
        // This is because Restart calls campaign.Request.Run().
        else return ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(References.miniTutorial,
            SceneRequest.Reason.START_ONE,
            //Prevents hangover information from previous campaign, will be overriden anyways
            req.SetupInstance,
            null, 
            () => ServiceLocator.Find<MiniTutorial>().RunMiniTutorial(() => req.Run())));
    }

    public static bool RunTutorial() {
        if (References.tutorial == null) return false;
        return ServiceLocator.Find<ISceneIntermediary>().LoadScene(
            new SceneRequest(References.tutorial, SceneRequest.Reason.START_ONE,
            () => GameManagement.NewInstance(InstanceMode.TUTORIAL, null)));
    }

    /// <summary>
    /// Sent before an instance is restarted. InstanceRequested will also be called immediately afterwards.
    /// </summary>
    public static readonly Event<InstanceRequest> InstanceRestarted = new Event<InstanceRequest>();
    /// <summary>
    /// Sent before an instance is run. Sent even if the instance was a replay.
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
