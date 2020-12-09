using System;
using System.Collections.Generic;
using System.Linq;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku;
using DMK.DMath;
using DMK.Graphics.Backgrounds;
using DMK.Player;
using DMK.Scenes;
using DMK.Scriptables;
using DMK.Services;
using JetBrains.Annotations;
using ProtoBuf;
using DMK.SM;
using InstanceLowRequest = DMK.Core.DU<DMK.GameInstance.CampaignRequest, DMK.GameInstance.BossPracticeRequest, 
    DMK.GameInstance.PhaseChallengeRequest, DMK.GameInstance.StagePracticeRequest>;
using static DMK.Core.GameManagement;
using static DMK.Scenes.SceneIntermediary;
using InstanceLowRequestKey = DMK.Core.DU<string, ((string, int), int), ((((string, int), int), int), int), ((string, int), int)>;


namespace DMK.GameInstance {

public readonly struct BossPracticeRequest {
    public readonly SMAnalysis.AnalyzedBoss boss;
    public readonly SMAnalysis.Phase phase;
    public PhaseType PhaseType => phase.type;

    public BossPracticeRequest(SMAnalysis.AnalyzedBoss boss, SMAnalysis.Phase? phase = null) {
        this.boss = boss;
        //0th listed phase is phase index 1
        this.phase = phase ?? boss.phases[0];
    }

    public ((string, int), int) Key => (boss.Key, phase.index);
    public static BossPracticeRequest Reconstruct(((string, int), int) key) {
        var boss = SMAnalysis.AnalyzedBoss.Reconstruct(key.Item1);
        return new BossPracticeRequest(boss, boss.phases.First(p => p.index == key.Item2));
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
    public readonly PlayerTeam team;
    public readonly DifficultySettings difficulty;
    
    public SharedInstanceMetadata(PlayerTeam team, DifficultySettings difficulty) {
        this.team = team;
        this.difficulty = difficulty;
    }
    
    public SharedInstanceMetadata(Saveable saved) : this(new PlayerTeam(saved.team), saved.difficulty) { }

    public (string, string) Key => (team.Describe, difficulty.DescribeSafe());

    [Serializable]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct Saveable {
        public PlayerTeam.Saveable team { get; set; }
        public DifficultySettings difficulty { get; set; }

        public Saveable(SharedInstanceMetadata gm) {
            this.team = new PlayerTeam.Saveable(gm.team);
            this.difficulty = gm.difficulty;
        }
    }
}


public class InstanceRequest {
    private readonly List<Cancellable> gameTrackers = new List<Cancellable>();
    [CanBeNull] public readonly Func<bool> cb;
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
    public InstanceRequest(Func<bool> cb, InstanceLowRequest lowerRequest, 
        Replay replay) : this(cb, replay.metadata.Record.SharedInstanceMetadata, lowerRequest, replay) {}

    public InstanceRequest(Func<bool> cb, SharedInstanceMetadata metadata, CampaignRequest? campaign = null,
        BossPracticeRequest? boss = null, PhaseChallengeRequest? challenge = null, StagePracticeRequest? stage = null,
        Replay? replay = null) : 
        this(cb, metadata, InstanceLowRequest.FromNullable(
            campaign, boss, challenge, stage) ?? throw new Exception("No valid request type made of GameReq"), 
            replay) { }

    public InstanceRequest(Func<bool> cb, SharedInstanceMetadata metadata, InstanceLowRequest lowerRequest, Replay? replay) {
        this.metadata = metadata;
        this.cb = cb;
        this.replay = replay;
        this.lowerRequest = lowerRequest;
        this.seed = replay?.metadata.Record.Seed ?? new Random().Next();
    }

    public void SetupCampaign() {
        Log.Unity(
            $"Starting game with mode {Mode} on difficulty {metadata.difficulty.Describe()}.");
        GameManagement.NewCampaign(Mode, SaveData.r.GetHighScore(this), this);
        if (replay == null) Replayer.BeginRecording();
        else Replayer.BeginReplaying(replay.Value.frames);
    }

    public bool Finish() => cb?.Invoke() ?? true;
    public InstanceRecord MakeSaveGameRecord([CanBeNull] AyaPhoto[] photos = null, [CanBeNull] string ending = null) {
        var record = new InstanceRecord(this, GameManagement.instance, true) {
            Photos = photos ?? new AyaPhoto[0],
            Ending = ending
        };
        if (Saveable) SaveData.r.RecordGame(record);
        return record;
    }
    /// <summary>
    /// If the record is not already created, it will be created here.
    /// </summary>
    public bool FinishAndPostReplay([CanBeNull] InstanceRecord record = null) {
        if (Finish()) {
            record?.Update(GameManagement.instance);
            Replayer.End(record ?? MakeSaveGameRecord());
            return true;
        } else {
            if (record != null && Saveable) {
                Log.Unity($"Invalidating record with UUID {record.Uuid}", level: Log.Level.INFO);
                SaveData.r.InvalidateRecord(record.Uuid);
            }
            return false;
        }
    }
    
    public static InstanceLowRequestKey CampaignIdentifier(InstanceLowRequest lowerRequest) => lowerRequest.Resolve(
        c => new InstanceLowRequestKey(0, c.Key, default, default, default),
        b => new InstanceLowRequestKey(1, default, b.Key, default, default),
        c => new InstanceLowRequestKey(2, default, default, c.Key, default),
        s => new InstanceLowRequestKey(3, default, default, default, s.Key));

    private void WaitThenFinishAndPostReplay([CanBeNull] InstanceRecord record = null) => 
        SceneLocalCRU.Main.RunDroppableRIEnumerator(WaitingUtils.WaitFor(
            WaitBeforeReturn, Cancellable.Null, () => FinishAndPostReplay(record)));

    public bool Run() {
        Cancel();
        RNG.Seed(seed);
        replay?.metadata.ApplySettings();
        return lowerRequest.Resolve(
            SelectCampaign,
            SelectBoss,
            SelectChallenge,
            SelectStage);
    }

    public void Cancel() {
        foreach (var c in gameTrackers) c.Cancel();
        gameTrackers.Clear();
    }

    private ICancellee NewTracker() {
        var c = new Cancellable();
        gameTrackers.Add(c);
        return c;
    }

    private bool SelectCampaign(CampaignRequest c) {
        var tracker = NewTracker();
        bool _Finalize(string endingKey = null) {
            if (FinishAndPostReplay(MakeSaveGameRecord(null, endingKey))) {
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
                    (index == 0) ? SetupCampaign : (Action) null,
                    //Note: this load during onHalfway is for the express purpose of preventing load lag
                    () => StateMachineManager.FromText(s.stage.stateMachine),
                    () => DependencyInjection.Find<LevelController>()
                        .Request(new LevelController.LevelRunRequest(1, tracker.Guard(() => ExecuteStage(index + 1)),
                        LevelController.LevelRunMethod.CONTINUE, s.stage))));
            } else return ExecuteEndcard();
        }
        return ExecuteStage(0);
    }
    
