using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Danmaku;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Profiling;
using KC = UnityEngine.KeyCode;
using static SaveUtils;
using static SM.SMAnalysis;

public static class Consts {
    public static readonly (int w, int h) BestResolution = (3840, 2160);
}
public static class SaveData {
    private const string SETTINGS = "settings.txt";
    private const string RECORD = "record.txt";

    public class Record {
        public bool TutorialDone = false;
        public HashSet<string> CompletedCampaigns = new HashSet<string>();
        [JsonIgnore]
        public bool MainCampaignCompleted => CompletedCampaigns.Contains(MainMenuCampaign.main.campaign.key);
        //day key, boss key, raw phase index, challenge index
        public Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<int, Dictionary<DifficultySet, ChallengeCompletion>>>>> SceneRecord = new Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<int, Dictionary<DifficultySet, ChallengeCompletion>>>>>();

        [CanBeNull]
        private Dictionary<int, Dictionary<int, Dictionary<DifficultySet, ChallengeCompletion>>>
            BossRecord(DayPhase phase) => SceneRecord.GetOrDefault2(phase.boss.day.day.key, phase.boss.boss.key);

        public bool ChallengeCompleted(DayPhase phase, int c) =>
            (BossRecord(phase)?.GetOrDefault2(phase.phase.index, c)?.Count ?? 0) > 0;

        public bool ChallengeCompleted(DayPhase phase, Challenge c) =>
            ChallengeCompleted(phase, phase.challenges.IndexOf(c));

        public bool PhaseCompletedOne(DayPhase phase) {
            for (int ii = 0; ii < phase.challenges.Length; ++ii) {
                if (ChallengeCompleted(phase, ii)) return true;
            }
            return false;
        }
        public bool PhaseCompletedAll(DayPhase phase) {
            for (int ii = 0; ii < phase.challenges.Length; ++ii) {
                if (!ChallengeCompleted(phase, ii)) return false;
            }
            return true;
        }

        public void CompleteChallenge(ChallengeRequest cr) {
            var dfcMap = SceneRecord.SetDefault(cr.Day.key).SetDefault(cr.Boss.key)
                .SetDefault(cr.phase.phase.index).SetDefault(cr.ChallengeIdx);
            dfcMap[cr.difficulty] = new ChallengeCompletion();
        }
    }
    public class Settings {
        public bool AllowInputLinearization = false;
    #if VER_BRUH
        public Locale Locale = Locale.JP;
        public bool Shaders = false;
        public bool LegacyRenderer = true;
    #else
        public Locale Locale = Locale.EN;
        public bool Shaders = true;
        public bool LegacyRenderer = false;
    #endif
        public int RefreshRate = 60;
        public (int w, int h) Resolution = Consts.BestResolution;
        public bool Dialogue = true;
    #if UNITY_EDITOR
        public static bool TeleportAtPhaseStart => false;
    #else
        public static bool TeleportAtPhaseStart => false;
    #endif
        public float Screenshake = 1f;
        public FullScreenMode Fullscreen = FullScreenMode.FullScreenWindow;
        public int Vsync = 1;
        public bool UnfocusedHitbox = true;
        public float DialogueWaitMultiplier = 1f;
        public float BGMVolume = 1f;
        public bool Backgrounds = true;

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
    public static void UpdateResolution((int w, int h)? wh = null) {
        if (wh.HasValue) {
            s.Resolution = wh.Value;
            Screen.SetResolution(s.Resolution.w, s.Resolution.h, s.Fullscreen);
        }
        BackgroundOrchestrator.RecreateTextures();
        BackgroundCombiner.Reconstruct();
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