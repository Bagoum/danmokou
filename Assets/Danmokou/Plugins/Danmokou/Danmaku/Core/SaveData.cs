using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;
using Danmokou.Achievements;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Graphics;
using Danmokou.Services;
using Danmokou.SM;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using Suzunoya.Dialogue;
using UnityEngine;
using UnityEngine.Profiling;
using KC = UnityEngine.KeyCode;
using static FileUtils;

namespace Danmokou.Core {
public static class SaveData {
    private const string SETTINGS = FileUtils.SAVEDIR + "settings.txt";
    private static string RECORD => FileUtils.SAVEDIR + $"record-{GameManagement.References.gameIdentifier}.txt";
    private const string REPLAYS_DIR = FileUtils.SAVEDIR + "Replays/";

    [Serializable]
    [ProtoContract]
    public class Record {
        [ProtoMember(1)] public bool TutorialDone = false;
        [ProtoMember(2)] public Dictionary<string, InstanceRecord> FinishedGames = new Dictionary<string, InstanceRecord>();
        [ProtoMember(3)] public Dictionary<string, State> Achievements = new Dictionary<string, State>();

        public State? GetAchievementState(string acvKey) =>
            Achievements.TryGetValue(acvKey, out var s) ? s : (State?) null;

        public void UpdateAchievement(Achievement a) {
            if (!GetAchievementState(a.Key).Try(out var s) || s < a.State) {
                Achievements[a.Key] = a.State;
            }
            SaveRecord();
        }

        [JsonIgnore]
        public IEnumerable<InstanceRecord> FinishedCampaignGames => 
            FinishedGames.Values.Where(gr => gr.RequestKey is CampaignRequestKey);

        [JsonIgnore]
        private ICollection<string> CompletedCampaigns =>
            new HashSet<string>(
                FinishedGames.Values
                    .Select(g => (g.Completed && g.RequestKey is CampaignRequestKey cr) ? cr.Campaign : null)
                    .FilterNone());

        public bool CampaignCompleted(string key) =>
            CompletedCampaigns.Contains(key);
        
        [JsonIgnore]
        public bool MainCampaignCompleted => CampaignCompleted(GameManagement.References.campaign.key);

        public static readonly Event<Unit> TutorialCompleted = new Event<Unit>();

        public void CompleteTutorial() {
            TutorialDone = true;
            TutorialCompleted.OnNext(default);
            SaveData.SaveRecord();
        }

        public void RecordGame(InstanceRecord rec) {
            FinishedGames[rec.Uuid] = rec;
            SaveData.SaveRecord();
        }

        public void InvalidateRecord(string uuid) {
            FinishedGames.Remove(uuid);
            SaveData.SaveRecord();
        }

        public Dictionary<BossPracticeRequestKey, (int success, int total)> GetCampaignSpellHistory() =>
            Statistics.AccSpellHistory(FinishedCampaignGames);

        public Dictionary<BossPracticeRequestKey, (int success, int total)> GetPracticeSpellHistory() =>
            Statistics.AccSpellHistory(FinishedGames.Values.Where(gr => gr.RequestKey is BossPracticeRequestKey));


        public long? GetHighScore(InstanceRequest req) {
            var campaign = req.lowerRequest.Key;
            return FinishedGames.Values.Where(g =>
                Equals(g.RequestKey, campaign) &&
                g.SavedMetadata.difficulty.standard == req.metadata.difficulty.standard
            ).Select(x => x.Score).OrderByDescending(x => x).FirstOrNull();
        }

        public InstanceRecord? ChallengeCompletion(SMAnalysis.DayPhase phase, int c, SharedInstanceMetadata meta) {
            var key = new PhaseChallengeRequest(phase, c).Key;
            //TODO you can add filters on the meta properties (difficulty/player) as necessary.

            return FinishedGames.Values
                .Where(g =>
                    g.Completed &&
                    g.RequestKey.Equals(key))
                .OrderByDescending(g => g.Date)
                .FirstOrDefault();
        }

        public bool ChallengeCompleted(SMAnalysis.DayPhase phase, int c, SharedInstanceMetadata meta) =>
            ChallengeCompletion(phase, c, meta) != null;

        public bool PhaseCompletedOne(SMAnalysis.DayPhase phase, SharedInstanceMetadata meta) {
            for (int ii = 0; ii < phase.challenges.Length; ++ii) {
                if (ChallengeCompleted(phase, ii, meta)) return true;
            }
            return false;
        }

