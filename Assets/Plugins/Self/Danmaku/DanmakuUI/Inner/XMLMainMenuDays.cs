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
using Object = UnityEngine.Object;
using static GameManagement;
using static Danmaku.MainMenuDays;
using static SM.SMAnalysis;

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

    protected override UIScreen[] Screens => new[] { SceneSelectScreen, MainScreen };

    public VisualTreeAsset GenericUIScreen;
    public VisualTreeAsset GenericUINode;
    public VisualTreeAsset MainScreenV;
    public VisualTreeAsset LROptionNode;
    public VisualTreeAsset VTASceneSelect;
    public VisualTreeAsset VTALR2OptionNode;
    public VisualTreeAsset VTALR2Option;

    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), GenericUIScreen},
        {typeof(UINode), GenericUINode},
    };

    private const string smallDescrClass = "node80";
    private const string medDescrClass = "node100";
    private const string shotDescrClass = "descriptor";
    private const string completed1Class = "lblue";
    private const string completedAllClass = "lgreen";
    private static UINode[] DifficultyNodes(Func<DifficultySet, UINode> map) =>
        MainMenuDays.VisibleDifficulties.Select(map).ToArray();

    private static UINode[] DifficultyFuncNodes(Func<DifficultySet, Action> map) =>
        DifficultyNodes(d => new FuncNode(map(d), d.Describe()));
    protected override void Awake() {
        if (!Application.isPlaying) return;
        UINode[] DifficultyThenShot(Action<DifficultySet, ShotConfig> cb) {
            if (MainMenuCampaign.main.shotOptions.Length == 1) {
                return DifficultyFuncNodes(d => () => cb(d, MainMenuCampaign.main.shotOptions[0]));
            }
            throw new Exception("Days-Campaign WIP: one shot only");
        }

        DifficultySet dfc = DifficultySet.Normal;
        (DayPhase p, Challenge c)? current = null;
        (string, Challenge)[] CurrentChallenges2() {
            if (current == null) return new (string, Challenge)[0];
            return current.Value.p.challenges.Enumerate().Select(c => (c.idx.ToString(), c.val)).ToArray();
        }

        UINode detailParent = null;

        UINode FixedDetailRow(UINode x) => x
            .FixDepth(1)
            .SetAlwaysVisible()
            .SetDisplayParentOverride(() => detailParent)
            .SetBackOverride(() => detailParent)
            .SetConfirmOverride(() => {
                if (!current.HasValue) return (false, x);
                var (p, c) = current.Value;
                ConfirmCache();
                MainMenuDays.SelectBossChallenge(
                    new GameReq(CampaignMode.SCENE_CHALLENGE, DefaultReturn, dfc, toPhase: p.phase.index), p.phase.type,
                    new ChallengeRequest(p, c, dfc));
                return (true, null);
            })
        ;
        
        var descrNode = FixedDetailRow(new UINode(() =>
            current == null ? "None Selected" : $"{current?.c.Description(current?.p.boss.boss)}"))
            .SetLeftOverride(() => detailParent).With("node140");

        DelayOptionNodeLR2<int> challengeSelNode = FixedDetailRow(new DelayOptionNodeLR2<int>("", VTALR2Option, c => {
            if (current.HasValue) current = (current.Value.p, current.Value.p.challenges[c]);
        }, () => {
            if (current.HasValue) return current.Value.p.challenges.Length.Range().ToArray();
            return new int[0];
        }, (c, v, b) => {
            v.Query(null, "bracket").ForEach(x => x.style.display = b ? DisplayStyle.Flex : DisplayStyle.None);
            v.Q("Star").style.unityBackgroundImageTintColor = new StyleColor((current?.p?.Completed(c) ?? false) ?
                current.Value.p.boss.boss.colors.uiHPColor :
                new Color(1, 1, 1, 0.52f));
        })).With(VTALR2OptionNode).With("nokey") as DelayOptionNodeLR2<int> ?? throw new Exception("Startup error");

        UINode VisitDetailRow(UINode x) => x.SetRightOverride(() => {
            detailParent = x;
            return descrNode;
        });

        SceneSelectScreen = new LazyUIScreen(() => 
            //new UINode[] {
            Days.days[0].bosses.SelectMany(
            //new NavigateOptionNodeLR("", Days.days.Select(d => 
            //    (UINode)new Invisible1Node(d.day.dayTitle, d.bosses.SelectMany(
                b => b.phases.Select(p => {
                    var n = new CacheNavigateUINode(TentativeCache, () => p.Title).With(medDescrClass);
                    return p.Enabled ? 
                        VisitDetailRow(n.SetOnVisit(_ => {
                            if (current?.p != p) {
                                current = (p, p.challenges[0]);
                                challengeSelNode.ResetIndex();
                            }
                        })).With(
                            p.CompletedAll ? completedAllClass :
                                p.CompletedOne ? completed1Class :
                                null
                            ) : 
                        n.EnabledIf(false);
                }).ToArray()
            ).Concat(new [] {
            
            descrNode, 
            challengeSelNode,
            FixedDetailRow(new UINode(() => "Press Z to start level".Locale("Zキー押すとレベルスタート"))).SetLeftOverride(() => detailParent)
            //FixedDetailRow(new OptionNodeLR<DifficultySet>("Difficulty", nd => dfc = nd, 
            //    MainMenuDays.VisibleDifficulties.Select(vd => (vd.Describe(), vd)).ToArray(), dfc
            //    ).With(LROptionNode)), 
            }).ToArray()
        ).With(VTASceneSelect);

        MainScreen = new UIScreen(
            new TransferNode(SceneSelectScreen, "Game Start"),
            new OptionNodeLR<Locale>("Language", l => SaveData.s.Locale = l, new[] {
                ("English", Locale.EN),
                ("日本語", Locale.JP)
            }, SaveData.s.Locale).With(LROptionNode),
            //new FuncNode(RunTutorial, "Tutorial"),
            new FuncNode(Application.Quit, "Quit"),
            new OpenUrlNode("https://twitter.com/rdbatz", "Twitter (Browser)"),
            new OpenUrlNode("https://github.com/Bagoum/danmokou", "Github (Browser)")
            ).With(MainScreenV);
        base.Awake();
    }
}