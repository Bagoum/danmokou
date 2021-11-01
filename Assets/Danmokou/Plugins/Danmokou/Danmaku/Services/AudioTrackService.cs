using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.GameInstance;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Danmokou.Services {

public readonly struct BGMInvokeFlags {
    /// <summary>
    /// If null, existing tracks will not be destroyed.
    /// </summary>
    public readonly float? fadeOutExistingTime;
    public readonly float fadeInNewTime;

    public static BGMInvokeFlags Default => new BGMInvokeFlags(2f, 2f);
    public BGMInvokeFlags(float? fadeOutExistingTime = 2f, float fadeInNewTime = 2f) {
        this.fadeOutExistingTime = fadeOutExistingTime;
        this.fadeInNewTime = fadeInNewTime;
    }
    
}

public interface IAudioTrackService {
    IRunningAudioTrack? InvokeBGM(string? trackName, BGMInvokeFlags? flags = null);
    IRunningAudioTrack? InvokeBGM(IAudioTrackInfo? track, BGMInvokeFlags? flags = null);
}
public class AudioTrackService : CoroutineRegularUpdater, IAudioTrackService {
    private static readonly Dictionary<string, IAudioTrackInfo> trackInfo = new Dictionary<string, IAudioTrackInfo>();
    private readonly DMCompactingArray<IRunningAudioTrack> tracks = new DMCompactingArray<IRunningAudioTrack>();

    private bool preserveBGMOnNextScene = false;

    public void Setup() {
        trackInfo.Clear();
        foreach (var t in GameManagement.References.tracks) {
            if (t != null) trackInfo[t.key] = t;
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IAudioTrackService>(this);

        Listen(Events.SceneCleared, () => {
            if (preserveBGMOnNextScene)
                preserveBGMOnNextScene = false;
            else
                ClearAllAudio();
        });
        Listen(EngineStateManager.EvState, HandleEngineStateChange);
        Listen(InstanceRequest.InstanceRestarted, ir => {
            if (ir.Mode.PreserveReloadAudio())
                preserveBGMOnNextScene = true;
        });
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        for (int ii = 0; ii < tracks.Count; ++ii) {
            if (tracks.ExistsAt(ii)) {
                if (!tracks[ii]._RegularUpdate())
                    tracks.Delete(ii);
            }
        }
        tracks.Compact();
    }

    public override EngineState UpdateDuring => EngineState.LOADING_PAUSE;
    

    public IRunningAudioTrack? InvokeBGM(string? trackName, BGMInvokeFlags? flags = null) {
        if (!string.IsNullOrWhiteSpace(trackName) && trackName != "_")
            return InvokeBGM(trackInfo.GetOrThrow(trackName!, "BGM tracks"), flags);
        return null;
    }

    public IRunningAudioTrack? InvokeBGM(IAudioTrackInfo? track, BGMInvokeFlags? flags = null) {
        if (track == null) return null;
        for (int ii = 0; ii < tracks.Count; ++ii) {
            if (tracks.ExistsAt(ii) && !tracks[ii].Active.Cancelled && tracks[ii].Track == track)
                return tracks[ii];
        }
        var f = flags ?? BGMInvokeFlags.Default;
        if (f.fadeOutExistingTime.Try(out var fout)) {
            for (int ii = 0; ii < tracks.Count; ++ii) {
                if (tracks.ExistsAt(ii))
                    tracks[ii].FadeOutThenDestroy(fout);
            }
        }
        IRunningAudioTrack rtrack = track.Loop switch {
            AudioTrackLoopMode.Naive => new NaiveLoopRAT(track, this),
            AudioTrackLoopMode.Timed => new TimedLoopRAT(track, this),
            _ => throw new Exception($"No handling for loop type {track.Loop} on track {track.Title}")
        };
        tracks.Add(rtrack);
        if (f.fadeInNewTime > 0)
            rtrack.FadeIn(f.fadeInNewTime);
        return rtrack;
    }

    protected override void OnDisable() {
        ClearAllAudio();
        base.OnDisable();
    }

    private void HandleEngineStateChange(EngineState state) {
        for (int ii = 0; ii < tracks.Count; ++ii) {
            if (tracks.ExistsAt(ii)) {
                if (state == EngineState.RUN)
                    tracks[ii].UnPause();
                else if (tracks[ii].Track.StopOnPause)
                    tracks[ii].Pause();
            }
        }
    }

    public void ClearAllAudio() {
        for (int ii = 0; ii < tracks.Count; ++ii) {
            if (tracks.ExistsAt(ii))
                tracks[ii]._Destroy();
        }
        tracks.Empty();
    }
}
}
