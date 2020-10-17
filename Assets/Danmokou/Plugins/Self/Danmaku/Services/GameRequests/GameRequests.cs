using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using static SM.SMAnalysis;
using GameLowRequest = DU<Danmaku.CampaignRequest, Danmaku.BossPracticeRequest, 
    PhaseChallengeRequest, Danmaku.StagePracticeRequest>;
using static GameManagement;
using static SceneIntermediary;
using static StaticNullableStruct;
using static Danmaku.Enums;
using GameLowRequestKey = DU<string, ((string, int), int), ((((string, string), int), int), int), ((string, int), int)>;

//https://issuetracker.unity3d.com/issues/build-that-contains-a-script-with-a-struct-which-has-a-static-nullable-reference-to-itself-fails-on-il2cpp
public static class StaticNullableStruct {
    public static Danmaku.GameRequest? LastGame { get; set; } = null;
}

namespace Danmaku {

public readonly struct BossPracticeRequest {
    public readonly AnalyzedBoss boss;
    public readonly Phase phase;
    public PhaseType PhaseType => phase.type;

    public BossPracticeRequest(AnalyzedBoss boss, Phase? phase = null) {
        this.boss = boss;
        //0th listed phase is phase index 1
        this.phase = phase ?? boss.phases[0];
    }

    public ((string, int), int) Key => (boss.Key, phase.index);
    public static BossPracticeRequest Reconstruct(((string, int), int) key) {
        var boss = AnalyzedBoss.Reconstruct(key.Item1);
        return new BossPracticeRequest(boss, boss.phases.First(p => p.index == key.Item2));
    }
}

public readonly struct StagePracticeRequest {
    public readonly AnalyzedStage stage;
    public readonly int phase;
    public readonly LevelController.LevelRunMethod method;

    public StagePracticeRequest(AnalyzedStage stage, int phase, LevelController.LevelRunMethod method = LevelController.LevelRunMethod.CONTINUE) {
        this.stage = stage;
        this.phase = phase;
        this.method = method;
    }
    
    public ((string, int), int) Key => (stage.Key, phase);
    public static StagePracticeRequest Reconstruct(((string, int), int) key) =>
        new StagePracticeRequest(AnalyzedStage.Reconstruct(key.Item1), key.Item2);
}

public readonly struct CampaignRequest {
    public readonly AnalyzedCampaign campaign;

    public CampaignRequest(AnalyzedCampaign campaign) {
        this.campaign = campaign;
    }
    
    public string Key => campaign.Key;
    public static CampaignRequest Reconstruct(string key) =>
        new CampaignRequest(AnalyzedCampaign.Reconstruct(key));
    
}

public readonly struct GameMetadata {
    public readonly PlayerTeam team;
    public readonly DifficultySettings difficulty;
    public readonly CampaignMode mode;
    
    public GameMetadata(PlayerTeam team, DifficultySettings difficulty, CampaignMode mode) {
        this.team = team;
        this.difficulty = difficulty;
        this.mode = mode;
    }

    public (string, string, CampaignMode) Key => (team.Describe, difficulty.DescribeSafe, mode);
}
public readonly struct GameRequest {
    [CanBeNull] public readonly Func<bool> cb;
    public readonly bool newCampaign;
    public readonly GameMetadata metadata;
    public readonly Replay? replay;
    public bool Saveable => replay == null;
    public readonly GameLowRequest lowerRequest;
    public readonly int seed;

    public GameRequest(Func<bool> cb, GameLowRequest lowerRequest, 
        Replay replay) : this(cb, replay.metadata.Difficulty, lowerRequest, true, 
        replay.metadata.Player, replay) {}

    public GameRequest(Func<bool> cb, DifficultySettings difficulty, CampaignRequest? campaign = null,
        BossPracticeRequest? boss = null, PhaseChallengeRequest? challenge = null, StagePracticeRequest? stage = null,
        bool newCampaign = true, PlayerTeam? player = null, Replay? replay = null) : 
        this(cb, difficulty,  GameLowRequest.FromNullable(
            campaign, boss, challenge, stage) ?? throw new Exception("No valid request type made of GameReq"), 
            newCampaign, player ?? PlayerTeam.Empty, replay) { }

    public GameRequest(Func<bool> cb, DifficultySettings difficulty, GameLowRequest lowerRequest,
        bool newCampaign, PlayerTeam team, Replay? replay) {
        this.metadata = new GameMetadata(team, difficulty, lowerRequest.Resolve(
            _ => CampaignMode.MAIN, 
            _ => CampaignMode.CARD_PRACTICE, 
            _ => CampaignMode.SCENE_CHALLENGE, 
            _ => CampaignMode.STAGE_PRACTICE));
        this.cb = cb;
        this.newCampaign = newCampaign;
        this.replay = replay;
        this.lowerRequest = lowerRequest;
        this.seed = replay?.metadata.Seed ?? new System.Random().Next();
    }

    public void SetupOrCheckpoint() {
        if (newCampaign) {
            Log.Unity(
                $"Starting game with mode {metadata.mode} on difficulty {metadata.difficulty.Describe}.");
            GameManagement.Difficulty = metadata.difficulty;
            GameManagement.NewCampaign(metadata.mode, SaveData.r.GetHighScore(Identifier), this);
            if (replay == null) Replayer.BeginRecording();
            else Replayer.BeginReplaying(replay.Value.frames);
        } else Checkpoint();
    }

    private void Checkpoint() {
        GameManagement.CheckpointCampaignData();
    }

    public bool FinishAndPostReplay() {
        if (cb?.Invoke() ?? true) {
            if (Saveable) GameManagement.campaign.SaveCampaign(Identifier);
            Replayer.End(this);
            return true;
        } else return false;
    }

    public string Identifier => GameIdentifer(metadata, lowerRequest);

    public static string GameIdentifer(GameMetadata metadata, GameLowRequest lowerRequest) =>
        $"{metadata.Key}-{CampaignIdentifier(lowerRequest).Tuple}";
    public static GameLowRequestKey CampaignIdentifier(GameLowRequest lowerRequest) => lowerRequest.Resolve(
        c => new GameLowRequestKey(0, c.Key, default, default, default),
        b => new GameLowRequestKey(1, default, b.Key, default, default),
        c => new GameLowRequestKey(2, default, default, c.Key, default),
        s => new GameLowRequestKey(3, default, default, default, s.Key));
    
    public void vFinishAndPostReplay() => FinishAndPostReplay();

    public bool Run() {
        var r = this;
        LastGame = r;
        RNG.Seed(seed);
        replay?.metadata.ApplySettings();
        return lowerRequest.Resolve(
            c => SelectCampaign(c, r),
            b => SelectBoss(b, r),
            c => SelectChallenge(c, r),
            s => SelectStage(s, r));
    }

    public static bool? Rerun() {
        if (LastGame == null) return null;
        else if (LastGame.Value.Run()) {
            if (LastGame.Value.metadata.mode.PreserveReloadAudio()) AudioTrackService.PreserveBGM();
            return true;
        } else return false;
    }

    public void vRun() => Run();

    private static bool SelectCampaign(CampaignRequest c, GameRequest req) {
        bool Finalize() {
            if (req.FinishAndPostReplay()) {
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
                    () => SaveData.r.CompleteCampaign(c.campaign.campaign.key, ed.key),
                    null,
                    () => {
                        LevelController.Request(new LevelController.LevelRunRequest(1, () => Finalize(),
                            LevelController.LevelRunMethod.CONTINUE, new EndcardStageConfig(ed.dialogueKey)));
                    }));
            } else if (Finalize()) {
                SaveData.r.CompleteCampaign(c.campaign.campaign.key, null);
                return true;
            } else return false;
        }
        bool ExecuteStage(int index) {
            if (index < c.campaign.stages.Length) {
                var s = c.campaign.stages[index];
                return SceneIntermediary.LoadScene(new SceneRequest(s.stage.sceneConfig,
                    SceneRequest.Reason.RUN_SEQUENCE,
                    (index == 0) ? req.SetupOrCheckpoint : (Action) req.Checkpoint,
                    //Note: this load during onHalfway is for the express purpose of preventing load lag
                    () => StateMachineManager.FromText(s.stage.stateMachine),
                    () => LevelController.Request(new LevelController.LevelRunRequest(1, () => ExecuteStage(index + 1),
                        LevelController.LevelRunMethod.CONTINUE, s.stage))));
            } else return ExecuteEndcard();
        }
        return ExecuteStage(0);
    }
    
    private static bool SelectStage(StagePracticeRequest s, GameRequest req) =>
        SceneIntermediary.LoadScene(new SceneRequest(s.stage.stage.sceneConfig,
            SceneRequest.Reason.START_ONE,
            req.SetupOrCheckpoint,
            //Note: this load during onHalfway is for the express purpose of preventing load lag
            () => StateMachineManager.FromText(s.stage.stage.stateMachine),
            () => LevelController.Request(
                new LevelController.LevelRunRequest(s.phase, req.vFinishAndPostReplay, s.method, s.stage.stage))));
    

    private static bool SelectBoss(BossPracticeRequest ab, GameRequest req) {
        var b = ab.boss.boss;
        BackgroundOrchestrator.NextSceneStartupBGC = b.Background(ab.PhaseType);
        return SceneIntermediary.LoadScene(new SceneRequest(References.unitScene,
            SceneRequest.Reason.START_ONE,
            req.SetupOrCheckpoint,
            //Note: this load during onHalfway is for the express purpose of preventing load lag
            () => StateMachineManager.FromText(b.stateMachine),
            () => {
                var beh = UnityEngine.Object.Instantiate(b.boss).GetComponent<BehaviorEntity>();
                beh.behaviorScript = b.stateMachine;
                beh.phaseController.Override(ab.phase.index, req.vFinishAndPostReplay);
                beh.RunAttachedSM();
            }));
    }

    private static bool SelectChallenge(PhaseChallengeRequest cr, GameRequest req) {
        BackgroundOrchestrator.NextSceneStartupBGC = cr.Boss.Background(cr.phase.phase.type);
        return SceneIntermediary.LoadScene(new SceneRequest(References.unitScene,
            SceneRequest.Reason.START_ONE,
            req.SetupOrCheckpoint,
            () => ChallengeManager.TrackChallenge(new SceneChallengeReqest(req, cr)),
            () => {
                var beh = UnityEngine.Object.Instantiate(cr.Boss.boss).GetComponent<BehaviorEntity>();
                ChallengeManager.LinkBEH(beh);
            }));
    }
    


    private static SceneConfig MaybeSaveReplayScene => 
        (References.replaySaveMenu != null && (Replayer.IsRecording || Replayer.PostedReplay != null)) ?
        References.replaySaveMenu : References.mainMenu;

    public static bool WaitDefaultReturn() {
        if (SceneIntermediary.LOADING) return false;
        GlobalSceneCRU.Main.RunDroppableRIEnumerator(WaitingUtils.WaitFor(1f, Cancellable.Null, () =>
            LoadScene(new SceneRequest(MaybeSaveReplayScene,
                SceneRequest.Reason.FINISH_RETURN))));
        return true;
    }

    public static bool DefaultReturn() => SceneIntermediary.LoadScene(
        new SceneRequest(MaybeSaveReplayScene, SceneRequest.Reason.FINISH_RETURN)
    );
    
    public static bool ShowPracticeSuccessMenu() {
        GameStateManager.SendSuccessEvent();
        return true;
    }
    public static bool WaitShowPracticeSuccessMenu() {
        if (SceneIntermediary.LOADING) return false;
        GlobalSceneCRU.Main.RunDroppableRIEnumerator(WaitingUtils.WaitFor(1f, Cancellable.Null, GameStateManager.SendSuccessEvent));
        return true;
    }
    public static bool ViewReplay(Replay r) {
        return new GameRequest(WaitDefaultReturn, r.metadata.ReconstructedRequest, r).Run();
    }

    public static bool ViewReplay(Replay? r) => r != null && ViewReplay(r.Value);


    public static bool RunCampaign([CanBeNull] AnalyzedCampaign campaign, [CanBeNull] Action cb, 
        DifficultySettings difficulty, PlayerTeam player) {
        if (campaign == null) return false;
        var req = new GameRequest(() => LoadScene(new SceneRequest(MaybeSaveReplayScene, 
            SceneRequest.Reason.FINISH_RETURN, () => {
                cb?.Invoke();
                GameManagement.CheckpointCampaignData();
            })), 
            difficulty, campaign: new CampaignRequest(campaign), player: player);


        if (SaveData.r.TutorialDone || References.miniTutorial == null) return req.Run();
        else return LoadScene(new SceneRequest(References.miniTutorial,
            SceneRequest.Reason.START_ONE,
            //Prevents hangover information from previous campaign, will be overriden anyways
            req.SetupOrCheckpoint,
            null, 
            () => MiniTutorial.RunMiniTutorial(req.vRun)));
    }

    public static bool RunTutorial() => 
        SceneIntermediary.LoadScene(new SceneRequest(References.tutorial, SceneRequest.Reason.START_ONE, 
            () => GameManagement.NewCampaign(CampaignMode.TUTORIAL, null)));


}
}
