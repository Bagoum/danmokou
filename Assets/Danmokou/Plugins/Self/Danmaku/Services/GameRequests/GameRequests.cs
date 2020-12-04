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
using GameLowRequestKey = DU<string, ((string, int), int), ((((string, int), int), int), int), ((string, int), int)>;

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
    
    public GameMetadata(PlayerTeam team, DifficultySettings difficulty) {
        this.team = team;
        this.difficulty = difficulty;
    }
    
    public GameMetadata(Saveable saved) : this(new PlayerTeam(saved.team), saved.difficulty) { }

    public (string, string) Key => (team.Describe, difficulty.DescribeSafe());

    [Serializable]
    public struct Saveable {
        public PlayerTeam.Saveable team { get; set; }
        public DifficultySettings difficulty { get; set; }

        public Saveable(GameMetadata gm) {
            this.team = new PlayerTeam.Saveable(gm.team);
            this.difficulty = gm.difficulty;
        }
    }
}
public readonly struct GameRequest {
    [CanBeNull] public readonly Func<bool> cb;
    public readonly bool newCampaign;
    public readonly GameMetadata metadata;
    public readonly Replay? replay;
    public bool Saveable => replay == null;
    public readonly GameLowRequest lowerRequest;
    public readonly int seed;
    public CampaignMode Mode => lowerRequest.Resolve(
        _ => CampaignMode.MAIN,
        _ => CampaignMode.CARD_PRACTICE,
        _ => CampaignMode.SCENE_CHALLENGE,
        _ => CampaignMode.STAGE_PRACTICE);
    public GameRequest(Func<bool> cb, GameLowRequest lowerRequest, 
        Replay replay) : this(cb, replay.metadata.Record.GameMetadata, lowerRequest, true, replay) {}

    public GameRequest(Func<bool> cb, GameMetadata metadata, CampaignRequest? campaign = null,
        BossPracticeRequest? boss = null, PhaseChallengeRequest? challenge = null, StagePracticeRequest? stage = null,
        bool newCampaign = true, Replay? replay = null) : 
        this(cb, metadata, GameLowRequest.FromNullable(
            campaign, boss, challenge, stage) ?? throw new Exception("No valid request type made of GameReq"), 
            newCampaign, replay) { }

    public GameRequest(Func<bool> cb, GameMetadata metadata, GameLowRequest lowerRequest, bool newCampaign, Replay? replay) {
        this.metadata = metadata;
        this.cb = cb;
        this.newCampaign = newCampaign;
        this.replay = replay;
        this.lowerRequest = lowerRequest;
        this.seed = replay?.metadata.Record.Seed ?? new System.Random().Next();
    }

    public void SetupIfNew() {
        if (newCampaign) {
            Log.Unity(
                $"Starting game with mode {Mode} on difficulty {metadata.difficulty.Describe()}.");
            GameManagement.NewCampaign(Mode, SaveData.r.GetHighScore(this), this);
            if (replay == null) Replayer.BeginRecording();
            else Replayer.BeginReplaying(replay.Value.frames);
        }
    }

    public bool FinishAndPostReplay() {
        if (cb?.Invoke() ?? true) {
            var record = new GameRecord(this, GameManagement.campaign, true);
            if (Saveable) SaveData.r.RecordGame(record);
            Replayer.End(record);
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
        if (LastGame.Try(out var game)) {
            if (game.Run()) {
                if (game.Mode.PreserveReloadAudio()) AudioTrackService.PreserveBGM();
                return true;
            } else return false;
        } else return null;
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
                    () => DependencyInjection.Find<LevelController>()
                        .Request(new LevelController.LevelRunRequest(1, () => Finalize(),
                            LevelController.LevelRunMethod.CONTINUE, new EndcardStageConfig(ed.dialogueKey)))
                    ));
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
                    (index == 0) ? req.SetupIfNew : (Action) null,
                    //Note: this load during onHalfway is for the express purpose of preventing load lag
                    () => StateMachineManager.FromText(s.stage.stateMachine),
                    () => DependencyInjection.Find<LevelController>()
                        .Request(new LevelController.LevelRunRequest(1, () => ExecuteStage(index + 1),
                        LevelController.LevelRunMethod.CONTINUE, s.stage))));
            } else return ExecuteEndcard();
        }
        return ExecuteStage(0);
    }
    
    private static bool SelectStage(StagePracticeRequest s, GameRequest req) =>
        SceneIntermediary.LoadScene(new SceneRequest(s.stage.stage.sceneConfig,
            SceneRequest.Reason.START_ONE,
            req.SetupIfNew,
            //Note: this load during onHalfway is for the express purpose of preventing load lag
            () => StateMachineManager.FromText(s.stage.stage.stateMachine),
            () => DependencyInjection.Find<LevelController>().Request(
                new LevelController.LevelRunRequest(s.phase, req.vFinishAndPostReplay, s.method, s.stage.stage))));
    

    private static bool SelectBoss(BossPracticeRequest ab, GameRequest req) {
        var b = ab.boss.boss;
        BackgroundOrchestrator.NextSceneStartupBGC = b.Background(ab.PhaseType);
        return SceneIntermediary.LoadScene(new SceneRequest(References.unitScene,
            SceneRequest.Reason.START_ONE,
            req.SetupIfNew,
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
            req.SetupIfNew,
            () => DependencyInjection.Find<IChallengeManager>().TrackChallenge(new SceneChallengeReqest(req, cr)),
            () => {
                var beh = UnityEngine.Object.Instantiate(cr.Boss.boss).GetComponent<BehaviorEntity>();
                DependencyInjection.Find<IChallengeManager>().LinkBoss(beh);
            }));
    }
    


    private static SceneConfig MaybeSaveReplayScene => 
        (References.replaySaveMenu != null && (Replayer.IsRecording || Replayer.PostedReplay != null)) ?
        References.replaySaveMenu : References.mainMenu;

    public static bool WaitDefaultReturn() {
        if (SceneIntermediary.LOADING) return false;
        SceneLocalCRU.Main.RunDroppableRIEnumerator(WaitingUtils.WaitFor(1f, Cancellable.Null, () =>
            LoadScene(new SceneRequest(MaybeSaveReplayScene,
                SceneRequest.Reason.FINISH_RETURN, 
                () => BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground))));
        return true;
    }

    public static bool ShowPracticeSuccessMenu() {
        GameStateManager.SendSuccessEvent();
        return true;
    }
    public static bool WaitShowPracticeSuccessMenu() {
        if (SceneIntermediary.LOADING) return false;
        SceneLocalCRU.Main.RunDroppableRIEnumerator(WaitingUtils.WaitFor(1f, Cancellable.Null, GameStateManager.SendSuccessEvent));
        return true;
    }
    public static bool ViewReplay(Replay r) {
        return new GameRequest(WaitDefaultReturn, r.metadata.Record.ReconstructedRequest, r).Run();
    }

    public static bool ViewReplay(Replay? r) => r != null && ViewReplay(r.Value);


    public static bool RunCampaign([CanBeNull] AnalyzedCampaign campaign, [CanBeNull] Action cb, 
        GameMetadata metadata) {
        if (campaign == null) return false;
        var req = new GameRequest(() => LoadScene(new SceneRequest(MaybeSaveReplayScene, 
            SceneRequest.Reason.FINISH_RETURN, () => {
                cb?.Invoke();
                BackgroundOrchestrator.NextSceneStartupBGC = References.defaultMenuBackground;
            })), metadata, campaign: new CampaignRequest(campaign));


        if (SaveData.r.TutorialDone || References.miniTutorial == null) return req.Run();
        else return LoadScene(new SceneRequest(References.miniTutorial,
            SceneRequest.Reason.START_ONE,
            //Prevents hangover information from previous campaign, will be overriden anyways
            req.SetupIfNew,
            null, 
            () => MiniTutorial.RunMiniTutorial(req.vRun)));
    }

    public static bool RunTutorial() => 
        SceneIntermediary.LoadScene(new SceneRequest(References.tutorial, SceneRequest.Reason.START_ONE, 
            () => GameManagement.NewCampaign(CampaignMode.TUTORIAL, null)));


}
}
