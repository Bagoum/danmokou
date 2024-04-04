using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.Services {
/// <summary>
/// A set of multiple audio tracks running concurrently.
/// </summary>
public class AudioTrackSet : ITokenized {
    public AudioTrackService Srv { get; }
    public Evented<AudioTrackState> State { get; } = new(AudioTrackState.Active);
    public bool BreakCoroutine => State == AudioTrackState.DestroyReady;
    public bool UpdateCoroutine =>
        State.Value is AudioTrackState.Active or AudioTrackState.Pausing or AudioTrackState.DestroyPrepare;
    
    public PushLerper<float> Fader { get; } = new(2);
    
    /// <summary>
    /// The result state after a fade-out, if a fade-out is being processed
    /// (DestroyReady or Paused or null).
    /// </summary>
    public AudioTrackState? FadeOutNextState { get; private set; } = null;
    
    public string TrackNames => Tracks.Count > 0 ? string.Join(", ", Tracks.Select(x => x.Track.Title))
        : $"(No tracks for ID {GetHashCode()})";
    public ICancellee cT { get; set; }
    public bool IsRunningAsBGM { get; init; }
    public List<IRunningAudioTrack> Tracks { get; } = new();
    public BGMInvokeFlags? Flags { get; }

    public List<IDisposable> Tokens { get; } = new();
    public ParametricInfo Pi;

    public AudioTrackSet(AudioTrackService srv, BGMInvokeFlags? flags, PIData? pi, ICancellee? cT) {
        this.Srv = srv;
        this.Flags = flags;
        this.cT = cT ?? Cancellable.Null;
        Pi = new ParametricInfo(pi ?? PIData.NewUnscoped(), Vector3.zero, 0, 0, 0);
        if (flags?.fadeInNewTime is {} inTime and > 0) {
            Fader.Push(0);
            FadeIn(inTime);
        } else {
            Fader.Push(1);
        }
    }


    public void FadeIn(float? time) {
        Logs.Log($"Fading in audio mixer with tracks: {TrackNames}");
        //We can fade in if we're running a fadeout to pause, but not if we're running a fadeout to destroy
        if (FadeOutNextState == AudioTrackState.DestroyReady) return;
        Fader.Push(1);
        var t = time ?? Flags?.fadeInNewTime ?? BGMInvokeFlags.Default.fadeInNewTime;
        Fader.ChangeLerpTime((a, b) => t);
    }
    
    public void FadeOut(float? time, AudioTrackState next) {
        if (next is not (AudioTrackState.Paused or AudioTrackState.DestroyReady))
            throw new Exception($"Fade out must end in either PAUSED or DESTROYREADY, not {next}");
        Logs.Log($"Fading out audio mixer to {next} with tracks: {TrackNames}");
        if (FadeOutNextState.Try(out var f)) {
            if (f < next)
                FadeOutNextState = next;
            return;
        }
        FadeOutNextState = next;
        State.Value = next - 1;
        Fader.Push(0);
        var t = time ?? Flags?.fadeOutExistingTime ?? BGMInvokeFlags.Default.fadeOutExistingTime;
        Fader.ChangeLerpTime((a, b) => t);
    }
    
    public void FinalizeFadeOut() {
        var next = FadeOutNextState ?? throw new Exception("No fadeout finalizer exists");
        FadeOutNextState = null;
        if (next == AudioTrackState.Paused)
            Pause();
        else if (next == AudioTrackState.DestroyReady)
            Cancel();
        else
            throw new Exception($"No audiotrack finalize handling for {next}");
    }

    /// <summary>
    /// Returns false iff this object should be removed (Destroy will already have been run).
    /// </summary>
    /// <returns></returns>
    public bool _RegularUpdate() {
        if (cT.Cancelled && State < AudioTrackState.DestroyReady)
            Cancel();
        if (State == AudioTrackState.DestroyReady) {
            Destroy();
            return false;
        }
        if (UpdateCoroutine) {
            Pi.t += ETime.FRAME_TIME;
            Fader.Update(ETime.FRAME_TIME);
            if (Fader.IsSteadyState && Fader.Value <= 0)
                FinalizeFadeOut();
        }
        for (int ii = 0; ii < Tracks.Count; ++ii)
            Tracks[ii].RegularUpdateSrc();
        return true;
    }
    
    public void Pause() {
        if (State.Value == AudioTrackState.Active)
            State.Value = AudioTrackState.Pausing;
        State.Value = AudioTrackState.Paused;
        foreach (var track in Tracks) {
            track.PauseSrc();
        }
        
    }
    public void UnPause() {
        State.Value = AudioTrackState.Active;
        foreach (var track in Tracks) {
            track.UnPauseSrc();
        }
    }
    public void Cancel() {
        if (State < AudioTrackState.DestroyReady) {
            if (State.Value < AudioTrackState.DestroyPrepare)
                State.Value = AudioTrackState.DestroyPrepare;
            foreach (var track in Tracks) {
                track.CancelSrc();
            }
            State.Value = AudioTrackState.DestroyReady;
        }
    }
    public void Destroy() {
        Tokens.DisposeAll();
        foreach (var track in Tracks) {
            track.DestroySrc();
        }
        Pi.Dispose();
    }
    
    /// <summary>
    /// Used to instantly destroy the track's resources.
    /// </summary>
    public void CancelAndDestroy() {
        Cancel();
        Destroy();
    }

    public bool HasTrack(IAudioTrackInfo? track, out IRunningAudioTrack res) {
        res = null!;
        if (track is null) return false;
        for (int ii = 0; ii < Tracks.Count; ++ii)
            if (Tracks[ii].Track == track) {
                res = Tracks[ii];
                return true;
            }
        return false;
    }

    public IRunningAudioTrack? AddTrack(string? trackName) => AddTrack(Srv.FindTrack(trackName));

    public IRunningAudioTrack? AddTrack(IAudioTrackInfo? track) {
        if (track is null) return null;
        if (!HasTrack(track, out var rtrack)) {
            rtrack = track.Loop switch {
                AudioTrackLoopMode.Naive => new NaiveLoopRAT(track, this),
                AudioTrackLoopMode.Timed => new TimedLoopRAT(track, this),
                _ => throw new Exception($"No handling for loop type {track.Loop} on track {track.Title}")
            };
            Logs.Log($"Added new track (state: {State.Value}). Now running: {TrackNames}");
        }
        return rtrack;
    }
}
}