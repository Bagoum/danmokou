using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Core;
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
    private static string RECORD => $"record-{GameManagement.References.gameIdentifier}.txt";
    //private static string REPLAYS_LIST => $"replays-{GameManagement.References.gameIdentifier}.txt";
    private const string REPLAYS_DIR = "Replays/";

    public class Record {
        public bool TutorialDone = false;

        public void CompleteTutorial() {
            TutorialDone = true;
            SaveData.SaveRecord();
        }
        public Dictionary<string, HashSet<string>> EndingsAchieved = new Dictionary<string, HashSet<string>>();

        public void CompleteCampaign(string campaign, [CanBeNull] string ending) {
            EndingsAchieved.SetDefault(campaign).Add(ending);
            SaveRecord();
        }
        [JsonIgnore]
        public bool MainCampaignCompleted => EndingsAchieved.ContainsKey(GameManagement.References.campaign.key);
        public Dictionary<string, ChallengeCompletion> SceneRecord = new Dictionary<string, ChallengeCompletion>();
        public Dictionary<string, GameRecord> FinishedGames = new Dictionary<string, GameRecord>();

        public void RecordGame(GameRecord rec) {
            FinishedGames[rec.Uuid] = rec;
            SaveData.SaveRecord();
        }

        private Dictionary<(string, string, int), (int success, int total)> AccSpellHistory(IEnumerable<GameRecord> over) {
            var res = new Dictionary<(string, string, int), (int, int)>();
            foreach (var g in over) {
                foreach (var cpt in g.CardCaptures) {
                    var (success, total) = res.SetDefault(cpt.Key, (0, 0));
                    ++total;
                    if (cpt.captured) ++success;
                    res[cpt.Key] = (success, total);
                }
            }
            return res;
        }

        public Dictionary<(string, string, int), (int success, int total)> GetCampaignSpellHistory() =>
            AccSpellHistory(FinishedGames.Values.Where(gr => gr.GameKey.type == 0));
        
        public Dictionary<(string, string, int), (int success, int total)> GetPracticeSpellHistory() =>
            AccSpellHistory(FinishedGames.Values.Where(gr => gr.GameKey.type == 1));

        
        public long? GetHighScore(GameRequest req) {
            var campaign = GameRequest.CampaignIdentifier(req.lowerRequest).Tuple;
            return FinishedGames.Values.Where(g => 
                g.GameKey == campaign &&
                g.SavedMetadata.difficulty.standard == req.metadata.difficulty.standard
            ).Select(x => x.Score).OrderByDescending(x => x).FirstOrNull();
        }

        public void CompleteChallenge(GameRequest req, IEnumerable<AyaPhoto> photos) {
            SceneRecord[req.Identifier] = new ChallengeCompletion(photos);
            SaveData.SaveRecord();
        }


        [CanBeNull] public ChallengeCompletion ChallengeCompletion(DayPhase phase, int c, GameMetadata meta) {
            var key = GameRequest.GameIdentifer(meta,
                new DU<CampaignRequest, BossPracticeRequest, PhaseChallengeRequest, StagePracticeRequest>(
                    new PhaseChallengeRequest(phase, c)));
            return SceneRecord.TryGetValue(key, out var cc) ? cc : null;
        }

        public bool ChallengeCompleted(DayPhase phase, int c, GameMetadata meta) =>
            ChallengeCompletion(phase, c, meta) != null;

        public bool PhaseCompletedOne(DayPhase phase, GameMetadata meta) {
            for (int ii = 0; ii < phase.challenges.Length; ++ii) {
                if (ChallengeCompleted(phase, ii, meta)) return true;
            }
            return false;
        }
        public bool PhaseCompletedAll(DayPhase phase, GameMetadata meta) {
            for (int ii = 0; ii < phase.challenges.Length; ++ii) {
                if (!ChallengeCompleted(phase, ii, meta)) return false;
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
        //Don't change this!
        public static bool TeleportAtPhaseStart => false;
    #endif
        public float Screenshake = 1f;
        public FullScreenMode Fullscreen = FullScreenMode.FullScreenWindow;
        public int Vsync = 1;
        public bool UnfocusedHitbox = true;
        public float DialogueWaitMultiplier = 1f;
        public float BGMVolume = 1f;
        public float SEVolume = 1f;
        public bool Backgrounds = true;
        public bool AllowControllerInput = true;

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
                else if (w > 1400) Resolution = (1600, 900);
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
        public List<Replay> ReplayData { get; }
        public Replays() {
            ReplayData = SaveUtils.EnumerateDirectory(REPLAYS_DIR)
                .Where(f => f.EndsWith(RMETAEXT))
                .Select(f => f.Substring(0, f.Length - RMETAEXT.Length))
                .SelectNotNull<string, Replay>(f => {
                    try {
                        var metadata = Read<ReplayMetadata>(f + RMETAEXT);
                        if (metadata == null || metadata.Record.TitleIdentifier != 
                            GameManagement.References.gameIdentifier) return null;
                        return new Replay(LoadReplayFrames(f), metadata);
                    } catch (Exception e) {
                        Log.Unity($"Failed to read replay data: {e.Message}", true, Log.Level.WARNING);
                        return null;
                    }
                })
                .OrderByDescending(r => r.metadata.Record.Date).ToList();
        }

        public bool TryDeleteReplay(int i) {
            if (i < ReplayData.Count) {
                var filename = ReplayFilename(ReplayData[i]);
                try {
                    File.Delete(SaveUtils.DIR + filename + RMETAEXT);
                    File.Delete(SaveUtils.DIR + filename + RFRAMEEXT);
                } catch (Exception e) {
                    Log.Unity(e.Message, true, Log.Level.WARNING);
                }
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
        public void SaveNewReplay(Replay r) {
            var filename = ReplayFilename(r);
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
            ReplayData.Insert(0, new Replay(LoadReplayFrames(filename), r.metadata));
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
        UpdateAllowController(s.AllowControllerInput);
        ETime.SetVSync(s.Vsync);
        Log.Unity($"Initial settings: resolution {s.Resolution}, fullscreen {s.Fullscreen}, vsync {s.Vsync}");
        r = Read<Record>(RECORD) ?? new Record();
        p = new Replays();
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
        ResolutionHasChanged.Proc();
    }

    public static readonly Events.Event0 ResolutionHasChanged = new Events.Event0();

    public static void UpdateFullscreen(FullScreenMode mode) {
        Screen.fullScreenMode = s.Fullscreen = mode;
    }

    public static void UpdateAllowController(bool allowed) {
        s.AllowControllerInput = allowed;
        InputManager.AllowControllerInput = allowed;
    }

    public static void AssignSettingsChanges() {
        Write(SETTINGS, s);
        ETime.SetForcedFPS(s.RefreshRate);
        ETime.SetVSync(s.Vsync);
        MainCamera.main.ReassignGlobalShaderVariables();
    }
    
}