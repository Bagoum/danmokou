using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scenes;
using UnityEngine;
using Object = System.Object;

namespace Danmokou.Services {
public interface IRunningAudioTrack {
    IAudioTrackInfo Track { get; }
    bool IsRunningAsBGM { get; }
    bool _RegularUpdate();
    void FadeIn(float time);
    
    /// <summary>
    /// Fade out a track and then set it to the provided state (either <see cref="AudioTrackState.Paused"/> or
    ///     <see cref="AudioTrackState.DestroyReady"/>)
    /// <br/>Should be safe to run twice.
    /// </summary>
    void FadeOut(float time, AudioTrackState next);

    void Pause();
    void UnPause();
    
    Evented<AudioTrackState> State { get; }
    
    /// <summary>
    /// Destroy the track's resources on the next update.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Used to instantly destroy the track's resources.
    /// </summary>
    void CancelAndDestroy();

    void AddScopedToken(IDisposable token);
}

/// <summary>
/// The current state of an audio track.
/// </summary>
public enum AudioTrackState {
    /// <summary>
    /// The audio track is playing.
    /// </summary>
    Active = 0,
    /// <summary>
    /// The audio track is in the process of pausing. This may involve a fade-out before <see cref="Paused"/>.
    /// </summary>
    Pausing = 1,
    /// <summary>
    /// The audio track is paused.
    /// </summary>
    Paused = 2,
    /// <summary>
    /// The audio track is in the process of being destroyed. This may involve a fade-out before <see cref="DestroyReady"/>.
    /// </summary>
    DestroyPrepare = 3,
    /// <summary>
    /// The audio track is done, and its resources can be fully disposed.
    /// </summary>
    DestroyReady = 4
}

public abstract class BaseRunningAudioTrack : IRunningAudioTrack {
    public IAudioTrackInfo Track { get; }
    public bool IsRunningAsBGM { get; init; }
    public Evented<AudioTrackState> State { get; private set; } = new(AudioTrackState.Active);
    private ICancellee cT;
    protected bool BreakCoroutine => State == AudioTrackState.DestroyReady;
    protected bool UpdateCoroutine =>
        State.Value is AudioTrackState.Active or AudioTrackState.Pausing or AudioTrackState.DestroyPrepare;
    /// <summary>
    /// The result state after a fade-out, if a fade-out is being processed
    /// (DestroyReady or Paused or null).
    /// </summary>
    protected AudioTrackState? FadeOutNextState { get; private set; } = null;

    protected readonly AudioTrackService host;
    protected readonly List<IDisposable> tokens = new();
    protected DisturbedProduct<float> Src1Volume { get; }
    protected PushLerper<float> Fader { get; } = new(2);
    protected AudioSource currSrc;

    public BaseRunningAudioTrack(IAudioTrackInfo track, AudioTrackService host, float initialVolume = 0, ICancellee? cT = null) {
        Src1Volume = new DisturbedProduct<float>(track.Volume);
        tokens.Add(Src1Volume.AddDisturbance(SaveData.s.BGMVolume));
        Fader.Push(initialVolume);
        tokens.Add(Src1Volume.AddDisturbance(Fader));
        Track = track;
        this.host = host;
        this.cT = cT ?? Cancellable.Null;
        var src1 = currSrc = host.gameObject.AddComponent<AudioSource>();
        src1.clip = Track.Clip;
        src1.pitch = Track.Pitch;
        tokens.Add(Src1Volume.Subscribe(v => src1.volume = v));
        Logs.Log($"Created audio track: {Track.Title}");
        PlayDelayed();
    }
    
    //Delay initial audio source play. This is useful to avoid Unity bugs where the volume doesn't update
    // properly after creating a new source.
    protected void PlayDelayed(int frames=2) {
        IEnumerator _Inner() {
            if (BreakCoroutine) yield break;
            for (int ii = 0; ii < frames;) {
                yield return null;
                if (BreakCoroutine) yield break;
                if (UpdateCoroutine) ++ii;
            }
            Logs.Log($"Playing {this.GetType()} audio track {Track.Title} (delayed by {frames} frames)");
            currSrc.Play();
        }
        host.Run(_Inner());
    }

    protected bool? __RegularUpdate() {
        if (cT.Cancelled && State < AudioTrackState.DestroyReady)
            Cancel();
        if (State == AudioTrackState.DestroyReady) {
            Destroy();
            return false;
        }
        if (UpdateCoroutine) {
            Fader.Update(ETime.FRAME_TIME);
            if (Fader.IsSteadyState && Fader.Value <= 0)
                FinalizeFadeOut();
        }
        //TODO simplify this override handling by simply checking AudioTrackState in the override of _RegularUpdate
        if (EngineStateManager.State != EngineState.RUN && Track.StopOnPause)
            return true;
        return null;
    }

    public virtual bool _RegularUpdate() => __RegularUpdate() ?? true;
    
    
    public void FadeIn(float time) {
        //We can fade in if we're running a fadeout to pause, but not if we're running a fadeout to destroy
        if (FadeOutNextState == AudioTrackState.DestroyReady) return;
        Logs.Log($"Fading in audio track: {Track.Title}", level: LogLevel.DEBUG1);
        Fader.Push(1);
        Fader.ChangeLerpTime((a, b) => time);
    }
    
