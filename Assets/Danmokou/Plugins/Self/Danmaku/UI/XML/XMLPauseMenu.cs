using System;
using System.Collections.Generic;
using System.Linq;
using DMK.Core;
using DMK.Scriptables;
using DMK.Services;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;
using static DMK.UI.XML.XMLUtils;
using static DMK.Core.LocalizedStrings;
using static DMK.Core.LocalizedStrings.UI;
using static DMK.Core.LocalizedStrings.Generic;

namespace DMK.UI.XML {
/// <summary>
/// Class to manage the main menu UI.
/// </summary>
[Preserve]
public class XMLPauseMenu : XMLMenu {

    public VisualTreeAsset UIScreen = null!;

    public SFXConfig? openPauseSound;
    public SFXConfig? closePauseSound;

    public static IEnumerable<UINode> GetOptions(bool staticOptions) {
        return new UINode[] {
            new OptionNodeLR<bool>(shaders, yn => SaveData.s.Shaders = yn, new[] {
                (shaders_low, false),
                (shaders_high, true)
            }, SaveData.s.Shaders),
            new OptionNodeLR<(int, int)>(resolution, b => SaveData.UpdateResolution(b), new[] {
                ("3840x2160", (3840, 2160)),
                ("2560x1440", (2560, 1440)),
                ("1920x1080", (1920, 1080)),
                ("1600x900", (1600, 900)),
                ("1280x720", (1280, 720)),
                ("800x450", (800, 450)),
                ("640x360", (640, 360))
            }, SaveData.s.Resolution),
            new OptionNodeLR<int>(refresh, r => SaveData.s.RefreshRate = r, new[] {
#if UNITY_EDITOR
                ("1Hz", 1),
                ("6Hz", 6),
#endif
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
            new OptionNodeLR<float>(screenshake, b => SaveData.s.Screenshake = b, new[] {
                    ("Off", 0),
                    ("x0.5", 0.5f),
                    ("x1", 1f),
                    ("x1.5", 1.5f),
                    ("x2", 2f)
                },
                SaveData.s.Screenshake),
            new OptionNodeLR<bool>(controller, SaveData.UpdateAllowController, OnOffOption,
                SaveData.s.AllowControllerInput),
            staticOptions ?
                new OptionNodeLR<float>(dialogue_speed, b => SaveData.s.DialogueWaitMultiplier = b, new[] {
                    ("x2", 0.5f),
                    ("x1.5", .67f),
                    ("x1", 1f),
                    ("x0.7", 1.4f),
                    ("x0.5", 2f),
                }, SaveData.s.DialogueWaitMultiplier) :
                null!,
            new OptionNodeLR<float>(bgm_volume, v => {
                SaveData.s.BGMVolume = v;
                AudioTrackService.ReassignExistingBGMVolumeIfNotFading();
            }, 21.Range().Select(x =>
                (new LocalizedString($"{x * 10}"), x / 10f)).ToArray(), SaveData.s.BGMVolume),
            new OptionNodeLR<float>(sfx_volume, v => { SaveData.s.SEVolume = v; }, 21.Range().Select(x =>
                (new LocalizedString($"{x * 10}"), x / 10f)).ToArray(), SaveData.s.BGMVolume),
            new OptionNodeLR<bool>(hitbox, b => SaveData.s.UnfocusedHitbox = b, new[] {
                    (hitbox_always, true),
                    (hitbox_focus, false)
                }, SaveData.s.UnfocusedHitbox),
            new OptionNodeLR<bool>(backgrounds, b => {
                    SaveData.s.Backgrounds = b;
                    SaveData.UpdateResolution();
                }, OnOffOption,
                SaveData.s.Backgrounds),
        }.FilterNone();
    }

    private static (LocalizedString, bool)[] OnOffOption => new[] {
        (generic_on, true),
        (generic_off, false)
    };
    protected override string HeaderOverride => pause_header;

    private UINode unpause = null!;

    protected override void Awake() {
        unpause = new FuncNode(EngineStateManager.AnimatedUnpause, LocalizedStrings.UI.unpause, true).With(small1Class);
        MainScreen = new UIScreen(
            GetOptions(false).Select(x => x.With(small1Class)).Concat(
                new[] {
                    new PassthroughNode(LocalizedString.Empty),
                    unpause,
                    new ConfirmFuncNode(GameManagement.Restart, restart, true)
                        .EnabledIf(() => GameManagement.CanRestart)
                        .With(small1Class),
                    new ConfirmFuncNode(GameManagement.GoToMainMenu, to_menu, true).With(small1Class),
                    new ConfirmFuncNode(Application.Quit, to_desktop).With(small1Class)
                }
            ).ToArray()
        ).With(UIScreen);
        MainScreen.ExitNode = MainScreen.top[MainScreen.top.Length - 4];
        base.Awake();
    }

    protected override void Start() {
        base.Start();
        HideOptions(false);
        MenuActive = false;
        UI.style.right = UIManager.MenuRightOffset;
    }

    public void HideOptions(bool withSave) {
        if (UITop != null) {
            if (MenuActive && withSave) {
                MainScreen.ResetNodeProgress();
                SaveData.AssignSettingsChanges();
                SFXService.Request(closePauseSound);
            }
            //safer to do this even if the menu is already "inactive" since css may set it open initially
            UITop.style.display = DisplayStyle.None;
            MenuActive = false;
        }
    }

    public void ShowOptions() {
        //This check is because death-pause may, theoretically, occur in places where the pause menu is disabled
        if (UITop != null && !MenuActive) {
            MenuActive = true;
            SFXService.Request(openPauseSound);
            UITop.style.display = DisplayStyle.Flex;
            ResetCurrentNode();
            Redraw();
        }
    }

    protected override void ResetCurrentNode() {
        Current = unpause;
    }

    public void GoToOption(int opt) {
        Current = MainScreen.top[opt];
        Redraw();
    }
}
}