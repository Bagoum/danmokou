using System;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using JetBrains.Annotations;
using Newtonsoft.Json;
using static Danmaku.Enums;
using static InputManager;
using GameLowRequest = DU<Danmaku.CampaignRequest, Danmaku.BossPracticeRequest, 
    ChallengeRequest, Danmaku.StagePracticeRequest>;
using GameLowRequestKey = DU<string, ((string, int), int), (((string, int), int), int), ((string, int), int)>;

public class ReplayMetadata {
    public int Seed { get; set; }
    public string PlayerKey { get; set; }
    public string ShotKey { get; set; }
    public long Score { get; set; }

    [JsonIgnore]
    [CanBeNull]
    public PlayerConfig Player => GameManagement.References.AllPlayers.FirstOrDefault(p => p.key == PlayerKey);
    [JsonIgnore]
    [CanBeNull]
    public ShotConfig Shot => GameManagement.References.AllShots.FirstOrDefault(s => s.key == ShotKey);
    public DifficultySet Difficulty { get; set; }
    public CampaignMode Mode { get; set; }
    public DateTime Now { get; set; }
    public string ID { get; set; }
    public (string campaign, ((string campaign, int boss), int phase) boss, (((string campaign, int boss), int phase), int challenge) challenge, ((string campaign, int boss), int stage) stage, short type) Key { get; set; }
    public Version EngineVersion { get; set; }
    public Version GameVersion { get; set; }
    public string GameIdentifier { get; set; }
    
    public float DialogueSpeed { get; set; }
    public bool SmoothInput { get; set; }

    public string CustomName { get; set; } = "";
    public void AssignName(string newName) => CustomName = newName.Substring(0, Math.Min(newName.Length, 10));

    [UsedImplicitly]
    public ReplayMetadata() {}
    public ReplayMetadata(GameRequest req, CampaignData end, string name="") {
        Seed = req.seed;
        Score = end.Score;
        PlayerKey = req.PlayerKey;
        ShotKey = req.ShotKey;
        Difficulty = req.difficulty;
        Mode = req.mode;
        Now = DateTime.Now;
        ID = RNG.RandStringOffFrame();
        Key = req.CampaignIdentifier.Tuple;
        EngineVersion = GameManagement.EngineVersion;
        GameVersion = GameManagement.References.gameVersion;
        GameIdentifier = GameManagement.References.gameIdentifier;
        DialogueSpeed = SaveData.s.DialogueWaitMultiplier;
        SmoothInput = SaveData.s.AllowInputLinearization;
        CustomName = name;
    }

    public void ApplySettings() {
        SaveData.s.DialogueWaitMultiplier = DialogueSpeed;
        SaveData.s.AllowInputLinearization = SmoothInput;
    }

    [JsonIgnore]
    private string RequestDescription => ReconstructedRequest.Resolve(
        c => $"{c.campaign.campaign.shortTitle.PadRight(10)} All",
        b => $"{b.boss.boss.ReplayName.PadRight(10)} p{b.phase.IndexInParentPhases}",
        c => $"{c.Boss.ReplayName.PadRight(10)} p{c.phase.phase.IndexInParentPhases}-{c.ChallengeIdx}",
        s => $"{s.stage.campaign.campaign.shortTitle.PadRight(10)} s{s.stage.stageIndex}"
    );
    [JsonIgnore]
    public string AsFilename => $"{Mode}_{Difficulty}_{ID}";

    public string AsDisplay(bool showScore) {
        var p = Player;
        var playerDesc = (p == null) ? "???" : p.shortTitle;
        var shotDesc = "?";
        if (p != null && Shot != null) {
            var shotInd = p.shots.IndexOf(Shot);
            shotDesc = (shotInd > -1) ? $"{shotInd.ToABC()}" : "?";
        }
        var pstr = $"{playerDesc}-{shotDesc}".PadRight(10);
        var score = showScore ? $"{Score} ".PadLeft(10, '0') : "";
        return
            $"{CustomName.PadRight(12)} {score} {pstr} {RequestDescription.PadRight(16)} {Difficulty.DescribePadR()} {Now.SimpleTime()}";
    }

