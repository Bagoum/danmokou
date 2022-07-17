using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.Serialization;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;
using Danmokou.Achievements;
using Danmokou.ADV;
using Danmokou.Core.DInput;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Graphics;
using Danmokou.Services;
using Danmokou.SM;
using Danmokou.VN;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using Suzunoya.Data;
using Suzunoya.Dialogue;
using UnityEngine;
using UnityEngine.Profiling;
using KC = UnityEngine.KeyCode;
using static FileUtils;

namespace Danmokou.Core {
public static class SaveData {
    private const string SETTINGS = FileUtils.SAVEDIR + "settings.json";
    private static string RECORD => FileUtils.SAVEDIR + $"record-{GameManagement.References.gameIdentifier}.json";
    private const string REPLAYS_DIR = FileUtils.SAVEDIR + "Replays/";

    [Serializable]
    public class Record {
        public bool TutorialDone = false;
        public Dictionary<string, InstanceRecord> FinishedGames { get; init; } = new();
        public Dictionary<string, State> Achievements { get; init; } = new();
        public GlobalData GlobalVNData { get; init; } = new();

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

        public static readonly Event<Unit> TutorialCompleted = new();

        
        public State? GetAchievementState(string acvKey) =>
            Achievements.TryGetValue(acvKey, out var s) ? s : (State?) null;

        public void UpdateAchievement(Achievement a) {
            if (!GetAchievementState(a.Key).Try(out var s) || s < a.State) {
                Achievements[a.Key] = a.State;
            }
            SaveRecord();
        }
        
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

    public class Settings : IDMKLocaleProvider {
        public Evented<string?> TextLocale { get; init; } = new(Locales.EN);
        public Evented<string?> VoiceLocale { get; init; } = new(Locales.EN);
        public bool AllowInputLinearization = false;
        public bool Verbose = false;
        public bool Shaders = true;
        public bool LegacyRenderer = false;
        public (int w, int h) Resolution = GraphicsUtils.BestResolution;
#if UNITY_EDITOR && !EXBAKE_SAVE && !EXBAKE_LOAD
        public static bool TeleportAtPhaseStart => true;
#else
        //Don't change this! TeleportAtPhaseStart is a editor utility and should not be enabled in builds.
        public static bool TeleportAtPhaseStart => false;
#endif
        public float Screenshake = 1f;
        public FullScreenMode Fullscreen = FullScreenMode.FullScreenWindow;
        public int Vsync = 0;
        public bool UnfocusedHitbox = true;
        public Evented<float> BGMVolume { get; init; } = new(1f);
        public float SEVolume = 1f;
        public bool Backgrounds = true;
        public bool ProfilingEnabled = false;
        
        public float VNDialogueOpacity = 0.9f;
        public float VNTypingSoundVolume = 1f;
        public bool VNOnlyFastforwardReadText = true;
        
        public List<(string name, DifficultySettings settings)> DifficultySettings { get; init; } =
            new();


        public void AddDifficultySettings(string name, DifficultySettings settings) {
            DifficultySettings.Add((name, FileUtils.CopyJson(settings)));
            AssignSettingsChanges();
        }
        public bool RemoveDifficultySettings((string, DifficultySettings) dc) {
            var succ = DifficultySettings.Remove(dc);
            AssignSettingsChanges();
            return succ;
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
                    Resolution = (848, 477);
                else
                    Resolution = (640, 360);
                
                //Lower default rez for phones
            #if UNITY_ANDROID || UNITY_IOS
                Resolution = (1280, 720);
            #endif

                return new Settings() {
#if WEBGL
                    Shaders = false,
                    AllowInputLinearization = false,
                    SaveAsBinary = false,
                    Fullscreen = FullScreenMode.Windowed,
                    LegacyRenderer = true,
                    UnfocusedHitbox = false,
                    Vsync = 0,
                    Resolution = (1600, 900),
#else
                    Resolution = Resolution,
#endif
                };
            }
        }
    }

    public class VNSaves {
        private const string VNMETAEXT = ".txt";
        private const string VNIMGEXT = ".jpg";
        private const string VNDATAEXT = ".dat";

        public Dictionary<int, SerializedSave> Saves { get; } = new();
        public SerializedSave MostRecentSave => Saves.Values.MaxBy(s => s.SaveTime);

        public VNSaves() {
            foreach (var save in FileUtils.EnumerateDirectory(INSTANCESAVEDIR)
                .Where(f => f.EndsWith(VNMETAEXT))
                .Select(f => f[..^VNMETAEXT.Length])
                .SelectNotNull(f => {
                    try {
                        var metadata = ReadJson<SerializedSave>(f + VNMETAEXT);
                        if (metadata?.GameIdentifier != GameManagement.References.gameIdentifier)
                            return null;
                        return metadata;
                    } catch (Exception e) {
                        Logs.Log($"Failed to read replay data: {e.Message}", true, LogLevel.WARNING);
                        return null;
                    }
                })) {
                if (!Saves.ContainsKey(save.Slot) || save.SaveTime > Saves[save.Slot].SaveTime)
                    Saves[save.Slot] = save;
            }
        }
        
