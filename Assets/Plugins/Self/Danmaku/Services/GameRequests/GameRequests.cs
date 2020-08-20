using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using SM;
using UnityEngine;

namespace Danmaku {
public readonly struct GameReq {
    [CanBeNull] public readonly Func<bool> cb;
    public readonly int toPhase;
    public readonly bool newCampaign;
    [CanBeNull] public readonly ShotConfig shot;
    public readonly DifficultySet? difficulty;
    public readonly CampaignMode mode;

    public GameReq(CampaignMode mode, Func<bool> cb, DifficultySet? difficulty = DifficultySet.Abex,
        bool newCampaign = true, int toPhase = 1, ShotConfig shot = null) {
        this.mode = mode;
        this.cb = cb;
        this.newCampaign = newCampaign;
        this.toPhase = toPhase;
        this.shot = shot;
        this.difficulty = difficulty;
    }

    public GameReq WithCB([CanBeNull] Func<bool> newCB, int newPhase = 1) =>
        new GameReq(mode, newCB, difficulty, newCampaign, newPhase, shot);

    public GameReq NotNew() => new GameReq(mode, cb, difficulty, false, toPhase, shot);

    public void SetupOrCheckpoint() {
        if (newCampaign) {
            if (difficulty.HasValue) GameManagement.Difficulty = difficulty.Value;
            GameManagement.NewCampaign(mode, shot);
        } else GameManagement.CheckpointCampaignData();
    }

    public LevelController.LevelRunRequest LevelRequest(StageConfig s, LevelController.LevelRunMethod method) =>
        new LevelController.LevelRunRequest(toPhase, cb.Void(), method, s);

    public void RequestLevel(StageConfig s, LevelController.LevelRunMethod method) =>
        LevelController.Request(LevelRequest(s, method));


    private static bool _SelectStage(StageConfig s, LevelController.LevelRunMethod method, GameReq req) =>
        SceneIntermediary.LoadScene(new SceneIntermediary.SceneRequest(s.sceneConfig,
            req.SetupOrCheckpoint,
            //Note: this load during onHalfway is for the express purpose of preventing load lag
            () => StateMachineManager.FromText(s.stateMachine),
            () => req.RequestLevel(s, method)));

    public bool SelectStageSinglePhase(StageConfig s) => _SelectStage(s, LevelController.LevelRunMethod.SINGLE, this);

    public bool SelectStageContinue(StageConfig s) => _SelectStage(s, LevelController.LevelRunMethod.CONTINUE, this);


    private static bool SelectBoss(SceneConfig scene, BossConfig b, Action<BehaviorEntity> cont, GameReq req, Enums.PhaseType pt) {
        BackgroundOrchestrator.NextSceneStartupBGC = b.Background(pt);
        return SceneIntermediary.LoadScene(new SceneIntermediary.SceneRequest(scene,
            req.SetupOrCheckpoint,
            //Note: this load during onHalfway is for the express purpose of preventing load lag
            () => StateMachineManager.FromText(b.stateMachine),
            () => {
                var beh = UnityEngine.Object.Instantiate(b.boss).GetComponent<BehaviorEntity>();
                beh.behaviorScript = b.stateMachine;
                cont(beh);
            }));
    }

    public static bool SelectBossChallenge(SceneConfig scene, GameReq req, Enums.PhaseType pt, ChallengeRequest cr) {
        BackgroundOrchestrator.NextSceneStartupBGC = cr.Boss.Background(pt);
        return SceneIntermediary.LoadScene(new SceneIntermediary.SceneRequest(scene,
            req.SetupOrCheckpoint,
            () => ChallengeManager.TrackChallenge(cr.WithCB(() => req.cb?.Invoke())),
            () => {
                var beh = UnityEngine.Object.Instantiate(cr.Boss.boss).GetComponent<BehaviorEntity>();
                ChallengeManager.LinkBEH(beh);
            }));
        
        
    }
    

    public static bool SelectBossSinglePhase(SceneConfig scene, BossConfig b, GameReq req, Enums.PhaseType pt) =>
        SelectBoss(scene, b, beh => beh.phaseController.Override(req.toPhase, req.cb.Void()), req, pt);

    public static bool SelectBossContinue(SceneConfig scene, BossConfig b, GameReq req, Enums.PhaseType pt) =>
        SelectBoss(scene, b, beh => beh.phaseController.SetGoTo(req.toPhase, req.cb.Void()), req, pt);
    

}

public abstract class MainMenu : MonoBehaviour {
    [CanBeNull] public AudioTrack bgm;

    private void Start() {
        AudioTrackService.InvokeBGM(bgm);
    }
}
}
