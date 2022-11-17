using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using Danmokou.Achievements;
using Danmokou.ADV;
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
using Danmokou.VN;
using static Danmokou.Services.GameManagement;


namespace Danmokou.GameInstance {

public interface ILowInstanceRequest {
    public IDanmakuGameDef Game { get; }
    public LowInstanceRequestKey Key { get; }
    public InstanceMode Mode { get; }
    public ICampaignMeta Campaign { get; }
}

public class CampaignRequest : ILowInstanceRequest {
    public readonly SMAnalysis.AnalyzedCampaign campaign;
    public IDanmakuGameDef Game => campaign.Game;

    public CampaignRequest(SMAnalysis.AnalyzedCampaign campaign) {
        this.campaign = campaign;
    }

    public LowInstanceRequestKey Key => new CampaignRequestKey() {
        Campaign = campaign.Key
    };
    public InstanceMode Mode => InstanceMode.CAMPAIGN;
    public ICampaignMeta Campaign => campaign.campaign;
}
public class BossPracticeRequest : ILowInstanceRequest {
    public readonly SMAnalysis.AnalyzedBoss boss;
    public readonly SMAnalysis.Phase phase;
    public PhaseType PhaseType => phase.type;
    public IDanmakuGameDef Game => boss.Game;

    public BossPracticeRequest(SMAnalysis.AnalyzedBoss boss, SMAnalysis.Phase? phase = null) {
        this.boss = boss;
        //the array boss.phases contains nontrivial phases only
        this.phase = phase ?? boss.Phases[0];
    }

    public LowInstanceRequestKey Key => new BossPracticeRequestKey() {
        Campaign = boss.campaign.Key,
        Boss = boss.boss.key,
        PhaseIndex = phase.index
    };
    public InstanceMode Mode => InstanceMode.BOSS_PRACTICE;
    public ICampaignMeta Campaign => boss.campaign.campaign;
}

public class StagePracticeRequest : ILowInstanceRequest {
    public readonly SMAnalysis.AnalyzedStage stage;
    /// <summary>
    /// Index of the phase in the original state machine.
    /// </summary>
    public readonly int phase;
    public readonly LevelController.LevelRunMethod method;
    public IDanmakuGameDef Game => stage.Game;

    public StagePracticeRequest(SMAnalysis.AnalyzedStage stage, int phase, LevelController.LevelRunMethod method = LevelController.LevelRunMethod.CONTINUE) {
        this.stage = stage;
        this.phase = phase;
        this.method = method;
    }