    public void FadeOut(float time, AudioTrackState next) {
        if (next is not (AudioTrackState.Paused or AudioTrackState.DestroyReady))
            throw new Exception($"Fade out must end in either PAUSED or DESTROYREADY, not {next}");
        if (FadeOutNextState.Try(out var f)) {
            if (f < next)
                FadeOutNextState = next;
            return;
        }
        Logs.Log($"Fading out audio track: {Track.Title}", level: LogLevel.DEBUG1);
        FadeOutNextState = next;
        State.Value = next - 1;
        Fader.Push(0);
        Fader.ChangeLerpTime((a, b) => time);
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
    
    public virtual void Pause() {
        Logs.Log($"Paused audio track: {Track.Title}", level: LogLevel.DEBUG1);
        currSrc.Pause();
        if (State.Value == AudioTrackState.Active)
            State.Value = AudioTrackState.Pausing;
        State.Value = AudioTrackState.Paused;
    }
    public virtual void UnPause() {
        Logs.Log($"Unpaused audio track: {Track.Title}", level: LogLevel.DEBUG1);
        currSrc.UnPause();
        State.Value = AudioTrackState.Active;
    }
    
    public virtual void Cancel() {
        if (State < AudioTrackState.DestroyReady) {
            Logs.Log($"Cancelling audio track: {Track.Title}. It will be destroyed on the next frame.");
            Src1Volume.OnCompleted();
            currSrc.Stop();
            if (State.Value < AudioTrackState.DestroyPrepare)
                State.Value = AudioTrackState.DestroyPrepare;
            State.Value = AudioTrackState.DestroyReady;
        }
    }
    
    protected virtual void Destroy() {
        UnityEngine.Object.Destroy(currSrc);
        foreach (var t in tokens)
            t.Dispose();
    }

    public void CancelAndDestroy() {
        Cancel();
        Destroy();
    }

    public void AddScopedToken(IDisposable token) => tokens.Add(token);
}
public class NaiveLoopRAT : BaseRunningAudioTrack {
    public NaiveLoopRAT(IAudioTrackInfo track, AudioTrackService host, ICancellee? cT) : base(track, host, cT: cT) {
        currSrc.loop = true;
        currSrc.time = Track.StartTime;
    }
}
public class TimedLoopRAT : BaseRunningAudioTrack {
    private const float xfadeTime = 0.3f;
    private const float xfadeOverlap = 0.2f;
    //Two sources are necessary to smoothly loop by crossfading the track into itself.
    private PushLerper<float> currSrcFadeVol = new(xfadeTime, M.Lerp);
    private PushLerper<float> nextSrcFadeVol = new(xfadeTime, M.Lerp);
    private DisturbedProduct<float> Src2Volume { get; }
    private AudioSource nextSrc;

    public TimedLoopRAT(IAudioTrackInfo track, AudioTrackService host, ICancellee? cT) : base(track, host, cT: cT) {
        currSrcFadeVol.Push(1);
        tokens.Add(Src1Volume.AddDisturbance(currSrcFadeVol));
        nextSrcFadeVol.Push(0);
        Src2Volume = new DisturbedProduct<float>(track.Volume);
        tokens.Add(Src2Volume.AddDisturbance(SaveData.s.BGMVolume));
        tokens.Add(Src2Volume.AddDisturbance(nextSrcFadeVol));
        tokens.Add(Src2Volume.AddDisturbance(Fader));
        
        currSrc.time = Track.StartTime;
        var src2 = nextSrc = host.gameObject.AddComponent<AudioSource>();
        src2.clip = Track.Clip;
        src2.pitch = Track.Pitch;
        tokens.Add(Src2Volume.Subscribe(v => src2.volume = v));
    }

    /// <summary>
    /// Returns false iff this object should be removed (Destroy will already have been run).
    /// </summary>
    /// <returns></returns>
    public override bool _RegularUpdate() {
        if (__RegularUpdate().Try(out var b)) return b;
        currSrcFadeVol.Update(ETime.FRAME_TIME);
        nextSrcFadeVol.Update(ETime.FRAME_TIME);
        if (nextSrcFadeVol.Value <= 0 && nextSrc.time > Track.LoopSeconds.y)
            nextSrc.Stop();
        if (currSrc.time > Track.LoopSeconds.y && !nextSrc.isPlaying) {
            currSrcFadeVol.Push(0, -xfadeOverlap);
            nextSrcFadeVol.Push(1);
            Logs.Log($"Looping {Track.Title} at time {currSrc.time}");
            nextSrc.time = Track.LoopSeconds.x + (currSrc.time - Track.LoopSeconds.y);
            nextSrc.Play();
            (currSrc, nextSrc) = (nextSrc, currSrc);
            (currSrcFadeVol, nextSrcFadeVol) = (nextSrcFadeVol, currSrcFadeVol);
        }
        return true;
    }

    public override void Pause() {
        base.Pause();
        nextSrc.Pause();
    }

    public override void UnPause() {
        base.Pause();
        nextSrc.UnPause();
    }

    public override void Cancel() {
        if (State < AudioTrackState.DestroyPrepare) {
            base.Cancel();
            Src2Volume.OnCompleted();
            nextSrc.Stop();
        }
    }
    protected override void Destroy() {
        base.Destroy();
        UnityEngine.Object.Destroy(nextSrc);
    }
}
}