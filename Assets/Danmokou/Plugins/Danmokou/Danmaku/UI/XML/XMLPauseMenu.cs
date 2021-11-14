using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Scriptables;
using Danmokou.Services;
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
/// Class to manage the main menu UI.
/// </summary>
[Preserve]
public class XMLPauseMenu : PausedGameplayMenu, IPauseMenu {

    public static IEnumerable<UINode> GetOptions(bool staticOptions) {
        return new UINode[] {
            new OptionNodeLR<bool>(shaders, yn => SaveData.s.Shaders = yn, new[] {
                (shaders_low, false),
                (shaders_high, true)
            }, SaveData.s.Shaders),
            new OptionNodeLR<(int, int)>(resolution, b => SaveData.UpdateResolution(b), new (LString, (int, int))[] {
                ("3840x2160", (3840, 2160)),
                ("2560x1440", (2560, 1440)),
                ("1920x1080", (1920, 1080)),
                ("1600x900", (1600, 900)),
                ("1280x720", (1280, 720)),
                ("800x450", (800, 450)),
                ("640x360", (640, 360))
            }, SaveData.s.Resolution),
            new OptionNodeLR<int>(refresh, r => SaveData.s.RefreshRate = r, new (LString, int)[] {
#if UNITY_EDITOR
                ("1Hz", 1),
                ("6Hz", 6),
#endif
                ("12Hz", 12),
                ("30Hz", 30),
                ("40Hz", 40),
                ("60Hz", 60),
                ("120Hz", 120)
            }, SaveData.s.RefreshRate),
            new OptionNodeLR<FullScreenMode>(fullscreen, SaveData.UpdateFullscreen, new[] {
                (fullscreen_exclusive, FullScreenMode.ExclusiveFullScreen),
                (fullscreen_borderless, FullScreenMode.FullScreenWindow),
                (fullscreen_window, FullScreenMode.Windowed),
            }, SaveData.s.Fullscreen),
            new OptionNodeLR<int>(vsync, v => SaveData.s.Vsync = v, new[] {
                (generic_off, 0),
                (generic_on, 1),
                (vsync_double, 2)
            }, SaveData.s.Vsync),
            new OptionNodeLR<bool>(LocalizedStrings.UI.renderer, b => SaveData.s.LegacyRenderer = b, new[] {
                (renderer_legacy, true),
                (renderer_normal, false)
            }, SaveData.s.LegacyRenderer),
            staticOptions ?
                new OptionNodeLR<bool>(smoothing, b => SaveData.s.AllowInputLinearization = b, OnOffOption,
                    SaveData.s.AllowInputLinearization) :
                null!,
            new OptionNodeLR<float>(screenshake, b => SaveData.s.Screenshake = b, new(LString, float)[] {
                    ("Off", 0),
                    ("x0.5", 0.5f),
                    ("x1", 1f),
                    ("x1.5", 1.5f),
                    ("x2", 2f)
                },
                SaveData.s.Screenshake),
            new OptionNodeLR<bool>(hitbox, b => SaveData.s.UnfocusedHitbox = b, new[] {
                (hitbox_always, true),
                (hitbox_focus, false)
            }, SaveData.s.UnfocusedHitbox),
            new OptionNodeLR<bool>(backgrounds, b => {
                    SaveData.s.Backgrounds = b;
                    SaveData.UpdateResolution();
                }, OnOffOption,
                SaveData.s.Backgrounds),
            new OptionNodeLR<bool>(controller, SaveData.UpdateAllowController, OnOffOption,
                SaveData.s.AllowControllerInput),
            new OptionNodeLR<float>(bgm_volume, SaveData.s.BGMVolumeEv.OnNext, 21.Range().Select(x =>
                (new LString($"{x * 10}"), x / 10f)).ToArray(), SaveData.s.BGMVolume),
            new OptionNodeLR<float>(sfx_volume, v => { SaveData.s.SEVolume = v; }, 21.Range().Select(x =>
                (new LString($"{x * 10}"), x / 10f)).ToArray(), SaveData.s.BGMVolume),
            new OptionNodeLR<float>("Dialogue Typing Volume", v => { SaveData.s.TypingSoundVolume = v; }, 21.Range().Select(x =>
                (new LString($"{x * 10}"), x / 10f)).ToArray(), SaveData.s.TypingSoundVolume),
            staticOptions ?
                new OptionNodeLR<float>(dialogue_speed, SaveData.s.DialogueSpeedEv.OnNext, new(LString, float)[] {
                    ("x2", 2f),
                    ("x1.5", 1.5f),
                    ("x1", 1f),
                    ("x0.75", 0.75f),
                    ("x0.5", 0.5f),
                }, SaveData.s.DialogueSpeed) :
                null!,
        }.FilterNone();
    }

    private static (LString, bool)[] OnOffOption => new[] {
        (generic_on, true),
        (generic_off, false)
    };

    public override void FirstFrame() {
        var unpause = new FuncNode(LocalizedStrings.UI.unpause, () => ProtectHide(() => HideOptions(true))).With(small1Class);
        MainScreen = new UIScreen(this, pause_header, UIScreen.Display.OverlayTH)  { Builder = (s, ve) => {
            ve.AddColumn();
        }, BackgroundOpacity = 0.8f  };
        _ = new UIColumn(MainScreen, null, 
            GetOptions(!Replayer.RequiresConsistency).Select(x => x.With(small1Class))
            .Concat(
                new[] {
                    new UINode(LString.Empty) {Passthrough = true}.With(small3Class),
                    unpause,
                    new ConfirmFuncNode(restart, GameManagement.Restart) {
                        EnabledIf = () => GameManagement.CanRestart,
                    }.With(small1Class),
                    new ConfirmFuncNode(to_menu, GameManagement.GoToMainMenu)
                        .With(small1Class),
                    new ConfirmFuncNode(to_desktop, Application.Quit).With(small1Class)
                }
            )) {
            EntryIndexOverride = () => -4,
            ExitIndexOverride = -4
        };
        base.FirstFrame();
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IPauseMenu>(this);
    }

    private void HideOptions(bool withSave) {
        if (MenuActive && withSave) {
            SaveData.AssignSettingsChanges();
        }
        base.HideMe();
    }

    private bool openQueued = false;
    public override void RegularUpdate() {
        if (RegularUpdateGuard) {
            if (InputManager.Pause.Active && MenuActive)
                ProtectHide(() => HideOptions(true));
            else if ((InputManager.Pause.Active || openQueued) && EngineStateManager.State == EngineState.RUN)
                ShowMe();
            openQueued = false;
        }
        base.RegularUpdate();
    }

    public void QueueOpen() => openQueued = true;
}
}