    public LowInstanceRequestKey Key => new StagePracticeRequestKey() {
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
    
    public SharedInstanceMetadata(Saveable saved, IDanmakuGameDef game) :
        this(new TeamConfig(saved.team, game), saved.difficulty) { }

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

/// <summary>
/// Contains the information required to start a danmaku instance, as well as the cancellation tracker bounding it.
/// </summary>
public record InstanceRequest {
    private readonly Cancellable instTracker = new();
    public IDanmakuGameDef GameDef => lowerRequest.Game;
    public InstanceFeatures Features => GameDef.MakeFeatures(metadata.difficulty, SaveData.r.GetHighScore(this));
    /// <summary>
    /// Callback to run when this instance is complete.
    /// <br/>Note: I use this instead of having <see cref="Run"/> return a task because callbacks can be preserved
    ///  when <see cref="GameManagement.Restart"/> is called.
    ///  You could theoretically also preserve a <see cref="TaskCompletionSource{TResult}"/>
    ///  if it really becomes necessary to have task support.
    /// </summary>
    public Action<InstanceRequest, InstanceRecord> Finalize { get; }
    public SharedInstanceMetadata metadata { get; }
    public ReplayMode replay { get; }
    public ILowInstanceRequest lowerRequest { get; }
    public int seed { get; }
    public ICancellee InstTracker => instTracker;
    public bool Saveable => replay is not ReplayMode.Replaying;
    public InstanceMode Mode => lowerRequest.Mode;

    public InstanceRequest(Action<InstanceRequest, InstanceRecord> finalize, ILowInstanceRequest lowerRequest, Replay replay) : 
        this(finalize, replay.metadata.Record.SharedInstanceMetadata, lowerRequest, new ReplayMode.Replaying(replay)) {}
    public InstanceRequest(Action<InstanceRequest, InstanceRecord> finalize, SharedInstanceMetadata metadata, ILowInstanceRequest lowReq) : 
        this(finalize, metadata, lowReq, null) { }

    public InstanceRequest(Action<InstanceRequest, InstanceRecord> finalize, SharedInstanceMetadata metadata, ILowInstanceRequest lowerRequest, ReplayMode? replay) {
        this.Finalize = finalize;
        this.metadata = metadata;
        this.replay = replay ?? (lowerRequest.Campaign.Replayable ? 
            new ReplayMode.RecordingReplay() : 
            new ReplayMode.NotRecordingReplay());
        this.lowerRequest = lowerRequest;
        this.seed = this.replay is ReplayMode.Replaying r ? r.replay.metadata.Record.Seed : new Random().Next();
    }

    /// <summary>
    /// Make a new instance request from the same information (the seed will be changed).
    /// </summary>
    public InstanceRequest Copy() => new(Finalize, metadata, lowerRequest, replay);

    public void SetupInstance() {
        GameManagement.DeactivateInstance();
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
        GameManagement.NewInstance(Mode, Features, this, actor);
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
    /// Call this when the instance is complete and the record should be preserved in save data.
    /// <br/>If the record is not already created, it will be created here.
    /// </summary>
    internal InstanceRecord CompileAndSaveRecord(InstanceRecord? record = null) {
        if (InstTracker.Cancelled)
            throw new OperationCanceledException();
        var d = GameManagement.Instance;
        record?.Update(d);
        record ??= MakeGameRecord();
        GameManagement.DeactivateInstance(); //Also stops the replay
        TrySave(record);
        InstanceCompleted.OnNext((d, record));
        return record;
        /*
            if (record != null && Saveable) {
                Logs.Log($"Invalidating record with UUID {record.Uuid}", level: LogLevel.INFO);
                SaveData.r.InvalidateRecord(record.Uuid);
            }
            return false;
        }*/
    }

    /// <summary>
    /// Run the instance request.
    /// </summary>
    /// <returns>Whether or not the instance successfully started.</returns>
    public bool Run() {
        RNG.Seed(seed);
        if (replay is ReplayMode.Replaying r)
            r.replay.metadata.ApplySettings();
        InstancedRequested.OnNext(this);

        if (lowerRequest switch {
                CampaignRequest cr => SelectCampaign(cr),
                BossPracticeRequest br => SelectBoss(br),
                PhaseChallengeRequest sc => SelectChallenge(sc),
                StagePracticeRequest sr => SelectStage(sr),
                _ => throw new Exception($"No instance run handling for request type {lowerRequest.GetType()}")
            } is not { } task) {
            return false;
        }
        _ = task.ContinueSuccessWithSync(rec => Finalize(this, rec));
        return true;
        
    }

    public void Cancel() {
        GameManagement.DeactivateInstance();
        instTracker.Cancel();
    }


    private Task<InstanceRecord>? SelectCampaign(CampaignRequest c) =>
        c.campaign.campaign.RunEntireCampaign(this, c.campaign);
    
    private Task<InstanceRecord>? SelectStage(StagePracticeRequest s) {
        if (ServiceLocator.Find<ISceneIntermediary>().LoadScene(SceneRequest.TaskFromOnFinish(
                s.stage.stage.sceneConfig,
                SceneRequest.Reason.START_ONE,
                SetupInstance,
                //Note: this load during onHalfway is for the express purpose of preventing load lag
                () => StateMachineManager.FromText(s.stage.stage.stateMachine),
                () => ServiceLocator.Find<LevelController>()
                    .RunLevel(new(s.phase, s.method, s.stage.stage, InstTracker)),
                out var tcs)) is { }) {
            async Task<InstanceRecord> Rest() {
                await tcs.Task;
                return CompileAndSaveRecord();
            }
            return Rest();
        } else return null;
    }


    private Task<InstanceRecord>? SelectBoss(BossPracticeRequest ab) {
        var b = ab.boss.boss;
        BackgroundOrchestrator.NextSceneStartupBGC = b.Background(ab.PhaseType);
        if (ServiceLocator.Find<ISceneIntermediary>().LoadScene(SceneRequest.TaskFromOnFinish(References.unitScene,
                SceneRequest.Reason.START_ONE,
                SetupInstance,
                //Note: this load during onHalfway is for the express purpose of preventing load lag
                () => StateMachineManager.FromText(b.stateMachine),
                async () => {
                    var beh = UnityEngine.Object.Instantiate(b.boss).GetComponent<BehaviorEntity>();
                    beh.phaseController.Override(ab.phase.index);
                    await beh.RunBehaviorSM(SMRunner.RunRoot(b.stateMachine, InstTracker));
                    return Unit.Default;
                }, out var tcs)) is { }) {
            async Task<InstanceRecord> Rest() {
                await tcs.Task;
                //Allow slight delay for item collection
                await SM.WaitingUtils.WaitFor(GameManagement.Main, InstTracker, WaitBeforeReturn, false);
                return CompileAndSaveRecord();
            }
            return Rest();
        } else return null;
    }

    private Task<InstanceRecord>? SelectChallenge(PhaseChallengeRequest cr) {
        BackgroundOrchestrator.NextSceneStartupBGC = cr.Boss.Background(cr.phase.phase.type);
        if (ServiceLocator.Find<ISceneIntermediary>().LoadScene(SceneRequest.TaskFromOnLoad(References.unitScene,
                SceneRequest.Reason.START_ONE,
                SetupInstance,
                () => {
                    StateMachineManager.FromText(cr.Boss.stateMachine);
                    return ServiceLocator.Find<IChallengeManager>().TrackChallenge(new SceneChallengeReqest(this, cr), InstTracker);
                },
                () => {
                    var beh = UnityEngine.Object.Instantiate(cr.Boss.boss).GetComponent<BehaviorEntity>();
                    ServiceLocator.Find<IChallengeManager>().LinkBoss(beh, InstTracker);
                }, out var tcs)) is { }) {
            async Task<InstanceRecord> Rest() {
                var rec = await tcs.Task;
                //Allow slight delay for item collection
                await SM.WaitingUtils.WaitFor(GameManagement.Main, InstTracker, WaitBeforeReturn, false);
                return CompileAndSaveRecord(rec);
            }
            return Rest();
        } else return null;
    }

    /// <summary>
    /// In some cases, a challenge can execute another challenge immediately after it. In such a case,
    ///  cancel the first <see cref="InstanceRequest"/> and then run this method on another request.
    /// <br/>Note: This calls <see cref="Finalize"/>.
    /// </summary>
    public async Task<InstanceRecord> RunChallengeContinuation(PhaseChallengeRequest cr, ChallengeManager.TrackingContext ctx) {
        SetupInstance();
        GameManagement.Instance.Replay?.Cancel(); //can't replay both scenes together,
        //or even just the second scene due to time-dependency of world objects such as shots
        var t = ctx.cm.TrackChallenge(new SceneChallengeReqest(this, cr), InstTracker, ctx.tracker);
        ctx.cm.LinkBoss(ctx.exec, InstTracker);
        var rec = await t;
        await SM.WaitingUtils.WaitFor(GameManagement.Main, InstTracker, WaitBeforeReturn, false);
        var result = CompileAndSaveRecord(rec);
        Finalize(this, result);
        return result;
    }
    


    private static SceneConfig MaybeSaveReplayScene(bool isRecordingReplay, IDanmakuGameDef game) => 
        (game.ReplaySaveMenu != null && isRecordingReplay) ?
        game.ReplaySaveMenu : References.mainMenu;

    public const float WaitBeforeReturn = 2f;

    private static void WaitThenReturn(SceneRequest toScene) => 
        SM.WaitingUtils.WaitThenCB(GameManagement.Main, ServiceLocator.Find<ISceneIntermediary>().SceneBoundedToken, 
            WaitBeforeReturn, false, () => ServiceLocator.Find<ISceneIntermediary>().LoadScene(toScene));

    private static SceneRequest DefaultReturnScene(InstanceRequest req) => ReturnScene(
        MaybeSaveReplayScene(req.replay is ReplayMode.RecordingReplay, req.lowerRequest.Game));
    private static SceneRequest ReturnScene(SceneConfig sc) => new(sc, SceneRequest.Reason.FINISH_RETURN);
    public static bool DefaultReturn(InstanceRequest req) => 
        ServiceLocator.Find<ISceneIntermediary>().LoadScene(DefaultReturnScene(req)) is {};
    
    public static void PracticeSuccess(InstanceRequest req, InstanceRecord rec) => 
        Instance.PracticeSuccess.OnNext(req.lowerRequest);
    
    public static bool ViewReplay(Replay? r) {
        if (r == null) return false;
        return new InstanceRequest((_, __) => ServiceLocator.Find<ISceneIntermediary>().LoadScene(
            ReturnScene(References.mainMenu)), r.metadata.Record.ReconstructedRequest, r).Run();
    }

    /// <summary>
    /// </summary>
    /// <param name="campaign"></param>
    /// <param name="cb">Run when moving to replay screen.</param>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public static bool RunCampaign(SMAnalysis.AnalyzedCampaign? campaign, Action? cb, 
        SharedInstanceMetadata metadata) {
        if (campaign == null) return false;
        var req = new InstanceRequest((req, __) => DefaultReturn(req), metadata, new CampaignRequest(campaign));

        if (SaveData.r.TutorialDone || campaign.Game.MiniTutorial == null) return req.Run();
        //Note: if you Restart within the mini-tutorial, it will send you to stage 1.
        // This is because Restart calls campaign.Request.Run().
        else return ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(campaign.Game.MiniTutorial,
            SceneRequest.Reason.START_ONE,
            //Prevents hangover information from previous campaign, will be overriden anyways
            req.SetupInstance,
            null, 
            () => ServiceLocator.Find<MiniTutorial>().RunMiniTutorial(() => req.Run()))) is {};
    }

    public static bool RunTutorial(IDanmakuGameDef game) {
        if (game.Tutorial == null) return false;
        return ServiceLocator.Find<ISceneIntermediary>().LoadScene(
            new SceneRequest(game.Tutorial, SceneRequest.Reason.START_ONE,
            () => GameManagement.NewInstance(InstanceMode.TUTORIAL, game.MakeFeatures(defaultDifficulty, null)))) is {};
    }

    /// <summary>
    /// Sent before an instance is restarted. InstanceRequested will also be called immediately afterwards.
    /// </summary>
    public static readonly Event<InstanceRequest> InstanceRestarted = new();
    /// <summary>
    /// Sent before an instance is run. Sent even if the instance was a replay.
    /// </summary>
    public static readonly Event<InstanceRequest> InstancedRequested = new();
    /// <summary>
    /// Sent once the stage is completed (during a campaign only), before the next stage is loaded.
    /// </summary>
    public static readonly Event<(string campaign, int stage)> StageCompleted =
        new();
    /// <summary>
    /// Sent once the instance is completed (during any mode), before executing the callback (which may eg. return to the main menu).
    /// Sent even if the instance was a replay. 
    /// </summary>
    public static readonly Event<(InstanceData data, InstanceRecord record)> InstanceCompleted = 
        new();
}
}
