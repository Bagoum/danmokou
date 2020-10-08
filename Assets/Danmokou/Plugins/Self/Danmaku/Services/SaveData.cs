using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Danmaku;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Profiling;
using KC = UnityEngine.KeyCode;
using static SaveUtils;
using static SM.SMAnalysis;
using static Danmaku.Enums;

public static class Consts {
    public static readonly (int w, int h) BestResolution = (3840, 2160);
}
public static class SaveData {
    private const string SETTINGS = "settings.txt";
    private const string RECORD = "record.txt";
    private const string REPLAYS_LIST = "replays.txt";
    private const string REPLAYS_DIR = "Replays/";

    public class Record {
        public bool TutorialDone = false;

        public void CompleteTutorial() {
            TutorialDone = true;
            SaveData.SaveRecord();
        }
        public Dictionary<string, HashSet<string>> EndingsAchieved = new Dictionary<string, HashSet<string>>();

        public void CompleteCampaign(string campaign, string ending) {
            EndingsAchieved.SetDefault(campaign).Add(ending);
            SaveRecord();
        }
        [JsonIgnore]
        public bool MainCampaignCompleted => EndingsAchieved.ContainsKey(GameManagement.References.campaign.key);
        public Dictionary<string, ChallengeCompletion> SceneRecord = new Dictionary<string, ChallengeCompletion>();

        public Dictionary<string, long> HighScores = new Dictionary<string, long>();

        public bool TrySetHighScore(string gameIdentifier, long score) {
            if (!HighScores.TryGetValue(gameIdentifier, out var currScore) || score > currScore) {
                HighScores[gameIdentifier] = score;
                SaveRecord();
                return true;
            } else return false;
        }

        public long? GetHighScore(string gameIdentifier) =>
            HighScores.TryGetValue(gameIdentifier, out var score) ? (long?) score : null;

        public void CompleteChallenge(FixedDifficulty difficulty, PhaseChallengeRequest cr, IEnumerable<AyaPhoto> photos) {
            var cs = cr.AsSetting;
            cs.Difficulty = difficulty;
            SceneRecord[cs.Key] = new ChallengeCompletion(photos);
            SaveData.SaveRecord();
        }


        [CanBeNull] public ChallengeCompletion ChallengeCompletion(DayPhase phase, int c, FixedDifficulty d) {
            var p = phase.AsSetting;
            p.ChallengeIndex = c;
            p.Difficulty = d;
            return SceneRecord.TryGetValue(p.Key, out var cc) ? cc : null;
        }

        public bool ChallengeCompleted(DayPhase phase, int c, FixedDifficulty d) =>
            ChallengeCompletion(phase, c, d) != null;

        public bool PhaseCompletedOne(DayPhase phase, FixedDifficulty d) {
            for (int ii = 0; ii < phase.challenges.Length; ++ii) {
                if (ChallengeCompleted(phase, ii, d)) return true;
            }
            return false;
        }
        public bool PhaseCompletedAll(DayPhase phase, FixedDifficulty d) {
            for (int ii = 0; ii < phase.challenges.Length; ++ii) {
                if (!ChallengeCompleted(phase, ii, d)) return false;
            }
            return true;
        }
    }
    public class Settings {
        public bool AllowInputLinearization = false;
        public Locale Locale = Locale.EN;
        public bool Shaders = true;
        public bool LegacyRenderer = false;
        public int RefreshRate = 60;
        public (int w, int h) Resolution = Consts.BestResolution;
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
                int w = Screen.currentResolution.width;
                (int, int) Resolution;
                if (w > 3000) Resolution = (3840, 2160);
                else if (w > 1700) Resolution = (1920, 1080);
                else if (w > 1000) Resolution = (1280, 720);
                else if (w > 700) Resolution = (800, 450);
                else Resolution = (640, 360);
                