    [JsonIgnore]
    public GameLowRequest ReconstructedRequest {
        get {
            var keydu = new GameLowRequestKey(Key.Item5, Key.Item1, Key.Item2, Key.Item3, Key.Item4);
            return keydu.Resolve(
                c => new GameLowRequest(CampaignRequest.Reconstruct(c)),
                b => new GameLowRequest(BossPracticeRequest.Reconstruct(b)),
                c => new GameLowRequest(ChallengeRequest.Reconstruct(c)),
                s => new GameLowRequest(StagePracticeRequest.Reconstruct(s))
            );
        }
    }
}
public readonly struct Replay {
    public readonly Func<FrameInput[]> frames;
    public readonly ReplayMetadata metadata;

    public Replay(FrameInput[] frames, GameRequest req, CampaignData end) : this(() => frames, new ReplayMetadata(req, end)) { }

    public Replay(Func<FrameInput[]> frames, ReplayMetadata metadata) {
        this.frames = frames;
        this.metadata = metadata;
    }
}
public static class Replayer {
    public enum ReplayStatus {
        RECORDING,
        REPLAYING,
        NONE
    }

    private static ReplayStatus status = ReplayStatus.NONE;
    private static int lastFrame = -1;
    private static int? replayStartFrame;
    private static int ReplayStartFrame {
        get {
            if (replayStartFrame == null) {
                ETime.ResetFrameNumber();
                replayStartFrame = 0;
            }
            return replayStartFrame.Value;
        }
    }
    [CanBeNull] private static List<FrameInput> recording;
    private static FrameInput[] replaying;
    [CanBeNull] private static Func<FrameInput[]> lazyReplaying;
    [CanBeNull]
    private static FrameInput[] Replaying {
        get => replaying = replaying ?? lazyReplaying?.Invoke();
        set {
            if (value == null) {
                replaying = null;
                lazyReplaying = null;
            } else throw new Exception("Cannot assign Replayer.Replaying to a non-null value");
        }
    }

    public static bool IsRecording => status == ReplayStatus.RECORDING;

    public static Replay? PostedReplay = null;

    public static void LoadLazy() {
        var _ = Replaying;
    }
    public static void BeginRecording() {
        Log.Unity("Replay recording started");
        lastFrame = -1;
        replayStartFrame = null;
        recording = new List<FrameInput>(1000000);
        status = ReplayStatus.RECORDING;
        PostedReplay = null;
    }

    public static void BeginReplaying(Func<FrameInput[]> data) {
        Log.Unity($"Replay playback started.");
        lastFrame = -1;
        replayStartFrame = null;
        //Minor optimization for replay restart
        if (lazyReplaying != data) {
            recording = null;
            Replaying = null;
            lazyReplaying = data;
        }
        status = ReplayStatus.REPLAYING;
    }

    public static void End(GameRequest req) {
        if (status == ReplayStatus.RECORDING) {
            Log.Unity($"Finished recording {recording?.Count ?? -1} frames.");
        } else if (status == ReplayStatus.REPLAYING) {
            Log.Unity($"Finished replaying {lastFrame - ReplayStartFrame + 1}/{Replaying?.Length ?? 0} frames.");
        }
        status = ReplayStatus.NONE;
        PostedReplay = (recording != null) ? new Replay(recording.ToArray(), req, GameManagement.campaign) : (Replay?) null;
        recording = null;
    }

    public static void Cancel() {
        Log.Unity("Cancelling in-progress replay recording.");
        status = ReplayStatus.NONE;
        recording = null;
        Replaying = null;
        PostedReplay = null;
    }

    public static void BeginFrame() {
        if (lastFrame == ETime.FrameNumber) return; //during pause/load
        //we want the counter to be treated the same for replay and record
        //Sorry for this order but replaystartframe may reset the frame number
        int replayIndex = -ReplayStartFrame + (lastFrame = ETime.FrameNumber);
        ReplayFrame(null);
        if (status == ReplayStatus.RECORDING && recording != null) {
            recording.Add(RecordFrame);
        } else if (status == ReplayStatus.REPLAYING && Replaying != null) {
            if (replayIndex >= Replaying?.Length) {
                Log.UnityError($"Ran out of replay data. On frame {lastFrame}, requested index {replayIndex}, " +
                                    $"but there are only {Replaying.Length}.");
            }
            ReplayFrame(Replaying[replayIndex]);
        }
    }
    
}
