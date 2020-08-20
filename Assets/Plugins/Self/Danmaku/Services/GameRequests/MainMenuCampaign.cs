using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using UnityEngine.SceneManagement;
using static SM.SMAnalysis;

namespace Danmaku {
public class MainMenuCampaign: MainMenu {

    public SceneConfig returnTo;
    public SceneConfig bossPractice;
    public SceneConfig tutorial;
    public CampaignConfig campaign;
    public CampaignConfig extraCampaign;
    public static IEnumerable<CampaignConfig> Campaigns => new[] {main.campaign, main.extraCampaign}.Where(c => c != null);
    public static IEnumerable<CampaignConfig> FinishedCampaigns =>
        Campaigns.Where(c => SaveData.r.CompletedCampaigns.Contains(c.key));
    public ShotConfig[] shotOptions;

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
    public static MainMenuCampaign main { get; private set; }

    private void Awake() {
        main = this;
        if (campaign == null) Log.UnityError("You do not have a Campaign set in the Main Menu object. The Main Scenario option will not work.");
    }

    public static Func<bool> DefaultReturn => () => SceneIntermediary.LoadScene(main.returnTo, delay: 1f);

    private static bool PlaySequential(GameReq req, StageConfig[] stages, int startFrom=0) {
        if (startFrom >= stages.Length) {
            if (req.cb?.Invoke() ?? true) {
                Log.Unity("Stage sequence finished.");
                return true;
            } else return false;
        } else return req.WithCB(() => PlaySequential(req.NotNew(), stages, startFrom + 1))
                        .SelectStageContinue(stages[startFrom]);
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

    public static bool RunTutorial() => 
        SceneIntermediary.LoadScene(main.tutorial, () => GameManagement.NewCampaign(CampaignMode.TUTORIAL, null));


    public static bool SelectBossSinglePhase(BossConfig b, GameReq req, Enums.PhaseType pt) =>
        GameReq.SelectBossSinglePhase(main.bossPractice, b, req, pt);
    
    public static bool SelectBossContinue(BossConfig b, GameReq req, Enums.PhaseType pt) =>
        GameReq.SelectBossContinue(main.bossPractice, b, req, pt);
}
}