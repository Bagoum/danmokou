using System;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using Danmaku.DanmakuUI;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;
using static XMLUtils;
using static Danmaku.Enums;

/// <summary>
/// Class to manage the main menu UI.
/// </summary>
[Preserve]
public class XMLPauseMenu : XMLMenu {

    public VisualTreeAsset UIScreen;
    public VisualTreeAsset UINode;
    public VisualTreeAsset OptionNode;
    
    public SFXConfig openPauseSound;
    public SFXConfig closePauseSound;

    public static IEnumerable<UINode> GetOptions(bool staticOptions, Func<UINode, UINode> with) {
        return new UINode[] {
            new OptionNodeLR<bool>("Shaders", yn => SaveData.s.Shaders = yn, YNOption, SaveData.s.Shaders),
            new OptionNodeLR<(int, int)>("Resolution", b => SaveData.UpdateResolution(b), new[] {
                ("3840x2160", (3840, 2160)),
                ("1920x1080", (1920, 1080)),
                ("1280x720", (1280, 720)),
                ("800x450", (800, 450)),
                ("640x360", (640, 360))
            }, SaveData.s.Resolution),
            new OptionNodeLR<int>("Refresh Rate", r => SaveData.s.RefreshRate = r, new[] {
        #if UNITY_EDITOR
                ("1Hz", 1),
                ("6Hz", 6),
        #endif
                ("30Hz", 30),
                ("40Hz", 40),
                ("60Hz", 60),
                ("120Hz", 120)
            }, SaveData.s.RefreshRate),
            new OptionNodeLR<FullScreenMode>("Fullscreen", SaveData.UpdateFullscreen, new[] {
                ("Exclusive", FullScreenMode.ExclusiveFullScreen),
                ("Borderless", FullScreenMode.FullScreenWindow),
                ("Windowed", FullScreenMode.Windowed),
            }, SaveData.s.Fullscreen),
            new OptionNodeLR<int>("VSync", v => SaveData.s.Vsync = v, new[] {
                ("Off", 0),
                ("On", 1),
                ("Double", 2)
            }, SaveData.s.Vsync),
            new OptionNodeLR<bool>("Legacy Renderer", b => SaveData.s.LegacyRenderer = b, YNOption,
                SaveData.s.LegacyRenderer),
            staticOptions
                ? new OptionNodeLR<bool>("Smooth Input", b => SaveData.s.AllowInputLinearization = b, YNOption,
                    SaveData.s.AllowInputLinearization)
                : null,
            new OptionNodeLR<float>("Screenshake", b => SaveData.s.Screenshake = b, new[] {
                    ("Off", 0),
                    ("x0.5", 0.5f),
                    ("x1", 1f),
                    ("x1.5", 1.5f),
                    ("x2", 2f)
                },
                SaveData.s.Screenshake),
            staticOptions
                ? new OptionNodeLR<float>("Dialogue Speed", b => SaveData.s.DialogueWaitMultiplier = b, new[] {
                    ("2x", 0.5f),
                    ("1.5x", .67f),
                    ("1x", 1f),
                    ("0.7x", 1.4f),
                    ("0.5x", 2f),
                }, SaveData.s.DialogueWaitMultiplier)
                : null,
            new OptionNodeLR<float>("BGM Volume", v => {
                SaveData.s.BGMVolume = v;
                AudioTrackService.ReassignExistingBGMVolumeIfNotFading();
            }, 21.Range().Select(x => 
                ($"{x*10}", x/10f)).ToArray(), SaveData.s.BGMVolume),
            new OptionNodeLR<bool>("Unfocused Hitbox", b => SaveData.s.UnfocusedHitbox = b, YNOption,
                SaveData.s.UnfocusedHitbox),
            new OptionNodeLR<bool>("Backgrounds", b => {
                    SaveData.s.Backgrounds = b;
                    SaveData.UpdateResolution();
                }, YNOption,
                SaveData.s.Backgrounds),
        }.Select(x => x == null ? null : with(x));
    }

    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), UIScreen},
        {typeof(UINode), UINode},
    };

    private static (string, bool)[] YNOption => new[] {
        ("Yes", true),
        ("No", false)
    };
    protected override string HeaderOverride => "Time.timeScale = 0;";

    private UINode unpause;
    protected override void Awake() {
        unpause = new FuncNode(GameStateManager.ForceUnpause, "Unpause", true).With(small1Class);
        MainScreen = new UIScreen(
            GetOptions(false, x => x.With(OptionNode).With(small1Class)).Concat(
                new[] {
                    unpause,
                    new ConfirmFuncNode(() => {
                        if (GameManagement.Restart()) {
                            HideOptions(true);
                            return true;
                        } else return false;
                    }, "Restart", true).With(small1Class),
                    GameManagement.MainMenuExists ? new ConfirmFuncNode(() => {
                        HideOptions(true);
                        GameManagement.GoToMainMenu();
                    }, "Return to Menu", true).With(small1Class) : null,
                    new ConfirmFuncNode(Application.Quit, "Quit to Desktop").With(small1Class)
                }
            ).ToArray()
        );
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
                MainScreen.ResetNodes();
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