        public bool PhaseCompletedAll(SMAnalysis.DayPhase phase, SharedInstanceMetadata meta) {
            for (int ii = 0; ii < phase.challenges.Length; ++ii) {
                if (!ChallengeCompleted(phase, ii, meta)) return false;
            }
            return true;
        }
    }

    public class Settings {
        public bool AllowInputLinearization = false;
        public string? Locale = null;
        public bool Shaders = true;
        public bool LegacyRenderer = false;
        public int RefreshRate = 60;
        public (int w, int h) Resolution = GraphicsUtils.BestResolution;
#if UNITY_EDITOR
        public bool SaveAsBinary = false;
        public static bool TeleportAtPhaseStart => false;
#else
        public bool SaveAsBinary = false;
        //Don't change this!
        public static bool TeleportAtPhaseStart => false;
#endif
        public float Screenshake = 1f;
        public FullScreenMode Fullscreen = FullScreenMode.FullScreenWindow;
        public int Vsync = 1;
        public bool UnfocusedHitbox = true;
        //Don't assign to this, use the event instead
        public float DialogueSpeed = 1f;
        [NonSerialized] public Evented<float> DialogueSpeedEv = null!;
        public float BGMVolume = 1f;
        [NonSerialized] public Evented<float> BGMVolumeEv = null!;
        public float SEVolume = 1f;
        public float TypingSoundVolume = 1f;
        public bool Backgrounds = true;
        public bool AllowControllerInput = true;

        public bool ProfilingEnabled = false;
        
        public List<(string name, DifficultySettings settings)> DifficultySettings =
            new List<(string name, DifficultySettings settings)>();

        public void AddDifficultySettings(string name, DifficultySettings settings) {
            DifficultySettings.Add((name, FileUtils.CopyJson(settings)));
            AssignSettingsChanges();
        }
        public void TryRemoveDifficultySettingsAt(int i) {
            if (i < DifficultySettings.Count) DifficultySettings.RemoveAt(i);
            AssignSettingsChanges();
        }

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
                if (w > 3000)
                    Resolution = (3840, 2160);
                else if (w > 1700)
                    Resolution = (1920, 1080);
                else if (w > 1400)
                    Resolution = (1600, 900);
                else if (w > 1000)
                    Resolution = (1280, 720);
                else if (w > 700)
                    Resolution = (800, 450);
                else
                    Resolution = (640, 360);

                return new Settings() {
#if WEBGL
                    Shaders = false,
                    AllowInputLinearization = false,
                    SaveAsBinary = false,
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
        public List<Replay> ReplayData { get; }

        public Replays() {
            ReplayData = FileUtils.EnumerateDirectory(REPLAYS_DIR)
                .Where(f => f.EndsWith(RMETAEXT))
                .Select(f => f.Substring(0, f.Length - RMETAEXT.Length))
                .SelectNotNull<string, Replay>(f => {
                    try {
                        var metadata = ReadJson<ReplayMetadata>(f + RMETAEXT);
                        if (metadata == null || metadata.Record.GameIdentifier !=
                            GameManagement.References.gameIdentifier) return null;
                        return new Replay(LoadReplayFrames(f), metadata);
                    } catch (Exception e) {
                        Logs.Log($"Failed to read replay data: {e.Message}", true, LogLevel.WARNING);
                        return null;
                    }
                })
                .OrderByDescending(r => r.metadata.Record.Date).ToList();
        }

        public bool TryDeleteReplay(int i) {
            if (i < ReplayData.Count) {
                var filename = ReplayFilename(ReplayData[i]);
                try {
                    File.Delete(filename + RMETAEXT);
                    File.Delete(filename + RFRAMEEXT);
                } catch (Exception e) {
                    Logs.Log(e.Message, true, LogLevel.WARNING);
                }
                ReplayData.RemoveAt(i);
                return true;
            } else return false;
        }

        private const string RMETAEXT = ".txt";
        private const string RFRAMEEXT = ".dat";
        private static string ReplayFilename(Replay r) => REPLAYS_DIR + r.metadata.AsFilename;

        private static InputManager.FrameInput[] AssertReplayLength(InputManager.FrameInput[] frames) {
            if (frames.Length == 0) throw new Exception("Loaded a replay with zero length");
            return frames;
        }
        private static Func<InputManager.FrameInput[]> LoadReplayFrames(string file) => () =>
            AssertReplayLength(ReadProtoCompressed<InputManager.FrameInput[]>(file + RFRAMEEXT) ?? throw new Exception($"Couldn't load replay from file {file}"));
        
        public static Func<InputManager.FrameInput[]> LoadReplayFrames(TextAsset file) => () =>
            AssertReplayLength(ReadProtoCompressed<InputManager.FrameInput[]>(file) ?? throw new Exception($"Couldn't load replay from textAsset {file.name}"));
        
        public static void SaveReplayFrames(string file, InputManager.FrameInput[] frames) =>
            WriteProtoCompressed(file + RFRAMEEXT, frames);

        public void SaveNewReplay(Replay r) {
            var filename = ReplayFilename(r);
            var f = r.frames();
            Logs.Log($"Saving replay {filename} with {f.Length} frames.");
            WriteJson(filename + RMETAEXT, r.metadata);
            SaveReplayFrames(filename, f);
            ReplayData.Insert(0, new Replay(LoadReplayFrames(filename), r.metadata));
        }
    }

