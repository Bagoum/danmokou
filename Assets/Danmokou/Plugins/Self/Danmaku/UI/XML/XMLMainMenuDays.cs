using System;
using System.Collections.Generic;
using System.Linq;
using DMK.Core;
using DMK.Danmaku;
using DMK.GameInstance;
using DMK.Player;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using static DMK.Core.GameManagement;
using static DMK.UI.XML.XMLUtils;

namespace DMK.UI.XML {
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
    private UIScreen OptionsScreen;
    private UIScreen ReplayScreen;

    protected override IEnumerable<UIScreen> Screens => new[] {
        SceneSelectScreen, OptionsScreen, ReplayScreen,
        MainScreen
    };

    public VisualTreeAsset GenericUIScreen;
    public VisualTreeAsset GenericUINode;
    public VisualTreeAsset MainScreenV;
    public VisualTreeAsset OptionsScreenV;
    public VisualTreeAsset ReplayScreenV;
    public VisualTreeAsset VTASceneSelect;
    public VisualTreeAsset VTALR2OptionNode;
    public VisualTreeAsset VTALR2Option;
    public VisualTreeAsset GenericOptionNodeV;
    public float photoSize;

    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), GenericUIScreen},
        {typeof(UINode), GenericUINode},
    };

    private const string completed1Class = "lblue";
    private const string completedAllClass = "lgreen";

    private static UINode[] DifficultyNodes(Func<FixedDifficulty, UINode> map) =>
        GameManagement.VisibleDifficulties.Select(map).ToArray();

    private static UINode[] DifficultyFuncNodes(Func<FixedDifficulty, Action> map) =>
        DifficultyNodes(d => new FuncNode(map(d), d.Describe()));

    protected override void Start() {
        if (ReturnTo == null) {
            uiRenderer.Slide(new Vector2(3, 0), Vector2.zero, 1f, DMath.M.EOutSine, null);
            uiRenderer.Fade(0, 1, 1f, x => x, null);
        }

        FixedDifficulty dfc = FixedDifficulty.Normal;
        var defaultPlayer = References.dayCampaign.players[0];
        var defaultShot = defaultPlayer.shots2[0];

        PlayerTeam Team() => new PlayerTeam(0, Subshot.TYPE_D, (defaultPlayer, defaultShot.shot));
        //TODO I currently don't have a story around game-specific configurations of meter/etc,
        // this disabling is a stopgap measure until then.
        SharedInstanceMetadata Meta() => new SharedInstanceMetadata(Team(), new DifficultySettings(dfc) {meterEnabled = false});

        var photoBoard = DependencyInjection.MaybeFind<IAyaPhotoBoard>();

        SceneSelectScreen = new UIScreen(DayCampaign.days[0].bosses.SelectMany(
            b => b.phases.Select(p => {
                //TODO: this return is not safe if you change the difficulty.
                if (!p.Enabled(Meta())) return new UINode(() => p.Title(Meta())).EnabledIf(false);
                Challenge c = p.challenges[0];
                void SetChallenge(int idx) {
                    c = p.challenges[idx];
                    var completion = SaveData.r.ChallengeCompletion(p, idx, Meta());
                    photoBoard?.ConstructPhotos(completion?.Photos, photoSize);
                }
                (bool, UINode) Confirm() {
                    ConfirmCache();
                    new InstanceRequest(InstanceRequest.ShowPracticeSuccessMenu, Meta(),
                        challenge: new PhaseChallengeRequest(p, c)).Run();
                    return (true, null);
                }
                var challengeSwitch = new DynamicOptionNodeLR2<int>("", VTALR2Option, SetChallenge,
                    p.challenges.Length.Range().ToArray, (i, v, on) => {
                        v.Query(null, "bracket")
                            .ForEach(x => x.style.display = on ? DisplayStyle.Flex : DisplayStyle.None);
                        v.Q("Star").style.unityBackgroundImageTintColor = new StyleColor(p.Completed(i, Meta()) ?
                            p.boss.boss.colors.uiHPColor :
                            new Color(1, 1, 1, 0.52f));
                    });
                return new CacheNavigateUINode(TentativeCache, () => p.Title(Meta()),
                    new UINode(() => c.Description(p.boss.boss))
                        .With(large1Class).With(centerTextClass).SetConfirmOverride(Confirm),
                    challengeSwitch.With(VTALR2OptionNode).With(optionNoKeyClass)
                        .SetConfirmOverride(Confirm)
                        .SetOnVisit(_ => SetChallenge(challengeSwitch.Index))
                        .SetOnLeave(_ => photoBoard?.TearDown()),
                    new UINode(() => "Press Z to start level".Locale("Zキー押すとレベルスタート"))
                        .SetConfirmOverride(Confirm).With(large1Class).With(centerTextClass)
                ).With(large1Class).With(
                    p.CompletedAll(Meta()) ? completedAllClass :
                    p.CompletedOne(Meta()) ? completed1Class :
                    null
                );
            })).ToArray()).With(VTASceneSelect);

        OptionsScreen = new UIScreen(XMLPauseMenu.GetOptions(true, x => x.With(GenericOptionNodeV)).ToArray())
            .With(OptionsScreenV).OnExit(SaveData.AssignSettingsChanges);
        ReplayScreen = XMLUtils.ReplayScreen(TentativeCache, ConfirmCache).With(ReplayScreenV);

        MainScreen = new UIScreen(
            new TransferNode(SceneSelectScreen, "Game Start")
                .With(large1Class),
            new OptionNodeLR<Locale>("Language", l => {
                    SaveData.UpdateLocale(l);
                    SaveData.AssignSettingsChanges();
                }, new[] {
                    ("English", Locale.EN),
                    ("日本語", Locale.JP)
                }, SaveData.s.Locale).With(GenericOptionNodeV)
                .With(large1Class),
            new TransferNode(ReplayScreen, "Replays").EnabledIf(SaveData.p.ReplayData.Count > 0)
                .With(large1Class),
            //new FuncNode(RunTutorial, "Tutorial"),
            new TransferNode(OptionsScreen, "Options")
                .With(large1Class),
            new FuncNode(Application.Quit, "Quit")
                .With(large1Class),
            new OpenUrlNode("https://twitter.com/rdbatz", "Twitter (Browser)")
                .With(large1Class)
        ).With(MainScreenV);
        ResetCurrentNode();

        base.Start();
    }
}
}