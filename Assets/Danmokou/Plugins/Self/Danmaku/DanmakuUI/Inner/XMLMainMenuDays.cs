using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using Danmaku.DanmakuUI;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using static GameManagement;
using static SM.SMAnalysis;
using static XMLUtils;
using static Danmaku.Enums;

/// <summary>
/// Class to manage the main menu UI for scene challenge-type games.
/// </summary>
[Preserve]
public class XMLMainMenuDays : XMLMenu {
    [CanBeNull] private static List<int> _returnTo;
    protected override List<int> ReturnTo {
        [CanBeNull] get => _returnTo;
        set => _returnTo = value;
    }

    private UIScreen SceneSelectScreen;
    private UIScreen ReplayScreen;

    protected override IEnumerable<UIScreen> Screens => new[] { SceneSelectScreen, ReplayScreen, MainScreen };

    public VisualTreeAsset GenericUIScreen;
    public VisualTreeAsset GenericUINode;
    public VisualTreeAsset MainScreenV;
    public VisualTreeAsset ReplayScreenV;
    public VisualTreeAsset LROptionNode;
    public VisualTreeAsset VTASceneSelect;
    public VisualTreeAsset VTALR2OptionNode;
    public VisualTreeAsset VTALR2Option;
    public float photoSize;

    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), GenericUIScreen},
        {typeof(UINode), GenericUINode},
    };

    private const string smallDescrClass = "small";
    private const string medDescrClass = "node100";
    private const string shotDescrClass = "descriptor";
    private const string completed1Class = "lblue";
    private const string completedAllClass = "lgreen";
    private static UINode[] DifficultyNodes(Func<FixedDifficulty, UINode> map) =>
        GameManagement.VisibleDifficulties.Select(map).ToArray();

    private static UINode[] DifficultyFuncNodes(Func<FixedDifficulty, Action> map) =>
        DifficultyNodes(d => new FuncNode(map(d), d.Describe()));
    protected override void Awake() {
        if (!Application.isPlaying) return;
        /*
        UINode[] DifficultyThenShot(Action<DifficultySet, ShotConfig> cb) {
            if (GameManagement.References.shots.Length == 1) {
                return DifficultyFuncNodes(d => () => cb(d, GameManagement.References.shots[0]));
            }
            throw new Exception("Days-Campaign WIP: one shot only");
        }*/

        FixedDifficulty dfc = FixedDifficulty.Normal;
        var defaultPlayer = References.dayCampaign.players[0];
        var defaultShot = defaultPlayer.shots[0];

        SceneSelectScreen = new UIScreen(DayCampaign.days[0].bosses.SelectMany(
            b => b.phases.Select(p => {
                //TODO: this return is not safe if you change the difficulty.
                if (!p.Enabled(dfc)) return new UINode(() => p.Title(dfc)).With(medDescrClass).EnabledIf(false);
                Challenge c = p.challenges[0];
                void SetChallenge(int idx) {
                    c = p.challenges[idx];
                    var completion = SaveData.r.ChallengeCompletion(p, idx, dfc);
                    AyaPhotoBoard.ConstructPhotos(completion?.photos, photoSize);
                }
                (bool, UINode) Confirm() {
                    ConfirmCache();
                    new GameRequest(GameRequest.ShowPracticeSuccessMenu, new DifficultySettings(dfc), 
                        challenge: new PhaseChallengeRequest(p, c),
                        player: defaultPlayer, shot: defaultShot).Run();
                    return (true, null);
                }
                return new CacheNavigateUINode(TentativeCache, () => p.Title(dfc), 
                    new UINode(() => c.Description(p.boss.boss)).SetConfirmOverride(Confirm),
                    new DynamicOptionNodeLR2<int>("", VTALR2Option, SetChallenge, 
                        p.challenges.Length.Range().ToArray, (i, v, on) => {
                            v.Query(null, "bracket").ForEach(x => x.style.display = on ? DisplayStyle.Flex : DisplayStyle.None);
                            v.Q("Star").style.unityBackgroundImageTintColor = new StyleColor(p.Completed(i, dfc) ?
                                p.boss.boss.colors.uiHPColor :
                                new Color(1, 1, 1, 0.52f));
                        }).With(VTALR2OptionNode).With(optionNoKeyClass)
                            .SetConfirmOverride(Confirm)
                            .SetOnVisit(_ => SetChallenge(0))
                            .SetOnLeave(_ => AyaPhotoBoard.TearDownAndHide()),
                    new UINode(() => "Press Z to start level".Locale("Zキー押すとレベルスタート")).SetConfirmOverride(Confirm)
                ).With(medDescrClass).With(
                    p.CompletedAll(dfc) ? completedAllClass :
                    p.CompletedOne(dfc) ? completed1Class :
                    null
                );
            })).ToArray()).With(VTASceneSelect);
        
        ReplayScreen = XMLUtils.ReplayScreen(false, TentativeCache, ConfirmCache).With(ReplayScreenV);

        MainScreen = new UIScreen(
            new TransferNode(SceneSelectScreen, "Game Start"),
            new OptionNodeLR<Locale>("Language", l => SaveData.s.Locale = l, new[] {
                ("English", Locale.EN),
                ("日本語", Locale.JP)
            }, SaveData.s.Locale).With(LROptionNode),
            new TransferNode(ReplayScreen, "Replays").EnabledIf(SaveData.p.ReplayData.Count > 0),
            //new FuncNode(RunTutorial, "Tutorial"),
            new FuncNode(Application.Quit, "Quit"),
            new OpenUrlNode("https://twitter.com/rdbatz", "Twitter (Browser)")
            ).With(MainScreenV);
        base.Awake();
    }
}