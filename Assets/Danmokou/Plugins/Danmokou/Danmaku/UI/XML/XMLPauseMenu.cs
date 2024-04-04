using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.ADV;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.VN;
using Suzunoya.ADV;
using SuzunoyaUnity;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.XMLUtils;
using static Danmokou.Core.LocalizedStrings;
using static Danmokou.Core.LocalizedStrings.UI;
using static Danmokou.Core.LocalizedStrings.Generic;

namespace Danmokou.UI.XML {
public interface IPauseMenu {
    void QueueOpen();
}
/// <summary>
/// Class to manage the pause menu UI. Links to an options screen and a VN save/load screen.
/// </summary>
[Preserve]
public class XMLPauseMenu : PausedGameplayMenu, IPauseMenu {
    private UINode unpause = null!;
    private UIScreen OptionsScreen = null!;
    private UIScreen? SaveLoadScreen;
    private RenderTexture? lastSaveLoadSS;
    protected override UINode? StartingNode => unpause;
    
    protected override UIScreen?[] Screens => new[] {MainScreen, OptionsScreen, SaveLoadScreen};

    public override void FirstFrame() {
        OptionsScreen = this.OptionsScreen(GameManagement.Instance.Replay == null);
        //Display the standard patterned bg
        OptionsScreen.BackgroundOpacity = 1f;
        //Keep this around to avoid opacity fade oddities
        OptionsScreen.MenuBackgroundOpacity = UIScreen.DefaultMenuBGOpacity;
        var advMan = ServiceLocator.FindOrNull<ADVManager>();
        if (GameManagement.Instance.Replay == null && advMan != null) {
            var backlog = ServiceLocator.Find<IVNWrapper>().TrackedVNs.First().backlog;
            var lastMessage = backlog.HasValue ? backlog.Value.readableSpeech : "(No dialogue)";
            SaveLoadScreen = this.SaveLoadVNScreen(inst => advMan.ExecAdv?.Inst.Request.Restart(inst.GetData()) ?? false, slot => new(advMan.GetSaveReadyADVData(), DateTime.Now, lastSaveLoadSS!.IntoTex(), slot, lastMessage));
            SaveLoadScreen.BackgroundOpacity = 1f;
            SaveLoadScreen.MenuBackgroundOpacity = UIScreen.DefaultMenuBGOpacity;
        }
        unpause = new FuncNode(LocalizedStrings.UI.unpause, ProtectHide);
        MainScreen = new UIScreen(this, pause_header, UIScreen.Display.OverlayTH)  { Builder = (s, ve) => {
            ve.AddColumn();
        }, MenuBackgroundOpacity = UIScreen.DefaultMenuBGOpacity };
        _ = new UIColumn(MainScreen, null,
            new TransferNode(main_options, OptionsScreen),
            GameManagement.Instance.Replay != null ? null : new TransferNode(saveload_header, SaveLoadScreen!),
            unpause,
            advMan == null ? 
                new ConfirmFuncNode(full_restart, GameManagement.Instance.Restart) {
                    EnabledIf = () => GameManagement.CanRestart
                } : null,
            advMan == null ? 
                new ConfirmFuncNode(checkpoint_restart, GameManagement.Instance.RestartFromCheckpoint) {
                    EnabledIf = () => GameManagement.CanRestart && GameManagement.Instance.CanRestartCheckpoint
                } : null,
            new ConfirmFuncNode(to_menu, GameManagement.GoToMainMenu),
            new ConfirmFuncNode(to_desktop, Application.Quit)) { ExitNodeOverride = unpause };
        base.FirstFrame();
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IPauseMenu>(this);
    }

    protected override void ShowMe() {
        if (SaveLoadScreen != null) {
            ServiceLocator.Find<IVNWrapper>().UpdateAllVNSaves();
            SaveData.SaveRecord();
            if (lastSaveLoadSS != null) {
                Logs.Log("Destroying VN save texture", level:LogLevel.DEBUG1);
                lastSaveLoadSS.DestroyTexOrRT();
            }
            lastSaveLoadSS = ServiceLocator.Find<IScreenshotter>().Screenshot(
                new CRect(-LocationHelpers.PlayableBounds.center.x, 0, MainCamera.ScreenWidth / 2f, 
                    MainCamera.ScreenHeight / 2f, 0), new[] { DMKMainCamera.CamType.UI });
        }
        base.ShowMe();
    }

    protected override Task HideMe() {
        if (MenuActive) {
            SaveData.AssignSettingsChanges();
        }
        return base.HideMe();
    }

    private bool openQueued = false;
    public override void RegularUpdate() {
        if (RegularUpdateGuard) {
            if (IsActiveCurrentMenu && (InputManager.Pause || InputManager.UIBack && Current == unpause))
                ProtectHide();
            else if (!MenuActive && (InputManager.Pause || openQueued) && EngineStateManager.State == EngineState.RUN)
                ShowMe();
            openQueued = false;
        }
        base.RegularUpdate();
    }

    public void QueueOpen() => openQueued = true;
}
}