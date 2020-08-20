using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using static SM.SMAnalysis;

namespace Danmaku {
public class MainMenuDays : MainMenu {
    public SceneConfig returnTo;
    public SceneConfig dayScene;
    public SceneConfig tutorial;
    public DayCampaignConfig campaign;
    public ShotConfig[] shotOptions;
    
    //no delay, that's handled by the Challenge Success message
    public static Func<bool> DefaultReturn => () => SceneIntermediary.LoadScene(
        new SceneIntermediary.SceneRequest(main.returnTo, null, SaveData.SaveRecord, null)); 
    public static MainMenuDays main { get; private set; }

    [CanBeNull] private static AnalyzedDays _days;
    public static AnalyzedDays Days => _days = _days ?? new AnalyzedDays(main.campaign.days);
    
    public static IEnumerable<DifficultySet> VisibleDifficulties => new[] {
        DifficultySet.Easier, DifficultySet.Easy, DifficultySet.Normal, DifficultySet.Hard,
        DifficultySet.Lunatic
    };

    private void Awake() {
        main = this;
    }
    
    public static bool SelectBossChallenge(GameReq req, Enums.PhaseType pt, ChallengeRequest cr) =>
        GameReq.SelectBossChallenge(main.dayScene, req, pt, cr);
}
}