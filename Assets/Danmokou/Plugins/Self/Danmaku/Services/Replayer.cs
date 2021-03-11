using System;
using System.Collections.Generic;
using System.Linq;
using DMK.Achievements;
using DMK.Core;
using DMK.DMath;
using DMK.GameInstance;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine.SocialPlatforms;
using static DMK.Core.InputManager;


namespace DMK.Services {
/// <summary>
/// Records information about a completed game run-through with an attached replay.
/// </summary>
public class ReplayMetadata {
    /// <summary>
    /// Serializing the record in the replay file allows transferring replays.
    /// </summary>
    public InstanceRecord Record { get; set; }
    [JsonIgnore] public string RecordUuid => Record.Uuid;
    //Frozen options
    public float DialogueSpeed { get; set; }
    public bool SmoothInput { get; set; }
    public Locale Locale { get; set; } = Locale.EN;
    // Not important but it's convenient
    public int Length { get; set; }
    
    public bool Debug { get; set; }

    [UsedImplicitly]
#pragma warning disable 8618
    public ReplayMetadata() {}
#pragma warning restore 8618
    public ReplayMetadata(InstanceRecord rec, bool debug=false) {
        Record = rec;
        Debug = debug;
        DialogueSpeed = SaveData.s.DialogueWaitMultiplier;
        SmoothInput = SaveData.s.AllowInputLinearization;
        Locale = SaveData.s.Locale;
    }

    public void ApplySettings() {
        SaveData.s.DialogueWaitMultiplier = DialogueSpeed;
        SaveData.s.AllowInputLinearization = SmoothInput;
        SaveData.UpdateLocale(Locale);
    }

    [JsonIgnore]
    public string AsFilename => $"{Record.Mode}_{Record.SavedMetadata.difficulty.DescribeSafe()}_{Record.Uuid}";

}
public readonly struct Replay {
    public readonly Func<FrameInput[]> frames;
    public readonly ReplayMetadata metadata;

    public Replay(FrameInput[] frames, InstanceRecord rec, bool debug=false) :
        this(() => frames, new ReplayMetadata(rec, debug)) {
        metadata.Length = frames.Length;
    }

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

    public readonly struct ReplayerConfig {
        public enum FinishMethod {
            ERROR,
            REPEAT,
            STOP
        }

        public readonly FinishMethod finishMethod;
        public readonly Func<FrameInput[]> frames;
        public readonly Action? onFinish;
        public ReplayerConfig(FinishMethod finishMethod, Func<FrameInput[]> frames, Action? onFinish = null) {
            this.finishMethod = finishMethod;
            this.frames = frames;
            this.onFinish = onFinish;
        }

        private (FinishMethod, Func<FrameInput[]>) Tuple => (finishMethod, frames);

        public static bool operator ==(ReplayerConfig b1, ReplayerConfig b2) => b1.Tuple == b2.Tuple;
        public static bool operator !=(ReplayerConfig b1, ReplayerConfig b2) => !(b1 == b2);
        public override int GetHashCode() => Tuple.GetHashCode();
        public override bool Equals(object o) => o is ReplayerConfig bc && this == bc;
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
    private static List<FrameInput>? recording;
    private static ReplayerConfig? replaying;
    private static FrameInput[]? loadedFrames;
    private static FrameInput[]? LoadedFrames => loadedFrames ??= replaying?.frames.Invoke();

    public static bool IsRecording => status == ReplayStatus.RECORDING;

    public static Replay? PostedReplay { get; private set; } = null;

    public static void LoadLazy() {
        var _ = LoadedFrames;
    }

    public static void BeginRecording() {
        Log.Unity("Replay recording started.");
        lastFrame = -1;
        replayStartFrame = null;
        recording = new List<FrameInput>(1000000);
        status = ReplayStatus.RECORDING;
        PostedReplay = null;
    }

    public static void BeginReplaying(ReplayerConfig data) {
        Log.Unity($"Replay playback started.");
        Achievement.ACHIEVEMENT_PROGRESS_ENABLED = false;
        lastFrame = -1;
        replayStartFrame = null;
        recording = null;
        //Minor optimization for replay restart
        if (!replaying.Try(out var r) || r != data) {
            Log.Unity($"Setting frames to lazy-load for replay.");
            loadedFrames = null;
            replaying = data;
        }
        status = ReplayStatus.REPLAYING;
    }

    public static Replay? End(InstanceRecord? rec) {
        if (status == ReplayStatus.RECORDING) {
            Log.Unity($"Finished recording {recording?.Count ?? -1} frames.");
        } else if (status == ReplayStatus.REPLAYING) {
            Log.Unity($"Finished replaying {lastFrame - ReplayStartFrame + 1}/{LoadedFrames?.Length ?? 0} frames.");
        }
        status = ReplayStatus.NONE;
        Achievement.ACHIEVEMENT_PROGRESS_ENABLED = true;
        PostedReplay = (recording != null && rec != null) ? new Replay(recording.ToArray(), rec) : (Replay?) null;
        recording = null;
        loadedFrames = null;
        replaying = null;
        return PostedReplay;
    }

    private static void SaveDebugReplay() {
        if (recording == null ||
            status != ReplayStatus.RECORDING ||
            GameManagement.Instance.Request == null)
            return;
        
        var r = new Replay(recording.ToArray(), GameManagement.Instance.Request.MakeGameRecord(), true);
        r.metadata.Record.AssignName($"Debug{RNG.RandStringOffFrame()}");
        SaveData.p.SaveNewReplay(r);
        Log.Unity($"Saved a debug replay {r.metadata.Record.CustomName}");
    }

    public static void Cancel() {
        Log.Unity("Cancelling in-progress replayer.");
        status = ReplayStatus.NONE;
        Achievement.ACHIEVEMENT_PROGRESS_ENABLED = true;
        recording = null;
        loadedFrames = null;
        replaying = null;
        PostedReplay = null;
    }

    public static void BeginFrame() {
        if (lastFrame == ETime.FrameNumber) return; //during pause/load
        //we want the counter to be treated the same for replay and record
        //Sorry for this order but replaystartframe may reset the frame number
        int replayIndex = -ReplayStartFrame + (lastFrame = ETime.FrameNumber);
        ReplayFrame(null);
        if (status == ReplayStatus.RECORDING && recording != null) {
            if (InputManager.ReplayDebugSave.Active)
                SaveDebugReplay();
            recording.Add(RecordFrame);
        } else if (status == ReplayStatus.REPLAYING && LoadedFrames != null) {
            if (replayIndex >= LoadedFrames.Length) {
                replaying?.onFinish?.Invoke();
                if (replaying?.finishMethod == ReplayerConfig.FinishMethod.REPEAT) {
                    Log.Unity("Restarting replay.");
                    BeginReplaying(replaying.Value);
                    BeginFrame();
                } else if (replaying?.finishMethod == ReplayerConfig.FinishMethod.STOP)
                    Cancel();
                else
                    Log.UnityError($"Ran out of replay data. On frame {lastFrame}, requested index {replayIndex}, " +
                                   $"but there are only {LoadedFrames.Length}.");
            } else
                ReplayFrame(LoadedFrames[replayIndex]);
        }
    }
}
}