        public void SaveNewSave(SerializedSave inst) {
            var prev = Saves.TryGetValue(inst.Slot, out var _prev) ? _prev : null;
            Logs.Log($"Saving vn-save {inst.SaveLocation} at slot {inst.Slot}. " +
                     $"Previous exists at this slot: {prev != null}");
            WriteJson(inst.SaveLocation, inst);
            if (prev != null)
                TryDeleteSave(prev);
            Saves[inst.Slot] = inst;
        }
        public bool TryDeleteSave(SerializedSave inst) {
            if (!Saves.Values.Contains(inst)) return false;
            try {
                FileUtils.Delete(inst.SaveLocation);
                inst.RemoveFromDisk();
            } catch (Exception e) {
                Logs.Log(e.Message, true, LogLevel.WARNING);
            }
            Saves.Remove(inst.Slot);
            return true;
        }
        
    }
    public class Replays {

        private const string RMETAEXT = ".txt";
        private const string RFRAMEEXT = ".dat";
        public List<Replay> ReplayData { get; }

        public Replays() {
            ReplayData = FileUtils.EnumerateDirectory(REPLAYS_DIR)
                .Where(f => f.EndsWith(RMETAEXT))
                .Select(f => f[..^RMETAEXT.Length])
                .SelectNotNull(f => {
                    try {
                        var metadata = ReadJson<ReplayMetadata>(f + RMETAEXT);
                        if (metadata?.Record.GameIdentifier != GameManagement.References.gameIdentifier) 
                            return null;
                        return new Replay(LoadReplayFrames(f), metadata);
                    } catch (Exception e) {
                        Logs.Log($"Failed to read replay data: {e.Message}", true, LogLevel.WARNING);
                        return null;
                    }
                })
                .OrderByDescending(r => r.metadata.Record.Date).ToList();
        }

        public void SaveNewReplay(Replay r) {
            var filename = ReplayFilename(r);
            var f = r.frames();
            Logs.Log($"Saving replay {filename} with {f.Length} frames.");
            WriteJson(filename + RMETAEXT, r.metadata);
            SaveReplayFrames(filename, f);
            ReplayData.Insert(0, new Replay(LoadReplayFrames(filename), r.metadata));
        }
        public bool TryDeleteReplay(Replay r) {
            if (!ReplayData.Contains(r)) return false;
            var filename = ReplayFilename(r);
            try {
                File.Delete(filename + RMETAEXT);
                File.Delete(filename + RFRAMEEXT);
            } catch (Exception e) {
                Logs.Log(e.Message, true, LogLevel.WARNING);
            }
            ReplayData.Remove(r);
            return true;
        }
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

    }

    public static Settings s { get; }
    public static readonly Evented<Settings> SettingsEv;
    public static Record r { get; }
    public static Replays p { get; }
    public static VNSaves v { get; }
    public static Suzunoya.Data.Settings VNSettings => r.GlobalVNData.Settings;

    static SaveData() {
        s = ReadJson<Settings>(SETTINGS) ?? Settings.Default;
        _ = ServiceLocator.Register<IDMKLocaleProvider>(s);
        UpdateResolution(s.Resolution);
        UpdateFullscreen(s.Fullscreen);
        ETime.SetVSync(s.Vsync);
        Logs.Verbose = s.Verbose;
        Logs.Log($"Initial settings: resolution {s.Resolution}, fullscreen {s.Fullscreen}, vsync {s.Vsync}");
        r = ReadRecord() ?? new Record();
        Achievement.AchievementStateUpdated.Subscribe(r.UpdateAchievement);
        p = new Replays();
        v = new VNSaves();
        StartProfiling();
        SettingsEv = new Evented<Settings>(s);
    }

    private static Record? ReadRecord() => ReadJson<Record>(RECORD);

    public static void SaveRecord() => WriteJson(RECORD, r);

    //Screen changes does not take effect immediately, so we need to do this on-change instead of together with
    //shader variable reassignment
    //this is also used to turn backgrounds on/off
    public static void UpdateResolution((int w, int h)? wh = null) {
        if (wh.HasValue) {
            s.Resolution = wh.Value;
            //Changing output resolution on phones doesn't make sense and does weird things
        #if !(UNITY_ANDROID || UNITY_IOS)
            Screen.SetResolution(s.Resolution.w, s.Resolution.h, s.Fullscreen);
        #endif
            Logs.Log($"Set resolution to {wh.Value}");
        }
        SuzunoyaUnity.Rendering.RenderHelpers.PreferredResolution.OnNext(s.Resolution);
    }

    public static void UpdateFullscreen(FullScreenMode mode) {
        Screen.fullScreenMode = s.Fullscreen = mode;
    }

    public static void AssignSettingsChanges() {
        WriteJson(SETTINGS, s);
        //dialogue speed settings are stored in the global VN info in record
        SaveData.SaveRecord();
        ETime.SetVSync(s.Vsync);
        Logs.Verbose = s.Verbose;
        SettingsEv.OnNext(s);
    }

    private static void StartProfiling() {
        if (s.ProfilingEnabled) {
            Profiler.logFile = "profilerLog";
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
        }
    }
}
}