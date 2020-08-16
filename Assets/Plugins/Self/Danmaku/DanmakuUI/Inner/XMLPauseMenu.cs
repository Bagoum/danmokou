using System;
using System.Collections.Generic;
using Danmaku.DanmakuUI;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

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

    protected override Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), UIScreen},
        {typeof(UINode), UINode},
    };

    private static (string, bool)[] YNOption => new[] {
        ("Yes", true),
        ("No", false)
    };
    protected override string HeaderOverride => "Time.timeScale = 0;";

    protected override void Awake() {
        MainScreen = new UIScreen(
            new OptionNodeLR<bool>("Shaders", yn => SaveData.s.Shaders = yn, YNOption, SaveData.s.Shaders)
                .With(OptionNode),
            new OptionNodeLR<(int, int)>("Resolution", b => SaveData.UpdateResolution(b), new[] {
                ("3840x2160", (3840, 2160)),
                ("1920x1080", (1920, 1080)),
                ("1280x720", (1280, 720)),
                ("800x450", (800, 450)),
                ("640x360", (640, 360))
            }, SaveData.s.Resolution).With(OptionNode),
            new OptionNodeLR<int>("Refresh Rate", r => SaveData.s.RefreshRate = r, new[] {
                ("30Hz", 30),
                ("40Hz", 40),
                ("60Hz", 60),
                ("120Hz", 120)
            }, SaveData.s.RefreshRate).With(OptionNode),
            new OptionNodeLR<FullScreenMode>("Fullscreen", SaveData.UpdateFullscreen, new[] {
                ("Exclusive", FullScreenMode.ExclusiveFullScreen),
                ("Borderless", FullScreenMode.FullScreenWindow),
                ("Windowed", FullScreenMode.Windowed),
            }, SaveData.s.Fullscreen).With(OptionNode),
            new OptionNodeLR<int>("VSync", v => SaveData.s.Vsync = v, new[] {
                ("Off", 0),
                ("On", 1),
                ("Double", 2)
            }, SaveData.s.Vsync).With(OptionNode),
            new OptionNodeLR<bool>("Legacy Renderer", b => SaveData.s.LegacyRenderer = b, YNOption,
                SaveData.s.LegacyRenderer).With(OptionNode),
            new OptionNodeLR<bool>("Smooth Input", b => SaveData.s.AllowInputLinearization = b, YNOption,
                SaveData.s.AllowInputLinearization).With(OptionNode),
            new OptionNodeLR<float>("Screenshake", b => SaveData.s.Screenshake = b, new[] {
                    ("Off", 0),
                    ("x0.5", 0.5f),
                    ("x1", 1f),
                    ("x1.5", 1.5f),
                    ("x2", 2f)
                },
                SaveData.s.Screenshake).With(OptionNode),
            new OptionNodeLR<float>("Dialogue Speed", b => SaveData.s.DialogueWaitMultiplier = b, new[] {
                ("2x", 0.5f),
                ("1.5x", .67f),
                ("1x", 1f),
                ("0.7x", 1.4f),
                ("0.5x", 2f),
            }, SaveData.s.DialogueWaitMultiplier).With(OptionNode),
            new OptionNodeLR<bool>("Unfocused Hitbox", b => SaveData.s.UnfocusedHitbox = b, YNOption,
                SaveData.s.UnfocusedHitbox).With(OptionNode),
            new OptionNodeLR<bool>("Backgrounds", b => {
                    SaveData.s.Backgrounds = b;
                    SaveData.UpdateResolution();
                }, YNOption,
                SaveData.s.Backgrounds).With(OptionNode),
            new FuncNode(GameStateManager.ForceUnpause, "Unpause", true),
            new ConfirmFuncNode(() => {
                HideOptions(true);
                GameManagement.ReloadLevel();
            }, "Reload Level", true),
            new ConfirmFuncNode(() => {
                HideOptions(true);
                GameManagement.GoToMainMenu();
            }, "Quit to Menu", true),
            new ConfirmFuncNode(Application.Quit, "Quit to Desktop"));
        base.Awake();
    }

    protected override void Start() {
        base.Start();
        HideOptions(false);
        MenuActive = false;
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
        Current = MainScreen.top[MainScreen.top.Length - 4];
    }

    public void GoToOption(int opt) {
        Current = MainScreen.top[opt];
        Redraw();
    }
}