    public static Settings s { get; }
    public static Record r { get; }

    public static Replays p { get; }

    static SaveData() {
        s = ReadJson<Settings>(SETTINGS) ?? Settings.Default;
        s.RefreshRate = Settings.DefaultRefresh;
        s.DialogueSpeedEv = new Evented<float>(s.DialogueSpeed);
        _ = SpeechSettings.SpeedMultiplier.AddDisturbance(s.DialogueSpeedEv);
        _ = s.DialogueSpeedEv.Subscribe(v => s.DialogueSpeed = v);
        s.BGMVolumeEv = new Evented<float>(s.BGMVolume);
        _ = s.BGMVolumeEv.Subscribe(v => s.BGMVolume = v);
        ETime.SetForcedFPS(s.RefreshRate);
        UpdateResolution(s.Resolution);
        UpdateFullscreen(s.Fullscreen);
        UpdateAllowController(s.AllowControllerInput);
        UpdateLocale(s.Locale);
        ETime.SetVSync(s.Vsync);
        Logs.Log($"Initial settings: resolution {s.Resolution}, fullscreen {s.Fullscreen}, vsync {s.Vsync}");
        r = ReadRecord() ?? new Record();
        Achievement.AchievementStateUpdated.Subscribe(r.UpdateAchievement);
        p = new Replays();
        StartProfiling();
    }

    private static Record? ReadRecord() => s.SaveAsBinary ? ReadProto<Record>(RECORD) : ReadJson<Record>(RECORD);

    public static void SaveRecord() {
        if (s.SaveAsBinary) {
            WriteProto(RECORD, r);
        } else {
            WriteJson(RECORD, r);
        }
    }

    //Screen.setRes does not take effect immediately, so we need to do this on-change instead of together with
    //shader variable reassignment
    //this is also used to turn backgrounds on/off
    public static void UpdateResolution((int w, int h)? wh = null) {
        if (wh.HasValue) {
            s.Resolution = wh.Value;
            Screen.SetResolution(s.Resolution.w, s.Resolution.h, s.Fullscreen);
            Logs.Log($"Set resolution to {wh.Value}");
        }
        SuzunoyaUnity.Rendering.RenderHelpers.PreferredResolution.OnNext(s.Resolution);
    }

    public static void UpdateFullscreen(FullScreenMode mode) {
        Screen.fullScreenMode = s.Fullscreen = mode;
    }

    public static void UpdateAllowController(bool allowed) {
        s.AllowControllerInput = allowed;
        InputManager.AllowControllerInput = allowed;
    }

    public static void UpdateLocale(string? loc) {
        s.Locale = loc;
        Localization.Locale.Value = loc;
    }

    public static void AssignSettingsChanges() {
        WriteJson(SETTINGS, s);
        ETime.SetForcedFPS(s.RefreshRate);
        ETime.SetVSync(s.Vsync);
        SettingsChanged.OnNext(s);
    }
    
    public static readonly Event<Settings> SettingsChanged = new Event<Settings>();

    private static void StartProfiling() {
        if (s.ProfilingEnabled) {
            Profiler.logFile = "profilerLog";
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
        }
    }
}
}