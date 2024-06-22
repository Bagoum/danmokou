using System;
using System.Collections.Generic;
using BagoumLib;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine.UIElements;
using UnityEngine.Scripting;
using static Danmokou.Core.LocalizedStrings.UI;


namespace Danmokou.UI.XML {
/// <summary>
/// Class to manage the success menu UI. This is invoked when a scene or boss practice is finished.
/// </summary>
[Preserve]
public class XMLPracticeSuccessMenu : PausedGameplayMenu {
    private IDanmakuGameDef? game;
    public override void FirstFrame() {
        MainScreen = new UIScreen(this, "YOU HUNTED", UIScreen.Display.OverlayTH)  { Builder = (s, ve) => {
            ve.AddColumn();
        }, MenuBackgroundOpacity = UIScreen.DefaultMenuBGOpacity };
        _ = new UIColumn(MainScreen, null,
            new FuncNode(full_restart, GameManagement.Instance.Restart)
                {EnabledIf = (() => GameManagement.CanRestart)},
            new FuncNode(save_replay, () => ServiceLocator.Find<ISceneIntermediary>().LoadScene(
                new SceneRequest(game!.ReplaySaveMenu,
                    SceneRequest.Reason.FINISH_RETURN))) {
                EnabledIf = (() => GameManagement.Instance.Replay is ReplayRecorder { State: ReplayActorState.Finalized }),
                VisibleIf = () => game != null
            },
            new FuncNode(to_menu, GameManagement.GoToMainMenu)) {
            EntryIndexOverride = () => 1
        };
        base.FirstFrame();
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(GameManagement.EvInstance, i => i.PracticeSuccess, ir => {
            game = ir.Game;
            OpenWithAnimationV();
        });
    }


}
}