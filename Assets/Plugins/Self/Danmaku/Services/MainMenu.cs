using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Danmaku {
public class MainMenu: MonoBehaviour {
    public readonly struct GameReq {
        [CanBeNull] public readonly Func<bool> cb;
        public readonly int toPhase;
        public readonly bool newCampaign;
        [CanBeNull] public readonly ShotConfig shot;
        public readonly DifficultySet? difficulty;
        public readonly CampaignMode mode;

        public GameReq(CampaignMode mode, Func<bool> cb, DifficultySet? difficulty = DifficultySet.Abex, 
            bool newCampaign=true, int toPhase = 1, ShotConfig shot = null) {
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
    }
    public readonly struct AnalyzedStage {
        public readonly StageConfig stage;
        public readonly List<SMAnalysis.Phase> phases;
        public AnalyzedStage(StageConfig s) {
            stage = s;
            phases = SMAnalysis.Analyze(StateMachineManager.GetSMFromTextAsset(s.stateMachine) as PatternSM);
        }
    }
    public readonly struct AnalyzedBoss {
        public readonly BossConfig boss;
        public readonly List<SMAnalysis.Phase> phases;

        public AnalyzedBoss(BossConfig sb) {
            boss = sb;
            phases = SMAnalysis.Analyze(StateMachineManager.GetSMFromTextAsset(sb.stateMachine) as PatternSM);
        }
    }

    public SceneConfig returnTo;
    public SceneConfig bossPractice;
    public SceneConfig tutorial;
    public CampaignConfig campaign;
    public CampaignConfig extraCampaign;
    public static IEnumerable<CampaignConfig> Campaigns => new[] {main.campaign, main.extraCampaign}.Where(c => c != null);
    public static IEnumerable<CampaignConfig> FinishedCampaigns =>
        Campaigns.Where(c => SaveData.r.CompletedCampaigns.Contains(c.key));
    public ShotConfig[] shotOptions;
    [CanBeNull] public AudioTrack bgm;

    [CanBeNull] private static AnalyzedBoss[] _bosses;
    [CanBeNull] private static AnalyzedStage[] _stages;
    public static AnalyzedBoss[] Bosses => _bosses = 
        _bosses == null || _bosses.Length != FinishedCampaigns.SelectMany(c => c.practiceBosses).Count() ?
        FinishedCampaigns.SelectMany(c => c.practiceBosses.Select(x => new AnalyzedBoss(x))).ToArray() 
        : _bosses;
    public static AnalyzedStage[] Stages => _stages =
        _stages == null || _stages.Length != FinishedCampaigns.SelectMany(c => c.practiceStages).Count() ?
        FinishedCampaigns.SelectMany(c => c.practiceStages.Select(x => new AnalyzedStage(x))).ToArray()
        : _stages;
    public static MainMenu main { get; private set; }

    private void Awake() {
        main = this;
        if (campaign == null) Log.UnityError("You do not have a Campaign set in the Main Menu object. The Main Scenario option will not work.");
    }

    private void Start() {
        AudioTrackService.InvokeBGM(bgm);
    }

    public static Func<bool> DefaultReturn => () => SceneIntermediary.LoadScene(main.returnTo, delay: 1f);

    private static bool PlaySequential(GameReq req, StageConfig[] stages, int startFrom=0) {
        if (startFrom >= stages.Length) {
            if (req.cb?.Invoke() ?? true) {
                Log.Unity("Stage sequence finished.");
                return true;
            } else return false;
        } else return SelectStageContinue(stages[startFrom],
                req.WithCB(() => PlaySequential(req.NotNew(), stages, startFrom + 1)));
    }

    public SceneConfig miniTutorial;

    public static void MainScenario(GameReq req) => RunCampaign(main.campaign, req);

    public static void ExtraScenario(GameReq req) => RunCampaign(main.extraCampaign, req);

    public static void RunCampaign(CampaignConfig campaign, GameReq req) {
        void Inner() => PlaySequential(req.WithCB(req.cb.Then(() => SceneIntermediary.LoadScene(main.returnTo, () => {
                GameManagement.CheckpointCampaignData();
                SaveData.r.CompletedCampaigns.Add(campaign.key);
                SaveData.SaveRecord();
            }))), campaign.stages);

        if (SaveData.r.TutorialDone) Inner();
        else SceneIntermediary.LoadScene(new SceneIntermediary.SceneRequest(main.miniTutorial,
            //Prevents hangover information from previous campaign, will be overriden anyways
            req.SetupOrCheckpoint,
            null, 
            () => MiniTutorial.RunMiniTutorial(Inner)));
    }

    private static bool SelectBoss(BossConfig b, Action<BehaviorEntity> cont, GameReq req) =>
        SceneIntermediary.LoadScene(new SceneIntermediary.SceneRequest(main.bossPractice,
            req.SetupOrCheckpoint,
            //Note: this load during onHalfway is for the express purpose of preventing load lag
            () => StateMachineManager.GetSMFromTextAsset(b.stateMachine), 
            () => {
                var beh = Instantiate(b.boss).GetComponent<BehaviorEntity>();
                beh.ExternalSetLocalPosition(new Vector2(2, 10));
                beh.behaviorScript = b.stateMachine;
                cont(beh);
            }));

    public static bool RunTutorial() => 
        SceneIntermediary.LoadScene(main.tutorial, () => GameManagement.NewCampaign(CampaignMode.TUTORIAL, null));
    
    public static StageConfig CurrentStage { get; private set; }
    
    private static bool _SelectStage(StageConfig s, LevelController.LevelRunMethod method, GameReq req) => 
        SceneIntermediary.LoadScene(new SceneIntermediary.SceneRequest(s.sceneConfig,
            req.SetupOrCheckpoint,
            () => {
                CurrentStage = s;
                //Note: this load during onHalfway is for the express purpose of preventing load lag
                StateMachineManager.GetSMFromTextAsset(s.stateMachine);
                
            }, 
            () => req.RequestLevel(s, method)));

    public static bool SelectStageSinglePhase(StageConfig s, GameReq req) =>
        _SelectStage(s, LevelController.LevelRunMethod.SINGLE, req);
    
    public static bool SelectStageContinue(StageConfig s, GameReq req) =>
        _SelectStage(s, LevelController.LevelRunMethod.CONTINUE, req);


    public static bool SelectBossSinglePhase(BossConfig b, GameReq req) =>
        SelectBoss(b, beh => beh.phaseController.Override(req.toPhase, req.cb.Void()), req);
    
    public static bool SelectBossContinue(BossConfig b, GameReq req) =>
        SelectBoss(b, beh => beh.phaseController.SetGoTo(req.toPhase, req.cb.Void()), req);
}
}