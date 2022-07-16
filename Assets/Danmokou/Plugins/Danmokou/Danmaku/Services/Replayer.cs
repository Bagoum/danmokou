﻿using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Achievements;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Scenes;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using static Danmokou.Core.DInput.InputManager;


namespace Danmokou.Services {
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
    public string? Locale { get; set; } = null;
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
        DialogueSpeed = SaveData.VNSettings.TextSpeed;
        SmoothInput = SaveData.s.AllowInputLinearization;
        Locale = SaveData.s.TextLocale;
    }

    public void ApplySettings() {
        SaveData.VNSettings.TextSpeed = DialogueSpeed;
        SaveData.s.AllowInputLinearization = SmoothInput;
        SaveData.s.TextLocale.OnNext(Locale);
    }

    [JsonIgnore]
    public string AsFilename => $"{Record.Mode}_{Record.SavedMetadata.difficulty.DescribeSafe()}_{Record.Uuid}";

}
public class Replay {
    public readonly Func<FrameInput[]> frames;
    public readonly ReplayMetadata metadata;

    public Replay(FrameInput[] frames, InstanceRecord rec, bool? debug=null) :
        this(() => frames, new ReplayMetadata(rec, debug ?? false)) {
        metadata.Length = frames.Length;
    }

    public Replay(Func<FrameInput[]> frames, ReplayMetadata metadata) {
        this.frames = frames;
        this.metadata = metadata;
    }
}

public abstract class ReplayActor {
    public bool Cancelled { get; protected set; } = false;
    public int LastFrame { get; set; }= -1;
    private int? replayStartFrame;
    protected int ReplayStartFrame {
        get {
            if (replayStartFrame == null) {
                ETime.ResetFrameNumber();
                replayStartFrame = 0;
            }
            return replayStartFrame.Value;
        }
    }
    protected int ReplayIndex => 
        //Sorry for this order but replaystartframe may reset the frame number
        -ReplayStartFrame + (LastFrame = ETime.FrameNumber);

    public abstract void Step();
    public virtual void Load() { }

    public virtual void Cancel() => Cancelled = true;

    protected virtual void ResetState() {
        replayStartFrame = null;
        LastFrame = -1;
    }
}

public class ReplayRecorder : ReplayActor {
    private readonly List<FrameInput> recording;
    public IEnumerable<FrameInput> Recording => recording;

    public ReplayRecorder() {
        this.recording = new List<FrameInput>(1000000);
    }

    public override void Step() {
        _ = ReplayIndex;
        if (InputManager.PlayerInput.ReplayDebugSave.Active)
            Replayer.SaveDebugReplay();
        recording.Add(RecordFrame);
    }

    public Replay Compile(InstanceRecord rec, bool? debug = null) {
        Logs.Log($"Finished recording {recording.Count} frames.");
        return new Replay(recording.ToArray(), rec, debug);
    }
}

/// <summary>
/// Input source that overrides gameplay input (not menu input) as copied from a replay.
/// </summary>
public class ReplayPlayerInputSource : ReplayActor, IInputSource {
    public readonly Replayer.ReplayerConfig replaying;
    private FrameInput[]? loadedFrames;
    private FrameInput[] LoadedFrames => loadedFrames ??= replaying.frames();
    private FrameInput CurrentFrame => LoadedFrames[ReplayIndex];
    
    private readonly List<IDisposable> tokens = new();

    public ReplayPlayerInputSource(Replayer.ReplayerConfig replaying) {
        tokens.Add(Achievement.ACHIEVEMENT_PROGRESS_ENABLED.AddConst(false));
        tokens.Add(InputManager.PlayerInput.AddSource(this, AggregateInputSource.REPLAY_PRIORITY));
        this.replaying = replaying;
    }

    public override void Load() {
        var _ = LoadedFrames;
    }
    
    public override void Step() {
        if (ReplayIndex >= LoadedFrames.Length) {
            replaying.onFinish?.Invoke();
            if (replaying.finishMethod == Replayer.ReplayerConfig.FinishMethod.REPEAT) {
                Logs.Log("Restarting replay.");
                ResetState();
                return;
            }
            Cancel();
            if (replaying.finishMethod != Replayer.ReplayerConfig.FinishMethod.STOP)
                Logs.UnityError($"Ran out of replay data. On frame {LastFrame}, requested index {ReplayIndex}, " +
                               $"but there are only {LoadedFrames.Length}.");
        }
    }

    public override void Cancel() {
        Logs.Log($"Finished replaying {LastFrame - ReplayStartFrame + 1}/{LoadedFrames.Length} frames.");
        foreach (var t in tokens)
            t.Dispose();
        base.Cancel();
    }

    public short? HorizontalSpeed => CurrentFrame.horizontal;
    public short? VerticalSpeed => CurrentFrame.vertical;
    public bool? Firing => CurrentFrame.fire;
    public bool? Focus => CurrentFrame.focus;
    public bool? Bomb => CurrentFrame.bomb;
    public bool? Meter => CurrentFrame.meter;
    public bool? Swap => CurrentFrame.swap;
    public bool? DialogueConfirm => CurrentFrame.dialogueConfirm;
    public bool? DialogueSkipAll => CurrentFrame.dialogueSkipAll;

    //handled by Step
    public void OncePerUnityFrameToggleControls() {}
}

public static class Replayer {
    static Replayer() {
        if (!Application.isPlaying) return;
        SceneIntermediary.SceneLoaded.Subscribe(_ => LoadLazy());
    }
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

    private static ReplayActor? actor;
    private static ReplayActor? Actor {
        get {
            if (actor?.Cancelled == true)
                actor = null;
            return actor;
        }
        set {
            if (actor?.Cancelled == false) {
                Logs.UnityError($"Setting a new replay actor before the previous one {actor} is completed");
                actor.Cancel();
            }
            actor = value;
        }
    }
    
    private static ReplayStatus Status => Actor switch {
        null => ReplayStatus.NONE,
        ReplayRecorder _ => ReplayStatus.RECORDING,
        ReplayPlayerInputSource _ => ReplayStatus.REPLAYING,
        _ => throw new Exception($"Unhandled replay actor: {Actor}")
    };
    public static bool RequiresConsistency => Actor != null;


    private static void LoadLazy() {
        Actor?.Load();
    }

    public static ReplayActor BeginRecording() {
        Logs.Log("Replay recording started.");
        return Actor = new ReplayRecorder();
    }

    public static ReplayActor BeginReplaying(ReplayerConfig data) {
        Logs.Log($"Replay playback started.");
        return Actor = new ReplayPlayerInputSource(data);
    }
    public static void SaveDebugReplay() {
        if (Actor is ReplayRecorder rr && GameManagement.Instance.Request != null) {
            var r = rr.Compile(GameManagement.Instance.Request.MakeGameRecord(), true);
            r.metadata.Record.AssignName($"Debug{RNG.RandStringOffFrame()}");
            SaveData.p.SaveNewReplay(r);
            Logs.Log($"Saved a debug replay {r.metadata.Record.CustomName}");
        }

    }

    public static void BeginFrame() {
        if (Actor == null) return;
        if (Actor.LastFrame == ETime.FrameNumber) return; //during pause/load
        Actor.Step();
    }
}
}