    private bool SelectStage(StagePracticeRequest s) {
        var tracker = NewTracker();
        return SceneIntermediary.LoadScene(new SceneRequest(s.stage.stage.sceneConfig,
            SceneRequest.Reason.START_ONE,
            SetupCampaign,
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
            SetupCampaign,
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
            SetupCampaign,
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
    


    private static SceneConfig MaybeSaveReplayScene => 
        (References.replaySaveMenu != null && (Replayer.IsRecording || Replayer.PostedReplay != null)) ?
        References.replaySaveMenu : References.mainMenu;

    public const float WaitBeforeReturn = 2f;

    public static bool DefaultReturn() => LoadScene(new SceneRequest(MaybeSaveReplayScene,
        SceneRequest.Reason.FINISH_RETURN,
        () => BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground));
    
    public static bool ShowPracticeSuccessMenu() {
        if (SceneIntermediary.LOADING) return false;
        EngineStateManager.SendSuccessEvent();
        return true;
    }
    public static bool ViewReplay(Replay r) {
        return new InstanceRequest(DefaultReturn, r.metadata.Record.ReconstructedRequest, r).Run();
    }

    public static bool ViewReplay(Replay? r) => r != null && ViewReplay(r.Value);


    public static bool RunCampaign([CanBeNull] SMAnalysis.AnalyzedCampaign campaign, [CanBeNull] Action cb, 
        SharedInstanceMetadata metadata) {
        if (campaign == null) return false;
        var req = new InstanceRequest(() => LoadScene(new SceneRequest(MaybeSaveReplayScene, 
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
            req.SetupCampaign,
            null, 
            () => MiniTutorial.RunMiniTutorial(() => req.Run())));
    }

    public static bool RunTutorial() => 
        SceneIntermediary.LoadScene(new SceneRequest(References.tutorial, SceneRequest.Reason.START_ONE, 
            () => GameManagement.NewCampaign(InstanceMode.TUTORIAL, null)));


}
}
