using System;
using System.Collections.Generic;
using System.Linq;
using DMK.Core;
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
    [JsonIgnore] public InstanceRecord Record => 
        SaveData.r.FinishedGames.TryGetValue(RecordUuid, out var gr) ? gr : SerializedRecord;
    
    /// <summary>
    /// Serializing the record in the replay file allows transferring replays.
    /// </summary>
    public InstanceRecord SerializedRecord { get; set; }
    public string RecordUuid { get; set; }
    //Frozen options
    public float DialogueSpeed { get; set; }
    public bool SmoothInput { get; set; }
    public Locale Locale { get; set; } = Locale.EN;
    // Not important but it's convenient
    public int Length { get; set; }

    [UsedImplicitly]
    public ReplayMetadata() {}
    public ReplayMetadata(InstanceRecord rec) {
        SerializedRecord = rec;
        RecordUuid = rec.Uuid;
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

    public Replay(FrameInput[] frames, InstanceRecord rec) :
        this(() => frames, new ReplayMetadata(rec)) {
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

    public static void End(InstanceRecord rec) {
        if (status == ReplayStatus.RECORDING) {
            Log.Unity($"Finished recording {recording?.Count ?? -1} frames.");
        } else if (status == ReplayStatus.REPLAYING) {
            Log.Unity($"Finished replaying {lastFrame - ReplayStartFrame + 1}/{Replaying?.Length ?? 0} frames.");
        }
        status = ReplayStatus.NONE;
        PostedReplay = (recording != null) ? new Replay(recording.ToArray(), rec) : (Replay?) null;
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
}
