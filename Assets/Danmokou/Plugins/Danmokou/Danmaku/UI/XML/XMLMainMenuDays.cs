using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Player;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using static Danmokou.Services.GameManagement;
using static Danmokou.UI.XML.XMLUtils;
using static Danmokou.Core.LocalizedStrings.UI;

namespace Danmokou.UI.XML {
/// <summary>
/// Class to manage the main menu UI for scene challenge-type games.
/// </summary>
[Preserve]
public class XMLMainMenuDays : XMLMainMenu {
    private static List<CacheInstruction>? _returnTo;
    protected override List<CacheInstruction>? ReturnTo {
        get => _returnTo;
        set => _returnTo = value;
    }

    private UIScreen SceneSelectScreen = null!;
    private UIScreen OptionsScreen = null!;
    private UIScreen ReplayScreen = null!;

    protected override IEnumerable<UIScreen> Screens => new[] {
        SceneSelectScreen, OptionsScreen, ReplayScreen,
        MainScreen
    };

    public VisualTreeAsset MainScreenV = null!;
    public VisualTreeAsset OptionsScreenV = null!;
    public VisualTreeAsset ReplayScreenV = null!;
    public VisualTreeAsset VTASceneSelect = null!;
    public VisualTreeAsset VTALR2Option = null!;
    public float photoSize;


    private const string completed1Class = "lblue";
    private const string completedAllClass = "lgreen";

    private static UINode[] DifficultyNodes(Func<FixedDifficulty, UINode> map) =>
        GameManagement.VisibleDifficulties.Select(map).ToArray();

    private static UINode[] DifficultyFuncNodes(Func<FixedDifficulty, Action> map) =>
        DifficultyNodes(d => new FuncNode(map(d), d.Describe()));

    public override void FirstFrame() {
        FixedDifficulty dfc = FixedDifficulty.Normal;
        var defaultPlayer = References.dayCampaign!.players[0];
        var defaultShot = defaultPlayer.shots2[0];
        var defaultSupport = defaultPlayer.supports[0];

        TeamConfig Team() => new TeamConfig(0, Subshot.TYPE_D, defaultSupport.ability, (defaultPlayer, defaultShot.shot));
        //TODO I currently don't have a story around game-specific configurations of meter/etc,
        // this disabling is a stopgap measure until then.
        SharedInstanceMetadata Meta() => new SharedInstanceMetadata(Team(), new DifficultySettings(dfc) {meterEnabled = false});

        var photoBoard = ServiceLocator.MaybeFind<IAyaPhotoBoard>();

        SceneSelectScreen = new UIScreen(this, DayCampaign.days[0].bosses.SelectMany(
            b => b.phases.Select(p => {
                //TODO: this return is not safe if you change the difficulty.
                if (!p.Enabled(Meta())) return new UINode(() => p.Title(Meta())).EnabledIf(false);
                Challenge c = p.challenges[0];
                void SetChallenge(int idx) {
                    c = p.challenges[idx];
                    var completion = SaveData.r.ChallengeCompletion(p, idx, Meta());
                    photoBoard?.ConstructPhotos(completion?.Photos, photoSize);
                }
                Func<(bool, UINode?)> Confirm = () => {
                    ConfirmCache();
                    new InstanceRequest(InstanceRequest.PracticeSuccess, Meta(),
                        new PhaseChallengeRequest(p, c)).Run();
                    return (true, null);
                };
                var challengeSwitch = new DynamicComplexOptionNodeLR<int>(LString.Empty, VTALR2Option, SetChallenge,
                    p.challenges.Length.Range().ToArray, (i, v, on) => {
                        v.Query(null!, "bracket")
                            .ForEach(x => x.style.display = on ? DisplayStyle.Flex : DisplayStyle.None);
                        v.Q("Star").style.unityBackgroundImageTintColor = new StyleColor(p.Completed(i, Meta()) ?
                            p.boss.boss.colors.uiHPColor :
                            new Color(1, 1, 1, 0.52f));
                    });
                return new CacheNavigateUINode(TentativeCache, () => p.Title(Meta()),
                    new UINode(() => c.Description(p.boss.boss))
                        .With(large1Class).With(centerTextClass).SetConfirmOverride(Confirm),
                    challengeSwitch.With(optionNoKeyClass)
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

        OptionsScreen = new UIScreen(this, XMLPauseMenu.GetOptions(true).ToArray())
            .With(OptionsScreenV).OnExit(SaveData.AssignSettingsChanges);
        ReplayScreen = XMLUtils.ReplayScreen(this, TentativeCache, ConfirmCache).With(ReplayScreenV);

        MainScreen = new UIScreen(this,
            new TransferNode(SceneSelectScreen, main_gamestart)
                .With(large1Class),
            new OptionNodeLR<string?>(main_lang, l => {
                    SaveData.UpdateLocale(l);
                    SaveData.AssignSettingsChanges();
                }, new[] {
                    (new LString("English"), Locales.EN),
                    (new LString("日本語"), Locales.JP)
                }, SaveData.s.Locale)
                .With(large1Class),
            new TransferNode(ReplayScreen, main_replays).EnabledIf(SaveData.p.ReplayData.Count > 0)
                .With(large1Class),
            //new FuncNode(RunTutorial, "Tutorial"),
            new TransferNode(OptionsScreen, main_options)
                .With(large1Class),
            new FuncNode(Application.Quit, main_quit)
                .With(large1Class),
            new OpenUrlNode("https://twitter.com/rdbatz", main_twitter)
                .With(large1Class)
        ).With(MainScreenV);
        ResetCurrentNode();

        base.FirstFrame();
        if (ReturnTo == null) {
            _ = uiRenderer.Slide(new Vector2(3, 0), Vector2.zero, 1f, DMath.M.EOutSine);
            _ = uiRenderer.Fade(0, 1, 1f, null);
        } else
            uiRenderer.Fade(1, 1, 0, null);
    }
}
}