                return new Settings() {
                #if WEBGL
                    AllowInputLinearization = false,
                    Backgrounds = false,
                    Fullscreen = FullScreenMode.Windowed,
                    LegacyRenderer = true,
                    UnfocusedHitbox = false,
                    Vsync = 0,
                    RefreshRate = 60,
                    Resolution = (1600, 900),
                #else
                    RefreshRate = DefaultRefresh, 
                    Resolution = Resolution,
                #endif
                };
            }
        }
    }

    public class Replays {
        public List<string> ReplayFiles { get; }
        [JsonIgnore]
        public List<Replay> ReplayData { get; }
        public Replays(string[] files) {
            ReplayFiles = new List<string>();
            ReplayData = files.SelectNotNull<string, Replay>(f => {
                try {
                    var metadata = Read<ReplayMetadata>(f + RMETAEXT);
                    if (metadata == null || ReplayFiles.Contains(f)) return null;
                    ReplayFiles.Add(f);
                    if (metadata.GameIdentifier != GameManagement.References.gameIdentifier) return null;
                    return new Replay(LoadReplayFrames(f), metadata);
                } catch (Exception e) {
                    Log.Unity($"Failed to read replay data: {e.Message}", true, Log.Level.WARNING);
                    return null;
                }
            }).OrderByDescending(r => r.metadata.Now).ToList();
        }

        public bool TryDeleteReplay(int i) {
            if (i < ReplayData.Count) {
                var filename = ReplayFilename(ReplayData[i]);
                ReplayFiles.Remove(filename);
                try {
                    File.Delete(SaveUtils.DIR + filename + RMETAEXT);
                    File.Delete(SaveUtils.DIR + filename + RFRAMEEXT);
                } catch (Exception e) {
                    Log.Unity(e.Message, true, Log.Level.WARNING);
                }
                Write(REPLAYS_LIST, ReplayFiles);
                ReplayData.RemoveAt(i);
                return true;
            } else return false;
        }

        private const string RMETAEXT = ".txt";
        private const string RFRAMEEXT = ".dat";
        private static string ReplayFilename(Replay r) => REPLAYS_DIR + r.metadata.AsFilename;

        private static Func<InputManager.FrameInput[]> LoadReplayFrames(string file) => () => {
            using (var fr = File.OpenRead(SaveUtils.DIR + file + RFRAMEEXT)) {
                return Serializer.Deserialize<InputManager.FrameInput[]>(fr);
                //var w = new BinaryFormatter();
                //return (InputManager.FrameInput[]) w.Deserialize(fr);
            }
        };
        public bool SaveNewReplay(Replay r) {
            var filename = ReplayFilename(r);
            if (ReplayFiles.Contains(filename)) return false;
            var f = r.frames();
            Log.Unity($"Saving replay {filename} with {f.Length} frames.");
            Write(filename + RMETAEXT, r.metadata);
            using (var fw = File.Create(SaveUtils.DIR + filename + RFRAMEEXT)) {
                Serializer.Serialize(fw, f);
            }
            //using (var fw = File.OpenWrite(SaveUtils.DIR + filename + RFRAMEEXT)) {
            //    var w = new BinaryFormatter();
            //    w.Serialize(fw, f);
            //}
            ReplayFiles.Add(filename);
            ReplayData.Insert(0, new Replay(LoadReplayFrames(filename), r.metadata));
            Write(REPLAYS_LIST, ReplayFiles);
            return true;
        }
    }
    public static Settings s { get; }
    public static Record r { get; }
    
    public static Replays p { get; }

    static SaveData() {
        s = Read<Settings>(SETTINGS) ?? Settings.Default;
        s.RefreshRate = Settings.DefaultRefresh;
        ETime.SetForcedFPS(s.RefreshRate);
        UpdateResolution(s.Resolution);
        UpdateFullscreen(s.Fullscreen);
        ETime.SetVSync(s.Vsync);
        Log.Unity($"Initial settings: resolution {s.Resolution}, fullscreen {s.Fullscreen}, vsync {s.Vsync}");
        r = Read<Record>(RECORD) ?? new Record();
        p = new Replays(Read<string[]>(REPLAYS_LIST) ?? new string[0]);
    }

    public static void SaveRecord() => Write(RECORD, r);

    //Screen.setRes does not take effect immediately, so we need to do this on-change instead of together with
    //shader variable reassignment
    public static void UpdateResolution((int w, int h)? wh = null) {
        if (wh.HasValue) {
            s.Resolution = wh.Value;
            Screen.SetResolution(s.Resolution.w, s.Resolution.h, s.Fullscreen);
            Log.Unity($"Set resolution to {wh.Value}");
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