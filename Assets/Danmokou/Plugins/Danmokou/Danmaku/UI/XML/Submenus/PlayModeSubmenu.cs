using System;
using System.Collections.Generic;
using BagoumLib;
using Danmokou.Core;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using Danmokou.UI.XML;
using UnityEngine;
using static Danmokou.UI.PlayModeCommentator;
using static Danmokou.Services.GameManagement;

namespace Danmokou.UI {
public class PlayModeSubmenu : IndexedSubmenuHandler {
    public TelescopingDisplay mainDisplay = null!;
    public TelescopingDisplay exDisplay = null!;
    public TelescopingDisplay stagePracDisplay = null!;
    public TelescopingDisplay bossPracDisplay = null!;
    public TelescopingDisplay tutorialDisplay = null!;
    
    public PlayModeCommentator? commentator;
    private List<(Mode key, bool locked, TelescopingDisplay display)> modes = null!;
    private Func<(bool, UINode?)> mainContinuation = null!;
    private Func<(bool, UINode?)> exContinuation = null!;
    protected override int NumOptions => modes.Count;
    protected override int DefaultOption =>
        (SaveData.r.TutorialDone || References.tutorial == null) ? 0 :
            modes.IndexOf(x => x.key == Mode.TUTORIAL);
    
    public UIScreen Initialize(XMLMainMenu menu, Func<CampaignConfig, Func<SharedInstanceMetadata, bool>, Func<(bool, UINode?)>> campaignRealizer) {
        modes = new List<(Mode key, bool locked, TelescopingDisplay display)>();
        modes.Add((Mode.MAIN, false, mainDisplay));
        mainContinuation = campaignRealizer(References.campaign,
            meta => InstanceRequest.RunCampaign(MainCampaign, null, meta));
        if (References.exCampaign != null) {
            modes.Add((Mode.EX, !SaveData.r.MainCampaignCompleted, exDisplay));
            exContinuation = campaignRealizer(References.exCampaign,
                meta => InstanceRequest.RunCampaign(ExtraCampaign, null, meta));
        }
        if (PracticeStagesExist)
            modes.Add((Mode.STAGEPRAC, PStages.Length == 0, stagePracDisplay));
        if (PracticeBossesExist)
            modes.Add((Mode.BOSSPRAC, PBosses.Length == 0, bossPracDisplay));
        if (References.tutorial != null)
            modes.Add((Mode.TUTORIAL, false, tutorialDisplay));

        return base.Initialize(menu);
    }
    protected override void HideOnExit() {
        modes.ForEach(x => x.display.Show(false));
    }

    protected override void Show(int index, bool isOnEnter) {
        if (commentator == null) {
            modes.ForEachI((i, x) => {
                x.display.Show(true);
                x.display.SetRelative(Vector2.zero, new Vector2(1.3f, -2f).normalized * 0.8f, i, index, NumOptions, isOnEnter, x.locked);
            });
        } else {
            commentator.SetComment(modes[index].key, modes[index].locked);
            modes.ForEachI((i, x) => {
                x.display.Show(true);
                x.display.SetRelative(new Vector2(-3.1f, 0), new Vector2(0, -0.45f), i, index, NumOptions, isOnEnter, x.locked);
            });
        }
    }

    protected override (bool success, UINode? nxt) Activate(int index) {
        var (mode, locked, _) = modes[index];
        if (locked)
            return (false, null);
        var c = Menu as XMLMainMenuCampaign;
        return mode switch {
            Mode.STAGEPRAC => (true, c!.StagePracticeScreen.First),
            Mode.BOSSPRAC => (true, c!.BossPracticeScreen.First),
            Mode.TUTORIAL => (InstanceRequest.RunTutorial(), null),
            Mode.EX => exContinuation(),
            _ => mainContinuation()
        };
    }

    protected override void OnPreExit() {
        if (commentator != null)
            commentator.Disappear();
    }
    protected override void OnPreEnter(int index) {
        if (commentator != null) {
            commentator.SetComment(modes[index].key, modes[index].locked);
            commentator.Appear();
        }
    }
}
}