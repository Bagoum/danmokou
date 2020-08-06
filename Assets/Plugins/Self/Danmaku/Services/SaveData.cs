using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Danmaku;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Profiling;

public static class Consts {
    public static readonly (int w, int h) BestResolution = (3840, 2160);
}
public static class SaveData {
    private const string DIR = "Saves/";
    private const string SETTINGS = "settings.txt";
    private const string RECORD = "record.txt";
    private static void Write(string file, object obj) {
        if (!Directory.Exists(DIR)) Directory.CreateDirectory(DIR);
        using (StreamWriter sw = new StreamWriter($"{DIR}{file}")) {
            sw.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
        }
    }
    [CanBeNull]
    private static T Read<T>(string file) where T : class {
        try {
            using (StreamReader sr = new StreamReader($"{DIR}{file}")) {
                return JsonConvert.DeserializeObject<T>(sr.ReadToEnd());
            }
        } catch (Exception e) {
            Log.Unity($"Couldn't read {typeof(T).RName()} from file {DIR}{file}.", false, Log.Level.WARNING);
            return null;
        }
    }

    public class Record {
        public bool TutorialDone = false;
        public HashSet<string> CompletedCampaigns = new HashSet<string>();
        public bool MainCampaignCompleted => CompletedCampaigns.Contains(MainMenu.main.campaign.key);
    }
    public class Settings {
        public bool Shaders = true;
        public bool AllowInputLinearization = false;
        public bool LegacyRenderer = false;
        public int RefreshRate = 60;
        public (int, int) Resolution = Consts.BestResolution;
        public bool Dialogue = true;
    #if UNITY_EDITOR
        public static bool TeleportAtPhaseStart => true;
    #else
        public static bool TeleportAtPhaseStart => false;
    #endif
        public float Screenshake = 1f;
        public FullScreenMode Fullscreen = FullScreenMode.FullScreenWindow;
        public int Vsync = 1;
        public bool UnfocusedHitbox = true;
        public float DialogueWaitMultiplier = 1f;
        //public bool Backgrounds = true;

        public static int DefaultRefresh {
            get {
                int hz = Screen.currentResolution.refreshRate;
                if (hz < 35) return 30;
                else if (hz < 50) return 40;
                else if (hz < 80) return 60;
                else if (hz < 160) return 120;
                return 60;
            }
        }
        public static Settings Default {
            get {
                int w = Screen.width;
                (int, int) Resolution;
                if (w > 3000) Resolution = (3840, 2160);
                else if (w > 1700) Resolution = (1920, 1080);
                else if (w > 1000) Resolution = (1280, 720);
                else if (w > 700) Resolution = (800, 450);
                else Resolution = (640, 360);
                return new Settings() { RefreshRate = DefaultRefresh, Resolution = Resolution };
            }
        }
    }
    public static Settings s { get; }
    public static Record r { get; }

    static SaveData() {
        s = Read<Settings>(SETTINGS) ?? Settings.Default;
        s.RefreshRate = Settings.DefaultRefresh;
        ETime.SetForcedFPS(s.RefreshRate);
        UpdateResolution(s.Resolution);
        UpdateFullscreen(s.Fullscreen);
        _ = DelayInitialVSyncWrite();
        r = Read<Record>(RECORD) ?? new Record();
        //Profiler.logFile = "profilerLog.raw";
        //Profiler.enabled = true;
        //Profiler.enableBinaryLog = true;
    }

    private static async Task DelayInitialVSyncWrite() {
        await Task.Delay(1000);
        ETime.SetVSync(s.Vsync);
        Debug.Log($"Assigned delayed VSync to {s.Vsync}");
    }

    public static void SaveRecord() => Write(RECORD, r);

    //Screen.setRes does not take effect immediately, so we need to do this on-change instead of together with
    //shader variable reassignment
    public static void UpdateResolution((int w, int h) wh) {
        s.Resolution = (wh.w, wh.h);
        Screen.SetResolution(wh.w, wh.h, s.Fullscreen);
        BackgroundOrchestrator.RecreateTextures();
    }

    public static void UpdateFullscreen(FullScreenMode mode) {
        Screen.fullScreenMode = s.Fullscreen = mode;
    }

    public static void AssignSettingsChanges() {
        Write(SETTINGS, s);
        ETime.SetForcedFPS(s.RefreshRate);
        ETime.SetVSync(s.Vsync);
        MainCamera.main.ReassignGlobalShaderVariables();
    }
    
}